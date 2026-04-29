using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly DbService _db;
    public DeliveriesController(DbService db) => _db = db;

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

    // ─────────────────────────────────────────────────────────────
    // GET /api/deliveries — admin: all, vendor: deliveries of my orders, user: mine
    // Enriched with client info (from Users + OrderDetails) + transport name
    // ─────────────────────────────────────────────────────────────
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

        if (IsAdmin)
        {
            sql = baseSelect;
            if (!string.IsNullOrEmpty(status)) { sql += " WHERE d.Status=@Status"; param = new { Status = status }; }
            sql += " ORDER BY d.IdDelivery DESC";
        }
        else if (IsVendor)
        {
            sql = baseSelect + " WHERE dl.idUser = @UserId";
            if (!string.IsNullOrEmpty(status)) { sql += " AND d.Status=@Status"; param = new { UserId = CurrentUserId, Status = status }; }
            else                                param = new { UserId = CurrentUserId };
            sql += " ORDER BY d.IdDelivery DESC";
        }
        else
        {
            sql = baseSelect + " WHERE o.IdUser = @UserId";
            if (!string.IsNullOrEmpty(status)) { sql += " AND d.Status=@Status"; param = new { UserId = CurrentUserId, Status = status }; }
            else                                param = new { UserId = CurrentUserId };
            sql += " ORDER BY d.IdDelivery DESC";
        }

        var rows = await _db.QueryAsync<dynamic>(sql, param);

        // Project to a clean snake_case-friendly shape
        var result = rows.Select(r => new
        {
            id_delivery     = (long)r.IdDelivery,
            id_order        = (long)r.IdOrder,
            id_transport    = (int?)r.IdTransport,
            transport_name  = (string?)r.TransportName,
            transport_phone = (string?)r.TransportPhone,
            tracking_number = (string?)r.TrackingNumber,
            status          = (string)r.Status,
            address_line    = (string?)r.AddressLine ?? (string?)r.DetailAddress,
            city            = (string?)r.City,
            postal_code     = (string?)r.PostalCode,
            phone           = (string?)r.Phone ?? (string?)r.DetailPhone ?? (string?)r.UserPhone,
            client_name     = !string.IsNullOrWhiteSpace((string?)r.DetailClientName) ? (string?)r.DetailClientName : (string?)r.UserClientName,
            client_email    = (string?)r.DetailEmail ?? (string?)r.UserEmail,
            deal_title      = (string?)r.DealTitle,
            deal_price      = (decimal?)r.DealPrice ?? 0m,
            delivery_fee    = (decimal)r.DeliveryFee,
            estimated_at    = (DateTime?)r.EstimatedAt,
            delivered_at    = (DateTime?)r.DeliveredAt,
            note            = (string?)r.Note,
            created_at      = (DateTime?)r.CreatedAt,
            updated_at      = (DateTime?)r.UpdatedAt,
        });

        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/deliveries — flexible body (camelCase or snake_case)
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body)
    {
        var idOrder        = ReadLong  (body, "idOrder", "id_order");
        if (idOrder <= 0) return BadRequest(new { message = "id_order requis." });

        var idTransport    = ReadIntN  (body, "idTransport", "id_transport");
        var trackingNumber = ReadStr   (body, "trackingNumber", "tracking_number");
        var statusStr      = ReadStr   (body, "status") ?? "pending";
        var addressLine    = ReadStr   (body, "addressLine", "address_line");
        var city           = ReadStr   (body, "city");
        var postalCode     = ReadStr   (body, "postalCode", "postal_code");
        var phone          = ReadStr   (body, "phone");
        var deliveryFee    = ReadDec   (body, "deliveryFee", "delivery_fee");
        var note           = ReadStr   (body, "note");

        // If vendor, ensure the order belongs to him
        if (IsVendor)
        {
            var owns = await _db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Orders o JOIN Deals d ON o.IdDeal=d.IdDeal
                  WHERE o.IdOrder=@Id AND d.idUser=@U",
                new { Id = idOrder, U = CurrentUserId });
            if (owns == 0) return StatusCode(403, new { message = "Commande non autorisée." });
        }

        // If a delivery already exists for this order → update it instead
        var existing = await _db.QueryFirstOrDefaultAsync<long?>(
            "SELECT IdDelivery FROM Deliveries WHERE IdOrder=@Id", new { Id = idOrder });

        try
        {
            if (existing.HasValue && existing.Value > 0)
            {
                await _db.ExecuteAsync(@"
                    UPDATE Deliveries SET
                        IdTransport    = COALESCE(@IdTransport, IdTransport),
                        TrackingNumber = COALESCE(@TrackingNumber, TrackingNumber),
                        Status         = @Status,
                        AddressLine    = COALESCE(@AddressLine, AddressLine),
                        City           = COALESCE(@City, City),
                        PostalCode     = COALESCE(@PostalCode, PostalCode),
                        Phone          = COALESCE(@Phone, Phone),
                        DeliveryFee    = CASE WHEN @DeliveryFee > 0 THEN @DeliveryFee ELSE DeliveryFee END,
                        Note           = COALESCE(@Note, Note),
                        UpdatedAt      = GETDATE()
                    WHERE IdDelivery=@Id",
                    new { IdTransport = idTransport, TrackingNumber = trackingNumber, Status = statusStr,
                          AddressLine = addressLine, City = city, PostalCode = postalCode, Phone = phone,
                          DeliveryFee = deliveryFee, Note = note, Id = existing.Value });
                return Ok(new { id_delivery = existing.Value, updated = true });
            }

            // Auto-fill DeliveryFee from transport if not provided
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
                      Status = statusStr, AddressLine = addressLine, City = city, PostalCode = postalCode,
                      Phone = phone, DeliveryFee = deliveryFee, Note = note });

            return Ok(new { id_delivery = id, created = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deliveries.Create] {ex.Message}");
            return StatusCode(500, new { message = "Création de la livraison impossible : " + ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PUT /api/deliveries/:id — update all editable fields
    // ─────────────────────────────────────────────────────────────
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] JsonElement body)
    {
        if (IsVendor)
        {
            var owns = await _db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Deliveries dv
                  JOIN Orders o ON dv.IdOrder=o.IdOrder
                  JOIN Deals  d ON o.IdDeal=d.IdDeal
                  WHERE dv.IdDelivery=@Id AND d.idUser=@U",
                new { Id = id, U = CurrentUserId });
            if (owns == 0) return StatusCode(403, new { message = "Livraison non autorisée." });
        }

        var idTransport    = ReadIntN(body, "idTransport", "id_transport");
        var trackingNumber = ReadStr (body, "trackingNumber", "tracking_number");
        var addressLine    = ReadStr (body, "addressLine", "address_line");
        var city           = ReadStr (body, "city");
        var postalCode     = ReadStr (body, "postalCode", "postal_code");
        var phone          = ReadStr (body, "phone");
        var deliveryFee    = ReadDec (body, "deliveryFee", "delivery_fee");
        var note           = ReadStr (body, "note");

        try
        {
            await _db.ExecuteAsync(@"
                UPDATE Deliveries SET
                    IdTransport    = @IdTransport,
                    TrackingNumber = @TrackingNumber,
                    AddressLine    = @AddressLine,
                    City           = @City,
                    PostalCode     = @PostalCode,
                    Phone          = @Phone,
                    DeliveryFee    = @DeliveryFee,
                    Note           = @Note,
                    UpdatedAt      = GETDATE()
                WHERE IdDelivery=@Id",
                new { IdTransport = idTransport, TrackingNumber = trackingNumber,
                      AddressLine = addressLine, City = city, PostalCode = postalCode,
                      Phone = phone, DeliveryFee = deliveryFee, Note = note, Id = id });
            return Ok(new { id_delivery = id, updated = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deliveries.Update] {ex.Message}");
            return StatusCode(500, new { message = "Mise à jour impossible : " + ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PATCH /api/deliveries/:id/status
    // ─────────────────────────────────────────────────────────────
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] JsonElement body)
    {
        var status = ReadStr(body, "status");
        if (string.IsNullOrEmpty(status))
            return BadRequest(new { message = "status requis." });

        if (IsVendor)
        {
            var owns = await _db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Deliveries dv
                  JOIN Orders o ON dv.IdOrder=o.IdOrder
                  JOIN Deals  d ON o.IdDeal=d.IdDeal
                  WHERE dv.IdDelivery=@Id AND d.idUser=@U",
                new { Id = id, U = CurrentUserId });
            if (owns == 0) return StatusCode(403, new { message = "Livraison non autorisée." });
        }

        try
        {
            var deliveredAt = status == "delivered" ? "GETDATE()" : "DeliveredAt";
            await _db.ExecuteAsync($@"
                UPDATE Deliveries SET Status=@Status, DeliveredAt={deliveredAt}, UpdatedAt=GETDATE()
                WHERE IdDelivery=@Id", new { Status = status, Id = id });
            return Ok(new { id_delivery = id, status });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Deliveries.UpdateStatus] {ex.Message}");
            return StatusCode(500, new { message = "Mise à jour impossible : " + ex.Message });
        }
    }

    // GET /api/deliveries/:orderId (from stashed version)
    [HttpGet("order/{orderId:long}")]
    public async Task<IActionResult> GetByOrder(long orderId)
    {
        var delivery = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT d.*, t.Name AS TransportName, t.Price AS TransportPrice
            FROM Deliveries d
            LEFT JOIN Transports t ON d.IdTransport = t.IdTransport
            WHERE d.IdOrder = @OrderId",
            new { OrderId = orderId });
        if (delivery == null) return NotFound(new { message = "Livraison introuvable." });
        return Ok(delivery);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers — read JSON values regardless of casing
    // ─────────────────────────────────────────────────────────────
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
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var s)) return s;
        return 0m;
    }
}
