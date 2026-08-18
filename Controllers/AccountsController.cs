using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public class AccountsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Account>>> GetAccounts()
    {
        var query = db.Accounts
            .Include(account => account.Customer)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            var userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var customerId = await db.Users
                .Where(user => user.Id == userId)
                .Select(user => user.CustomerId)
                .SingleOrDefaultAsync();

            if (customerId is null)
            {
                return Ok(new List<Account>());
            }

            query = query.Where(account => account.CustomerId == customerId);
        }

        return Ok(await query.OrderBy(account => account.Id).ToListAsync());
    }

    [HttpPost]
    public async Task<ActionResult<Account>> CreateAccount(
        CreateAccountRequest request)
    {
        if (!Enum.TryParse<AccountType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = "Hesap türü Bank veya CreditCard olmalıdır." });
        }

        int customerId;
        if (User.IsInRole("Admin"))
        {
            if (request.CustomerId is null)
            {
                return BadRequest(new { message = "Müşteri ID belirtilmelidir." });
            }

            customerId = request.CustomerId.Value;
        }
        else
        {
            var userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var resolvedCustomerId = await db.Users
                .Where(user => user.Id == userId)
                .Select(user => user.CustomerId)
                .SingleOrDefaultAsync();

            if (resolvedCustomerId is null)
            {
                return BadRequest(new { message = "Hesabınıza bağlı bir müşteri kaydı bulunamadı." });
            }

            customerId = resolvedCustomerId.Value;
        }

        var customerExists = await db.Customers
            .AnyAsync(customer => customer.Id == customerId);

        if (!customerExists)
        {
            return NotFound(new { message = "Müşteri bulunamadı." });
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? (type == AccountType.CreditCard ? "Kredi Kartım" : "Banka Hesabım")
            : request.Name.Trim();

        var accountNumber = await GenerateAccountNumber();

        var account = new Account
        {
            CustomerId = customerId,
            AccountNumber = accountNumber,
            Name = name,
            Type = type
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAccountById),
            new { id = account.Id },
            account);
    }

    [HttpPatch("{id:int}/name")]
    public async Task<ActionResult<Account>> RenameAccount(int id, RenameAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Kart adı boş olamaz." });
        }

        var account = await db.Accounts.SingleOrDefaultAsync(account => account.Id == id);
        if (account is null)
        {
            return NotFound(new { message = "Hesap bulunamadı." });
        }

        if (!User.IsInRole("Admin"))
        {
            var userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var customerId = await db.Users
                .Where(user => user.Id == userId)
                .Select(user => user.CustomerId)
                .SingleOrDefaultAsync();

            if (customerId != account.CustomerId)
            {
                return NotFound(new { message = "Hesap bulunamadı." });
            }
        }

        account.Name = request.Name.Trim();
        await db.SaveChangesAsync();

        return Ok(account);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Account>> GetAccountById(int id)
    {
        var account = await db.Accounts
            .Include(account => account.Customer)
            .SingleOrDefaultAsync(account => account.Id == id);

        if (account is not null && !User.IsInRole("Admin"))
        {
            var userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var customerId = await db.Users
                .Where(user => user.Id == userId)
                .Select(user => user.CustomerId)
                .SingleOrDefaultAsync();

            if (customerId != account.CustomerId) account = null;
        }

        return account is null
            ? NotFound(new { message = "Hesap bulunamadı." })
            : Ok(account);
    }

    private async Task<string> GenerateAccountNumber()
    {
        string accountNumber;

        do
        {
            accountNumber = Random.Shared
                .NextInt64(1_000_000_000_000_000, 9_999_999_999_999_999)
                .ToString();
        }
        while (await db.Accounts.AnyAsync(
            account => account.AccountNumber == accountNumber));

        return accountNumber;
    }
}
