using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/deposit-requests")]
[Authorize]
public class DepositRequestsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequests()
    {
        var query = db.DepositRequests
            .Include(request => request.Account)
            .ThenInclude(account => account!.Customer)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var customerId = await GetCurrentCustomerId();
            if (customerId is null) return Ok(Array.Empty<object>());
            query = query.Where(request => request.Account!.CustomerId == customerId);
        }

        return Ok(await query
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new
            {
                request.Id,
                request.AccountId,
                accountNumber = request.Account!.AccountNumber,
                customerName = request.Account.Customer!.FullName,
                request.Amount,
                status = request.Status.ToString(),
                request.CreatedAt,
                request.ReviewedAt
            })
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepositRequest request)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Para yatırma tutarı sıfırdan büyük olmalıdır." });
        }

        var account = await db.Accounts.FindAsync(request.AccountId);
        if (account is null || !account.IsActive)
        {
            return BadRequest(new { message = "Aktif bir hesap seçilmelidir." });
        }

        if (!User.IsInRole("Admin"))
        {
            var customerId = await GetCurrentCustomerId();
            if (customerId != account.CustomerId)
            {
                return Forbid();
            }
        }

        var deposit = new DepositRequest
        {
            AccountId = request.AccountId,
            Amount = request.Amount
        };

        db.DepositRequests.Add(deposit);
        await db.SaveChangesAsync();

        return Ok(new { message = "Para yatırma talebin admin onayına gönderildi.", deposit.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var deposit = await db.DepositRequests
            .Include(item => item.Account)
            .ThenInclude(account => account!.Customer)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (deposit is null) return NotFound(new { message = "Para yatırma talebi bulunamadı." });
        if (deposit.Status != DepositStatus.Pending)
            return BadRequest(new { message = "Bu talep daha önce sonuçlandırılmış." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        deposit.Status = DepositStatus.Approved;
        deposit.ReviewedAt = DateTime.UtcNow;
        deposit.Account!.Balance += deposit.Amount;
        AddAuditLog("Para yatırma onaylandı", $"{deposit.Account.Customer!.FullName} hesabına {deposit.Amount:0.00} TL yatırıldı.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { message = "Para yatırma talebi onaylandı ve bakiye güncellendi." });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id)
    {
        var deposit = await db.DepositRequests
            .Include(item => item.Account)
            .ThenInclude(account => account!.Customer)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (deposit is null) return NotFound(new { message = "Para yatırma talebi bulunamadı." });
        if (deposit.Status != DepositStatus.Pending)
            return BadRequest(new { message = "Bu talep daha önce sonuçlandırılmış." });

        deposit.Status = DepositStatus.Rejected;
        deposit.ReviewedAt = DateTime.UtcNow;
        AddAuditLog("Para yatırma reddedildi", $"{deposit.Account!.Customer!.FullName} hesabı için para yatırma talebi reddedildi.");
        await db.SaveChangesAsync();

        return Ok(new { message = "Para yatırma talebi reddedildi." });
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
}

public record CreateDepositRequest(int AccountId, decimal Amount);
