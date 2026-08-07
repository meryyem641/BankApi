using System.Text;
using System.Security.Cryptography;
using BankApi.Data;
using BankApi.Models;
using BankApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("BankDatabase")));

builder.Services.AddScoped<TransferService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!)),

            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Finansal Bütçe ve Harcama Yönetim Sistemi API",
        Version = "v1",
        Description = "Kullanıcı hesapları, PDF/Excel ekstre aktarımı, gelir-gider yönetimi, bütçe limitleri ve raporlar için REST API.",
        Contact = new OpenApiContact
        {
            Name = "Finansal Bütçe ve Harcama Yönetim Sistemi"
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    var tagNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth"] = "Kimlik ve Oturum",
        ["Accounts"] = "Hesaplar",
        ["Transfers"] = "Para Transferleri",
        ["Customers"] = "Müşteriler",
        ["Users"] = "Kullanıcı Yönetimi",
        ["AuditLogs"] = "İşlem Geçmişi",
        ["BudgetEntries"] = "Gelir ve Giderler",
        ["BudgetLimits"] = "Bütçe Limitleri",
        ["BudgetCategories"] = "Bütçe Kategorileri",
        ["MerchantCategoryRules"] = "İşletme Kategori Kuralları",
        ["StatementImport"] = "Ekstre Aktarımı",
        ["DepositRequests"] = "Para Yatırma Talepleri"
    };
    options.TagActionsBy(api =>
    {
        var controller = api.ActionDescriptor.RouteValues["controller"] ?? "API";
        return new[] { tagNames.GetValueOrDefault(controller, controller) };
    });
    options.OrderActionsBy(api =>
        $"{api.ActionDescriptor.RouteValues["controller"]}:{api.HttpMethod}:{api.RelativePath}");
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Giriş endpointinden aldığınız JWT token'ı buraya yapıştırın. Bearer öneki Swagger tarafından eklenir."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Finansal Bütçe ve Harcama Yönetim Sistemi API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Finansal Bütçe API | Swagger UI";
    options.DisplayRequestDuration();
    options.EnableTryItOutByDefault();
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    options.DefaultModelsExpandDepth(-1);
    options.EnableFilter();
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache";
    }
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<BankDbContext>();

    db.Database.EnsureCreated();

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS AuditLogs (
            Id INTEGER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY AUTOINCREMENT,
            ActorUserName TEXT NOT NULL,
            Action TEXT NOT NULL,
            Description TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS DepositRequests (
            Id INTEGER NOT NULL CONSTRAINT PK_DepositRequests PRIMARY KEY AUTOINCREMENT,
            AccountId INTEGER NOT NULL,
            Amount TEXT NOT NULL,
            Status INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            ReviewedAt TEXT NULL,
            CONSTRAINT FK_DepositRequests_Accounts_AccountId FOREIGN KEY (AccountId) REFERENCES Accounts (Id) ON DELETE CASCADE
        );");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS BudgetEntries (
            Id INTEGER NOT NULL CONSTRAINT PK_BudgetEntries PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Type INTEGER NOT NULL,
            Category TEXT NOT NULL,
            Description TEXT NOT NULL,
            Amount DECIMAL NOT NULL,
            EntryDate TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_BudgetEntries_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_BudgetEntries_UserId
            ON BudgetEntries (UserId);");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS BudgetLimits (
            Id INTEGER NOT NULL CONSTRAINT PK_BudgetLimits PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Month TEXT NOT NULL,
            Category TEXT NOT NULL,
            LimitAmount DECIMAL NOT NULL,
            CONSTRAINT FK_BudgetLimits_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_BudgetLimits_UserId_Month_Category
            ON BudgetLimits (UserId, Month, Category);");

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE BudgetEntries ADD COLUMN Source TEXT NOT NULL DEFAULT 'Manuel kayıt';");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    // Temiz bir kurulumda yöneticinin ve kullanıcı ekranının hemen
    // denenebilmesi için örnek hesapları yalnızca yoksa oluştur.
    static string CreateSeedPasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    static string CreateSeedPassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        var value = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        return $"Demo-{value}!";
    }

    var seedUsers = new[]
    {
        new
        {
            UserName = "meryem",
            Email = "meryem@demo.local",
            Phone = "5550000001",
            Role = "Admin"
        },
        new
        {
            UserName = "demo.kullanici",
            Email = "demo@demo.local",
            Phone = "5550000002",
            Role = "User"
        }
    };

    var createdSeedCredentials = new List<(string UserName, string Password)>();
    foreach (var seed in seedUsers)
    {
        if (!db.Users.Any(user => user.UserName == seed.UserName))
        {
            var passwordSetting = seed.Role == "Admin"
                ? builder.Configuration["Seed:AdminPassword"]
                : builder.Configuration["Seed:DemoPassword"];
            var password = string.IsNullOrWhiteSpace(passwordSetting)
                ? CreateSeedPassword()
                : passwordSetting;

            db.Users.Add(new User
            {
                UserName = seed.UserName,
                Email = seed.Email,
                Phone = seed.Phone,
                PasswordHash = CreateSeedPasswordHash(password),
                Role = seed.Role,
                IsApproved = true,
                IsActive = true
            });

            createdSeedCredentials.Add((seed.UserName, password));
        }
    }

    db.SaveChanges();
    foreach (var (userName, password) in createdSeedCredentials)
    {
        Console.WriteLine($"Seed kullanıcı oluşturuldu - Kullanıcı: {userName}, Şifre: {password}");
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE BudgetEntries ADD COLUMN Treatment TEXT NOT NULL DEFAULT 'Budget';");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    // Daha önce içe aktarılmış transfer ve nakit hareketlerini de yeni
    // inceleme akışına alırız; manuel girilen kayıtlar etkilenmez.
    db.Database.ExecuteSqlRaw(@"
        UPDATE BudgetEntries
        SET Treatment = 'NeedsReview'
        WHERE Source <> 'Manuel kayıt'
          AND Category IN ('Para Transferi', 'Para Yatırma', 'Para Çekme')
          AND Treatment = 'Budget';");

    // Altın, döviz ve kuyumcu işlemleri yatırım varlığı olarak tutulur;
    // toplam gelir/gider listesine dahil edilmez, ancak banka bakiyesini
    // etkilemeye devam eder.
    db.Database.ExecuteSqlRaw(@"
        UPDATE BudgetEntries
        SET Category = 'Yatırım / Varlıklarım', Treatment = 'Investment'
        WHERE Source <> 'Manuel kayıt'
          AND (
              Description LIKE '%altın%' OR Description LIKE '%altin%'
              OR Description LIKE '%döviz%' OR Description LIKE '%doviz%'
              OR Description LIKE '%kuyumcu%'
          );");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Receivables (
            Id INTEGER NOT NULL CONSTRAINT PK_Receivables PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            SourceEntryId INTEGER NULL,
            PersonName TEXT NOT NULL,
            Amount DECIMAL NOT NULL,
            EntryDate TEXT NOT NULL,
            DueDate TEXT NULL,
            Description TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_Receivables_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_Receivables_UserId ON Receivables (UserId);");

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Receivables ADD COLUMN Direction TEXT NOT NULL DEFAULT 'Receivable';");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS BudgetCategories (
            Id INTEGER NOT NULL CONSTRAINT PK_BudgetCategories PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Type INTEGER NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_BudgetCategories_Name_Type
            ON BudgetCategories (Name, Type);");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS MerchantCategoryRules (
            Id INTEGER NOT NULL CONSTRAINT PK_MerchantCategoryRules PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NULL,
            Keyword TEXT NOT NULL,
            Category TEXT NOT NULL,
            Type INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_MerchantCategoryRules_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_MerchantCategoryRules_UserId_Keyword_Type
            ON MerchantCategoryRules (UserId, Keyword, Type);");

    var defaultCategories = new[]
    {
        ("Maaş", BudgetEntryType.Income), ("Burs", BudgetEntryType.Income), ("Ek gelir", BudgetEntryType.Income),
        ("Para Yatırma", BudgetEntryType.Income), ("Para Transferi", BudgetEntryType.Income),
        ("Kira", BudgetEntryType.Expense), ("Fatura", BudgetEntryType.Expense), ("Market", BudgetEntryType.Expense),
        ("Alışveriş", BudgetEntryType.Expense), ("Yemek / Kafe", BudgetEntryType.Expense),
        ("Ulaşım", BudgetEntryType.Expense), ("Giyim", BudgetEntryType.Expense), ("Abonelik", BudgetEntryType.Expense),
        ("Para Çekme", BudgetEntryType.Expense), ("Para Transferi", BudgetEntryType.Expense),
        ("Sağlık", BudgetEntryType.Expense), ("Eğitim", BudgetEntryType.Expense), ("Teknoloji", BudgetEntryType.Expense), ("Spor", BudgetEntryType.Expense),
        ("Ev", BudgetEntryType.Expense), ("Vergi / Resmi", BudgetEntryType.Expense), ("Komisyon / Ücret", BudgetEntryType.Expense),
        ("Diğer", BudgetEntryType.Expense)
    };
    foreach (var (name, type) in defaultCategories)
    {
        if (!db.BudgetCategories.Any(category => category.Name == name && category.Type == type))
        {
            db.BudgetCategories.Add(new BudgetCategory { Name = name, Type = type });
        }
    }
    db.SaveChanges();

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN CustomerId INTEGER NULL;");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN PasswordResetToken TEXT NULL;");
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN PasswordResetTokenExpiresAt TEXT NULL;");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütunlar mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN Email TEXT NOT NULL DEFAULT '';" );
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN Phone TEXT NOT NULL DEFAULT '';" );
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütunlar mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN IsApproved INTEGER NOT NULL DEFAULT 1;");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütun mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN LastLoginAt TEXT NULL;");
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE Users ADD COLUMN FailedLoginCount INTEGER NOT NULL DEFAULT 0;");
    }
    catch (Microsoft.Data.Sqlite.SqliteException exception)
        when (exception.SqliteErrorCode == 1)
    {
        // Sütunlar mevcutsa başlangıç işlemi tekrar çalıştırılabilir.
    }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Accounts (
            Id INTEGER NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY AUTOINCREMENT,
            CustomerId INTEGER NOT NULL,
            AccountNumber TEXT NOT NULL,
            Balance DECIMAL NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            CONSTRAINT FK_Accounts_Customers_CustomerId
                FOREIGN KEY (CustomerId) REFERENCES Customers (Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Accounts_AccountNumber
            ON Accounts (AccountNumber);
    """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Transfers (
            Id INTEGER NOT NULL CONSTRAINT PK_Transfers PRIMARY KEY AUTOINCREMENT,
            SenderAccountId INTEGER NOT NULL,
            ReceiverAccountId INTEGER NOT NULL,
            Amount DECIMAL NOT NULL,
            Status INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            CompletedAt TEXT NULL,
            CONSTRAINT FK_Transfers_Accounts_SenderAccountId
                FOREIGN KEY (SenderAccountId) REFERENCES Accounts (Id) ON DELETE RESTRICT,
            CONSTRAINT FK_Transfers_Accounts_ReceiverAccountId
                FOREIGN KEY (ReceiverAccountId) REFERENCES Accounts (Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_Transfers_SenderAccountId
            ON Transfers (SenderAccountId);
        CREATE INDEX IF NOT EXISTS IX_Transfers_ReceiverAccountId
            ON Transfers (ReceiverAccountId);
    """);

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

    // Mevcut kayıtları standartlaştırıp benzersiz iletişim indexlerini oluştur.
    foreach (var customer in db.Customers.ToList())
    {
        customer.Email = customer.Email.Trim().ToLowerInvariant();
        customer.Phone = new string(customer.Phone.Where(char.IsDigit).ToArray());
    }

    foreach (var user in db.Users.ToList())
    {
        user.Email = user.Email.Trim().ToLowerInvariant();
        user.Phone = new string(user.Phone.Where(char.IsDigit).ToArray());
    }

    db.SaveChanges();

    db.Database.ExecuteSqlRaw("""
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Customers_Email
            ON Customers (Email) WHERE Email <> '';
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Email
            ON Users (Email) WHERE Email <> '';
        DROP INDEX IF EXISTS IX_Customers_Phone;
        DROP INDEX IF EXISTS IX_Users_Phone;
    """);

    // Admin hesapları müşteri değildir. Önce yanlışlıkla oluşturulmuş bağlantıları kaldır.
    var adminUsersWithCustomer = db.Users
        .Where(user => user.Role == "Admin" && user.CustomerId != null)
        .ToList();

    if (adminUsersWithCustomer.Count > 0)
    {
        var adminCustomerIds = adminUsersWithCustomer
            .Where(user => user.CustomerId.HasValue)
            .Select(user => user.CustomerId!.Value)
            .ToList();

        foreach (var user in adminUsersWithCustomer)
        {
            user.CustomerId = null;
        }

        var unusedAdminCustomers = db.Customers
            .Where(customer => adminCustomerIds.Contains(customer.Id))
            .Where(customer => !db.Accounts.Any(account => account.CustomerId == customer.Id))
            .Where(customer => !db.Users.Any(user => user.CustomerId == customer.Id))
            .ToList();

        db.Customers.RemoveRange(unusedAdminCustomers);
        db.SaveChanges();
    }

    // Önceki çalıştırmalarda bağlantısı kaldırılmış ancak geride kalmış
    // admin adına ait boş müşteri kayıtlarını da temizle.
    var orphanedAdminCustomers = db.Customers
        .Where(customer => db.Users.Any(user =>
            user.Role == "Admin" && user.UserName == customer.FullName))
        .Where(customer => !db.Users.Any(user => user.CustomerId == customer.Id))
        .Where(customer => !db.Accounts.Any(account => account.CustomerId == customer.Id))
        .ToList();

    if (orphanedAdminCustomers.Count > 0)
    {
        db.Customers.RemoveRange(orphanedAdminCustomers);
        db.SaveChanges();
    }

    // Daha önce oluşturulmuş normal kullanıcılar için eksik müşteri bağlantısını tamamla.
    var usersWithoutCustomer = db.Users
        .Where(user => user.CustomerId == null &&
                       user.Role != "Admin" &&
                       user.IsApproved)
        .ToList();

    if (usersWithoutCustomer.Count > 0)
    {
        foreach (var user in usersWithoutCustomer)
        {
            var customer = new Customer
            {
                FullName = user.UserName,
                Email = user.Email,
                Phone = user.Phone
            };

            db.Customers.Add(customer);
            user.Customer = customer;
        }

        db.SaveChanges();
    }

    // Onaylı normal kullanıcıların hesabı yoksa otomatik olarak aktif hesap aç.
    var approvedCustomerIds = db.Users
        .Where(user => user.Role != "Admin" && user.IsApproved && user.CustomerId != null)
        .Select(user => user.CustomerId!.Value)
        .Distinct()
        .ToList();

    var customersWithoutAccount = db.Customers
        .Where(customer => approvedCustomerIds.Contains(customer.Id))
        .Where(customer => !db.Accounts.Any(account => account.CustomerId == customer.Id))
        .ToList();

    foreach (var customer in customersWithoutAccount)
    {
        string accountNumber;
        do
        {
            accountNumber = Random.Shared
                .NextInt64(1_000_000_000_000_000, 9_999_999_999_999_999)
                .ToString();
        }
        while (db.Accounts.Any(account => account.AccountNumber == accountNumber));

        db.Accounts.Add(new Account
        {
            CustomerId = customer.Id,
            AccountNumber = accountNumber,
            Balance = 0,
            IsActive = true
        });
    }

    if (customersWithoutAccount.Count > 0)
    {
        db.SaveChanges();
    }
}

app.Run();
