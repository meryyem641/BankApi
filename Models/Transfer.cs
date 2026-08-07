namespace BankApi.Models;

public class Transfer
{
    public int Id { get; set; }

    public int SenderAccountId { get; set; }

    public int ReceiverAccountId { get; set; }

    public decimal Amount { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public Account? SenderAccount { get; set; }

    public Account? ReceiverAccount { get; set; }
}
