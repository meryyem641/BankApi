namespace BankApi.Models;

public class PatchCustomerRequest
{
    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}
