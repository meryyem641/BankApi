namespace BankApi.Models;

public class DepositRequest
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public decimal Amount { get; set; }

    public DepositStatus Status { get; set; } = DepositStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public Account? Account { get; set; }
}

public enum DepositStatus
{
    Pending,
    Approved,
    Rejected
}
