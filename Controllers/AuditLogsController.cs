using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuditLog>>> GetAuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(log =>
                log.ActorUserName.Contains(value) ||
                log.Action.Contains(value) ||
                log.Description.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action);
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= from.Value.Date.ToUniversalTime());
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.CreatedAt < to.Value.Date.AddDays(1).ToUniversalTime());
        }

        return Ok(await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(200)
            .ToListAsync());
    }
}
