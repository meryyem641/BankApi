using System.Security.Claims;
using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/savings-goals")]
[Authorize]
public class SavingsGoalsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetGoals()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var goals = await db.SavingsGoals
            .Where(goal => goal.UserId == userId)
            .OrderBy(goal => goal.CreatedAt)
            .ToListAsync();

        return Ok(goals);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal(CreateSavingsGoalRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Hedef adı zorunludur." });
        }

        if (request.TargetAmount <= 0)
        {
            return BadRequest(new { message = "Hedef tutar sıfırdan büyük olmalıdır." });
        }

        var goal = new SavingsGoal
        {
            UserId = userId.Value,
            Name = request.Name.Trim(),
            TargetAmount = decimal.Round(request.TargetAmount, 2),
            TargetDate = request.TargetDate
        };

        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();

        return Ok(goal);
    }

    [HttpPost("{id:int}/contribute")]
    public async Task<IActionResult> Contribute(int id, ContributeSavingsGoalRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Eklenecek tutar sıfırdan büyük olmalıdır." });
        }

        var goal = await db.SavingsGoals
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (goal is null)
        {
            return NotFound(new { message = "Hedef bulunamadı." });
        }

        goal.SavedAmount = decimal.Round(goal.SavedAmount + request.Amount, 2);
        await db.SaveChangesAsync();

        return Ok(goal);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteGoal(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var goal = await db.SavingsGoals
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (goal is null)
        {
            return NotFound(new { message = "Hedef bulunamadı." });
        }

        db.SavingsGoals.Remove(goal);
        await db.SaveChangesAsync();

        return Ok(new { message = "Tasarruf hedefi silindi." });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }
}

public record CreateSavingsGoalRequest(string Name, decimal TargetAmount, DateTime? TargetDate = null);
public record ContributeSavingsGoalRequest(decimal Amount);
