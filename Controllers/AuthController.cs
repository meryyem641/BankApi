using System.Security.Cryptography;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(BankDbContext db) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Kullanıcı adı ve şifre zorunludur."
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
            PasswordHash = passwordHash
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created("", new
        {
            message = "Kullanıcı başarıyla oluşturuldu.",
            user.UserName
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await db.Users
            .SingleOrDefaultAsync(user => user.UserName == request.UserName);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
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
            return Unauthorized(new
            {
                message = "Kullanıcı adı veya şifre hatalı."
            });
        }

        return Ok(new
        {
            message = "Giriş başarılı.",
            user.UserName
        });
    }
}

public record RegisterRequest(string UserName, string Password);

public record LoginRequest(string UserName, string Password);