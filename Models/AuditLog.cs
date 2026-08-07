namespace BankApi.Models;

public class AuditLog
{
    public int Id { get; set; }

    public string ActorUserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
