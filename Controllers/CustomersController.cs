using BankApi.Data;
using BankApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Controllers;

[ApiController]
[Route("api/v1/customers")]
public class CustomersController(BankDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetCustomers()
    {
        return Ok(await db.Customers.OrderBy(customer => customer.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Customer>> GetCustomerById(int id)
    {
        var customer = await db.Customers.FindAsync(id);

        return customer is null
            ? NotFound(new { message = "Müşteri bulunamadı." })
            : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> CreateCustomer(CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "FullName, Email ve Phone zorunludur." });
        }

        var customer = new Customer
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim()
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCustomerById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Customer>> UpdateCustomer(
        int id,
        UpdateCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new { message = "Müşteri bulunamadı." });
        }

        customer.FullName = request.FullName.Trim();
        customer.Email = request.Email.Trim();
        customer.Phone = request.Phone.Trim();

        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<Customer>> PatchCustomer(
        int id,
        PatchCustomerRequest request)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new { message = "Müşteri bulunamadı." });
        }

        if (request.FullName is not null)
        {
            customer.FullName = request.FullName.Trim();
        }

        if (request.Email is not null)
        {
            customer.Email = request.Email.Trim();
        }

        if (request.Phone is not null)
        {
            customer.Phone = request.Phone.Trim();
        }

        await db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await db.Customers.FindAsync(id);

        if (customer is null)
        {
            return NotFound(new { message = "Müşteri bulunamadı." });
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
