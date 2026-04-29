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

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryAsync<T>(sql, param);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteAsync(sql, param);
    }

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

        // ── Colonnes optionnelles sur Users ────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'BirthDate')
            ALTER TABLE Users ADD BirthDate NVARCHAR(20) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'Gender')
            ALTER TABLE Users ADD Gender NVARCHAR(20) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'City')
            ALTER TABLE Users ADD City NVARCHAR(100) NULL;
        ");

        // ── Colonnes optionnelles sur Ads ──────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Ads') AND name = N'Type')
            ALTER TABLE Ads ADD Type NVARCHAR(20) NULL DEFAULT 'annonce';
        ");
        await conn.ExecuteAsync("UPDATE Ads SET Type = 'annonce' WHERE Type IS NULL");

        // ── Tables likes et commentaires ───────────────────────────────────
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

        // ── EmailTokens ────────────────────────────────────────────────────
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

        // ── EmailConfirmed sur Users ───────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'EmailConfirmed')
            ALTER TABLE Users ADD EmailConfirmed BIT NULL DEFAULT 1;
        ");
        await conn.ExecuteAsync("UPDATE Users SET EmailConfirmed = 1 WHERE EmailConfirmed IS NULL");

        // ── FacebookId sur Users ───────────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Users') AND name = N'FacebookId')
            ALTER TABLE Users ADD FacebookId NVARCHAR(100) NULL;
        ");

        // ── AdminSettings ──────────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdminSettings')
            CREATE TABLE AdminSettings (
                [Key]     NVARCHAR(100) NOT NULL PRIMARY KEY,
                [Value]   NVARCHAR(MAX) NULL,
                UpdatedAt DATETIME      NOT NULL DEFAULT GETDATE()
            );
        ");

        // ── PointPackets ───────────────────────────────────────────────────
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

        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'Title')
                ALTER TABLE PointPackets ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'Description')
                ALTER TABLE PointPackets ADD Description NVARCHAR(1000) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'PointsCount')
                ALTER TABLE PointPackets ADD PointsCount INT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'Price')
                ALTER TABLE PointPackets ADD Price DECIMAL(10,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'Discount')
                ALTER TABLE PointPackets ADD Discount DECIMAL(5,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'Active')
                ALTER TABLE PointPackets ADD Active BIT NOT NULL DEFAULT 1;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PointPackets') AND name = N'CreatedAt')
                ALTER TABLE PointPackets ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
        ");

        // ── AdminSettings defaults ─────────────────────────────────────────
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

        // ═══════════════ LOT 1 — CATALOG & MODERATION ═════════════════════
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
                IdCountry BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title     NVARCHAR(200) NOT NULL,
                Flag      NVARCHAR(500) NULL,
                Code      NVARCHAR(10)  NULL,
                PhoneCode NVARCHAR(10)  NULL,
                Active    BIT           NOT NULL DEFAULT 1,
                CreatedAt DATETIME      NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Cities')
            CREATE TABLE Cities (
                IdCity    BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title     NVARCHAR(200) NOT NULL,
                IdCountry BIGINT        NULL,
                TitleEn   NVARCHAR(200) NULL,
                TitleAr   NVARCHAR(200) NULL,
                Image     NVARCHAR(500) NULL,
                Active    BIT           NOT NULL DEFAULT 1,
                CreatedAt DATETIME      NOT NULL DEFAULT GETDATE()
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
                IdBoost     BIGINT IDENTITY(1,1) PRIMARY KEY,
                Title       NVARCHAR(200) NOT NULL,
                Price       DECIMAL(10,2) NOT NULL DEFAULT 0,
                Discount    DECIMAL(5,2)  NOT NULL DEFAULT 0,
                MaxDuration INT           NOT NULL DEFAULT 7,
                Sliders     BIT           NOT NULL DEFAULT 0,
                SideBar     BIT           NOT NULL DEFAULT 0,
                Footer      BIT           NOT NULL DEFAULT 0,
                RelatedPost BIT           NOT NULL DEFAULT 0,
                FirstLogin  BIT           NOT NULL DEFAULT 0,
                OrdersCount INT           NOT NULL DEFAULT 0,
                Links       BIT           NOT NULL DEFAULT 0,
                Active      BIT           NOT NULL DEFAULT 1,
                CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE()
            );
        ");

        // ── Migration additive LOT 1 ───────────────────────────────────────
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Brands') AND name = N'Title')
                ALTER TABLE Brands ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Brands') AND name = N'Description')
                ALTER TABLE Brands ADD Description NVARCHAR(1000) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Brands') AND name = N'Image')
                ALTER TABLE Brands ADD Image NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Brands') AND name = N'Active')
                ALTER TABLE Brands ADD Active BIT NOT NULL DEFAULT 1;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Brands') AND name = N'CreatedAt')
                ALTER TABLE Brands ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Countries') AND name = N'Title')
                ALTER TABLE Countries ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Countries') AND name = N'Active')
                ALTER TABLE Countries ADD Active BIT NOT NULL DEFAULT 1;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Countries') AND name = N'Code')
                ALTER TABLE Countries ADD Code NVARCHAR(10) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Countries') AND name = N'PhoneCode')
                ALTER TABLE Countries ADD PhoneCode NVARCHAR(10) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Countries') AND name = N'Flag')
                ALTER TABLE Countries ADD Flag NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'Title')
                ALTER TABLE Cities ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'IdCountry')
                ALTER TABLE Cities ADD IdCountry BIGINT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'TitleEn')
                ALTER TABLE Cities ADD TitleEn NVARCHAR(200) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'TitleAr')
                ALTER TABLE Cities ADD TitleAr NVARCHAR(200) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'Image')
                ALTER TABLE Cities ADD Image NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cities') AND name = N'Active')
                ALTER TABLE Cities ADD Active BIT NOT NULL DEFAULT 1;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Causes') AND name = N'Title')
                ALTER TABLE Causes ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Causes') AND name = N'Description')
                ALTER TABLE Causes ADD Description NVARCHAR(1000) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Causes') AND name = N'Email')
                ALTER TABLE Causes ADD Email NVARCHAR(200) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Causes') AND name = N'Type')
                ALTER TABLE Causes ADD Type NVARCHAR(50) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Coupons') AND name = N'Title')
                ALTER TABLE Coupons ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Coupons') AND name = N'Description')
                ALTER TABLE Coupons ADD Description NVARCHAR(1000) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Prizes') AND name = N'Title')
                ALTER TABLE Prizes ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Prizes') AND name = N'Description')
                ALTER TABLE Prizes ADD Description NVARCHAR(1000) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Prizes') AND name = N'Image')
                ALTER TABLE Prizes ADD Image NVARCHAR(500) NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'BoostAdsPacks') AND name = N'Title')
                ALTER TABLE BoostAdsPacks ADD Title NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'BoostAdsPacks') AND name = N'Price')
                ALTER TABLE BoostAdsPacks ADD Price DECIMAL(10,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'BoostAdsPacks') AND name = N'Discount')
                ALTER TABLE BoostAdsPacks ADD Discount DECIMAL(5,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'BoostAdsPacks') AND name = N'MaxDuration')
                ALTER TABLE BoostAdsPacks ADD MaxDuration INT NOT NULL DEFAULT 7;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'BoostAdsPacks') AND name = N'Active')
                ALTER TABLE BoostAdsPacks ADD Active BIT NOT NULL DEFAULT 1;
        ");

        // ═══════════════ LOT 2 — WINNERS, WISHLISTS, REVIEWS ══════════════
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Winners')
            CREATE TABLE Winners (
                IdWinner  BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT         NULL,
                IdPrize   BIGINT         NULL,
                IdOrder   BIGINT         NULL,
                FullName  NVARCHAR(300)  NULL,
                Email     NVARCHAR(200)  NULL,
                Phone     NVARCHAR(50)   NULL,
                Note      NVARCHAR(1000) NULL,
                WonAt     DATETIME       NOT NULL DEFAULT GETDATE(),
                Active    BIT            NOT NULL DEFAULT 1,
                CreatedAt DATETIME       NOT NULL DEFAULT GETDATE()
            );
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
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Reviews')
            CREATE TABLE Reviews (
                IdReview   BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser     BIGINT       NOT NULL,
                TargetType NVARCHAR(20) NOT NULL,
                TargetId   BIGINT       NOT NULL,
                Rating     TINYINT      NOT NULL DEFAULT 5,
                Comment    NVARCHAR(1000) NULL,
                Active     BIT          NOT NULL DEFAULT 1,
                CreatedAt  DATETIME     NOT NULL DEFAULT GETDATE(),
                CONSTRAINT UQ_Reviews UNIQUE (IdUser, TargetType, TargetId)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Likes')
            CREATE TABLE Likes (
                IdLike     BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser     BIGINT       NOT NULL,
                TargetType NVARCHAR(20) NOT NULL,
                TargetId   BIGINT       NOT NULL,
                CreatedAt  DATETIME     DEFAULT GETDATE(),
                CONSTRAINT UQ_Likes UNIQUE (IdUser, TargetType, TargetId)
            );
        ");

        // ═══════════════ LOT 3 + 6 — CORE TABLES ════════════════════════════
        // MERGED: Combines upstream comprehensive schema with stashed defensive checks
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Permissions' AND xtype='U')
            CREATE TABLE Permissions (
                IdPermission INT IDENTITY(1,1) PRIMARY KEY,
                IdRole       INT           NOT NULL,
                Resource     NVARCHAR(100) NOT NULL,
                CanRead      BIT DEFAULT 1,
                CanCreate    BIT DEFAULT 0,
                CanUpdate    BIT DEFAULT 0,
                CanDelete    BIT DEFAULT 0,
                CONSTRAINT UQ_Permissions UNIQUE (IdRole, Resource)
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Payments' AND xtype='U')
            CREATE TABLE Payments (
                IdPayment    BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser       BIGINT NOT NULL,
                IdOrder      BIGINT NULL,
                Amount       DECIMAL(18,3) NOT NULL,
                Method       NVARCHAR(40)  NOT NULL,
                Status       NVARCHAR(30)  NOT NULL DEFAULT 'pending',
                Reference    NVARCHAR(100) NULL,
                TransactionId NVARCHAR(150) NULL,
                CreatedAt    DATETIME DEFAULT GETDATE(),
                PaidAt       DATETIME NULL
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Transports' AND xtype='U')
            CREATE TABLE Transports (
                IdTransport  INT IDENTITY(1,1) PRIMARY KEY,
                Name         NVARCHAR(150) NOT NULL,
                Logo         NVARCHAR(500) NULL,
                Phone        NVARCHAR(40)  NULL,
                Email        NVARCHAR(200) NULL,
                DeliveryFee  DECIMAL(18,3) DEFAULT 0,
                FreeFrom     DECIMAL(18,3) NULL,
                Zones        NVARCHAR(500) NULL,
                Active       BIT DEFAULT 1,
                CreatedAt    DATETIME DEFAULT GETDATE(),
                -- Additional fields from TransportRequest DTO (support both schemas)
                Description  NVARCHAR(500) NULL,
                Price        DECIMAL(18,3) NULL,
                Duration     NVARCHAR(50)  NULL
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Deliveries' AND xtype='U')
            CREATE TABLE Deliveries (
                IdDelivery   BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdOrder      BIGINT NOT NULL,
                IdTransport  INT NULL,
                TrackingNumber NVARCHAR(100) NULL,
                Status       NVARCHAR(30) NOT NULL DEFAULT 'pending',
                AddressLine  NVARCHAR(500) NULL,
                City         NVARCHAR(150) NULL,
                PostalCode   NVARCHAR(20)  NULL,
                Phone        NVARCHAR(40)  NULL,
                DeliveryFee  DECIMAL(18,3) DEFAULT 0,
                EstimatedAt  DATETIME NULL,
                DeliveredAt  DATETIME NULL,
                Note         NVARCHAR(500) NULL,
                CreatedAt    DATETIME DEFAULT GETDATE(),
                UpdatedAt    DATETIME DEFAULT GETDATE()
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Invoices' AND xtype='U')
            CREATE TABLE Invoices (
                IdInvoice    BIGINT IDENTITY(1,1) PRIMARY KEY,
                Number       NVARCHAR(50) NOT NULL UNIQUE,
                IdOrder      BIGINT NOT NULL,
                IdUser       BIGINT NOT NULL,
                IdVendor     BIGINT NULL,
                Subtotal     DECIMAL(18,3) NOT NULL,
                Tax          DECIMAL(18,3) DEFAULT 0,
                DeliveryFee  DECIMAL(18,3) DEFAULT 0,
                Total        DECIMAL(18,3) NOT NULL,
                Status       NVARCHAR(30) DEFAULT 'issued',
                IssuedAt     DATETIME DEFAULT GETDATE(),
                PaidAt       DATETIME NULL
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SmsLogs' AND xtype='U')
            CREATE TABLE SmsLogs (
                IdSms       BIGINT IDENTITY(1,1) PRIMARY KEY,
                Recipient   NVARCHAR(40) NOT NULL,
                Message     NVARCHAR(500) NOT NULL,
                Status      NVARCHAR(30) DEFAULT 'queued',
                Provider    NVARCHAR(50) NULL,
                SentAt      DATETIME DEFAULT GETDATE(),
                Error       NVARCHAR(500) NULL
            );
        ");

        // ──────────────────────────────────────────────────────────────
        // Migration données: corrige les annonces sans prix + recalcule
        // les factures déjà émises à 0 TND. Idempotente.
        // ──────────────────────────────────────────────────────────────
        try
        {
            await conn.ExecuteAsync(@"
                -- 1) Normalise PriceDeal: virgule → point, trim
                UPDATE Deals
                   SET PriceDeal = LTRIM(RTRIM(REPLACE(PriceDeal, ',', '.')))
                 WHERE PriceDeal LIKE '%,%' OR PriceDeal LIKE ' %' OR PriceDeal LIKE '% ';

                -- 2) Affecte un prix aléatoire 50-500 TND aux annonces sans prix
                UPDATE Deals
                   SET PriceDeal = CAST(50 + ABS(CHECKSUM(NEWID())) % 451 AS NVARCHAR(50)) + '.000'
                 WHERE PriceDeal IS NULL
                    OR LTRIM(RTRIM(PriceDeal)) = ''
                    OR TRY_CAST(PriceDeal AS DECIMAL(18,3)) IS NULL;

                -- 3) Recalcule les factures à 0 TND (héritage du bug PriceDeal vide)
                IF OBJECT_ID('Invoices','U') IS NOT NULL
                BEGIN
                    UPDATE i
                       SET i.Subtotal    = ISNULL(TRY_CAST(d.PriceDeal AS DECIMAL(18,3)), 0),
                           i.Tax         = ROUND(ISNULL(TRY_CAST(d.PriceDeal AS DECIMAL(18,3)), 0) * 0.07, 3),
                           i.Total       = ISNULL(TRY_CAST(d.PriceDeal AS DECIMAL(18,3)), 0)
                                         + ROUND(ISNULL(TRY_CAST(d.PriceDeal AS DECIMAL(18,3)), 0) * 0.07, 3)
                      FROM Invoices i
                      JOIN Orders   o ON i.IdOrder = o.IdOrder
                      JOIN Deals    d ON o.IdDeal  = d.IdDeal
                     WHERE i.Total = 0
                       AND TRY_CAST(d.PriceDeal AS DECIMAL(18,3)) > 0;
                END

                -- 4) Renseigne IdVendor des factures où il manque
                IF OBJECT_ID('Invoices','U') IS NOT NULL
                BEGIN
                    UPDATE i
                       SET i.IdVendor = d.idUser
                      FROM Invoices i
                      JOIN Orders   o ON i.IdOrder = o.IdOrder
                      JOIN Deals    d ON o.IdDeal  = d.IdDeal
                     WHERE i.IdVendor IS NULL
                       AND d.idUser   IS NOT NULL;
                END
            ");
            Console.WriteLine("[Migration] Données Deals/Invoices normalisées.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Migration] Avertissement: " + ex.Message);
        }

        // Migrate Permissions table — add missing columns if the table exists with an older schema
        try
        {
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('Permissions','U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Permissions','Resource')  IS NULL ALTER TABLE Permissions ADD Resource  NVARCHAR(100) NULL;
                    IF COL_LENGTH('Permissions','CanRead')   IS NULL ALTER TABLE Permissions ADD CanRead   BIT DEFAULT 0;
                    IF COL_LENGTH('Permissions','CanCreate') IS NULL ALTER TABLE Permissions ADD CanCreate BIT DEFAULT 0;
                    IF COL_LENGTH('Permissions','CanUpdate') IS NULL ALTER TABLE Permissions ADD CanUpdate BIT DEFAULT 0;
                    IF COL_LENGTH('Permissions','CanDelete') IS NULL ALTER TABLE Permissions ADD CanDelete BIT DEFAULT 0;
                    IF COL_LENGTH('Permissions','IdRole')    IS NULL ALTER TABLE Permissions ADD IdRole    INT NULL;
                END
            ");
        }
        catch { /* tolérer */ }

        // Seed default permissions — schema-safe
        try
        {
            await conn.ExecuteAsync(@"
                IF COL_LENGTH('Permissions','Resource') IS NOT NULL
                   AND COL_LENGTH('Permissions','IdRole') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Permissions WHERE IdRole=1 AND Resource='users')
                BEGIN
                    INSERT INTO Permissions (IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete) VALUES
                        (1,'users',1,1,1,1),(1,'products',1,1,1,1),(1,'orders',1,1,1,1),
                        (1,'categories',1,1,1,1),(1,'payments',1,1,1,1),(1,'transports',1,1,1,1),
                        (1,'reports',1,0,0,0),
                        (3,'products',1,1,1,1),(3,'orders',1,0,1,0),(3,'reports',1,0,0,0),
                        (2,'orders',1,1,1,0),(2,'reviews',1,1,1,1);
                END
            ");
        }
        catch { /* tolérer */ }

        // ═══════════════ ADDITIONAL TABLES FROM STASHED ════════════════════
        // RolePermissions, PaymentMethods, WalletTransactions, BlockedUsers
        await conn.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='RolePermissions')
            CREATE TABLE RolePermissions (
                IdRolePermission BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdRole           INT    NOT NULL,
                IdPermission     BIGINT NOT NULL,
                CONSTRAINT UQ_RolePerm UNIQUE (IdRole, IdPermission)
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='PaymentMethods')
            CREATE TABLE PaymentMethods (
                IdPayment BIGINT IDENTITY(1,1) PRIMARY KEY,
                Name      NVARCHAR(100) NOT NULL,
                Type      NVARCHAR(50)  NOT NULL DEFAULT 'card',
                IsDefault BIT           NOT NULL DEFAULT 0,
                IdUser    BIGINT        NOT NULL,
                Details   NVARCHAR(500) NULL,
                Active    BIT           NOT NULL DEFAULT 1,
                CreatedAt DATETIME      NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='WalletTransactions')
            CREATE TABLE WalletTransactions (
                IdTransaction BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdWallet      BIGINT        NOT NULL,
                IdUser        BIGINT        NOT NULL,
                Type          NVARCHAR(50)  NOT NULL,
                Amount        DECIMAL(10,2) NOT NULL,
                Description   NVARCHAR(500) NULL,
                RefId         BIGINT        NULL,
                Status        NVARCHAR(20)  NOT NULL DEFAULT 'completed',
                CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
            );
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='BlockedUsers')
            CREATE TABLE BlockedUsers (
                IdBlock   BIGINT IDENTITY(1,1) PRIMARY KEY,
                IdUser    BIGINT NOT NULL,
                IdBlocked BIGINT NOT NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                CONSTRAINT UQ_BlockedUsers UNIQUE (IdUser, IdBlocked)
            );
        ");

        // ── Migration Countries — ajoute colonnes manquantes si DB ancienne ──
        try
        {
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('Countries','U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Countries','Title') IS NULL
                        ALTER TABLE Countries ADD Title NVARCHAR(200) NULL;
                    IF COL_LENGTH('Countries','Flag') IS NULL
                        ALTER TABLE Countries ADD Flag NVARCHAR(500) NULL;
                    IF COL_LENGTH('Countries','Code') IS NULL
                        ALTER TABLE Countries ADD Code NVARCHAR(10) NULL;
                    IF COL_LENGTH('Countries','PhoneCode') IS NULL
                        ALTER TABLE Countries ADD PhoneCode NVARCHAR(10) NULL;
                    IF COL_LENGTH('Countries','Active') IS NULL
                        ALTER TABLE Countries ADD Active BIT NOT NULL DEFAULT 1;
                    IF COL_LENGTH('Countries','CreatedAt') IS NULL
                        ALTER TABLE Countries ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
                END
            ");
            // Si l'ancienne colonne CountryName / Name / Country existe et Title est vide → recopier
            foreach (var legacy in new[] { "CountryName", "Name", "Country", "Nom" })
            {
                try
                {
                    await conn.ExecuteAsync($@"
                        IF COL_LENGTH('Countries','{legacy}') IS NOT NULL
                           AND COL_LENGTH('Countries','Title') IS NOT NULL
                        BEGIN
                            DECLARE @sql NVARCHAR(MAX) = N'UPDATE Countries SET Title = [{legacy}] WHERE (Title IS NULL OR Title='''') AND [{legacy}] IS NOT NULL';
                            EXEC sp_executesql @sql;
                        END
                    ");
                }
                catch { /* colonne absente, continue */ }
            }
            // Fallback : si Title est encore NULL, mettre "Pays #ID"
            await conn.ExecuteAsync(@"
                UPDATE Countries SET Title = CONCAT('Pays #', IdCountry) WHERE Title IS NULL OR Title = '';
                UPDATE Countries SET Active = 1 WHERE Active IS NULL;
            ");
        }
        catch (Exception ex) { Console.WriteLine($"[Countries migration] {ex.Message}"); }

        // ── Migration Cities — ajoute colonnes manquantes ──
        try
        {
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('Cities','U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('Cities','Title')     IS NULL ALTER TABLE Cities ADD Title NVARCHAR(200) NULL;
                    IF COL_LENGTH('Cities','TitleEn')   IS NULL ALTER TABLE Cities ADD TitleEn NVARCHAR(200) NULL;
                    IF COL_LENGTH('Cities','TitleAr')   IS NULL ALTER TABLE Cities ADD TitleAr NVARCHAR(200) NULL;
                    IF COL_LENGTH('Cities','Image')     IS NULL ALTER TABLE Cities ADD Image NVARCHAR(500) NULL;
                    IF COL_LENGTH('Cities','IdCountry') IS NULL ALTER TABLE Cities ADD IdCountry BIGINT NULL;
                    IF COL_LENGTH('Cities','Active')    IS NULL ALTER TABLE Cities ADD Active BIT NOT NULL DEFAULT 1;
                END
            ");
            foreach (var legacy in new[] { "CityName", "Name", "City", "Nom" })
            {
                try
                {
                    await conn.ExecuteAsync($@"
                        IF COL_LENGTH('Cities','{legacy}') IS NOT NULL
                           AND COL_LENGTH('Cities','Title') IS NOT NULL
                        BEGIN
                            DECLARE @sql NVARCHAR(MAX) = N'UPDATE Cities SET Title = [{legacy}] WHERE (Title IS NULL OR Title='''') AND [{legacy}] IS NOT NULL';
                            EXEC sp_executesql @sql;
                        END
                    ");
                }
                catch { /* colonne absente */ }
            }
            await conn.ExecuteAsync(@"
                UPDATE Cities SET Title = CONCAT('Ville #', IdCity) WHERE Title IS NULL OR Title = '';
                UPDATE Cities SET Active = 1 WHERE Active IS NULL;
            ");
        }
        catch (Exception ex) { Console.WriteLine($"[Cities migration] {ex.Message}"); }

        // Seed default transports
        try
        {
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('Transports','U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Transports)
                BEGIN
                    INSERT INTO Transports (Name, Phone, DeliveryFee, FreeFrom, Zones, Active) VALUES
                        ('Aramex Tunisie',  '+216 71 100 100', 8.000,  200.000, 'Grand Tunis, Sfax, Sousse', 1),
                        ('Colissimo',       '+216 71 200 200', 6.000,  150.000, 'Toute la Tunisie',           1),
                        ('Rapid Poste',     '+216 71 300 300', 4.500,  100.000, 'Toute la Tunisie',           1),
                        ('First Delivery',  '+216 71 400 400', 7.000,  180.000, 'Grand Tunis, Nabeul',        1);
                END
            ");
        }
        catch { /* tolérer */ }

        // Seed minimal defaults — schema-safe (tolère les colonnes absentes)
        try
        {
            await conn.ExecuteAsync(@"
                IF COL_LENGTH('Countries','Code') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Countries WHERE Code='TN')
                BEGIN
                    INSERT INTO Countries (Title, Flag, Code, PhoneCode) VALUES
                        ('Tunisie', N'🇹🇳', 'TN', '+216'),
                        ('France',  N'🇫🇷', 'FR', '+33'),
                        ('Maroc',   N'🇲🇦', 'MA', '+212'),
                        ('Algérie', N'🇩🇿', 'DZ', '+213');
                END
            ");
        }
        catch { /* schéma Countries différent — ignorer */ }

        try
        {
            await conn.ExecuteAsync(@"
                IF COL_LENGTH('Causes','Title') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Causes WHERE Title='Litige commande')
                BEGIN
                    INSERT INTO Causes (Title, Description, Type) VALUES
                        ('Litige commande',   'Problème avec une commande ou livraison', 'order'),
                        ('Produit défectueux','Article reçu endommagé ou non conforme', 'product'),
                        ('Vendeur injoignable','Vendeur ne répond pas', 'vendor'),
                        ('Autre',             'Autre type de réclamation', 'other');
                END
            ");
        }
        catch (SqlException ex )       {
             Console.WriteLine("[Seed Causes] Ignoré : " + ex.Message);
        }

}
} 