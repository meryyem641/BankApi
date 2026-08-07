using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/budget-categories")]
[Authorize]
public class BudgetCategoriesController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false)
    {
        var query = db.BudgetCategories.AsQueryable();
        if (!User.IsInRole("Admin") || !includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return Ok(await query
            .OrderBy(category => category.Type)
            .ThenBy(category => category.Name)
            .Select(category => new
            {
                category.Id,
                category.Name,
                type = category.Type.ToString(),
                category.IsActive
            })
            .ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateBudgetCategoryRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Kategori adı zorunludur." });
        }

        if (!Enum.TryParse<BudgetEntryType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = "Kategori türü gelir veya gider olmalıdır." });
        }

        if (await db.BudgetCategories.AnyAsync(category =>
                category.Name == name && category.Type == type))
        {
            return Conflict(new { message = "Bu kategori zaten mevcut." });
        }

        db.BudgetCategories.Add(new BudgetCategory { Name = name, Type = type });
        await db.SaveChangesAsync();
        return Ok(new { message = "Kategori eklendi." });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateBudgetCategoryRequest request)
    {
        var category = await db.BudgetCategories.FindAsync(id);
        if (category is null) return NotFound(new { message = "Kategori bulunamadı." });

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Kategori adı zorunludur." });
        }

        if (await db.BudgetCategories.AnyAsync(item =>
                item.Id != id && item.Name == name && item.Type == category.Type))
        {
            return Conflict(new { message = "Bu kategori zaten mevcut." });
        }

        category.Name = name;
        await db.SaveChangesAsync();
        return Ok(new { message = "Kategori güncellendi." });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var category = await db.BudgetCategories.FindAsync(id);
        if (category is null) return NotFound(new { message = "Kategori bulunamadı." });

        category.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "Kategori pasifleştirildi." });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var category = await db.BudgetCategories.FindAsync(id);
        if (category is null) return NotFound(new { message = "Kategori bulunamadı." });

        category.IsActive = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "Kategori aktifleştirildi." });
    }
}

public record CreateBudgetCategoryRequest(string? Name, string Type);
public record UpdateBudgetCategoryRequest(string? Name);
