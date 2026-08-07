using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/transaction-reviews")]
[Authorize]
public class TransactionReviewsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPending()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var items = await db.BudgetEntries
            .Where(entry => entry.UserId == userId && entry.Treatment == "NeedsReview")
            .OrderByDescending(entry => entry.EntryDate)
            .Select(entry => new
            {
                entry.Id,
                entry.EntryDate,
                type = entry.Type.ToString(),
                entry.Category,
                entry.Description,
                entry.Amount,
                kind = entry.Category == "Para Çekme" ? "Para çekme" : entry.Category == "Para Yatırma" ? "Para yatırma" : "Para transferi"
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, ResolveTransactionRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var entry = await db.BudgetEntries
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.Treatment == "NeedsReview");
        if (entry is null) return NotFound(new { message = "İnceleme bekleyen işlem bulunamadı." });

        var decision = request.Decision.Trim().ToLowerInvariant();
        switch (decision)
        {
            case "receivable":
            case "payable":
                if (string.IsNullOrWhiteSpace(request.PersonName))
                    return BadRequest(new { message = "Borç kaydı için kişi adı yazılmalıdır." });

                db.Receivables.Add(new Receivable
                {
                    UserId = userId.Value,
                    SourceEntryId = entry.Id,
                    PersonName = request.PersonName.Trim(),
                    Direction = decision == "payable" ? "Payable" : "Receivable",
                    Amount = entry.Amount,
                    EntryDate = entry.EntryDate,
                    DueDate = request.DueDate,
                    Description = string.IsNullOrWhiteSpace(request.Note) ? entry.Description : request.Note.Trim()
                });
                entry.Treatment = "Receivable";
                break;
            case "budget":
                if (!IsSelectableBudgetCategory(request.Category))
                    return BadRequest(new { message = "Bu işlem için gerçek bir harcama veya gelir kategorisi seçilmelidir." });
                entry.Treatment = "Budget";
                if (!string.IsNullOrWhiteSpace(request.Category)) entry.Category = request.Category.Trim();
                if (!string.IsNullOrWhiteSpace(request.Note)) entry.Description = request.Note.Trim();
                break;
            case "cash":
                entry.Treatment = "Cash";
                if (!string.IsNullOrWhiteSpace(request.Note)) entry.Description = request.Note.Trim();
                break;
            case "other":
                entry.Treatment = "Excluded";
                if (!string.IsNullOrWhiteSpace(request.Note)) entry.Description = request.Note.Trim();
                break;
            default:
                return BadRequest(new { message = "Geçersiz işlem amacı seçildi." });
        }

        await db.SaveChangesAsync();
        return Ok(new
        {
            message = decision == "receivable"
                ? "İşlem Alacaklarım bölümüne kaydedildi."
                : decision == "payable"
                    ? "İşlem Vereceklerim bölümüne kaydedildi."
                    : "İşlem sınıflandırıldı."
        });
    }

    [HttpGet("receivables")]
    public async Task<IActionResult> GetReceivables()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var items = await db.Receivables
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.EntryDate)
            .Select(item => new
            {
                item.Id,
                item.PersonName,
                item.Direction,
                item.Amount,
                item.EntryDate,
                item.DueDate,
                item.Description,
                item.Status
            })
            .ToListAsync();
        return Ok(items);
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static bool IsSelectableBudgetCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category) &&
        category is not ("Para Transferi" or "Para Yatırma" or "Para Çekme");
}

public record ResolveTransactionRequest(
    string Decision,
    string? PersonName = null,
    string? Note = null,
    DateTime? DueDate = null,
    string? Category = null);
