using BankApi.Models;
using BankApi.Services;
using BankApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/transfers")]
[Authorize]
public class TransfersController(
    TransferService transfers,
    BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTransfers()
    {
        var query = db.Transfers
            .Include(transfer => transfer.SenderAccount)
            .Include(transfer => transfer.ReceiverAccount)
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
                return Ok(Array.Empty<object>());
            }

            query = query.Where(transfer =>
                transfer.SenderAccount!.CustomerId == customerId ||
                transfer.ReceiverAccount!.CustomerId == customerId);
        }

        var transfersResult = await query
            .OrderByDescending(transfer => transfer.CreatedAt)
            .Take(100)
            .Select(transfer => new
            {
                transfer.Id,
                transfer.Amount,
                status = transfer.Status.ToString(),
                transfer.CreatedAt,
                transfer.CompletedAt,
                senderAccountNumber = transfer.SenderAccount!.AccountNumber,
                receiverAccountNumber = transfer.ReceiverAccount!.AccountNumber
            })
            .ToListAsync();

        return Ok(transfersResult);
    }

    [HttpPost]
    public async Task<ActionResult<Transfer>> Create(
        CreateTransferRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = int.Parse(userIdClaim!);
        var isAdmin = User.IsInRole("Admin");

        var transfer = await transfers.CreateAsync(request, userId, isAdmin);

        return transfer is null
            ? BadRequest(new
            {
                message = "Hesaplar aktif olmalı, farklı olmalı, tutar pozitif olmalı ve gönderen hesapta yeterli bakiye bulunmalıdır."
            })
            : Ok(transfer);
    }

}
