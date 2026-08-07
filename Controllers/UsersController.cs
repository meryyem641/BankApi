using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await db.Users
            .Include(user => user.Customer)
            .OrderBy(user => user.Id)
            .Select(user => new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.Phone,
                user.Role,
                user.IsApproved,
                user.IsActive,
                user.LastLoginAt,
                user.FailedLoginCount,
                user.CustomerId,
                customerName = user.Customer == null
                    ? null
                    : user.Customer.FullName
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{userId:int}/customer")]
    public async Task<IActionResult> AssignCustomer(
        int userId,
        AssignUserCustomerRequest request)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        if (request.CustomerId is not null)
        {
            var customerExists = await db.Customers
                .AnyAsync(customer => customer.Id == request.CustomerId);

            if (!customerExists)
            {
                return NotFound(new { message = "Müşteri bulunamadı." });
            }
        }

        user.CustomerId = request.CustomerId;
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = request.CustomerId is null
                ? "Kullanıcının müşteri bağlantısı kaldırıldı."
                : "Kullanıcı müşteriye bağlandı.",
            user.Id,
            user.UserName,
            user.CustomerId
        });
    }

    [HttpPut("{userId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int userId, UpdateUserStatusRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId);
        if (user is null) return NotFound(new { message = "Kullanıcı bulunamadı." });
        if (user.Role == "Admin" && !request.IsActive)
        {
            return BadRequest(new { message = "Admin hesabı pasif hale getirilemez." });
        }

        user.IsActive = request.IsActive;
        AddAuditLog(
            request.IsActive ? "Kullanıcı aktifleştirildi" : "Kullanıcı pasifleştirildi",
            $"{user.UserName} kullanıcısının durumu güncellendi.");
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = request.IsActive ? "Kullanıcı aktifleştirildi." : "Kullanıcı pasifleştirildi.",
            user.Id,
            user.IsActive
        });
    }

    [HttpPut("{userId:int}/approve")]
    public async Task<IActionResult> ApproveUser(int userId)
    {
        var user = await db.Users
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        user.IsApproved = true;

        if (user.Role != "Admin" && user.Customer is null)
        {
            user.Customer = new Customer
            {
                FullName = user.UserName,
                Email = user.Email,
                Phone = user.Phone
            };
        }

        if (user.Role != "Admin" && user.Customer is not null)
        {
            await EnsureAccount(user.Customer);
        }

        AddAuditLog("Kullanıcı onaylandı", $"{user.UserName} kullanıcısının başvurusu onaylandı.");
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Kullanıcı onaylandı ve müşteri kaydı oluşturuldu.",
            user.Id,
            user.UserName,
            user.IsApproved,
            user.CustomerId
        });
    }

    [HttpPost("approve-all-pending")]
    public async Task<IActionResult> ApproveAllPendingUsers()
    {
        var pendingUsers = await db.Users
            .Include(item => item.Customer)
            .Where(user => !user.IsApproved)
            .ToListAsync();

        foreach (var user in pendingUsers)
        {
            user.IsApproved = true;

            if (user.Role != "Admin" && user.Customer is null)
            {
                user.Customer = new Customer
                {
                    FullName = user.UserName,
                    Email = user.Email,
                    Phone = user.Phone
                };
            }

            if (user.Role != "Admin" && user.Customer is not null)
            {
                await EnsureAccount(user.Customer);
            }

            AddAuditLog("Kullanıcı onaylandı", $"{user.UserName} kullanıcısının başvurusu onaylandı.");
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"{pendingUsers.Count} kullanıcı onaylandı ve müşteri kayıtları oluşturuldu.",
            count = pendingUsers.Count
        });
    }

    [HttpDelete("{userId:int}/reject")]
    public async Task<IActionResult> RejectUser(int userId)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        if (user.IsApproved)
        {
            return BadRequest(new
            {
                message = "Onaylanmış kullanıcı reddedilemez; silme işlemini kullanın."
            });
        }

        AddAuditLog("Kullanıcı reddedildi", $"{user.UserName} kullanıcısının başvurusu reddedildi.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();

        return Ok(new { message = "Kullanıcı başvurusu reddedildi." });
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var user = await db.Users
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        if (user.Role == "Admin")
        {
            return BadRequest(new { message = "Admin kullanıcı silinemez." });
        }

        if (user.Customer is null)
        {
            AddAuditLog("Kullanıcı silindi", $"{user.UserName} kullanıcısı silindi.");
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            return Ok(new { message = "Kullanıcı silindi." });
        }

        var accountIds = await db.Accounts
            .Where(account => account.CustomerId == user.Customer.Id)
            .Select(account => account.Id)
            .ToListAsync();

        var linkedUserCount = await db.Users
            .CountAsync(item => item.CustomerId == user.Customer.Id);

        if (linkedUserCount > 1)
        {
            return Conflict(new
            {
                message = "Kullanıcı, başka bir kullanıcıyla aynı müşteriye bağlı olduğu için silinemiyor."
            });
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

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

        db.Customers.Remove(user.Customer);
        AddAuditLog("Kullanıcı silindi", $"{user.UserName} kullanıcısı ve bağlı müşteri kaydı silindi.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { message = "Kullanıcı ve bağlı müşteri kaydı silindi." });
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

    private async Task EnsureAccount(Customer customer)
    {
        if (customer.Id > 0 && await db.Accounts.AnyAsync(account => account.CustomerId == customer.Id))
        {
            return;
        }

        string accountNumber;
        do
        {
            accountNumber = Random.Shared
                .NextInt64(1_000_000_000_000_000, 9_999_999_999_999_999)
                .ToString();
        }
        while (await db.Accounts.AnyAsync(account => account.AccountNumber == accountNumber));

        db.Accounts.Add(new Account
        {
            Customer = customer,
            AccountNumber = accountNumber,
            Balance = 0,
            IsActive = true
        });
    }

}

public record UpdateUserStatusRequest(bool IsActive);
