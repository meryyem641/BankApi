using BankApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Data;

public sealed class BankDbContext(DbContextOptions<BankDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DepositRequest> DepositRequests => Set<DepositRequest>();
    public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
    public DbSet<BudgetLimit> BudgetLimits => Set<BudgetLimit>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<MerchantCategoryRule> MerchantCategoryRules => Set<MerchantCategoryRule>();
    public DbSet<Receivable> Receivables => Set<Receivable>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>()
            .HasIndex(account => account.AccountNumber)
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(customer => customer.Email)
            .IsUnique()
            .HasFilter("Email <> ''");

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique()
            .HasFilter("Email <> ''");

        modelBuilder.Entity<Account>()
            .HasOne(account => account.Customer)
            .WithMany()
            .HasForeignKey(account => account.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasOne(user => user.Customer)
            .WithMany()
            .HasForeignKey(user => user.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Transfer>()
            .HasOne(transfer => transfer.SenderAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.SenderAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Transfer>()
            .HasOne(transfer => transfer.ReceiverAccount)
            .WithMany()
            .HasForeignKey(transfer => transfer.ReceiverAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DepositRequest>()
            .HasOne(request => request.Account)
            .WithMany()
            .HasForeignKey(request => request.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetEntry>()
            .HasOne(entry => entry.User)
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetEntry>()
            .Property(entry => entry.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Receivable>()
            .Property(item => item.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<BudgetLimit>()
            .HasOne(limit => limit.User)
            .WithMany()
            .HasForeignKey(limit => limit.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetLimit>()
            .HasIndex(limit => new { limit.UserId, limit.Month, limit.Category })
            .IsUnique();

        modelBuilder.Entity<BudgetLimit>()
            .Property(limit => limit.LimitAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<BudgetCategory>()
            .HasIndex(category => new { category.Name, category.Type })
            .IsUnique();

        modelBuilder.Entity<MerchantCategoryRule>()
            .HasIndex(rule => new { rule.UserId, rule.Keyword, rule.Type })
            .IsUnique();
    }
}
