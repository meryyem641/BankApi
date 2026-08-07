namespace BankApi.Models;

public class Receivable
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SourceEntryId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string Direction { get; set; } = "Receivable";
    public decimal Amount { get; set; }
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Açık";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}
