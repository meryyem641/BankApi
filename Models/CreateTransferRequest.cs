namespace BankApi.Models;

public record CreateTransferRequest(
    int SenderAccountId,
    int ReceiverAccountId,
    decimal Amount);
