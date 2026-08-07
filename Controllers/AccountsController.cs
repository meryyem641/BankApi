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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Account>> CreateAccount(
        CreateAccountRequest request)
    {
        var customerExists = await db.Customers
            .AnyAsync(customer => customer.Id == request.CustomerId);

        if (!customerExists)
        {
            return NotFound(new { message = "Müşteri bulunamadı." });
        }

        var accountNumber = await GenerateAccountNumber();

        var account = new Account
        {
            CustomerId = request.CustomerId,
            AccountNumber = accountNumber
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAccountById),
            new { id = account.Id },
            account);
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
