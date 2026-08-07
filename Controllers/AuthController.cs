using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net.Mail;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    BankDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var user = await db.Users
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == userId);

        return user is null
            ? NotFound(new { message = "Kullanıcı bulunamadı." })
            : Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.Phone,
                user.Role
            });
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new
            {
                message = "E-posta ve cep telefonu zorunludur."
            });
        }

        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Id == userId);

        if (user is null)
        {
            return NotFound(new { message = "Kullanıcı bulunamadı." });
        }

        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);

        if (!IsValidEmail(email) || phone.Length < 10)
        {
            return BadRequest(new
            {
                message = "Geçerli bir e-posta ve en az 10 haneli telefon girilmelidir."
            });
        }

        var contactExists = await db.Users.AnyAsync(item =>
            item.Id != userId && item.Email == email);

        if (contactExists)
        {
            return Conflict(new
            {
                message = "Bu e-posta adresi başka bir kullanıcıda kayıtlı."
            });
        }

        user.Email = email;
        user.Phone = phone;

        if (user.Customer is not null)
        {
            user.Customer.Email = user.Email;
            user.Customer.Phone = user.Phone;
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "Profil bilgileri güncellendi." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 6)
        {
            return BadRequest(new
            {
                message = "Mevcut şifre zorunludur; yeni şifre en az 6 karakter olmalıdır."
            });
        }

        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(userId);

        if (user is null || !VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new { message = "Mevcut şifre hatalı." });
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            request.NewPassword,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        user.PasswordHash =
            $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        await db.SaveChangesAsync();

        return Ok(new { message = "Şifre başarıyla değiştirildi." });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new
            {
                message = "Kullanıcı adı, şifre, e-posta ve cep telefonu zorunludur."
            });
        }

        var userExists = await db.Users
            .AnyAsync(user => user.UserName == request.UserName);

        if (userExists)
        {
            return Conflict(new
            {
                message = "Bu kullanıcı zaten kayıtlı."
            });
        }

        var email = NormalizeEmail(request.Email);
        var phone = NormalizePhone(request.Phone);

        if (!IsValidEmail(email) || phone.Length < 10)
        {
            return BadRequest(new
            {
                message = "Geçerli bir e-posta ve en az 10 haneli telefon girilmelidir."
            });
        }

        var contactExists = await db.Users.AnyAsync(user => user.Email == email);

        if (contactExists)
        {
            return Conflict(new { message = "Bu e-posta adresi zaten kayıtlı." });
        }

        var salt = RandomNumberGenerator.GetBytes(16);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            request.Password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        var passwordHash =
            $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        var user = new User
        {
            UserName = request.UserName,
            Email = email,
            Phone = phone,
            PasswordHash = passwordHash,
            IsApproved = false
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created("", new
        {
            message = "Kaydınız alındı. Bilgileriniz admin tarafından kontrol ediliyor. Onaylandıktan sonra hesabınıza erişebilirsiniz.",
            user.UserName
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(user =>
                user.UserName == request.UserName);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
            });
        }

        if (!user.IsApproved)
        {
            return Unauthorized(new
            {
                message = "Bilgileriniz kontrol ediliyor. Hesabınız admin onayı bekliyor; onaylandıktan sonra giriş yapabilirsiniz."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                message = "Bu kullanıcı hesabı yönetici tarafından pasif hale getirildi."
            });
        }

        var passwordParts = user.PasswordHash.Split('.');

        if (passwordParts.Length != 2)
        {
            return Unauthorized(new
            {
                message = "Kullanıcı şifre kaydı geçersiz."
            });
        }

        var salt = Convert.FromBase64String(passwordParts[0]);
        var savedHash = Convert.FromBase64String(passwordParts[1]);

        var enteredHash = Rfc2898DeriveBytes.Pbkdf2(
            request.Password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        var isPasswordCorrect =
            CryptographicOperations.FixedTimeEquals(
                savedHash,
                enteredHash);

        if (!isPasswordCorrect)
        {
            user.FailedLoginCount++;
            await db.SaveChangesAsync();
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
            });
        }

        if (!string.IsNullOrWhiteSpace(request.LoginType) &&
            !string.Equals(request.LoginType, user.Role, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new
            {
                message = request.LoginType == "Admin"
                    ? "Bu hesap yönetici hesabı değil. Kullanıcı Girişi seçeneğini kullanın."
                    : "Bu hesap yönetici hesabı. Yönetici Girişi seçeneğini kullanın."
            });
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        await db.SaveChangesAsync();

        var claims = new[]
{
    new Claim(
        ClaimTypes.NameIdentifier,
        user.Id.ToString()),

    new Claim(
        ClaimTypes.Name,
        user.UserName),

    new Claim(
        ClaimTypes.Role,
        user.Role)
};

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var expiresMinutes = int.Parse(
            configuration["Jwt:ExpiresMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        return Ok(new
        {
            message = "Giriş başarılı.",
            userName = user.UserName,
            token = tokenString
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(item => item.UserName == request.UserName);

        // Kullanıcının kayıtlı olup olmadığını dışarıya açıklama.
        var response = new
        {
            message = "Kullanıcı mevcutsa şifre sıfırlama bilgisi hazırlanmıştır."
        };

        if (user is null)
        {
            return Ok(response);
        }

        user.PasswordResetToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);

        await db.SaveChangesAsync();

        if (!environment.IsDevelopment())
        {
            return Ok(response);
        }

        return Ok(new
        {
            response.message,
            resetToken = user.PasswordResetToken,
            expiresAt = user.PasswordResetTokenExpiresAt
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 6)
        {
            return BadRequest(new
            {
                message = "Yeni şifre en az 6 karakter olmalıdır."
            });
        }

        var user = await db.Users
            .SingleOrDefaultAsync(item => item.UserName == request.UserName);

        if (user is null ||
            user.PasswordResetToken != request.Token ||
            user.PasswordResetTokenExpiresAt is null ||
            user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "Şifre sıfırlama tokenı geçersiz veya süresi dolmuş."
            });
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            request.NewPassword,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        user.PasswordHash =
            $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        await db.SaveChangesAsync();

        return Ok(new { message = "Şifre başarıyla yenilendi." });
    }

    private static bool VerifyPassword(string passwordHash, string password)
    {
        var passwordParts = passwordHash.Split('.');

        if (passwordParts.Length != 2)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(passwordParts[0]);
            var savedHash = Convert.FromBase64String(passwordParts[1]);
            var enteredHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32);

            return CryptographicOperations.FixedTimeEquals(
                savedHash,
                enteredHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static string NormalizePhone(string value) =>
        new string(value.Where(char.IsDigit).ToArray());

    private static bool IsValidEmail(string value)
    {
        try
        {
            return new MailAddress(value).Address == value;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public record RegisterRequest(
    string UserName,
    string Password,
    string Email,
    string Phone);

public record LoginRequest(string UserName, string Password, string? LoginType = null);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UpdateProfileRequest(string Email, string Phone);
