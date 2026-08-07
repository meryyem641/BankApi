namespace BankApi.Models;

public class BudgetLimit
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Month { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal LimitAmount { get; set; }

    public User? User { get; set; }
}
