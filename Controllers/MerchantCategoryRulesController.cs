using System.Security.Claims;
using System.Text.RegularExpressions;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/merchant-rules")]
[Authorize]
public class MerchantCategoryRulesController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRules()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var rules = await db.MerchantCategoryRules
            .Where(rule => rule.UserId == null || rule.UserId == userId)
            .OrderByDescending(rule => rule.UserId == userId)
            .ThenBy(rule => rule.Keyword)
            .Select(rule => new
            {
                rule.Id,
                rule.Keyword,
                rule.Category,
                type = rule.Type.ToString(),
                source = rule.UserId == null ? "Sistem" : "Kullanıcı"
            })
            .ToListAsync();

        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> SaveRule(SaveMerchantCategoryRuleRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Keyword) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "İşletme anahtarı ve kategori zorunludur." });
        }

        if (!Enum.TryParse<BudgetEntryType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = "İşlem türü geçersiz." });
        }

        var keyword = NormalizeKeyword(request.Keyword);
        var rule = await db.MerchantCategoryRules.FirstOrDefaultAsync(item =>
            item.UserId == userId && item.Keyword == keyword && item.Type == type);
        if (rule is null)
        {
            rule = new MerchantCategoryRule { UserId = userId, Keyword = keyword, Type = type };
            db.MerchantCategoryRules.Add(rule);
        }

        rule.Category = request.Category.Trim();
        await db.SaveChangesAsync();
        return Ok(new { message = "İşletme kategori kuralı kaydedildi.", keyword });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("global")]
    public async Task<IActionResult> SaveGlobalRule(SaveMerchantCategoryRuleRequest request)
    {
        if (!Enum.TryParse<BudgetEntryType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = "İşlem türü geçersiz." });
        }

        var keyword = NormalizeKeyword(request.Keyword);
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { message = "İşletme anahtarı ve kategori zorunludur." });
        }

        var rule = await db.MerchantCategoryRules.FirstOrDefaultAsync(item =>
            item.UserId == null && item.Keyword == keyword && item.Type == type);
        if (rule is null)
        {
            rule = new MerchantCategoryRule { Keyword = keyword, Type = type };
            db.MerchantCategoryRules.Add(rule);
        }

        rule.Category = request.Category.Trim();
        await db.SaveChangesAsync();
        return Ok(new { message = "Genel işletme kategori kuralı kaydedildi.", keyword });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var rule = await db.MerchantCategoryRules.FindAsync(id);
        if (rule is null || (rule.UserId != userId && !User.IsInRole("Admin")))
        {
            return NotFound(new { message = "Kural bulunamadı." });
        }

        db.MerchantCategoryRules.Remove(rule);
        await db.SaveChangesAsync();
        return Ok(new { message = "İşletme kategori kuralı kaldırıldı." });
    }

    public static string NormalizeKeyword(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g")
            .Replace("ü", "u").Replace("ö", "o").Replace("ç", "c");
        return Regex.Replace(normalized, @"\s+", " ");
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public record SaveMerchantCategoryRuleRequest(string Keyword, string Category, string Type);
