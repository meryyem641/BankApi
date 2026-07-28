using BankApi.Data;
using BankApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BankDatabase")));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    db.Database.EnsureCreated();

    if (!db.Customers.Any())
    {
        db.Customers.AddRange(
            new Customer
            {
                FullName = "Ali Yılmaz",
                Email = "ali@example.com",
                Phone = "5551112233"
            },
            new Customer
            {
                FullName = "Ayşe Demir",
                Email = "ayse@example.com",
                Phone = "5554445566"
            });

        db.SaveChanges();
    }
}

app.Run();
