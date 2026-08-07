using BankApi.Data;
using BankApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Services;

public sealed class TransferService(BankDbContext db)
{
    public async Task<Transfer?> CreateAsync(
        CreateTransferRequest request,
        int userId,
        bool isAdmin)
    {
        if (request.SenderAccountId == request.ReceiverAccountId ||
            request.Amount <= 0)
        {
            return null;
        }

        var accounts = await db.Accounts
            .Where(account => account.Id == request.SenderAccountId ||
                              account.Id == request.ReceiverAccountId)
            .ToListAsync();

        if (accounts.Count != 2 || accounts.Any(account => !account.IsActive))
        {
            return null;
        }

        var senderAccount = accounts.Single(account =>
            account.Id == request.SenderAccountId);
        var receiverAccount = accounts.Single(account =>
            account.Id == request.ReceiverAccountId);

        if (senderAccount.Balance < request.Amount)
        {
            return null;
        }

        if (!isAdmin)
        {
            var user = await db.Users
                .SingleOrDefaultAsync(item => item.Id == userId);

            if (user?.CustomerId != senderAccount.CustomerId)
            {
                return null;
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        var transfer = new Transfer
        {
            SenderAccountId = request.SenderAccountId,
            ReceiverAccountId = request.ReceiverAccountId,
            Amount = request.Amount,
            Status = TransferStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };

        senderAccount.Balance -= request.Amount;
        receiverAccount.Balance += request.Amount;
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return transfer;
    }
}
