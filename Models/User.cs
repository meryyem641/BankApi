namespace BankApi.Models;

public class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "User";

    public bool IsApproved { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginCount { get; set; }

    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiresAt { get; set; }
}
