using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/recurring-payments")]
[Authorize]
public class RecurringPaymentsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPayments()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var payments = await db.RecurringPayments
            .Where(payment => payment.UserId == userId)
            .OrderBy(payment => payment.DueDay)
            .ToListAsync();

        return Ok(payments);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment(CreateRecurringPaymentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Ödeme adı zorunludur." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Tutar sıfırdan büyük olmalıdır." });
        }

        if (request.DueDay is < 1 or > 28)
        {
            return BadRequest(new { message = "Ödeme günü 1 ile 28 arasında olmalıdır." });
        }

        var payment = new RecurringPayment
        {
            UserId = userId.Value,
            Name = request.Name.Trim(),
            Amount = decimal.Round(request.Amount, 2),
            DueDay = request.DueDay,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Abonelik" : request.Category.Trim()
        };

        db.RecurringPayments.Add(payment);
        await db.SaveChangesAsync();

        return Ok(payment);
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> TogglePayment(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var payment = await db.RecurringPayments
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (payment is null)
        {
            return NotFound(new { message = "Tekrarlayan ödeme bulunamadı." });
        }

        payment.IsActive = !payment.IsActive;
        await db.SaveChangesAsync();

        return Ok(payment);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var payment = await db.RecurringPayments
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (payment is null)
        {
            return NotFound(new { message = "Tekrarlayan ödeme bulunamadı." });
        }

        db.RecurringPayments.Remove(payment);
        await db.SaveChangesAsync();

        return Ok(new { message = "Tekrarlayan ödeme kaldırıldı." });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public record CreateRecurringPaymentRequest(string Name, decimal Amount, int DueDay, string? Category = null);
