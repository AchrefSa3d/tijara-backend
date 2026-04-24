using Microsoft.Data.SqlClient;
using Dapper;

namespace TijaraApi.Services;

public class DbService
{
    private readonly string _connectionString;

    public DbService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    // Shorthand : execute a query and return results
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<T>(sql, param);
    }

    // Return first or default
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    // Execute (INSERT, UPDATE, DELETE) — returns rows affected
    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(sql, param);
    }

    // Execute scalar (COUNT, SUM…)
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task InitializeTablesAsync()
    {
        using var conn = CreateConnection();

        // ── Nouvelles tables ───────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserFollows')
            CREATE TABLE UserFollows (
                IdFollow  BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT NOT NULL,
                IdVendor  BIGINT NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_UserFollows UNIQUE (IdUser, IdVendor)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Notifications')
            CREATE TABLE Notifications (
                IdNotification BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser         BIGINT NOT NULL,
                Type           NVARCHAR(50) NOT NULL DEFAULT 'info',
                Title          NVARCHAR(255),
                Message        NVARCHAR(1000),
                Link           NVARCHAR(500),
                IsRead         BIT NOT NULL DEFAULT 0,
                CreatedAt      DATETIME DEFAULT GETDATE(),
                IdReference    BIGINT NULL
            );
        ");

        // ── Colonnes optionnelles sur Users (migration additive) ───────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'BirthDate')
            ALTER TABLE Users ADD BirthDate NVARCHAR(20) NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'Gender')
            ALTER TABLE Users ADD Gender NVARCHAR(20) NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'City')
            ALTER TABLE Users ADD City NVARCHAR(100) NULL;
        ");

        // ── Colonnes optionnelles sur Ads (migration additive) ─────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Ads') AND name = N'Type')
            ALTER TABLE Ads ADD Type NVARCHAR(20) NULL DEFAULT 'annonce';
        ");
        // Set default type for existing rows that have NULL
        await conn.ExecuteAsync(
            "UPDATE Ads SET Type = 'annonce' WHERE Type IS NULL"
        );

        // ── Tables pour likes et commentaires des annonces ─────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdLikes')
            CREATE TABLE AdLikes (
                IdLike    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdAd      BIGINT NOT NULL,
                IdUser    BIGINT NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_AdLikes UNIQUE (IdAd, IdUser)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdComments')
            CREATE TABLE AdComments (
                IdComment BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdAd      BIGINT NOT NULL,
                IdUser    BIGINT NOT NULL,
                Content   NVARCHAR(1000) NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                Active    INT DEFAULT 1
            );
        ");

        // ── Table pour tokens email (confirmation, reset mot de passe) ─────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmailTokens')
            CREATE TABLE EmailTokens (
                IdToken   BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT NOT NULL,
                Token     NVARCHAR(200) NOT NULL UNIQUE,
                Type      NVARCHAR(30)  NOT NULL DEFAULT 'email_confirm',
                ExpiresAt DATETIME      NOT NULL,
                Used      BIT           DEFAULT 0,
                CreatedAt DATETIME      DEFAULT GETDATE()
            );
        ");

        // ── Colonne EmailConfirmed sur Users ───────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'EmailConfirmed')
            ALTER TABLE Users ADD EmailConfirmed BIT NULL DEFAULT 1;
        ");
        // Marquer les utilisateurs existants comme confirmés
        await conn.ExecuteAsync("UPDATE Users SET EmailConfirmed = 1 WHERE EmailConfirmed IS NULL");

        // ── Colonne FacebookId sur Users ───────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Users') AND name = N'FacebookId')
            ALTER TABLE Users ADD FacebookId NVARCHAR(100) NULL;
        ");

        // ── Paramètres ADMIN (key/value) ───────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdminSettings')
            CREATE TABLE AdminSettings (
                [Key]       NVARCHAR(100) NOT NULL PRIMARY KEY,
                [Value]     NVARCHAR(MAX) NULL,
                UpdatedAt   DATETIME      NOT NULL DEFAULT GETDATE()
            );
        ");

        // ── Packets de points ──────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PointPackets')
            CREATE TABLE PointPackets (
                IdPacket    BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title       NVARCHAR(200)  NOT NULL,
                Description NVARCHAR(1000) NULL,
                PointsCount INT            NOT NULL DEFAULT 0,
                Price       DECIMAL(10,2)  NOT NULL DEFAULT 0,
                Discount    DECIMAL(5,2)   NOT NULL DEFAULT 0,
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
            );
        ");

        // ── Valeurs par défaut des AdminSettings ───────────────────────────
        var defaults = new Dictionary<string, string>
        {
            { "money_transfer_commission_pct",       "2.5"   },
            { "standard_purchase_commission_pct",    "5.0"   },
            { "account_pro_user_month_price",        "9.99"  },
            { "account_pro_entreprise_month_price",  "29.99" },
            { "min_subscription_months",             "1"     },
            { "max_jobs_duration_days",              "30"    },
            { "max_missions_duration_days",          "30"    },
            { "max_freelance_duration_days",         "30"    },
            { "standard_max_magasin",                "1"     },
            { "standard_max_points",                 "100"   },
            { "min_add_annonce_points",              "5"     },
            { "min_add_freelance_points",            "5"     },
            { "min_add_products_points",             "5"     },
            { "mode_magasin_deals_active",           "true"  },
            { "boost_ads_enabled",                   "true"  },
            { "upgrade_account_enabled",             "true"  },
            { "buy_points_enabled",                  "true"  },
            { "premium_account_enabled",             "true"  },
            { "rating_preview_standard_enabled",     "true"  },
            { "rating_preview_enabled",              "true"  },
            { "boost_ads_coupon_enabled",            "false" },
            { "boost_ads_coupon_price",              "0"     },
            { "freelance_coupon_enabled",            "false" },
            { "freelance_coupon_price",              "0"     },
        };
        foreach (var kv in defaults)
        {
            await conn.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM AdminSettings WHERE [Key] = @Key)
                INSERT INTO AdminSettings ([Key], [Value]) VALUES (@Key, @Value);",
                new { Key = kv.Key, Value = kv.Value });
        }

        // ═══════════════ LOT 1 — CATALOG & MODERATION TABLES ═══════════════
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Brands')
            CREATE TABLE Brands (
                IdBrand     BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title       NVARCHAR(200)  NOT NULL,
                Description NVARCHAR(1000) NULL,
                Image       NVARCHAR(500)  NULL,
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Countries')
            CREATE TABLE Countries (
                IdCountry  BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title      NVARCHAR(200)  NOT NULL,
                Flag       NVARCHAR(500)  NULL,
                Code       NVARCHAR(10)   NULL,
                PhoneCode  NVARCHAR(10)   NULL,
                Active     BIT            NOT NULL DEFAULT 1,
                CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Cities')
            CREATE TABLE Cities (
                IdCity     BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title      NVARCHAR(200)  NOT NULL,
                IdCountry  BIGINT         NULL,
                TitleEn    NVARCHAR(200)  NULL,
                TitleAr    NVARCHAR(200)  NULL,
                Image      NVARCHAR(500)  NULL,
                Active     BIT            NOT NULL DEFAULT 1,
                CreatedAt  DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Causes')
            CREATE TABLE Causes (
                IdCause     BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title       NVARCHAR(200)  NOT NULL,
                Description NVARCHAR(1000) NULL,
                Email       NVARCHAR(200)  NULL,
                Type        NVARCHAR(50)   NULL,
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Coupons')
            CREATE TABLE Coupons (
                IdCoupon       BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title          NVARCHAR(200)  NOT NULL,
                Description    NVARCHAR(1000) NULL,
                DateStart      DATETIME       NULL,
                DateEnd        DATETIME       NULL,
                Price          DECIMAL(10,2)  NOT NULL DEFAULT 0,
                NumberOfCoupon INT            NOT NULL DEFAULT 0,
                Used           INT            NOT NULL DEFAULT 0,
                Active         BIT            NOT NULL DEFAULT 1,
                CreatedAt      DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Prizes')
            CREATE TABLE Prizes (
                IdPrize     BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title       NVARCHAR(200)  NOT NULL,
                Description NVARCHAR(1000) NULL,
                Image       NVARCHAR(500)  NULL,
                DatePrize   DATETIME       NULL,
                IdUser      BIGINT         NULL,
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='BoostAdsPacks')
            CREATE TABLE BoostAdsPacks (
                IdBoost      BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title        NVARCHAR(200) NOT NULL,
                Price        DECIMAL(10,2) NOT NULL DEFAULT 0,
                Discount     DECIMAL(5,2)  NOT NULL DEFAULT 0,
                MaxDuration  INT           NOT NULL DEFAULT 7,
                Sliders      BIT           NOT NULL DEFAULT 0,
                SideBar      BIT           NOT NULL DEFAULT 0,
                Footer       BIT           NOT NULL DEFAULT 0,
                RelatedPost  BIT           NOT NULL DEFAULT 0,
                FirstLogin   BIT           NOT NULL DEFAULT 0,
                OrdersCount  INT           NOT NULL DEFAULT 0,
                Links        BIT           NOT NULL DEFAULT 0,
                Active       BIT           NOT NULL DEFAULT 1,
                CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
            );
        ");

        // ═══════════════ LOT 2 — WINNERS, WISHLISTS, REVIEWS ══════════════
        await conn.ExecuteAsync(@"
            -- Winners (gagnants des prix/tirages)
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Winners')
            CREATE TABLE Winners (
                IdWinner    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser      BIGINT         NULL,
                IdPrize     BIGINT         NULL,
                IdOrder     BIGINT         NULL,
                FullName    NVARCHAR(300)  NULL,
                Email       NVARCHAR(200)  NULL,
                Phone       NVARCHAR(50)   NULL,
                Note        NVARCHAR(1000) NULL,
                WonAt       DATETIME       NOT NULL DEFAULT GETDATE(),
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE()
            );

            -- Wishlists : annonces, deals, produits
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='WishlistAds')
            CREATE TABLE WishlistAds (
                IdWish    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT NOT NULL,
                IdAd      BIGINT NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_WishlistAds UNIQUE (IdUser, IdAd)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='WishlistDeals')
            CREATE TABLE WishlistDeals (
                IdWish    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT NOT NULL,
                IdDeal    BIGINT NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_WishlistDeals UNIQUE (IdUser, IdDeal)
            );

            -- Reviews (avis / notes) pour ads, deals, produits
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Reviews')
            CREATE TABLE Reviews (
                IdReview    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser      BIGINT         NOT NULL,
                TargetType  NVARCHAR(20)   NOT NULL,   -- 'ad' | 'deal' | 'product'
                TargetId    BIGINT         NOT NULL,
                Rating      TINYINT        NOT NULL DEFAULT 5,
                Comment     NVARCHAR(1000) NULL,
                Active      BIT            NOT NULL DEFAULT 1,
                CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                CONSTRAINT UQ_Reviews UNIQUE (IdUser, TargetType, TargetId)
            );

            -- Likes sur annonces (si pas encore la contrainte)
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Likes')
            CREATE TABLE Likes (
                IdLike      BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser      BIGINT      NOT NULL,
                TargetType  NVARCHAR(20) NOT NULL,   -- 'ad' | 'deal'
                TargetId    BIGINT      NOT NULL,
                CreatedAt   DATETIME    DEFAULT GETDATE(),
                CONSTRAINT UQ_Likes UNIQUE (IdUser, TargetType, TargetId)
            );
        ");

        // Seed minimal defaults
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM Countries WHERE Code='TN')
                INSERT INTO Countries (Title, Flag, Code, PhoneCode) VALUES
                    ('Tunisie', '🇹🇳', 'TN', '+216'),
                    ('France',  '🇫🇷', 'FR', '+33'),
                    ('Maroc',   '🇲🇦', 'MA', '+212'),
                    ('Algérie', '🇩🇿', 'DZ', '+213');
            IF NOT EXISTS (SELECT 1 FROM Causes WHERE Title='Litige commande')
                INSERT INTO Causes (Title, Description, Type) VALUES
                    ('Litige commande',  'Problème avec une commande ou livraison', 'order'),
                    ('Produit défectueux','Article reçu endommagé ou non conforme', 'product'),
                    ('Vendeur injoignable','Vendeur ne répond pas', 'vendor'),
                    ('Autre',            'Autre type de réclamation', 'other');
        ");

        // ── Seed demo accounts (password: Test1234) ───────────────────────
        // Hash computed in C# because SQL Server has no bcrypt.
        await SeedDemoAccountsAsync(conn);
    }

    private static async Task SeedDemoAccountsAsync(SqlConnection conn)
    {
        var demos = new[]
        {
            new { Email = "admin@tijara.tn",   Username = "Admin",   First = "Super",  Last = "Admin",  IdRole = 1 },
            new { Email = "moslem@gmail.com",  Username = "Moslem",  First = "Moslem", Last = "Test",   IdRole = 2 },
            new { Email = "user@tijara.tn",    Username = "User",    First = "Demo",   Last = "User",   IdRole = 2 },
            new { Email = "vendor@tijara.tn",  Username = "Vendor",  First = "Demo",   Last = "Vendor", IdRole = 3 },
        };

        foreach (var d in demos)
        {
            // Always (re)set the password so login works even after a manual DB edit.
            var hash = BCrypt.Net.BCrypt.HashPassword("Test1234");
            var existing = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT IdUser FROM Users WHERE Email = @Email", new { d.Email });

            if (existing == null)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Users (Username, FirstName, LastName, Email, Password,
                                       IdRole, Active, IsVerified, CreationDate)
                    VALUES (@Username, @First, @Last, @Email, @Password,
                            @IdRole, 1, 1, CONVERT(NVARCHAR(50), GETDATE(), 120));",
                    new { d.Username, d.First, d.Last, d.Email, Password = hash, d.IdRole });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE Users
                       SET Password = @Password, Active = 1, IsVerified = 1
                     WHERE Email = @Email;",
                    new { d.Email, Password = hash });
            }
        }
    }
}
