namespace BankApi.Models;

public record ForgotPasswordRequest(string UserName);

public record ResetPasswordRequest(string UserName, string Token, string NewPassword);
