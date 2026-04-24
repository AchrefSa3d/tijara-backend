-- ================================================================
--  DealsDB26 - Schéma complet + données de test
--  SQL Server 2025 | Instance SQLEXPRESS01
-- ================================================================
USE DealsDB26;
GO

-- ── Rôles ────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Roles')
CREATE TABLE Roles (
    IdRole   INT           NOT NULL PRIMARY KEY,
    RoleUser NVARCHAR(50)  NOT NULL
);
GO

-- ── Pays ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Countries')
CREATE TABLE Countries (
    IdCountry   BIGINT       NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CountryName NVARCHAR(100) NOT NULL
);
GO

-- ── États / Régions ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='States')
CREATE TABLE States (
    IdState   BIGINT       NOT NULL IDENTITY(1,1) PRIMARY KEY,
    StateName NVARCHAR(100) NOT NULL,
    IdCountry BIGINT        NULL REFERENCES Countries(IdCountry)
);
GO

-- ── Utilisateurs ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Users')
CREATE TABLE Users (
    IdUser            BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Username          NVARCHAR(100) NULL,
    FirstName         NVARCHAR(100) NULL,
    LastName          NVARCHAR(100) NULL,
    BirthDate         NVARCHAR(50)  NULL,
    Gender            NVARCHAR(20)  NULL,
    Email             NVARCHAR(200) NOT NULL UNIQUE,
    Telephone         NVARCHAR(30)  NULL,
    Password          NVARCHAR(500) NULL,
    IdRole            INT           NULL REFERENCES Roles(IdRole),
    ProfilePicture    NVARCHAR(500) NULL,
    CreationDate      NVARCHAR(50)  NULL DEFAULT CONVERT(NVARCHAR(50),GETDATE(),120),
    IsVerified        INT           NULL DEFAULT 0,
    IsPremuim         INT           NULL DEFAULT 0,
    IsBusinessAccount INT           NULL DEFAULT 0,
    IdState           BIGINT        NULL REFERENCES States(IdState),
    IdCountry         BIGINT        NULL REFERENCES Countries(IdCountry),
    Location          NVARCHAR(300) NULL,
    LastConnection    NVARCHAR(50)  NULL,
    Active            INT           NULL DEFAULT 1
);
GO

-- ── Types de catégories ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='TypeCategory')
CREATE TABLE TypeCategory (
    Idtypecat INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title     NVARCHAR(100) NOT NULL
);
GO

-- ── Catégories ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Categories')
CREATE TABLE Categories (
    IdCateg     INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TitleEn     NVARCHAR(100) NULL,
    TitleFr     NVARCHAR(100) NULL,
    TitleAr     NVARCHAR(100) NULL,
    Description NVARCHAR(500) NULL,
    Image       NVARCHAR(500) NULL,
    Idtypecat   INT           NULL REFERENCES TypeCategory(Idtypecat),
    Active      INT           NULL DEFAULT 1
);
GO

-- ── Deals (Produits/Offres) ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Deals')
CREATE TABLE Deals (
    IdDeal          INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TitleDeal       NVARCHAR(200) NULL,
    DescriptionDeal NVARCHAR(MAX) NULL,
    DetailsDeal     NVARCHAR(MAX) NULL,
    PriceDeal       NVARCHAR(50)  NULL,
    DiscountDeal    NVARCHAR(50)  NULL,
    Quantity        NVARCHAR(50)  NULL,
    DatePublication NVARCHAR(50)  NULL DEFAULT CONVERT(NVARCHAR(50),GETDATE(),120),
    DateEnd         NVARCHAR(50)  NULL,
    ImageDeal       NVARCHAR(500) NULL,
    Idtypecat       INT           NULL,
    IdCateg         INT           NULL REFERENCES Categories(IdCateg),
    IdUser          INT           NULL,
    IdState         BIGINT        NULL,
    IdPrize         BIGINT        NULL,
    LocationDeal    NVARCHAR(300) NULL,
    Active          INT           NULL DEFAULT 1,
    Colors          NVARCHAR(200) NULL,
    Likes           INT           NULL DEFAULT 0,
    Telephone       NVARCHAR(30)  NULL,
    Email           NVARCHAR(200) NULL,
    Ownerdeals      NVARCHAR(200) NULL,
    Brand           NVARCHAR(100) NULL,
    StartDate       NVARCHAR(50)  NULL
);
GO

-- ── Produits ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Products')
CREATE TABLE Products (
    IdProduct          BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CodeBarProduct     NVARCHAR(100) NULL,
    TitleProduct       NVARCHAR(200) NULL,
    DescriptionProduct NVARCHAR(MAX) NULL,
    QuantityProduct    INT           NULL DEFAULT 0,
    ColorProduct       NVARCHAR(100) NULL,
    PriceProduct       NVARCHAR(50)  NULL,
    ImageProduct       NVARCHAR(500) NULL,
    IdCateorie         BIGINT        NULL,
    IdUser             BIGINT        NULL REFERENCES Users(IdUser),
    IdCountrie         BIGINT        NULL,
    Active             INT           NULL DEFAULT 1
);
GO

-- ── Commandes ────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Commands')
CREATE TABLE Commands (
    IdOrder         BIGINT   NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUser          BIGINT   NULL REFERENCES Users(IdUser),
    IdDeal          BIGINT   NULL,
    IdState         BIGINT   NULL,
    DateTimeCommand DATETIME NULL DEFAULT GETDATE(),
    Active          INT      NULL DEFAULT 1
);
GO

-- ── Détails commande ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='OrderDetails')
CREATE TABLE OrderDetails (
    IdOrderDeatils  BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUser          BIGINT        NULL REFERENCES Users(IdUser),
    IdProduct       BIGINT        NULL,
    IdOrder         BIGINT        NULL REFERENCES Commands(IdOrder),
    Address         NVARCHAR(300) NULL,
    Email           NVARCHAR(200) NULL,
    Telephone       NVARCHAR(30)  NULL,
    FirstName       NVARCHAR(100) NULL,
    LastName        NVARCHAR(100) NULL,
    Quantity        INT           NULL DEFAULT 1,
    TotalAmount     NVARCHAR(50)  NULL,
    DateTimeCommand NVARCHAR(50)  NULL,
    Active          INT           NULL DEFAULT 1
);
GO

-- ── Chats (conversations) ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Chats')
CREATE TABLE Chats (
    IdChat        INT      NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUserSender  BIGINT   NULL REFERENCES Users(IdUser),
    IdUserReciver BIGINT   NULL REFERENCES Users(IdUser),
    CreatedAt     DATETIME NULL DEFAULT GETDATE(),
    Active        INT      NULL DEFAULT 1
);
GO

-- ── Messages de chat ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ChatMessages')
CREATE TABLE ChatMessages (
    IdChatMessage BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdChat        INT           NULL REFERENCES Chats(IdChat),
    Message       NVARCHAR(MAX) NULL,
    CreateDate    DATETIME      NULL DEFAULT GETDATE(),
    IdUserSender  BIGINT        NULL REFERENCES Users(IdUser),
    Active        INT           NULL DEFAULT 1
);
GO

-- ── Annonces (Ads) ───────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Ads')
CREATE TABLE Ads (
    IdAd            BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    TitleAd         NVARCHAR(200) NULL,
    DescriptionAd   NVARCHAR(MAX) NULL,
    DetailsAd       NVARCHAR(MAX) NULL,
    PriceAd         NVARCHAR(50)  NULL,
    DatePublication NVARCHAR(50)  NULL DEFAULT CONVERT(NVARCHAR(50),GETDATE(),120),
    ImageAd         NVARCHAR(500) NULL,
    IdCateg         INT           NULL REFERENCES Categories(IdCateg),
    IdUser          BIGINT        NULL REFERENCES Users(IdUser),
    Active          INT           NULL DEFAULT 1
);
GO

-- ── Causes de signalement ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='CausesReports')
CREATE TABLE CausesReports (
    IdCauseReport INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title         NVARCHAR(200) NOT NULL,
    Active        INT           NULL DEFAULT 1
);
GO

-- ── Signalements ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Reports')
CREATE TABLE Reports (
    IdReport      INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUser        BIGINT        NULL REFERENCES Users(IdUser),
    IdCauseReport INT           NULL REFERENCES CausesReports(IdCauseReport),
    Subject       NVARCHAR(300) NULL,
    Description   NVARCHAR(MAX) NULL,
    Date          DATETIME      NULL DEFAULT GETDATE(),
    State         INT           NULL DEFAULT 1,
    TypeTable     NVARCHAR(100) NULL,
    IdTable       BIGINT        NULL
);
GO

-- ── Notes / Avis ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Ratings')
CREATE TABLE Ratings (
    IdRating      BIGINT        NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUser        BIGINT        NULL REFERENCES Users(IdUser),
    RatingValue   BIGINT        NULL,
    CommentRating NVARCHAR(MAX) NULL,
    Date          NVARCHAR(50)  NULL DEFAULT CONVERT(NVARCHAR(50),GETDATE(),120),
    TableName     NVARCHAR(100) NULL,
    IdTable       BIGINT        NULL,
    Active        INT           NULL DEFAULT 1
);
GO

-- ── Notifications ────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Notifications')
CREATE TABLE Notifications (
    IdNotification INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title          NVARCHAR(200) NULL,
    Description    NVARCHAR(MAX) NULL,
    Date           NVARCHAR(50)  NULL DEFAULT CONVERT(NVARCHAR(50),GETDATE(),120),
    Type           NVARCHAR(50)  NULL,
    IsRead         INT           NULL DEFAULT 0,
    IdUser         BIGINT        NULL REFERENCES Users(IdUser)
);
GO

-- ── Portefeuilles ────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='Wallets')
CREATE TABLE Wallets (
    IdWallet        INT     NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUser          BIGINT  NULL REFERENCES Users(IdUser),
    NbrJeton        BIGINT  NULL DEFAULT 0,
    MoneyBudget     DECIMAL(18,2) NULL DEFAULT 0,
    MoneyBlocked    DECIMAL(18,2) NULL DEFAULT 0,
    MoneyTransfered DECIMAL(18,2) NULL DEFAULT 0,
    Active          INT     NULL DEFAULT 1
);
GO

-- ================================================================
--  DONNÉES DE BASE
-- ================================================================

-- Rôles
IF NOT EXISTS (SELECT 1 FROM Roles) BEGIN
    INSERT INTO Roles VALUES (1,'Admin'), (2,'Visitor'), (3,'Advertiser');
END
GO

-- Pays
IF NOT EXISTS (SELECT 1 FROM Countries) BEGIN
    INSERT INTO Countries (CountryName) VALUES
        ('Tunisie'),('Algérie'),('Maroc'),('France'),('Allemagne');
END
GO

-- États (gouvernorats tunisiens)
IF NOT EXISTS (SELECT 1 FROM States) BEGIN
    INSERT INTO States (StateName, IdCountry) VALUES
        ('Tunis',1),('Sfax',1),('Sousse',1),('Bizerte',1),('Nabeul',1),
        ('Monastir',1),('Mahdia',1),('Kairouan',1),('Gafsa',1),('Gabès',1);
END
GO

-- Types de catégories
IF NOT EXISTS (SELECT 1 FROM TypeCategory) BEGIN
    INSERT INTO TypeCategory (Title) VALUES
        ('Produits'),('Services'),('Événements'),('Offres d''emploi');
END
GO

-- Catégories
IF NOT EXISTS (SELECT 1 FROM Categories) BEGIN
    INSERT INTO Categories (TitleEn, TitleFr, TitleAr, Idtypecat, Active) VALUES
        ('Electronics',    'Électronique',      'إلكترونيات',   1, 1),
        ('Clothing',       'Vêtements',         'ملابس',         1, 1),
        ('Food',           'Alimentation',      'غذاء',          1, 1),
        ('Furniture',      'Meubles',           'أثاث',          1, 1),
        ('Cars',           'Voitures',          'سيارات',        1, 1),
        ('Real Estate',    'Immobilier',        'عقارات',        2, 1),
        ('Beauty',         'Beauté',            'جمال',          1, 1),
        ('Sports',         'Sports',            'رياضة',         1, 1),
        ('Books',          'Livres',            'كتب',           1, 1),
        ('Services',       'Services',          'خدمات',         2, 1);
END
GO

-- Causes de signalement
IF NOT EXISTS (SELECT 1 FROM CausesReports) BEGIN
    INSERT INTO CausesReports (Title, Active) VALUES
        ('Contenu inapproprié', 1),
        ('Arnaque ou fraude',   1),
        ('Spam',                1),
        ('Fausses informations',1),
        ('Violation des CGU',   1),
        ('Autre',               1);
END
GO

-- Utilisateurs de test
-- Admin:    admin@tijara.tn / Admin2026!
-- Vendor:   vendor@tijara.tn / Vendor2026!
-- Client:   client@tijara.tn / Client2026!
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email='admin@tijara.tn') BEGIN
    INSERT INTO Users (Username,FirstName,LastName,Email,Password,IdRole,IsVerified,Active,CreationDate)
    VALUES
    ('Admin','Admin','Tijara','admin@tijara.tn',
     '$2a$11$VdH5UNfxkM.Oq3GHHkFKbOZJNVvYM6QY1v2Xb7N8gTQ9dKl1P3He2',
     1,1,1,CONVERT(NVARCHAR(50),GETDATE(),120)),
    ('BestShop','Mohamed','Ben Ali','vendor@tijara.tn',
     '$2a$11$VdH5UNfxkM.Oq3GHHkFKbOZJNVvYM6QY1v2Xb7N8gTQ9dKl1P3He2',
     3,1,1,CONVERT(NVARCHAR(50),GETDATE(),120)),
    ('client1','Fatma','Chaouachi','client@tijara.tn',
     '$2a$11$VdH5UNfxkM.Oq3GHHkFKbOZJNVvYM6QY1v2Xb7N8gTQ9dKl1P3He2',
     2,1,1,CONVERT(NVARCHAR(50),GETDATE(),120));
END
GO

-- Deals de test (vendeur = IdUser 2)
IF NOT EXISTS (SELECT 1 FROM Deals) BEGIN
    INSERT INTO Deals (TitleDeal,DescriptionDeal,PriceDeal,Quantity,IdCateg,IdUser,Active,DatePublication,Brand,Colors)
    VALUES
    ('iPhone 15 Pro Max','Smartphone Apple 256Go, état neuf','1200 TND','5',1,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Apple','Noir'),
    ('Samsung Galaxy S24','Smartphone Samsung 128Go','900 TND','8',1,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Samsung','Bleu'),
    ('Laptop Dell XPS 15','Ordinateur portable i7 16Go RAM','2500 TND','3',1,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Dell','Gris'),
    ('Nike Air Max 2024','Chaussures de sport Nike taille 42','250 TND','15',8,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Nike','Blanc'),
    ('Canapé cuir 3 places','Canapé en cuir véritable marron','1800 TND','2',4,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Casa','Marron'),
    ('T-shirt Polo','Polo homme coton premium','45 TND','50',2,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Lacoste','Rouge'),
    ('Casque Sony WH-1000XM5','Casque sans fil avec réduction de bruit','480 TND','10',1,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Sony','Noir'),
    ('Pack café Arabica 1kg','Café Arabica premium torréfié','32 TND','100',3,2,1,CONVERT(NVARCHAR(50),GETDATE(),120),'Nespresso','N/A');
END
GO

SELECT 'Schema et données créés avec succès!' AS Message;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;
GO
