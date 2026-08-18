using System.Text.Json.Serialization;

namespace BankApi.Models;

public class Account
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.Bank;

    public decimal Balance { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountType
{
    Bank,
    CreditCard
}
