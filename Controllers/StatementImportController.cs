using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/statements")]
[Authorize]
public partial class StatementImportController(BankDbContext db) : ControllerBase
{
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, [FromQuery] bool preview = false)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var extension = Path.GetExtension(file?.FileName ?? "");
        if (file is null || file.Length == 0 ||
            (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) &&
             !extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Lütfen geçerli bir .xlsx veya .pdf ekstresi seçin." });
        }

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ImportPdf(file, preview, userId.Value);
        }

        try
        {
            await using var input = file.OpenReadStream();
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (sheetEntry is null) return BadRequest(new { message = "Excel çalışma sayfası bulunamadı." });

            using var sheetStream = sheetEntry.Open();
            var sheet = XDocument.Load(sheetStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = sheet.Descendants(ns + "row").ToList();
            if (rows.Count < 2)
            {
                return BadRequest(new { message = "Ekstrede başlık ve en az bir işlem satırı bulunmalıdır." });
            }

            var headers = rows[0].Elements(ns + "c")
                .ToDictionary(cell => ColumnIndex(cell.Attribute("r")?.Value ?? ""), cell => HeaderKey(CellValue(cell, sharedStrings)));
            if (!FindHeader(headers, ["tarih", "islemtarihi", "date"], out var dateColumn) ||
                !FindHeader(headers, ["aciklama", "islemaciklamasi", "description", "detay"], out var descriptionColumn) ||
                !FindHeader(headers, ["tutar", "amount", "miktar"], out var amountColumn))
            {
                return BadRequest(new { message = "Dosyada Tarih, Açıklama ve Tutar sütunları bulunmalıdır." });
            }

            FindHeader(headers, ["tur", "islemturu", "type"], out var typeColumn);
            var existing = await db.BudgetEntries
                .Where(entry => entry.UserId == userId)
                .Select(entry => new { entry.EntryDate, entry.Description, entry.Amount, entry.Type })
                .ToListAsync();
            var known = existing.Select(Key).ToHashSet();
            var entries = new List<BudgetEntry>();
            var skipped = 0;

            foreach (var row in rows.Skip(1))
            {
                var values = row.Elements(ns + "c")
                    .ToDictionary(cell => ColumnIndex(cell.Attribute("r")?.Value ?? ""), cell => CellValue(cell, sharedStrings));
                if (!TryParseDate(values.GetValueOrDefault(dateColumn, ""), out var date) ||
                    string.IsNullOrWhiteSpace(values.GetValueOrDefault(descriptionColumn, "")) ||
                    !TryParseAmount(values.GetValueOrDefault(amountColumn, ""), out var rawAmount) ||
                    rawAmount == 0)
                {
                    skipped++;
                    continue;
                }

                var description = values[descriptionColumn].Trim();
                var typeText = typeColumn >= 0 ? values.GetValueOrDefault(typeColumn, "") : "";
                var type = ResolveType(typeText, rawAmount);
                var amount = Math.Abs(rawAmount);
                var entry = new BudgetEntry
                {
                    UserId = userId.Value,
                    Type = type,
                    Category = GuessCategory(description, type),
                    Description = description,
                    Amount = decimal.Round(amount, 2),
                    EntryDate = ToIstanbulUtc(date),
                    Source = "Excel ekstresi",
                    Treatment = InitialTreatment(description, type)
                };

                if (!known.Add(Key(entry)))
                {
                    skipped++;
                    continue;
                }

                entries.Add(entry);
            }

            await ApplyMerchantRules(entries, userId.Value);

            if (preview)
            {
                return Ok(new
                {
                    rows = entries.Select(entry => new
                    {
                        entry.EntryDate,
                        entry.Description,
                        entry.Amount,
                        type = entry.Type.ToString(),
                        entry.Category,
                        entry.Source,
                        entry.Treatment
                    }),
                    parsed = entries.Count + skipped,
                    skipped
                });
            }

            db.BudgetEntries.AddRange(entries);
            if (entries.Count > 0) await db.SaveChangesAsync();

            return Ok(new
            {
                message = entries.Count > 0
                    ? $"{entries.Count} ekstre işlemi içe aktarıldı. {skipped} kayıt atlandı."
                    : "Yeni ekstre işlemi bulunamadı.",
                imported = entries.Count,
                skipped,
                reviewPending = await db.BudgetEntries.CountAsync(entry =>
                    entry.UserId == userId && entry.Treatment == "NeedsReview")
            });
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "Geçerli bir .xlsx dosyası yükleyin." });
        }
    }

    private async Task<IActionResult> ImportPdf(IFormFile file, bool preview, int userId)
    {
        try
        {
            await using var input = file.OpenReadStream();
            using var document = PdfDocument.Open(input);
            // Bankalar aynı bilgileri farklı sütun sıralarında verebilir. Önce
            // PDF içindeki başlıkların koordinatlarını kullanarak genel okuyucuyu
            // deneriz; başlık bulunamazsa eski Yapı Kredi okuyucusu devreye girer.
            var entries = ParsePdfEntries(document, userId);
            await ApplyMerchantRules(entries, userId);
            var existing = await db.BudgetEntries
                .Where(entry => entry.UserId == userId)
                .ToListAsync();
            var known = existing.Select(Key).ToHashSet();
            var uniqueEntries = new List<BudgetEntry>();
            foreach (var entry in entries)
            {
                if (known.Add(Key(entry)))
                {
                    uniqueEntries.Add(entry);
                    continue;
                }

                // Eski aktarım saat bilgisi olmadan kaydedildiyse, PDF yeniden
                // yüklendiğinde aynı kaydın saatini düzelt.
                if (!preview)
                {
                    var oldEntry = existing.FirstOrDefault(item => Key(item) == Key(entry));
                    if (oldEntry is not null && oldEntry.EntryDate.TimeOfDay == TimeSpan.Zero && entry.EntryDate.TimeOfDay != TimeSpan.Zero)
                    {
                        oldEntry.EntryDate = entry.EntryDate;
                        oldEntry.Category = entry.Category;
                        oldEntry.Source = "PDF ekstresi";
                        oldEntry.Treatment = entry.Treatment;
                    }
                }
            }
            var skipped = entries.Count - uniqueEntries.Count;

            if (preview)
            {
                return Ok(new
                {
                    rows = uniqueEntries.Select(entry => new
                    {
                        entry.EntryDate,
                        entry.Description,
                        entry.Amount,
                        type = entry.Type.ToString(),
                        entry.Category,
                        entry.Treatment
                    }),
                    parsed = entries.Count,
                    skipped
                });
            }

            db.BudgetEntries.AddRange(uniqueEntries);
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
            return Ok(new
            {
                message = uniqueEntries.Count > 0
                    ? $"{uniqueEntries.Count} PDF ekstre işlemi içe aktarıldı. {skipped} kayıt atlandı."
                    : "Yeni PDF ekstre işlemi bulunamadı.",
                imported = uniqueEntries.Count,
                skipped,
                reviewPending = await db.BudgetEntries.CountAsync(entry =>
                    entry.UserId == userId && entry.Treatment == "NeedsReview")
            });
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is InvalidOperationException)
        {
            return BadRequest(new { message = "PDF okunamadı. Bankadan alınan metin tabanlı hesap hareketleri PDF'sini yükleyin." });
        }
    }

    private static List<BudgetEntry> ParsePdfEntries(PdfDocument document, int userId)
    {
        var entries = new List<BudgetEntry>();
        PdfColumnLayout? layout = null;
        foreach (var page in document.GetPages())
        {
            // Bankalar çoğu zaman başlığı yalnızca ilk sayfaya koyar. İlk
            // sayfada bulunan sütun düzenini sonraki sayfalara da uygularız.
            if (TryGetPdfColumnLayout(page, out var detectedLayout)) layout = detectedLayout;
            if (layout is not null) entries.AddRange(ParsePdfPageByLayout(page, userId, layout));
        }

        var text = string.Join("\n", document.GetPages().Select(page => page.Text));
        var fallbackEntries = ParsePdfEntries(text, userId);
        // Bazı bankalarda başlık satırları metin katmanında parçalı olduğu için
        // koordinat okuyucu az satır yakalayabilir. İki sonucu karşılaştırıp
        // daha fazla işlem yakalayan parser'ın sonucunu kullanırız.
        return entries.Count >= fallbackEntries.Count ? entries : fallbackEntries;
    }

    private static List<BudgetEntry> ParsePdfPageByHeaders(Page page, int userId)
    {
        return TryGetPdfColumnLayout(page, out var layout)
            ? ParsePdfPageByLayout(page, userId, layout)
            : [];
    }

    private static bool TryGetPdfColumnLayout(Page page, out PdfColumnLayout layout)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0) { layout = default!; return false; }

        var lines = GroupPdfWords(words);
        var header = lines
            .Select(line => new
            {
                Line = line,
                Text = HeaderKey(string.Join(" ", line.Select(word => word.Text)))
            })
            .Where(item => HasAny(item.Text, "aciklama", "description", "detay") &&
                          HasAny(item.Text, "tutar", "amount", "miktar"))
            .OrderByDescending(item => item.Line[0].BoundingBox.Top)
            .FirstOrDefault();

        // Bu PDF'de tablo başlığı yoksa veya PDF taranmış görüntüyse, yedek
        // okuyucuya bırakılır.
        if (header is null) { layout = default!; return false; }

        // Bazı bankalar çok satırlı başlık kullanır: örneğin "Tarih" üstte,
        // "Tarihi" ve diğer sütunlar alt satırdadır. Başlık kelimelerini tek
        // satır yerine başlık bandından toplarız.
        var headerTop = header.Line[0].BoundingBox.Top;
        var headerWords = words
            .Where(word => Math.Abs(word.BoundingBox.Top - headerTop) <= 35 && IsPdfHeaderWord(word.Text))
            .ToList();
        var dateWord = FindHeaderWord(headerWords, ["tarih", "date"]);
        var timeWord = FindHeaderWord(headerWords, ["saat", "time"]);
        var descriptionWord = FindHeaderWord(headerWords, ["aciklama", "description", "detay"]);
        var amountWord = FindHeaderWord(headerWords, ["tutar", "amount", "miktar"]);
        var typeWord = FindHeaderWord(headerWords, ["islem", "tur", "type"]);
        var balanceWord = FindHeaderWord(headerWords, ["bakiye", "balance"]);


        if (dateWord is null || descriptionWord is null || amountWord is null) { layout = default!; return false; }

        // Bazı ekstrelerde tutar başlığı "İşlem Tutarı" şeklinde iki ayrı
        // kelimeye bölünür. Bu durumda gerçek tutar değerleri, yalnızca
        // "Tutarı" kelimesinin değil, başlığın ilk kelimesinin x konumundan
        // başlar. İkinci kelimeyi sütun başlangıcı kabul edersek değerler
        // sütuna giremez ve PDF'den hiç işlem çıkarılamaz.
        var amountColumnX = FindCompoundAmountColumnLeft(headerWords, amountWord);

        layout = new PdfColumnLayout(
            header.Line[0].BoundingBox.Top,
            dateWord.BoundingBox.Left,
            timeWord?.BoundingBox.Left,
            descriptionWord.BoundingBox.Left,
            amountColumnX,
            typeWord?.BoundingBox.Left,
            balanceWord?.BoundingBox.Left);
        return true;
    }

    private static double FindCompoundAmountColumnLeft(IReadOnlyList<Word> headerWords, Word amountWord)
    {
        var amountKey = HeaderKey(amountWord.Text);
        if (!amountKey.Contains("tutar") && !amountKey.Contains("amount") && !amountKey.Contains("miktar"))
            return amountWord.BoundingBox.Left;

        var previousAmountHeader = headerWords
            .Where(word => word.BoundingBox.Left < amountWord.BoundingBox.Left)
            .Where(word => HeaderKey(word.Text) is var key && key is "islem" or "transaction")
            .OrderByDescending(word => word.BoundingBox.Left)
            .FirstOrDefault(word => amountWord.BoundingBox.Left - word.BoundingBox.Right <= 45);

        return previousAmountHeader?.BoundingBox.Left ?? amountWord.BoundingBox.Left;
    }

    private static List<BudgetEntry> ParsePdfPageByLayout(Page page, int userId, PdfColumnLayout layout)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0) return [];
        var lines = GroupPdfWords(words);
        var dateX = layout.DateX;
        var timeX = layout.TimeX;
        var descriptionX = layout.DescriptionX;
        var amountX = layout.AmountX;
        var typeX = layout.TypeX;
        var balanceX = layout.BalanceX;
        var headerXs = new[] { dateX, timeX, descriptionX, amountX, typeX, balanceX }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .ToList();
        double? NextHeaderX(double x) => headerXs.Where(value => value > x + 1).DefaultIfEmpty().Min() is var next && next > 0 ? next : null;
        var dateNextX = NextHeaderX(dateX);
        var descriptionStartX = headerXs.Where(value => value < descriptionX - 1).DefaultIfEmpty(0).Max();
        var descriptionNextX = NextHeaderX(descriptionX);
        var amountNextX = NextHeaderX(amountX);
        var typeNextX = typeX is null ? null : NextHeaderX(typeX.Value);
        var dataLines = lines.Where(line => line[0].BoundingBox.Top < layout.HeaderTop - 5).ToList();
        var transactionLines = new List<List<Word>>();
        List<Word>? current = null;

        foreach (var line in dataLines.OrderByDescending(item => item[0].BoundingBox.Top))
        {
            if (line.Any(word => DateTokenRegex().IsMatch(word.Text.Trim()) && IsInColumn(word, dateX, dateNextX, 18)))
            {
                current = new List<Word>();
                transactionLines.Add(current);
            }

            // Üst bilgi, sayfa numarası ve tablo altındaki özet satırları işlem
            // satırına ait değildir; yalnızca tarih ile başlayan blokları toplarız.
            current?.AddRange(line);
        }

        var entries = new List<BudgetEntry>();
        foreach (var lineWords in transactionLines)
        {
            var ordered = lineWords.OrderBy(word => word.BoundingBox.Left).ToList();
            var dateText = ordered.FirstOrDefault(word =>
                DateTokenRegex().IsMatch(word.Text.Trim()) && IsInColumn(word, dateX, dateNextX, 18))?.Text.Trim();
            if (string.IsNullOrWhiteSpace(dateText) || !TryParsePdfDate(dateText, out var date)) continue;

            var timeText = ordered.FirstOrDefault(word =>
                TimeTokenRegex().IsMatch(word.Text.Trim()) &&
                (timeX is not null
                    ? IsInColumn(word, timeX.Value, NextHeaderX(timeX.Value), 18)
                    : IsInColumn(word, dateX, dateNextX, 18)))?.Text.Trim();
            if (!string.IsNullOrWhiteSpace(timeText) && TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out var time))
            {
                date = date.Date.Add(time);
            }

            var amountParts = ordered.Where(word => IsInColumn(word, amountX, amountNextX, 22)).Select(word => word.Text);
            var amountText = ExtractPdfAmount(string.Join(" ", amountParts));
            if (string.IsNullOrWhiteSpace(amountText) || !TryParseAmount(amountText, out var rawAmount) || rawAmount == 0) continue;

            var descriptionParts = ordered
                // Bazı ekstrelerde "Kanal" ayrı bir sütundur ve açıklama
                // metni, açıklama başlığının x koordinatından daha soldan
                // başlayabilir. Açıklamayı bir önceki başlık ile sonraki
                // başlık arasındaki tüm alandan toplarız.
                .Where(word => IsInColumn(word, descriptionStartX, descriptionNextX, 18))
                .Select(word => word.Text)
                .Where(text => !DateTokenRegex().IsMatch(text.Trim()) && !TimeTokenRegex().IsMatch(text.Trim()));
            var description = string.Join(" ", descriptionParts).Trim();
            if (string.IsNullOrWhiteSpace(description)) continue;

            var typeParts = typeX is not null
                ? ordered.Where(word => IsInColumn(word, typeX.Value, typeNextX, 18)).Select(word => word.Text)
                : [];
            var typeText = string.Join(" ", typeParts);
            var type = ResolveType($"{typeText} {description}", rawAmount);

            entries.Add(new BudgetEntry
            {
                UserId = userId,
                EntryDate = ToIstanbulUtc(date),
                Description = description,
                Amount = decimal.Round(Math.Abs(rawAmount), 2),
                Type = type,
                Category = GuessCategory(description, type),
                Source = "PDF ekstresi",
                Treatment = InitialTreatment(description, type)
            });
        }

        return entries;
    }

    private sealed record PdfColumnLayout(
        double HeaderTop,
        double DateX,
        double? TimeX,
        double DescriptionX,
        double AmountX,
        double? TypeX,
        double? BalanceX);

    private static List<List<Word>> GroupPdfWords(IReadOnlyList<Word> words)
    {
        var lines = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(item => item.BoundingBox.Top).ThenBy(item => item.BoundingBox.Left))
        {
            var line = lines.FirstOrDefault(item => Math.Abs(item[0].BoundingBox.Top - word.BoundingBox.Top) <= 3.5);
            if (line is null) lines.Add([word]);
            else line.Add(word);
        }

        foreach (var line in lines) line.Sort((left, right) => left.BoundingBox.Left.CompareTo(right.BoundingBox.Left));
        return lines.OrderByDescending(line => line[0].BoundingBox.Top).ToList();
    }

    private static Word? FindHeaderWord(IEnumerable<Word> words, IEnumerable<string> aliases) =>
        words.FirstOrDefault(word => aliases.Any(alias => HeaderKey(word.Text).Contains(HeaderKey(alias))));

    private static bool IsPdfHeaderWord(string value)
    {
        var text = HeaderKey(value);
        return HasAny(text, "tarih", "date", "saat", "time", "aciklama", "description", "detay",
            "tutar", "amount", "miktar", "islem", "tur", "type", "bakiye", "balance", "borc", "alacak");
    }

    private static bool IsInColumn(Word word, double left, double? nextLeft, double tolerance)
    {
        var x = word.BoundingBox.Left;
        return x >= left - tolerance && (nextLeft is null || x < nextLeft.Value - tolerance);
    }

    private static string ExtractPdfAmount(string value)
    {
        // Hem Türkçe (1.234,56) hem banka/İngilizce (1,234.56) sayı biçimini
        // tam olarak yakala. İngilizce biçim önce gelmeli; aksi halde
        // "63,910.00" değeri yanlışlıkla "63,91" olarak kesilir.
        var matches = Regex.Matches(value,
            @"[-+]?\d{1,3}(?:,\d{3})+\.\d{2}|[-+]?\d{1,3}(?:\.\d{3})+,\d{2}|[-+]?\d+(?:[.,]\d{2})");
        return matches.Count == 0 ? "" : matches[^1].Value.Replace(" ", "");
    }

    private static bool TryParsePdfDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value.Replace('.', '/').Replace('-', '/'),
            ["dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yyyy", "yyyy/MM/dd"],
            new CultureInfo("tr-TR"), DateTimeStyles.None, out date);

    private static bool HasAny(string value, params string[] aliases) => aliases.Any(value.Contains);

    [GeneratedRegex(@"^\d{1,2}[./-]\d{1,2}[./-]\d{4}$")]
    private static partial Regex DateTokenRegex();

    [GeneratedRegex(@"^\d{1,2}:\d{2}(:\d{2})?$")]
    private static partial Regex TimeTokenRegex();

    private static List<BudgetEntry> ParsePdfEntries(string text, int userId)
    {
        var entries = new List<BudgetEntry>();
        // Bazı banka PDF'lerinde satır sonları metin olarak bulunmaz. Bu yüzden
        // işlemleri tarih-saat ile başlayıp tutar-bakiye ile biten bloklar olarak okuruz.
        var transactionPattern = new Regex(
            @"(?<date>\d{2}/\d{2}/\d{4})(?<time>\d{2}:\d{2}:\d{2})(?<details>.*?)(?<amount>-?\d{1,3}(?:\.\d{3})*,\d{2})\s*TL\s*(?<balance>-?\d{1,3}(?:\.\d{3})*,\d{2})\s*TL",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        foreach (Match match in transactionPattern.Matches(text))
        {
            if (!DateTime.TryParseExact(
                    $"{match.Groups["date"].Value} {match.Groups["time"].Value}",
                    "dd/MM/yyyy HH:mm:ss", new CultureInfo("tr-TR"),
                    DateTimeStyles.None, out var entryDate)) continue;

            var description = match.Groups["details"].Value.Trim();
            var amountText = match.Groups["amount"].Value;
            var normalizedDescription = HeaderKey(description);
            if (normalizedDescription.Contains("parayatirma"))
            {
                // Yapı Kredi PDF'sinde ATM numarası ve tutar bazen bitişik gelir:
                // ... 109865 14740243.200,00 TL. Son 6 haneli hesap numarasını
                // ayırıp gerçek tutarı (43.200,00) kullanırız.
                var atmAmount = Regex.Match(
                    match.Value,
                    @"\d{6}(?<amount>-?\d{1,3}(?:\.\d{3})*,\d{2})\s*TL",
                    RegexOptions.CultureInvariant);
                if (atmAmount.Success) amountText = atmAmount.Groups["amount"].Value;
            }

            if (!TryParseAmount(amountText, out var rawAmount) || rawAmount == 0) continue;
            var type = ResolveType(description, rawAmount);
            entries.Add(new BudgetEntry
            {
                UserId = userId,
                EntryDate = ToIstanbulUtc(entryDate),
                Description = description,
                Amount = decimal.Round(Math.Abs(rawAmount), 2),
                Type = type,
                Category = GuessCategory(description, type),
                Source = "PDF ekstresi",
                Treatment = InitialTreatment(description, type)
            });
        }

        return entries;
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmStatementImportRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        if (request.Rows is null || request.Rows.Count == 0)
        {
            return BadRequest(new { message = "Kaydedilecek ekstre işlemi bulunamadı." });
        }

        var existing = await db.BudgetEntries
            .Where(entry => entry.UserId == userId)
            .Select(entry => new { entry.EntryDate, entry.Description, entry.Amount, entry.Type })
            .ToListAsync();
        var known = existing
            .Select(item => $"{item.EntryDate:yyyy-MM-dd}|{item.Type}|{item.Amount:0.00}|{item.Description.Trim().ToUpperInvariant()}")
            .ToHashSet();
        var userRules = await db.MerchantCategoryRules
            .Where(rule => rule.UserId == userId)
            .ToListAsync();
        var entries = new List<BudgetEntry>();
        var receivables = new List<Receivable>();
        foreach (var row in request.Rows)
        {
            if (!Enum.TryParse<BudgetEntryType>(row.Type, true, out var type))
            {
                return BadRequest(new { message = "Ekstre satır türü geçersiz. Tür Income veya Expense olmalıdır." });
            }

            var entry = new BudgetEntry
            {
                UserId = userId.Value,
                EntryDate = row.EntryDate,
                Description = row.Description.Trim(),
                Amount = decimal.Round(Math.Abs(row.Amount), 2),
                Type = type,
                Category = row.Category.Trim(),
                Source = string.IsNullOrWhiteSpace(row.Source) ? "PDF ekstresi" : row.Source.Trim(),
                Treatment = string.IsNullOrWhiteSpace(row.Treatment)
                    ? (NeedsUserReview(row.Description, type) ? "NeedsReview" : "Budget")
                    : row.Treatment.Trim()
            };

            if (entry.Treatment is not ("Budget" or "NeedsReview" or "Receivable" or "Payable" or "Cash" or "CashWithdrawal" or "CashDeposit" or "Excluded"))
                return BadRequest(new { message = "Ekstre işlem kararı geçersiz." });

            // Kullanıcının seçtiği nakit yönü, PDF'deki işlem türünden daha
            // belirleyicidir. Böylece yanlış/eksik banka türü bilgisi bakiyeyi
            // ters yönde değiştiremez.
            if (entry.Treatment == "CashWithdrawal") entry.Type = BudgetEntryType.Expense;
            if (entry.Treatment == "CashDeposit") entry.Type = BudgetEntryType.Income;
            if (entry.Category == "Yatırım / Varlıklarım" && entry.Treatment is ("Budget" or "NeedsReview"))
                entry.Treatment = "Investment";

            if (entry.Treatment is "Receivable" or "Payable")
            {
                if (string.IsNullOrWhiteSpace(row.PersonName))
                    return BadRequest(new { message = "Borç kaydı için kişi adı yazılmalıdır." });
            }

            if (!known.Add(Key(entry))) continue;
            entries.Add(entry);

            if (entry.Treatment is "Receivable" or "Payable")
            {
                receivables.Add(new Receivable
                {
                    UserId = userId.Value,
                    PersonName = row.PersonName!.Trim(),
                    Direction = entry.Treatment == "Payable" ? "Payable" : "Receivable",
                    Amount = entry.Amount,
                    EntryDate = entry.EntryDate,
                    Description = string.IsNullOrWhiteSpace(row.Note) ? entry.Description : row.Note.Trim()
                });
            }

            if (entry.Category != "Diğer" && entry.Category != "Belirsiz")
            {
                var keyword = ExtractMerchantKeyword(entry.Description);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    // Aynı işletme aynı ekstrede birden fazla kez geçebilir. Yeni
                    // eklenen ama henüz kaydedilmemiş kuralı da listede tutarız.
                    var rule = userRules.FirstOrDefault(item =>
                        item.Keyword == keyword && item.Type == type);
                    if (rule is null)
                    {
                        rule = new MerchantCategoryRule
                        {
                            UserId = userId,
                            Keyword = keyword,
                            Category = entry.Category,
                            Type = type
                        };
                        userRules.Add(rule);
                        db.MerchantCategoryRules.Add(rule);
                    }
                    else rule.Category = entry.Category;
                }
            }
        }

        // Borç kayıtları ayrı tabloda tutulur; ana bütçe listesine gelir/gider
        // satırı olarak eklenmez. Bakiye hesabında ayrıca nakit hareketi olarak
        // dikkate alınırlar.
        if (receivables.Count > 0) db.Receivables.AddRange(receivables);
        db.BudgetEntries.AddRange(entries.Where(entry => entry.Treatment is not ("Receivable" or "Payable")));
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        return Ok(new { message = $"{entries.Count} ekstre işlemi kaydedildi.", imported = entries.Count });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static string Key(BudgetEntry entry) =>
        $"{entry.EntryDate:yyyy-MM-dd}|{entry.Type}|{entry.Amount:0.00}|{entry.Description.Trim().ToUpperInvariant()}";

    private static string Key(dynamic entry) =>
        $"{entry.EntryDate:yyyy-MM-dd}|{entry.Type}|{entry.Amount:0.00}|{entry.Description.Trim().ToUpperInvariant()}";

    private static BudgetEntryType ResolveType(string typeText, decimal amount)
    {
        var normalized = HeaderKey(typeText);
        // Banka PDF'lerinde tutarın önündeki ayraç bazen eksi işareti gibi
        // okunabiliyor. Bu nedenle önce işlem açıklamasındaki anlamı kontrol ederiz.
        if (normalized.Contains("gelen") || normalized.Contains("parayatirma") ||
            normalized.Contains("maas") || normalized.Contains("gelir")) return BudgetEntryType.Income;
        if (normalized.Contains("giden") || normalized.Contains("ucreti") ||
            normalized.Contains("fatura") || normalized.Contains("pos") ||
            normalized.Contains("epos") || normalized.Contains("qr") ||
            normalized.Contains("odeme") || normalized.Contains("gider")) return BudgetEntryType.Expense;
        if (normalized.Contains("gelir") || normalized.Contains("income") || normalized.Contains("credit")) return BudgetEntryType.Income;
        if (normalized.Contains("gider") || normalized.Contains("expense") || normalized.Contains("debit")) return BudgetEntryType.Expense;
        return amount > 0 ? BudgetEntryType.Income : BudgetEntryType.Expense;
    }

    private static string GuessCategory(string description, BudgetEntryType type)
    {
        var text = HeaderKey(description);
        var investment = BestCategory(text,
            ("Yatırım / Varlıklarım", new[] { "altin", "gramaltin", "ceyrekaltin", "cumhuriyetaltini", "doviz", "euro", "usd", "dolar", "sterlin", "kuyumcu", "hisse", "borsa", "yatirimfonu", "menkul", "repo", "vadeli", "kripto", "bitcoin", "ethereum", "coin" }));
        if (investment is not "Diğer") return investment;

        if (type == BudgetEntryType.Income)
        {
            return BestCategory(text,
                ("Maaş", new[] { "maas", "maasodeme", "maasgelir", "salary", "ucretodemesi", "avans", "prim", "ikramiye", "emekliayligi", "sgk" }),
                ("Harçlık", new[] { "harclik", "cep harcligi", "cep harclik" }),
                ("Para Yatırma", new[] { "parayatirma", "nakityatirma", "hesabayatirma" }),
                ("Para Transferi", new[] { "gelenfast", "gelenhavale", "geleneft", "paratransferi", "paragonder", "havalealacak" })) is var incomeCategory and not "Diğer"
                ? incomeCategory
                : "Ek gelir";
        }

        var expenseCategory = BestCategory(text,
            ("Para Çekme", new[] { "nakitcek", "paracek", "atmnakitcek", "hesaptancekim" }),
            ("Komisyon / Ücret", new[] { "ucreti", "komisyon", "bsmv", "masraf", "hizmetbedeli", "kartaidati", "hesapisletim", "poskomisyon" }),
            ("Para Transferi", new[] { "gidenfast", "gidenhavale", "gideneft", "paratransferi", "havale", "eft" }),
            ("Market", new[] { "market", "bim", "a101", "migros", "carrefour", "carrefoursa", "sok", "sokmarket", "macrocenter", "filemarket", "hakmar", "onurmarket", "bizimtop", "metromarket", "secmarket" }),
            ("Yemek / Kafe", new[] { "kafe", "cafe", "restoran", "restaurant", "lokanta", "yemek", "kahve", "pizza", "burger", "dondurma", "nido", "mcdonalds", "burgerking", "kfc", "dominos", "getir", "yemeksepeti", "starbucks", "kahvedunyasi", "bigchefs", "simit", "pastane", "firin", "kasap", "etvetavuk", "balik", "cigkofte" }),
            ("Alışveriş", new[] { "trendyol", "hepsiburada", "amazon", "iyzico", "alisveris", "eticaret", "online", "siparis", "n11", "sahibinden", "dolap", "etsy", "shopier", "lcwaikiki" }),
            ("Giyim", new[] { "giyim", "kiyafet", "ayakkabi", "zara", "koton", "mavi", "defacto", "boyner", "hm", "hummel", "nike", "adidas", "puma", "stradivarius", "pullandbear", "bershka", "gap" }),
            ("Spor", new[] { "spor", "fitness", "gym", "pilates", "yoga", "yuzme", "futbol", "basketbol", "tenis", "halisaha", "decathlon", "macfit", "spor salonu", "formasalonu", "voleybol", "kayak", "kosu" }),
            ("Fatura", new[] { "fatura", "elektrik", "elektrikdagitim", "su", "dogalgaz", "internet", "telefon", "gsm", "avea", "turkcell", "vodafone", "turktelekom", "superonline", "digiturk", "dsmart" }),
            ("Kira", new[] { "kira", "kirasi", "konutkirasi", "isyeri kirasi" }),
            ("Ulaşım", new[] { "metro", "otobus", "otocar", "taksi", "akaryakit", "petrol", "petrolgaz", "aytemiz", "petrolc", "benzin", "motorin", "dizel", "lpg", "shell", "opet", "bp", "total", "goodyear", "lastik", "servis", "oto", "istanbulkart", "ankarakart", "istanbululasim", "istasyon", "gaz", "ispark", "otopark", "uber", "bitaksi", "marti", "scooter" }),
            ("Abonelik", new[] { "abonelik", "netflix", "spotify", "youtube", "dijital", "apple", "primevideo", "disney", "blutv", "exxen", "gamepass", "icloud", "googleone", "uyelik" }),
            ("Sağlık", new[] { "saglik", "eczane", "hastane", "doktor", "klinik", "medikal", "dishekimi", "disci", "optik", "lens", "veteriner", "laboratuvar", "checkup", "ilac", "muayene" }),
            ("Eğitim", new[] { "kitap", "kitapdunyasi", "kitapyurdu", "kurs", "egitim", "egitimodeme", "okul", "universite", "kolej", "dershane", "etut", "sinav", "yks", "lise", "ilkokul", "yukseklisans", "dilkursu", "udemy", "coursera", "sertifika", "kirtasiye" }),
            ("Teknoloji", new[] { "elektronik", "teknoloji", "bilgisayar", "telefon", "vatan", "teknosa", "mediamarkt", "samsung", "huawei", "lenovo", "asus", "playstation", "xbox", "steam", "epicgames", "yazilim", "hosting", "domain" }),
            ("Ev", new[] { "ev", "mobilya", "dekorasyon", "emlak", "ikea", "koctas", "bauhaus", "englishhome", "madamecoco", "zucaciye", "beyazesya", "klima", "tesisat", "temizlik", "perde", "hali" }),
            ("Vergi / Resmi", new[] { "vergi", "resmi", "harc", "noter", "mahkeme", "belediye", "trafikcezasi", "pasaport", "ehliyet", "tapu", "sgkprim" }));
        if (expenseCategory is not "Diğer") return expenseCategory;
        if (HasAny(text, "qr")) return "Para Transferi";
        return "Diğer";
    }

    private static string BestCategory(string text, params (string Category, string[] Keywords)[] groups)
    {
        var ignoredShortKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "ev", "su", "gaz", "oto", "sok", "bp", "hm", "qr", "eft", "pos"
        };

        var match = groups
            .SelectMany(group => group.Keywords.Select(keyword => new
            {
                group.Category,
                Keyword = HeaderKey(keyword)
            }))
            .Where(item => item.Keyword.Length >= 4 || !ignoredShortKeywords.Contains(item.Keyword))
            .Where(item => text.Contains(item.Keyword, StringComparison.Ordinal))
            .OrderByDescending(item => item.Keyword.Length)
            .FirstOrDefault();

        return match?.Category ?? "Diğer";
    }

    private static bool NeedsUserReview(string description, BudgetEntryType type)
    {
        var category = GuessCategory(description, type);
        return category is "Para Transferi" or "Para Yatırma" or "Para Çekme";
    }

    private static string InitialTreatment(string description, BudgetEntryType type)
    {
        var category = GuessCategory(description, type);
        if (category == "Yatırım / Varlıklarım") return "Investment";
        return NeedsUserReview(description, type) ? "NeedsReview" : "Budget";
    }

    private async Task ApplyMerchantRules(List<BudgetEntry> entries, int userId)
    {
        var rules = await db.MerchantCategoryRules
            .Where(rule => rule.UserId == null || rule.UserId == userId)
            .OrderByDescending(rule => rule.UserId == userId)
            .ThenByDescending(rule => rule.Keyword.Length)
            .ToListAsync();

        foreach (var entry in entries)
        {
            var text = MerchantCategoryRulesController.NormalizeKeyword(entry.Description);
            var rule = rules.FirstOrDefault(item =>
                item.Type == entry.Type && text.Contains(item.Keyword));
            if (rule is not null)
            {
                entry.Category = rule.Category;
                // Kullanıcı bu kelime için açıkça bir kategori belirlediyse
                // aynı işlem sonraki ekstrelerde tekrar sorulmaz. Özel
                // transfer kategorileri ise yine kullanıcı incelemesine kalır.
                if (entry.Category == "Yatırım / Varlıklarım")
                    entry.Treatment = "Investment";
                else if (entry.Category is not ("Para Transferi" or "Para Yatırma" or "Para Çekme"))
                    entry.Treatment = "Budget";
            }
        }
    }

    private static string ExtractMerchantKeyword(string description)
    {
        var text = MerchantCategoryRulesController.NormalizeKeyword(description);
        var knownMerchants = new[]
        {
            "iyzico", "aytemiz", "shell", "trendyol", "migros", "bim", "a101",
            "avea", "ispark", "metro", "nido", "pentas", "kitapdunyasi",
            "metindemir", "mustafakaratas", "cansuceri", "meryemkasikci"
        };
        return knownMerchants.FirstOrDefault(text.Contains) ?? text;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParse(value, new CultureInfo("tr-TR"), DateTimeStyles.AssumeUniversal, out date)) return true;
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
        {
            try { date = DateTime.FromOADate(serial); return true; } catch (ArgumentException) { }
        }
        date = default;
        return false;
    }

    private static DateTime ToIstanbulUtc(DateTime localDateTime)
    {
        var local = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Replace("₺", "").Replace("TL", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "").Trim();
        if (normalized.StartsWith("(") && normalized.EndsWith(")"))
            normalized = "-" + normalized[1..^1];

        // Bankalar hem 1.234,56 hem de 1,234.56 biçimini kullanabilir.
        // Hangi ayıracın ondalık olduğunu son ayıraca bakarak belirleriz.
        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            var culture = lastComma > lastDot ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
            if (decimal.TryParse(normalized, NumberStyles.Any, culture, out amount)) return true;
        }
        else if (lastComma >= 0)
        {
            var decimals = normalized.Length - lastComma - 1;
            var culture = decimals == 2 ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
            if (decimal.TryParse(normalized, NumberStyles.Any, culture, out amount)) return true;
        }
        else if (lastDot >= 0)
        {
            var decimals = normalized.Length - lastDot - 1;
            var culture = decimals == 2 ? CultureInfo.InvariantCulture : new CultureInfo("tr-TR");
            if (decimal.TryParse(normalized, NumberStyles.Any, culture, out amount)) return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.Any, new CultureInfo("tr-TR"), out amount)) return true;
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
    }

    private static bool FindHeader(IReadOnlyDictionary<int, string> headers, IEnumerable<string> names, out int column)
    {
        foreach (var name in names)
        {
            var normalized = HeaderKey(name);
            var match = headers.FirstOrDefault(item => item.Value == normalized);
            if (!match.Equals(default(KeyValuePair<int, string>))) { column = match.Key; return true; }
        }
        column = -1;
        return false;
    }

    private static string HeaderKey(string value) => value.Trim()
        .Replace("İ", "i")
        .ToLowerInvariant()
        .Replace("\u0307", "")
        .Replace("ı", "i")
        .Replace("ş", "s")
        .Replace("ğ", "g")
        .Replace("ü", "u")
        .Replace("ö", "o")
        .Replace("ç", "c")
        .Replace(" ", "")
        .Replace("-", "");

    private static string CellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var value = cell.Element(cell.Name.Namespace + "v")?.Value ?? cell.Element(cell.Name.Namespace + "is")?.Element(cell.Name.Namespace + "t")?.Value ?? "";
        if (cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Count) value = sharedStrings[index];
        return value.Trim();
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si").Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value))).ToList();
    }

    private static int ColumnIndex(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
        var index = 0;
        foreach (var letter in letters) index = index * 26 + char.ToUpperInvariant(letter) - 'A' + 1;
        return index - 1;
    }
}

public record ConfirmStatementImportRequest(List<ConfirmStatementImportRow> Rows);
public record ConfirmStatementImportRow(DateTime EntryDate, string Description, decimal Amount, string Type, string Category, string? Source = null, string? Treatment = null, string? PersonName = null, string? Note = null);
