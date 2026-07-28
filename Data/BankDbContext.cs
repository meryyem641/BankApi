using BankApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Data;

public sealed class BankDbContext(DbContextOptions<BankDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
