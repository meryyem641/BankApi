namespace BankApi.Models;

public class BudgetEntry
{
    public int Id { get; set; }

    public int UserId { get; set; }

    // Ekstre içe aktarımında hangi banka hesabından/kredi kartından geldiğini
    // takip etmek için kullanılır; manuel kayıtlarda boş kalabilir.
    public int? AccountId { get; set; }

    public BudgetEntryType Type { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime EntryDate { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Source { get; set; } = "Manuel kayıt";

    // Banka ekstresinden gelen ve amacı kullanıcı tarafından henüz
    // belirlenmemiş transfer/nakit hareketleri için kullanılır.
    public string Treatment { get; set; } = "Budget";

    public User? User { get; set; }

    public Account? Account { get; set; }
}

public enum BudgetEntryType
{
    Income,
    Expense
}
