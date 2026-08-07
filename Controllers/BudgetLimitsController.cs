using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/budget-limits")]
[Authorize]
public class BudgetLimitsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLimits([FromQuery] string? month)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var selectedMonth = string.IsNullOrWhiteSpace(month)
            ? DateTime.UtcNow.ToString("yyyy-MM")
            : month.Trim();

        if (!IsValidMonth(selectedMonth))
        {
            return BadRequest(new { message = "Ay bilgisi yyyy-AA formatında olmalıdır." });
        }

        var selectedYear = int.Parse(selectedMonth.Substring(0, 4));
        var selectedMonthNumber = int.Parse(selectedMonth.Substring(5, 2));

        var limits = await db.BudgetLimits
            .Where(limit => limit.UserId == userId && limit.Month == selectedMonth)
            .OrderBy(limit => limit.Category)
            .Select(limit => new
            {
                limit.Id,
                limit.Month,
                limit.Category,
                limit.LimitAmount,
                spent = db.BudgetEntries
                    .Where(entry => entry.UserId == userId &&
                                    entry.Type == BudgetEntryType.Expense &&
                                    entry.Category == limit.Category &&
                                    entry.EntryDate.Year == selectedYear &&
                                    entry.EntryDate.Month == selectedMonthNumber)
                    .Sum(entry => (decimal?)entry.Amount) ?? 0
            })
            .ToListAsync();

        return Ok(limits);
    }

    [HttpPost]
    public async Task<IActionResult> Save(SaveBudgetLimitRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (!IsValidMonth(request.Month) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "Ay ve kategori bilgisi zorunludur." });
        }

        if (request.LimitAmount <= 0)
        {
            return BadRequest(new { message = "Bütçe limiti sıfırdan büyük olmalıdır." });
        }

        var limit = await db.BudgetLimits.SingleOrDefaultAsync(item =>
            item.UserId == userId &&
            item.Month == request.Month &&
            item.Category == request.Category.Trim());

        if (limit is null)
        {
            limit = new BudgetLimit
            {
                UserId = userId.Value,
                Month = request.Month.Trim(),
                Category = request.Category.Trim()
            };
            db.BudgetLimits.Add(limit);
        }

        limit.LimitAmount = decimal.Round(request.LimitAmount, 2);
        await db.SaveChangesAsync();

        return Ok(new { message = "Aylık bütçe limiti kaydedildi." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var limit = await db.BudgetLimits
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (limit is null)
        {
            return NotFound(new { message = "Bütçe limiti bulunamadı." });
        }

        db.BudgetLimits.Remove(limit);
        await db.SaveChangesAsync();
        return Ok(new { message = "Bütçe limiti kaldırıldı." });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static bool IsValidMonth(string value) =>
        value.Length == 7 &&
        value[4] == '-' &&
        int.TryParse(value[..4], out _) &&
        int.TryParse(value[5..7], out var month) &&
        month is >= 1 and <= 12;
}

public record SaveBudgetLimitRequest(string Month, string Category, decimal LimitAmount);
