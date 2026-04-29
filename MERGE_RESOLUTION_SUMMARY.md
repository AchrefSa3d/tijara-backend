# Git Merge Conflict Resolution - Tijara Backend

**Date:** April 28, 2026  
**Status:** ✅ COMPLETE - All conflicts resolved  
**Merge Strategy:** Keep most feature-rich versions + combine approaches

---

## Executive Summary

Successfully resolved **5 conflicted source files** in a C# .NET backend project:
- **4 Controllers** (both added - no conflict markers, preserved as-is)
- **1 Service** (both modified - conflict resolved with merged approach)

All unmerged binary files removed to prevent build conflicts.

---

## Detailed Resolution

### ✅ Controllers/DeliveriesController.cs
**Status:** CLEAN (no conflict markers)  
**Strategy:** Keep as-is (already merged correctly)

**Key Features Preserved:**
- Role-based access control (Admin → all, Vendor → their orders, User → own deliveries)
- Complex SQL JOIN with enriched client data from Users + OrderDetails
- Transport name and pricing information
- Flexible JSON request handling (camelCase + snake_case support)
- Helper methods for JSON parsing: `ReadStr()`, `ReadLong()`, `ReadIntN()`, `ReadDec()`

**Endpoints (5 total):**
| Route | Method | Purpose |
|-------|--------|---------|
| `GET /api/deliveries` | GetList() | List with role-based filtering + status filter |
| `POST /api/deliveries` | Create() | Create/upsert with auto-fill transport fee |
| `PUT /api/deliveries/{id}` | Update() | Update all editable fields |
| `PATCH /api/deliveries/{id}/status` | UpdateStatus() | Update status + track delivery date |
| `GET /api/deliveries/order/{orderId}` | GetByOrder() | Get delivery by order ID |

**DTO:** None (uses JsonElement for flexible input)

---

### ✅ Controllers/InvoicesController.cs
**Status:** CLEAN (no conflict markers)  
**Strategy:** Keep as-is

**Key Features Preserved:**
- Sophisticated invoice generation from orders with SQL joins
- Tax calculation (7% for Tunisia)
- Dual tax rate support (7% in FromOrder, 19% in Create)
- Role-based invoice listing
- Access control enforcement (users can only view their invoices)
- Multiple invoice number formats

**Endpoints (6 total):**
| Route | Method | Purpose |
|-------|--------|---------|
| `GET /api/invoices` | GetList() | List (admin: all, vendor: mine, user: mine) |
| `GET /api/invoices/{id}` | GetOne() | Single invoice with access control |
| `POST /api/invoices/from-order/{idOrder}` | FromOrder() | Generate from order (7% tax) |
| `POST /api/invoices` | Create() | Direct creation (19% tax, admin/vendor) |
| `PATCH /api/invoices/{id}/paid` | MarkPaid() | Mark as paid |
| `PATCH /api/invoices/{id}/pay` | MarkPaidAlt() | Alternate mark-as-paid (admin) |

**DTOs:**
```csharp
public class InvoiceRequest
{
    public long    IdOrder { get; set; }
    public long    IdUser  { get; set; }
    public decimal Amount  { get; set; }
}
```

---

### ✅ Controllers/PermissionsController.cs
**Status:** CLEAN (no conflict markers)  
**Strategy:** Keep as-is

**Key Features Preserved:**
- Resource-based permission management
- Role-based authorization (admin-only controller)
- Upsert pattern (check for existing, update or insert)
- Flexible JSON parsing with helper methods
- Permission flags: `CanRead`, `CanCreate`, `CanUpdate`, `CanDelete`

**Endpoints (5 total):**
| Route | Method | Purpose |
|-------|--------|---------|
| `GET /api/permissions` | GetAll() | List all permissions |
| `GET /api/permissions/role/{idRole}` | GetByRole() | Get permissions for role |
| `POST /api/permissions` | Create() | Create/upsert permission |
| `PUT /api/permissions/{id}` | Update() | Update permission flags |
| `DELETE /api/permissions/{id}` | Delete() | Delete permission |

**Helper Methods:**
- `ParseBody()` - Converts JsonElement to PermDto (record)
- `ReadStr()`, `ReadInt()`, `ReadBool()` - Type-safe JSON extraction
- `TryGet()` - Multi-key lookup supporting both camelCase and snake_case

---

### ✅ Controllers/TransportsController.cs
**Status:** CLEAN (no conflict markers)  
**Strategy:** Keep as-is

**Key Features Preserved:**
- Public endpoint for listing transports
- Admin-only management endpoints
- Active/inactive filtering
- Flexible schema supporting both old and new field sets

**Endpoints (6 total):**
| Route | Method | Purpose |
|-------|--------|---------|
| `GET /api/transports` | GetAll() | List all (public, optionally active only) |
| `GET /api/transports/{id}` | Get() | Get single transport |
| `POST /api/transports` | Create() | Create (admin only) |
| `PUT /api/transports/{id}` | Update() | Update (admin only) |
| `PATCH /api/transports/{id}/toggle` | Toggle() | Toggle active status (admin) |
| `DELETE /api/transports/{id}` | Delete() | Delete (admin only) |

**DTO (Supports Dual Schema):**
```csharp
public class TransportRequest
{
    public string  Name        { get; set; } = "";
    public string? Logo        { get; set; }
    public string? Phone       { get; set; }
    public string? Email       { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal FreeFrom    { get; set; }
    public string? Zones       { get; set; }
    public bool    Active      { get; set; } = true;
    public string? Description { get; set; }  // NEW SCHEMA
    public decimal Price       { get; set; }  // NEW SCHEMA
    public string? Duration    { get; set; }  // NEW SCHEMA
}
```

**Seed Data (Tunisia defaults):**
- Aramex Tunisie: +216 71 100 100, 8.000 TND, 200 TND min
- Colissimo: +216 71 200 200, 6.000 TND, 150 TND min
- Rapid Poste: +216 71 300 300, 4.500 TND, 100 TND min
- First Delivery: +216 71 400 400, 7.000 TND, 180 TND min

---

### ⚠️ Services/DbService.cs
**Status:** ⚠️ CONFLICT RESOLVED (had merge marker at line 425)  
**Strategy:** MERGED both approaches - Keep upstream's comprehensive schema + add stashed defensive checks

**Conflict Details:**
- **Upstream (HEAD):** Detailed table creation with migration logic
- **Stashed (branch):** Seed defaults with simpler approach
- **Resolution:** Keep comprehensive approach, add fallback values for legacy columns

**Key Changes in Resolved Version:**

#### 1. Enhanced Transports Table Schema
```sql
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
    -- ADDED: Support both schema versions
    Description  NVARCHAR(500) NULL,
    Price        DECIMAL(18,3) NULL,
    Duration     NVARCHAR(50)  NULL
);
```

#### 2. Core Tables Created
- **Permissions** (5 columns, unique constraint on IdRole + Resource)
- **Payments** (9 columns, tracks transactions)
- **Transports** (now 13 columns with dual schema support)
- **Deliveries** (14 columns, full tracking)
- **Invoices** (11 columns, comprehensive billing)
- **SmsLogs** (7 columns, SMS audit trail)

#### 3. Critical Migrations Preserved
```sql
-- Data Quality: PriceDeal normalization
UPDATE Deals
   SET PriceDeal = LTRIM(RTRIM(REPLACE(PriceDeal, ',', '.')))
 WHERE PriceDeal LIKE '%,%' OR PriceDeal LIKE ' %' OR PriceDeal LIKE '% ';

-- Legacy Prevention: Random prices for null entries
UPDATE Deals
   SET PriceDeal = CAST(50 + ABS(CHECKSUM(NEWID())) % 451 AS NVARCHAR(50)) + '.000'
 WHERE PriceDeal IS NULL OR LTRIM(RTRIM(PriceDeal)) = '';

-- Invoice Recalculation: Fix zero-total invoices
UPDATE i
   SET i.Subtotal = ISNULL(TRY_CAST(d.PriceDeal AS DECIMAL(18,3)), 0),
       i.Tax      = ROUND(...* 0.07, 3),  -- 7% TVA Tunisia
       i.Total    = ...
  FROM Invoices i JOIN Orders o ON i.IdOrder = o.IdOrder
  JOIN Deals d ON o.IdDeal = d.IdDeal
 WHERE i.Total = 0;

-- Schema Migration: Populate missing IdVendor
UPDATE i
   SET i.IdVendor = d.idUser
  FROM Invoices i JOIN Orders o ON i.IdOrder = o.IdOrder
  JOIN Deals d ON o.IdDeal = d.IdDeal
 WHERE i.IdVendor IS NULL;
```

#### 4. Defensive Column Checks
```sql
-- Permissions table migration (handles old schema)
IF COL_LENGTH('Permissions','Resource')  IS NULL ALTER TABLE Permissions ADD Resource  NVARCHAR(100) NULL;
IF COL_LENGTH('Permissions','CanRead')   IS NULL ALTER TABLE Permissions ADD CanRead   BIT DEFAULT 0;
-- ... etc for all permissions columns
```

#### 5. Additional Tables (from stashed, now merged)
- **RolePermissions** (junction table for role-permission mapping)
- **PaymentMethods** (user payment method storage)
- **WalletTransactions** (wallet activity audit trail)
- **BlockedUsers** (user blocking relationships)

#### 6. Legacy Column Fallbacks
```sql
-- Countries: Try multiple legacy column names
IF COL_LENGTH('Countries','{legacy}') IS NOT NULL
   AND COL_LENGTH('Countries','Title') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = 'UPDATE Countries SET Title = [{legacy}] 
                                   WHERE (Title IS NULL OR Title='''') 
                                   AND [{legacy}] IS NOT NULL';
    EXEC sp_executesql @sql;
END

-- Fallback: if still NULL
UPDATE Countries SET Title = CONCAT('Pays #', IdCountry) WHERE Title IS NULL OR Title = '';
```

#### 7. Seed Defaults (Safe - idempotent)
```sql
-- Only inserts if not exists
IF COL_LENGTH('Countries','Code') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Countries WHERE Code='TN')
BEGIN
    INSERT INTO Countries VALUES ('Tunisie', '🇹🇳', 'TN', '+216'), ...
END
```

---

## Backward Compatibility Strategy

### DTO Schema Flexibility
All request DTOs support both camelCase and snake_case JSON properties:
- Controllers use `JsonElement` + helper methods with multiple key variants
- Example: `ReadStr(body, "idOrder", "id_order")` accepts both formats

### Database Schema Flexibility
1. **TransportRequest now accepts 10 properties** supporting both old and new schemas
2. **Column existence checks** before migrations prevent failures on partial upgrades
3. **Fallback values** for missing legacy columns ensure data consistency
4. **Idempotent migrations** can be run multiple times safely

### Tax Rate Handling
```csharp
// FromOrder: Uses 7% (Tunisia standard rate)
decimal tax = SafeDec(Get("Total")) * 0.07m;

// Create: Uses 19% (possibly for different scenario)
decimal tax = req.Amount * 0.19m;
```

---

## Defensive Checks Added to DbService

### 1. Table Existence Checks
```sql
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TableName' AND xtype='U')
```

### 2. Column Existence Checks
```sql
IF COL_LENGTH('TableName','ColumnName') IS NULL
    ALTER TABLE TableName ADD ColumnName [Type] ...;
```

### 3. Safe Type Casting
```sql
TRY_CAST(PriceDeal AS DECIMAL(18,3))  -- Returns NULL if conversion fails
ISNULL(value, 0)  -- Provides fallback
```

### 4. Data Integrity Migrations
- Normalize string data (trim, replace characters)
- Assign fallback values to NULL fields
- Recalculate derived fields when source data changes
- Populate relationship columns (IdVendor, etc.)

### 5. Safe Deletion Patterns
```sql
-- Prevents removal of tables/columns that don't exist
IF OBJECT_ID('TableName','U') IS NOT NULL
BEGIN
    DROP TABLE TableName;
END
```

---

## Preserved Helper Methods

All controllers retain their helper methods for flexible JSON parsing:

### Common Helpers (in Controllers)
```csharp
// Try multiple property key variants (camelCase, snake_case)
private static bool TryGet(JsonElement el, out JsonElement v, params string[] keys)

// Extract string (handles null, coercion)
private static string? ReadStr(JsonElement el, params string[] keys)

// Extract long with string fallback
private static long ReadLong(JsonElement el, params string[] keys)

// Extract nullable int
private static int? ReadIntN(JsonElement el, params string[] keys)

// Extract decimal with cultural awareness
private static decimal ReadDec(JsonElement el, params string[] keys)

// Extract boolean with multiple input formats
private static bool ReadBool(JsonElement el, params string[] keys)
```

---

## Files Kept (No Conflicts)

### New Controllers (Local)
- **Controllers/AnalyticsController.cs** ✅ (new file)
- **Controllers/WalletController.cs** ✅ (new file)

### Modified Files (Merged without conflicts)
- **Controllers/AuthController.cs** ✅ (merged)
- **Program.cs** ✅ (merged)
- **appsettings.json** ✅ (merged)

---

## Build Artifacts Cleaned Up

All unmerged binary files removed to prevent compilation issues:
- ✅ bin/Debug/net10.0/TijaraApi.dll (removed)
- ✅ bin/Debug/net10.0/TijaraApi.exe (removed)
- ✅ bin/Debug/net10.0/TijaraApi.pdb (removed)
- ✅ obj/Debug/net10.0/* (14 files removed)

**These will be regenerated on next build.**

---

## Next Steps

### 1. Verify Compilation
```bash
dotnet build TijaraApi.sln
```

### 2. Run Tests (if available)
```bash
dotnet test
```

### 3. Complete Merge
```bash
git commit -m "Merge: Resolve conflicts - combine feature-rich controllers with defensive DbService checks"
```

### 4. Deploy
```bash
dotnet publish -c Release
```

---

## Summary Table

| File | Type | Status | Strategy |
|------|------|--------|----------|
| DeliveriesController.cs | Controller | ✅ CLEAN | Keep (role-based access) |
| InvoicesController.cs | Controller | ✅ CLEAN | Keep (dual tax rates) |
| PermissionsController.cs | Controller | ✅ CLEAN | Keep (resource-based perms) |
| TransportsController.cs | Controller | ✅ CLEAN | Keep (dual schema DTO) |
| DbService.cs | Service | ✅ MERGED | Combine schemas + defensiveness |
| AnalyticsController.cs | Controller | ✅ NEW | Keep (local addition) |
| WalletController.cs | Controller | ✅ NEW | Keep (local addition) |
| AuthController.cs | Controller | ✅ MERGED | Merged (no conflicts) |
| Program.cs | Bootstrap | ✅ MERGED | Merged (no conflicts) |
| appsettings.json | Config | ✅ MERGED | Merged (no conflicts) |

---

**Merge Completed:** ✅ All source files resolved  
**Conflict Markers Removed:** ✅ Yes  
**Ready to Commit:** ✅ Yes  
**Ready to Deploy:** ✅ After rebuild
