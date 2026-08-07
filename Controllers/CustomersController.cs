using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Xml.Linq;
using System.Net.Mail;
using System.Security.Claims;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public class CustomersController(BankDbContext db) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetCustomers()
    {
        var query = db.Customers.AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var customerId = await GetCurrentCustomerId();
            if (customerId is null) return Ok(new List<Customer>());
            query = query.Where(customer => customer.Id == customerId);
        }

        return Ok(await query
            .OrderBy(customer => customer.Id)
            .ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Customer>> GetCustomerById(int id)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is not null && !User.IsInRole("Admin"))
        {
            var customerId = await GetCurrentCustomerId();
            if (customerId != customer.Id) customer = null;
        }

        return customer is null
            ? NotFound(new { message = "Müşteri bulunamadı." })
            : Ok(customer);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Customer>> CreateCustomer(
        CreateCustomerRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            !IsValidEmail(email) ||
            phone.Length < 10)
        {
            return BadRequest(new
            {
                message = "Ad soyad, geçerli bir e-posta ve en az 10 haneli telefon zorunludur."
            });
        }

        if (await db.Customers.AnyAsync(customer => customer.Email == email))
        {
            return Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });
        }

        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone
        };

        db.Customers.Add(customer);
        AddAuditLog("Müşteri eklendi", $"{customer.FullName} müşterisi oluşturuldu.");
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCustomerById),
            new { id = customer.Id },
            customer);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportCustomers(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Lütfen bir Excel dosyası seçin." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Sadece .xlsx dosyaları yüklenebilir." });
        }

        try
        {
            await using var input = file.OpenReadStream();
            using var archive = new ZipArchive(input, ZipArchiveMode.Read);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");

            if (sheetEntry is null)
            {
                return BadRequest(new { message = "Excel çalışma sayfası bulunamadı." });
            }

            using var sheetStream = sheetEntry.Open();
            var sheet = XDocument.Load(sheetStream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            var rows = sheet.Descendants(ns + "row").ToList();
            if (rows.Count < 2)
            {
                return BadRequest(new { message = "Dosyada başlık ve en az bir müşteri satırı bulunmalıdır." });
            }

            var headerColumns = new Dictionary<string, int>();
            foreach (var cell in rows[0].Elements(ns + "c"))
            {
                var column = ColumnIndex(cell.Attribute("r")?.Value ?? "");
                var header = CellValue(cell, sharedStrings);
                if (!string.IsNullOrWhiteSpace(header))
                {
                    headerColumns[HeaderKey(header)] = column;
                }
            }

            if (headerColumns.Count != 3 ||
                !TryGetColumn(headerColumns, ["adsoyad", "musteriismi", "musteriadi", "isim"], out var nameColumn) ||
                !TryGetColumn(headerColumns, ["eposta", "email", "emailadresi"], out var emailColumn) ||
                !TryGetColumn(headerColumns, ["telefon", "telefonnumarasi", "telefonno", "cept telefonu", "cepttelefonu", "no", "tel"], out var phoneColumn) ||
                nameColumn != 0 || emailColumn != 1 || phoneColumn != 2)
            {
                return BadRequest(new
                {
                    message = "Dosya sırası şu şekilde olmalıdır: A sütunu Ad Soyad, B sütunu E-posta, C sütunu Telefon. Yalnızca 3 sütunlu dosyalar kabul edilir."
                });
            }

            var existing = await db.Customers
                .Select(customer => new { customer.Email, customer.Phone })
                .ToListAsync();
            var existingEmails = existing
                .Select(item => Normalize(item.Email))
                .Where(value => value.Length > 0)
                .ToHashSet();
            var customers = new List<Customer>();
            var skipped = 0;
            var duplicates = new List<string>();

            foreach (var row in rows.Skip(1))
            {
                var values = row.Elements(ns + "c")
                    .ToDictionary(
                        cell => ColumnIndex(cell.Attribute("r")?.Value ?? ""),
                        cell => CellValue(cell, sharedStrings));

                var fullName = values.GetValueOrDefault(nameColumn, "");
                var email = NormalizeEmail(values.GetValueOrDefault(emailColumn, ""));
                var phone = values.GetValueOrDefault(phoneColumn, "");
                var normalizedPhone = NormalizePhone(phone);
                var normalizedEmail = Normalize(email);

                var isDuplicate = existingEmails.Contains(normalizedEmail);

                if (isDuplicate)
                {
                    duplicates.Add($"{fullName} - {email}");
                    skipped++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fullName) ||
                    !IsValidEmail(email) ||
                    normalizedPhone.Length < 10)
                {
                    skipped++;
                    continue;
                }

                customers.Add(new Customer
                {
                    FullName = fullName,
                    Email = email,
                    Phone = normalizedPhone
                });

                existingEmails.Add(normalizedEmail);
            }

            db.Customers.AddRange(customers);
            if (customers.Count > 0)
            {
                AddAuditLog("Müşteri içe aktarıldı", $"Excel dosyasından {customers.Count} müşteri aktarıldı.");
            }
            await db.SaveChangesAsync();

            return Ok(new
            {
                message = duplicates.Count > 0
                    ? $"{customers.Count} müşteri içe aktarıldı. {duplicates.Count} kayıt zaten mevcut olduğu için eklenmedi."
                    : $"{customers.Count} müşteri içe aktarıldı.",
                imported = customers.Count,
                skipped,
                duplicates = duplicates.Take(20).ToList()
            });
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "Geçerli bir .xlsx dosyası yükleyin." });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Customer>> UpdateCustomer(
        int id,
        UpdateCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new
            {
                message = "Müşteri bulunamadı."
            });
        }

        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            !IsValidEmail(email) ||
            phone.Length < 10)
        {
            return BadRequest(new
            {
                message = "Ad soyad, geçerli bir e-posta ve en az 10 haneli telefon zorunludur."
            });
        }

        if (await db.Customers.AnyAsync(item => item.Id != id &&
                item.Email == email))
        {
            return Conflict(new
            {
                message = "Bu e-posta adresi başka bir müşteride kayıtlı."
            });
        }

        customer.FullName = request.FullName.Trim();
        customer.Email = email;
        customer.Phone = phone;

        AddAuditLog("Müşteri güncellendi", $"{customer.FullName} müşterisinin bilgileri güncellendi.");
        await db.SaveChangesAsync();

        return Ok(customer);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<Customer>> PatchCustomer(
        int id,
        PatchCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new
            {
                message = "Müşteri bulunamadı."
            });
        }

        var email = request.Email is null
            ? customer.Email
            : NormalizeEmail(request.Email);
        var phone = request.Phone is null
            ? customer.Phone
            : NormalizePhone(request.Phone);

        if ((request.Email is not null && !IsValidEmail(email)) ||
            (request.Phone is not null && phone.Length < 10))
        {
            return BadRequest(new
            {
                message = "Geçerli bir e-posta ve en az 10 haneli telefon girilmelidir."
            });
        }

        if (await db.Customers.AnyAsync(item => item.Id != id &&
                item.Email == email))
        {
            return Conflict(new
            {
                message = "Bu e-posta adresi başka bir müşteride kayıtlı."
            });
        }

        if (request.FullName is not null)
        {
            customer.FullName = request.FullName.Trim();
        }

        if (request.Email is not null)
        {
            customer.Email = email;
        }

        if (request.Phone is not null)
        {
            customer.Phone = phone;
        }

        AddAuditLog("Müşteri güncellendi", $"{customer.FullName} müşterisinin bilgileri güncellendi.");
        await db.SaveChangesAsync();

        return Ok(customer);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new
            {
                message = "Müşteri bulunamadı."
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var accountIds = await db.Accounts
            .Where(account => account.CustomerId == id)
            .Select(account => account.Id)
            .ToListAsync();

        if (accountIds.Count > 0)
        {
            var transfers = await db.Transfers
                .Where(transfer =>
                    accountIds.Contains(transfer.SenderAccountId) ||
                    accountIds.Contains(transfer.ReceiverAccountId))
                .ToListAsync();

            db.Transfers.RemoveRange(transfers);

            var accounts = await db.Accounts
                .Where(account => accountIds.Contains(account.Id))
                .ToListAsync();

            db.Accounts.RemoveRange(accounts);
        }

        AddAuditLog("Müşteri silindi", $"{customer.FullName} müşterisi ve bağlı kayıtları silindi.");
        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }

    private void AddAuditLog(string action, string description)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserName = User.Identity?.Name ?? "Bilinmeyen kullanıcı",
            Action = action,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<int?> GetCurrentCustomerId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId)) return null;

        return await db.Users
            .Where(user => user.Id == userId)
            .Select(user => user.CustomerId)
            .SingleOrDefaultAsync();
    }

    private static string CellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var value = cell.Element(cell.Name.Namespace + "v")?.Value ??
                    cell.Element(cell.Name.Namespace + "is")?.Element(cell.Name.Namespace + "t")?.Value ?? "";

        if (cell.Attribute("t")?.Value == "s" &&
            int.TryParse(value, out var sharedIndex) &&
            sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
        {
            value = sharedStrings[sharedIndex];
        }

        return value.Trim();
    }

    private static bool TryGetColumn(
        IReadOnlyDictionary<string, int> headers,
        IEnumerable<string> names,
        out int column)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(HeaderKey(name), out column)) return true;
        }

        column = -1;
        return false;
    }

    private static string HeaderKey(string value) =>
        value.Trim().ToLowerInvariant()
            .Replace("ı", "i")
            .Replace("ş", "s")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace(" ", "")
            .Replace("-", "");

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static int ColumnIndex(string reference)
    {
        var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
        var index = 0;

        foreach (var letter in letters)
        {
            index = index * 26 + char.ToUpperInvariant(letter) - 'A' + 1;
        }

        return index - 1;
    }

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static string NormalizePhone(string value) =>
        new string(value.Where(char.IsDigit).ToArray());

    private static bool IsValidEmail(string value)
    {
        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
