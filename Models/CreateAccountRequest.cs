namespace BankApi.Models;

public record CreateAccountRequest(string Name, string Type, int? CustomerId = null);

public record RenameAccountRequest(string Name);
