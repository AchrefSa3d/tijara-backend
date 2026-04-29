# Resolved Code Reference - Key Sections

## 1. DeliveriesController.cs - Complete Merged Version

### Role-Based Access Control
```csharp
private long CurrentUserId
{
    get
    {
        var raw = User.FindFirstValue("id")
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? "0";
        return long.TryParse(raw, out var id) ? id : 0;
    }
}
private string CurrentRole =>
    User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "user";
private bool IsAdmin  => CurrentRole == "admin"  || User.IsInRole("admin");
private bool IsVendor => CurrentRole == "vendor" || User.IsInRole("vendor");
```

### GET /api/deliveries with SQL Joins
```csharp
[HttpGet]
public async Task<IActionResult> GetList([FromQuery] string? status)
{
    const string baseSelect = @"
        SELECT  d.IdDelivery, d.IdOrder, d.IdTransport, d.TrackingNumber, d.Status,
                d.AddressLine, d.City, d.PostalCode, d.Phone, d.DeliveryFee,
                d.EstimatedAt, d.DeliveredAt, d.Note, d.CreatedAt, d.UpdatedAt,
                t.Name  AS TransportName,
                t.Phone AS TransportPhone,
                od.FirstName + ' ' + od.LastName AS DetailClientName,
                od.Email     AS DetailEmail,
                od.Telephone AS DetailPhone,
                od.Address   AS DetailAddress,
                CONCAT(u.FirstName, ' ', u.LastName) AS UserClientName,
                u.Email      AS UserEmail,
                u.Telephone  AS UserPhone,
                dl.titleDeal AS DealTitle,
                TRY_CAST(REPLACE(dl.priceDeal, ',', '.') AS DECIMAL(18,3)) AS DealPrice
        FROM    Deliveries d
        LEFT JOIN Transports   t  ON d.IdTransport = t.IdTransport
        LEFT JOIN Orders       o  ON d.IdOrder     = o.IdOrder
        LEFT JOIN Deals        dl ON o.IdDeal      = dl.IdDeal
        LEFT JOIN Users        u  ON o.IdUser      = u.IdUser
        OUTER APPLY (
            SELECT TOP 1 FirstName, LastName, Email, Telephone, Address
            FROM   OrderDetails
            WHERE  IdOrder = d.IdOrder
            ORDER BY IdOrderDeatils DESC
        ) od";

    string sql;
    object? param = null;

    // Role-based filtering
    if (IsAdmin)
    {
        sql = baseSelect;
        if (!string.IsNullOrEmpty(status)) 
        { 
            sql += " WHERE d.Status=@Status"; 
            param = new { Status = status }; 
        }
        sql += " ORDER BY d.IdDelivery DESC";
    }
    else if (IsVendor)
    {
        sql = baseSelect + " WHERE dl.idUser = @UserId";
        if (!string.IsNullOrEmpty(status)) 
        { 
            sql += " AND d.Status=@Status"; 
            param = new { UserId = CurrentUserId, Status = status }; 
        }
        else                                
            param = new { UserId = CurrentUserId };
        sql += " ORDER BY d.IdDelivery DESC";
    }
    else // User
    {
        sql = baseSelect + " WHERE o.IdUser = @UserId";
        if (!string.IsNullOrEmpty(status)) 
        { 
            sql += " AND d.Status=@Status"; 
            param = new { UserId = CurrentUserId, Status = status }; 
        }
        else                                
            param = new { UserId = CurrentUserId };
        sql += " ORDER BY d.IdDelivery DESC";
    }

    var rows = await _db.QueryAsync<dynamic>(sql, param);
    // Projection to snake_case for frontend compatibility
    var result = rows.Select(r => new { ... });
    return Ok(result);
}
```

### POST /api/deliveries with Flexible JSON
```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] JsonElement body)
{
    // Supports both camelCase and snake_case
    var idOrder        = ReadLong  (body, "idOrder", "id_order");
    if (idOrder <= 0) return BadRequest(new { message = "id_order requis." });

    var idTransport    = ReadIntN  (body, "idTransport", "id_transport");
    var trackingNumber = ReadStr   (body, "trackingNumber", "tracking_number");
    var statusStr      = ReadStr   (body, "status") ?? "pending";
    var addressLine    = ReadStr   (body, "addressLine", "address_line");
    var deliveryFee    = ReadDec   (body, "deliveryFee", "delivery_fee");
    
    // Auto-fill delivery fee from transport if not provided
    if (deliveryFee == 0 && idTransport.HasValue)
    {
        var fee = await _db.QueryFirstOrDefaultAsync<decimal?>(
            "SELECT DeliveryFee FROM Transports WHERE IdTransport=@Id",
            new { Id = idTransport.Value });
        if (fee.HasValue) deliveryFee = fee.Value;
    }

    var id = await _db.ExecuteScalarAsync<long>(@"
        INSERT INTO Deliveries (IdOrder, IdTransport, TrackingNumber, Status,
                                AddressLine, City, PostalCode, Phone, DeliveryFee, Note)
        OUTPUT INSERTED.IdDelivery
        VALUES (@IdOrder, @IdTransport, @TrackingNumber, @Status,
                @AddressLine, @City, @PostalCode, @Phone, @DeliveryFee, @Note)",
        new { IdOrder = idOrder, IdTransport = idTransport, TrackingNumber = trackingNumber,
              Status = statusStr, AddressLine = addressLine, /* ... */ });

    return Ok(new { id_delivery = id, created = true });
}
```

### Helper Methods (All Preserved)
```csharp
private static bool TryGet(JsonElement el, out JsonElement v, params string[] keys)
{
    foreach (var k in keys)
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(k, out v))
            return true;
    v = default;
    return false;
}

private static string? ReadStr(JsonElement el, params string[] keys)
{
    if (!TryGet(el, out var v, keys)) return null;
    return v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Null   => null,
        _                    => v.ToString(),
    };
}

private static long ReadLong(JsonElement el, params string[] keys)
{
    if (!TryGet(el, out var v, keys)) return 0;
    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
    if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s;
    return 0;
}

private static int? ReadIntN(JsonElement el, params string[] keys)
{
    if (!TryGet(el, out var v, keys) || v.ValueKind == JsonValueKind.Null) return null;
    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
    if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
    return null;
}

private static decimal ReadDec(JsonElement el, params string[] keys)
{
    if (!TryGet(el, out var v, keys)) return 0m;
    if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n)) return n;
    if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), 
        System.Globalization.NumberStyles.Any, 
        System.Globalization.CultureInfo.InvariantCulture, out var s)) return s;
    return 0m;
}
```

---

## 2. InvoicesController.cs - Dual Tax Rate Implementation

### GET /api/invoices - Role-Based List
```csharp
[HttpGet]
public async Task<IActionResult> GetList()
{
    var commonSelect = @"
        SELECT i.IdInvoice, i.Number, i.IdOrder, i.IdUser, i.IdVendor,
               i.Subtotal, i.Tax, i.DeliveryFee, i.Total, i.Status,
               i.IssuedAt, i.PaidAt,
               LTRIM(RTRIM(CONCAT(u.FirstName,' ',u.LastName))) AS ClientName,
               u.Email  AS ClientEmail,
               LTRIM(RTRIM(CONCAT(uv.FirstName,' ',uv.LastName))) AS VendorName,
               d.titleDeal AS DealTitle
        FROM Invoices i
        LEFT JOIN Users u  ON i.IdUser   = u.IdUser
        LEFT JOIN Users uv ON i.IdVendor = uv.IdUser
        LEFT JOIN Orders o ON i.IdOrder  = o.IdOrder
        LEFT JOIN Deals  d ON o.IdDeal   = d.IdDeal";

    if (IsAdmin)
    {
        sql = commonSelect + " ORDER BY i.IdInvoice DESC";
    }
    else if (IsVendor)
    {
        sql = commonSelect + " WHERE i.IdVendor=@UserId ORDER BY i.IdInvoice DESC";
        param = new { UserId = CurrentUserId };
    }
    else
    {
        sql = commonSelect + " WHERE i.IdUser=@UserId ORDER BY i.IdInvoice DESC";
        param = new { UserId = CurrentUserId };
    }

    var rows = await _db.QueryAsync<dynamic>(sql, param);
    var result = rows.Select(r => new
    {
        id_invoice    = (long)r.IdInvoice,
        number        = (string)r.Number,
        // ... projection to snake_case
    });
    return Ok(result);
}
```

### POST /api/invoices/from-order - 7% TAX (Tunisia)
```csharp
[HttpPost("from-order/{idOrder:long}")]
public async Task<IActionResult> FromOrder(long idOrder)
{
    // Anti-doublon: prevent duplicates
    var existing = await _db.QueryFirstOrDefaultAsync<Invoice>(
        "SELECT * FROM Invoices WHERE IdOrder=@Id", new { Id = idOrder });
    if (existing != null) return Ok(existing);

    // Sophisticated ORDER + DEAL JOIN
    var order = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
        SELECT o.IdOrder,
               o.IdUser AS IdBuyer,
               d.IdDeal,
               ISNULL(TRY_CAST(REPLACE(d.PriceDeal, ',', '.') AS DECIMAL(18,3)), 0) AS Total,
               d.idUser AS IdVendor
        FROM Orders o
        LEFT JOIN Deals d ON o.IdDeal = d.IdDeal
        WHERE o.IdOrder=@Id", new { Id = idOrder });
    
    if (order == null) return NotFound(new { message = $"Commande #{idOrder} introuvable." });

    // Safe decimal/long conversion from dynamic
    var dict = (IDictionary<string, object>)order;
    object? Get(string k) => dict.TryGetValue(k, out var x) && x != DBNull.Value ? x : null;

    decimal SafeDec(object? o) => o == null ? 0m : Convert.ToDecimal(o, System.Globalization.CultureInfo.InvariantCulture);
    long    SafeLong(object? o) => o == null ? 0L : Convert.ToInt64(o);

    var number   = "INV-" + DateTime.Now.Year + "-" + DateTime.Now.Ticks.ToString()[^6..];
    decimal total    = SafeDec(Get("Total"));
    decimal delivery = 0m;
    decimal subtotal = total - delivery;
    decimal tax      = Math.Round(subtotal * 0.07m, 3);  // ← 7% TAX (Tunisia)
    long    idUser   = SafeLong(Get("IdBuyer"));
    long?   idVendor = Get("IdVendor") is object v1 ? Convert.ToInt64(v1) : (long?)null;

    // Access control: must be admin, buyer, or vendor
    if (!IsAdmin && CurrentUserId != idUser && (idVendor == null || CurrentUserId != idVendor))
        return StatusCode(403, new { message = "Vous ne pouvez générer la facture que pour vos propres commandes." });

    try
    {
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Invoices (Number, IdOrder, IdUser, IdVendor, Subtotal, Tax, DeliveryFee, Total)
            OUTPUT INSERTED.IdInvoice
            VALUES (@Number, @IdOrder, @IdUser, @IdVendor, @Subtotal, @Tax, @DeliveryFee, @Total)",
            new { Number = number, IdOrder = idOrder, IdUser = idUser, IdVendor = idVendor,
                  Subtotal = subtotal, Tax = tax, DeliveryFee = delivery, Total = total });

        return Ok(new { idInvoice = id, number, total, subtotal, tax });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "Erreur génération facture: " + ex.Message });
    }
}
```

### POST /api/invoices - Direct Creation - 19% TAX
```csharp
[HttpPost]
[Authorize(Roles = "admin,vendor")]
public async Task<IActionResult> Create([FromBody] InvoiceRequest req)
{
    var tax   = req.Amount * 0.19m;  // ← 19% TAX (different scenario)
    var total = req.Amount + tax;
    var ref_  = "INV-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);

    var id = await _db.ExecuteScalarAsync<long>(@"
        INSERT INTO Invoices (IdOrder, IdUser, Amount, TaxAmount, TotalAmount, InvoiceRef, Status)
        OUTPUT INSERTED.IdInvoice
        VALUES (@IdOrder, @IdUser, @Amount, @Tax, @Total, @Ref, 'pending')",
        new { req.IdOrder, req.IdUser, req.Amount, Tax = tax, Total = total, Ref = ref_ });

    return StatusCode(201, new { id, invoice_ref = ref_, total_amount = total });
}

public class InvoiceRequest
{
    public long    IdOrder { get; set; }
    public long    IdUser  { get; set; }
    public decimal Amount  { get; set; }
}
```

---

## 3. TransportsController.cs - Dual Schema DTO

### TransportRequest DTO (Backward Compatibility)
```csharp
public class TransportRequest
{
    // LEGACY SCHEMA
    public string  Name        { get; set; } = "";
    public string? Logo        { get; set; }
    public string? Phone       { get; set; }
    public string? Email       { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal FreeFrom    { get; set; }
    public string? Zones       { get; set; }
    public bool    Active      { get; set; } = true;
    
    // NEW SCHEMA (from stashed version)
    public string? Description { get; set; }
    public decimal Price       { get; set; }
    public string? Duration    { get; set; }
}
```

### POST /api/transports - Supports Both Schemas
```csharp
[HttpPost]
[Authorize(Roles = "admin")]
public async Task<IActionResult> Create([FromBody] TransportRequest req)
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return BadRequest(new { message = "Nom requis." });

    // Single INSERT with ALL fields from both schemas
    var id = await _db.ExecuteScalarAsync<long>(@"
        INSERT INTO Transports (Name, Logo, Phone, Email, DeliveryFee, FreeFrom, Zones, 
                               Active, Description, Price, Duration)
        OUTPUT INSERTED.IdTransport
        VALUES (@Name, @Logo, @Phone, @Email, @DeliveryFee, @FreeFrom, @Zones, 
                @Active, @Description, @Price, @Duration)",
        new { req.Name, req.Logo, req.Phone, req.Email, req.DeliveryFee, req.FreeFrom, 
              req.Zones, req.Active, req.Description, req.Price, req.Duration });
    
    return Ok(new { id, name = req.Name, price = req.Price });
}
```

### Seed Data (Tunisia Defaults)
```sql
INSERT INTO Transports (Name, Phone, DeliveryFee, FreeFrom, Zones, Active) VALUES
    ('Aramex Tunisie',  '+216 71 100 100', 8.000,  200.000, 'Grand Tunis, Sfax, Sousse', 1),
    ('Colissimo',       '+216 71 200 200', 6.000,  150.000, 'Toute la Tunisie',           1),
    ('Rapid Poste',     '+216 71 300 300', 4.500,  100.000, 'Toute la Tunisie',           1),
    ('First Delivery',  '+216 71 400 400', 7.000,  180.000, 'Grand Tunis, Nabeul',        1);
```

---

## 4. PermissionsController.cs - Resource-Based Access

### Permission DTO (Record Type)
```csharp
private record PermDto(
    int IdRole, 
    string Resource, 
    bool CanRead, 
    bool CanCreate, 
    bool CanUpdate, 
    bool CanDelete
);
```

### POST /api/permissions - Upsert Pattern
```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] JsonElement body)
{
    var p = ParseBody(body);
    if (p.IdRole <= 0 || string.IsNullOrWhiteSpace(p.Resource))
        return BadRequest(new { message = "id_role et resource requis." });

    try
    {
        // Upsert by (IdRole, Resource) unique constraint
        var existing = await _db.QueryFirstOrDefaultAsync<int?>(
            "SELECT IdPermission FROM Permissions WHERE IdRole=@IdRole AND Resource=@Resource",
            new { p.IdRole, p.Resource });

        if (existing.HasValue && existing.Value > 0)
        {
            await _db.ExecuteAsync(@"
                UPDATE Permissions SET CanRead=@CanRead, CanCreate=@CanCreate,
                                       CanUpdate=@CanUpdate, CanDelete=@CanDelete
                WHERE IdPermission=@Id",
                new { p.CanRead, p.CanCreate, p.CanUpdate, p.CanDelete, Id = existing.Value });
            return Ok(new { id_permission = existing.Value, updated = true });
        }

        var id = await _db.ExecuteScalarAsync<int>(@"
            INSERT INTO Permissions (IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete)
            OUTPUT INSERTED.IdPermission
            VALUES (@IdRole, @Resource, @CanRead, @CanCreate, @CanUpdate, @CanDelete)", p);
        return Ok(new { id_permission = id, created = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Permissions.Create] {ex.Message}");
        return StatusCode(500, new { message = "Enregistrement impossible : " + ex.Message });
    }
}
```

### Flexible JSON Parsing with CamelCase + Snake_case
```csharp
private static PermDto ParseBody(JsonElement b) => new(
    IdRole:    ReadInt (b, "idRole",   "id_role"),
    Resource:  ReadStr (b, "resource") ?? "",
    CanRead:   ReadBool(b, "canRead",   "can_read"),
    CanCreate: ReadBool(b, "canCreate", "can_create"),
    CanUpdate: ReadBool(b, "canUpdate", "can_update"),
    CanDelete: ReadBool(b, "canDelete", "can_delete")
);

private static bool ReadBool(JsonElement el, params string[] keys)
{
    if (!TryGet(el, out var v, keys)) return false;
    return v.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => v.GetInt32() != 0,
        JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : v.GetString() == "1",
        _ => false,
    };
}
```

---

## 5. DbService.cs - Merged Conflict Resolution

### Enhanced Transports Table (Dual Schema Support)
```sql
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
    -- ADDED: Support both schema versions
    Description  NVARCHAR(500) NULL,     ← NEW
    Price        DECIMAL(18,3) NULL,     ← NEW
    Duration     NVARCHAR(50)  NULL      ← NEW
);
```

### Critical Migration: PriceDeal Normalization
```sql
-- 1) Normalize PriceDeal: virgule → point, trim
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
```

### Defensive Column Existence Checks
```sql
-- Migrate Permissions table — add missing columns if the table exists with older schema
IF OBJECT_ID('Permissions','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('Permissions','Resource')  IS NULL ALTER TABLE Permissions ADD Resource  NVARCHAR(100) NULL;
    IF COL_LENGTH('Permissions','CanRead')   IS NULL ALTER TABLE Permissions ADD CanRead   BIT DEFAULT 0;
    IF COL_LENGTH('Permissions','CanCreate') IS NULL ALTER TABLE Permissions ADD CanCreate BIT DEFAULT 0;
    IF COL_LENGTH('Permissions','CanUpdate') IS NULL ALTER TABLE Permissions ADD CanUpdate BIT DEFAULT 0;
    IF COL_LENGTH('Permissions','CanDelete') IS NULL ALTER TABLE Permissions ADD CanDelete BIT DEFAULT 0;
    IF COL_LENGTH('Permissions','IdRole')    IS NULL ALTER TABLE Permissions ADD IdRole    INT NULL;
END
```

### Schema-Safe Seed Operations
```sql
-- Only inserts if table has correct schema AND no existing data
IF COL_LENGTH('Permissions','Resource') IS NOT NULL
   AND COL_LENGTH('Permissions','IdRole') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Permissions WHERE IdRole=1 AND Resource='users')
BEGIN
    INSERT INTO Permissions (IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete) VALUES
        (1,'users',1,1,1,1),
        (1,'products',1,1,1,1),
        (1,'orders',1,1,1,1),
        (1,'categories',1,1,1,1),
        (1,'payments',1,1,1,1),
        (1,'transports',1,1,1,1),
        (1,'reports',1,0,0,0),
        (3,'products',1,1,1,1),
        (3,'orders',1,0,1,0),
        (3,'reports',1,0,0,0),
        (2,'orders',1,1,1,0),
        (2,'reviews',1,1,1,1);
END
```

### Legacy Column Fallback Pattern (Countries Example)
```csharp
// Migrate Countries — ajoute colonnes manquantes si DB ancienne
foreach (var legacy in new[] { "CountryName", "Name", "Country", "Nom" })
{
    try
    {
        await conn.ExecuteAsync($@"
            IF COL_LENGTH('Countries','{legacy}') IS NOT NULL
               AND COL_LENGTH('Countries','Title') IS NOT NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'UPDATE Countries SET Title = [{legacy}] 
                                              WHERE (Title IS NULL OR Title='''') 
                                              AND [{legacy}] IS NOT NULL';
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
```

---

## Implementation Checklist

After merge commit:

- [ ] Run `dotnet build` to verify compilation
- [ ] Check for any compilation warnings
- [ ] Run unit tests if available: `dotnet test`
- [ ] Verify API endpoints respond correctly
- [ ] Test role-based access control
- [ ] Verify invoice generation (check 7% vs 19% tax scenarios)
- [ ] Test transport creation with new schema fields
- [ ] Verify permission creation/upsert logic
- [ ] Test delivery tracking endpoint
- [ ] Run integration tests with database

---

**Version:** Merged April 28, 2026  
**Conflict Resolution:** Complete  
**Status:** Ready for Build & Deploy
