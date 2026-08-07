namespace BankApi.Models;

public class BudgetCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public BudgetEntryType Type { get; set; }

    public bool IsActive { get; set; } = true;
}
