using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/budget-entries")]
[Authorize]
public class BudgetEntriesController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetEntries()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var entries = await db.BudgetEntries
            .Where(entry => entry.UserId == userId && entry.Treatment == "Budget")
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new
            {
                entry.Id,
                type = entry.Type.ToString(),
                entry.Category,
                entry.Description,
                entry.Amount,
                entry.EntryDate,
                entry.CreatedAt,
                entry.Source
            })
            .ToListAsync();

        var cashFlowEntries = await db.BudgetEntries
            .Where(entry => entry.UserId == userId &&
                (entry.Treatment == "Budget" || entry.Treatment == "Investment" || entry.Treatment == "Receivable" || entry.Treatment == "Payable" || entry.Treatment == "Cash" || entry.Treatment == "CashWithdrawal" || entry.Treatment == "CashDeposit"))
            .Select(entry => new { entry.Type, entry.Amount, entry.Treatment })
            .ToListAsync();

        var investments = await db.BudgetEntries
            .Where(entry => entry.UserId == userId && entry.Treatment == "Investment")
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new
            {
                entry.Id,
                type = entry.Type.ToString(),
                entry.Category,
                entry.Description,
                entry.Amount,
                entry.EntryDate,
                entry.Source
            })
            .ToListAsync();

        var bankBalance = cashFlowEntries.Sum(entry =>
            entry.Type == BudgetEntryType.Income ? entry.Amount : -entry.Amount);
        var cashBalance = cashFlowEntries
            .Where(entry => entry.Treatment == "Cash" || entry.Treatment == "CashWithdrawal" || entry.Treatment == "CashDeposit")
            .Sum(entry => entry.Treatment == "CashWithdrawal" ||
                         (entry.Treatment == "Cash" && entry.Type == BudgetEntryType.Expense)
                ? entry.Amount
                : -entry.Amount);

        return Ok(new
        {
            entries,
            investments,
            totalIncome = entries
                .Where(entry => entry.type == nameof(BudgetEntryType.Income))
                .Sum(entry => entry.Amount),
            totalExpense = entries
                .Where(entry => entry.type == nameof(BudgetEntryType.Expense))
                .Sum(entry => entry.Amount),
            availableBalance = bankBalance,
            bankBalance,
            cashBalance,
            totalAssets = bankBalance + cashBalance
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateBudgetEntryRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Tutar sıfırdan büyük olmalıdır." });
        }

        if (!Enum.TryParse<BudgetEntryType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = "İşlem türü gelir veya gider olmalıdır." });
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Kategori seçilmelidir." });
        }

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Gelecek tarihli kayıt eklenemez." });
        }

        var entry = new BudgetEntry
        {
            UserId = userId.Value,
            Type = type,
            Category = request.Category.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Amount = decimal.Round(request.Amount, 2),
            EntryDate = request.EntryDate.Date,
            CreatedAt = DateTime.UtcNow,
            Source = "Manuel kayıt"
        };

        db.BudgetEntries.Add(entry);
        await db.SaveChangesAsync();

        return Ok(new { message = type == BudgetEntryType.Income
            ? "Gelir kaydı eklendi."
            : "Gider kaydı eklendi.", entry.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateBudgetEntryRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var entry = await db.BudgetEntries
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (entry is null)
        {
            return NotFound(new { message = "Gelir/gider kaydı bulunamadı." });
        }

        if (!Enum.TryParse<BudgetEntryType>(request.Type, true, out var type) ||
            string.IsNullOrWhiteSpace(request.Category) ||
            request.Amount <= 0)
        {
            return BadRequest(new { message = "İşlem türü, kategori ve geçerli bir tutar zorunludur." });
        }

        if (request.EntryDate.Date > DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Gelecek tarihli kayıt eklenemez." });
        }

        entry.Type = type;
        entry.Category = request.Category.Trim();
        entry.Description = request.Description?.Trim() ?? string.Empty;
        entry.Amount = decimal.Round(request.Amount, 2);
        entry.EntryDate = request.EntryDate.Date;

        await db.SaveChangesAsync();
        return Ok(new { message = "Gelir/gider kaydı güncellendi." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var entry = await db.BudgetEntries
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (entry is null)
        {
            return NotFound(new { message = "Gelir/gider kaydı bulunamadı." });
        }

        db.BudgetEntries.Remove(entry);
        await db.SaveChangesAsync();

        return Ok(new { message = "Kayıt silindi." });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public record CreateBudgetEntryRequest(
    string Type,
    string Category,
    string? Description,
    decimal Amount,
    DateTime EntryDate);
