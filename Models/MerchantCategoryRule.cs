namespace BankApi.Models;

public class MerchantCategoryRule
{
    public int Id { get; set; }

    // Null means a global rule created by an administrator.
    public int? UserId { get; set; }

    public string Keyword { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public BudgetEntryType Type { get; set; } = BudgetEntryType.Expense;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
