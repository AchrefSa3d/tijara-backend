using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Dapper;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly DbService _db;
    public OrdersController(DbService db) => _db = db;

    // ─── GET /api/orders ──────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // Robustly read role and id — try both custom claim and standard claim types
        var role   = User.FindFirstValue("role")
                  ?? User.FindFirstValue(ClaimTypes.Role)
                  ?? "user";
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        if (role == "admin")
        {
            var all = await _db.QueryAsync<Order>(
                @"SELECT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active,
                         CONCAT(u.FirstName,' ',u.LastName) AS ClientName, u.Email AS ClientEmail,
                         d.titleDeal AS DealTitle, d.priceDeal AS DealPrice,
                         CONCAT(uv.FirstName,' ',uv.LastName) AS VendorName
                  FROM Orders o
                  LEFT JOIN Users u  ON o.IdUser = u.IdUser
                  LEFT JOIN Deals d  ON o.IdDeal = d.IdDeal
                  LEFT JOIN Users uv ON d.idUser = uv.IdUser
                  ORDER BY o.IdOrder DESC"
            );
            return Ok(all.Select(MapOrder));
        }

        if (role == "vendor")
        {
            var vend = await _db.QueryAsync<Order>(
                @"SELECT DISTINCT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active,
                         CONCAT(u.FirstName,' ',u.LastName) AS ClientName, u.Email AS ClientEmail,
                         d.titleDeal AS DealTitle, d.priceDeal AS DealPrice
                  FROM Orders o
                  LEFT JOIN Users u ON o.IdUser = u.IdUser
                  JOIN Deals d ON o.IdDeal = d.IdDeal
                  WHERE d.idUser = @VendorId
                  ORDER BY o.IdOrder DESC",
                new { VendorId = userId }
            );
            return Ok(vend.Select(MapOrder));
        }

        // regular user
        var mine = await _db.QueryAsync<Order>(
            @"SELECT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active,
                     d.titleDeal AS DealTitle, d.priceDeal AS DealPrice,
                     CONCAT(uv.FirstName,' ',uv.LastName) AS VendorName
              FROM Orders o
              LEFT JOIN Deals d  ON o.IdDeal = d.IdDeal
              LEFT JOIN Users uv ON d.idUser = uv.IdUser
              WHERE o.IdUser = @UserId
              ORDER BY o.IdOrder DESC",
            new { UserId = userId }
        );
        return Ok(mine.Select(MapOrder));
    }

    // ─── GET /api/orders/:id ──────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOne(long id)
    {
        var role   = User.FindFirstValue("role")
                  ?? User.FindFirstValue(ClaimTypes.Role)
                  ?? "user";
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var order = await _db.QueryFirstOrDefaultAsync<Order>(
            @"SELECT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active,
                     CONCAT(u.FirstName,' ',u.LastName) AS ClientName, u.Email AS ClientEmail,
                     d.titleDeal AS DealTitle, d.priceDeal AS DealPrice
              FROM Orders o
              LEFT JOIN Users u ON o.IdUser = u.IdUser
              LEFT JOIN Deals d ON o.IdDeal = d.IdDeal
              WHERE o.IdOrder = @Id",
            new { Id = id }
        );

        if (order == null) return NotFound(new { message = "Commande introuvable." });
        if (role == "user" && order.IdUser != userId)
            return StatusCode(403, new { message = "Accès refusé." });

        var details = await _db.QueryAsync<OrderDetail>(
            "SELECT * FROM OrderDetails WHERE IdOrder = @Id",
            new { Id = id }
        );

        return Ok(new { order = MapOrder(order), details });
    }

    // ─── POST /api/orders ────────────────────────────────────
    [HttpPost]
    [Authorize]   // both users and vendors can place orders
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        if (req.IdDeal <= 0)
            return BadRequest(new { message = "Deal requis." });

        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var deal = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT IdDeal, priceDeal FROM Deals WHERE IdDeal=@Id AND active=1",
            new { Id = req.IdDeal }
        );
        if (deal == null)
            return BadRequest(new { message = "Produit introuvable." });

        using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        using var tran = conn.BeginTransaction();
        try
        {
            var order = await conn.QueryFirstOrDefaultAsync<Order>(
                @"INSERT INTO Orders (IdUser, IdDeal, DateTimeCommand, Active)
                  OUTPUT INSERTED.*
                  VALUES (@IdUser, @IdDeal, @Now, 1)",
                new { IdUser = userId, IdDeal = req.IdDeal, Now = DateTime.Now },
                tran
            );

            if (req.Detail != null)
            {
                await conn.ExecuteAsync(
                    @"INSERT INTO OrderDetails
                        (IdUser, IdOrder, Address, Email, Telephone, FirstName, LastName,
                         Quantity, TotalAmount, DateTimeCommand, Active)
                      VALUES
                        (@IdUser, @IdOrder, @Address, @Email, @Telephone, @FirstName, @LastName,
                         @Quantity, @TotalAmount, @DateTimeCommand, 1)",
                    new
                    {
                        IdUser          = userId,
                        IdOrder         = order!.IdOrder,
                        Address         = req.Detail.Address,
                        Email           = req.Detail.Email,
                        Telephone       = req.Detail.Telephone,
                        FirstName       = req.Detail.FirstName,
                        LastName        = req.Detail.LastName,
                        Quantity        = req.Detail.Quantity ?? 1,
                        TotalAmount     = deal.priceDeal,
                        DateTimeCommand = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    },
                    tran
                );
            }

            await tran.CommitAsync();

            // ── Notifier le vendeur du produit ────────────────
            try
            {
                var vendorId = await _db.ExecuteScalarAsync<long?>(
                    "SELECT idUser FROM Deals WHERE IdDeal=@Id", new { Id = req.IdDeal });
                if (vendorId.HasValue && vendorId.Value != userId)
                {
                    var clientName = await _db.ExecuteScalarAsync<string>(
                        "SELECT CONCAT(FirstName,' ',LastName) FROM Users WHERE IdUser=@Id",
                        new { Id = userId });
                    await NotificationsController.CreateAsync(_db, vendorId.Value,
                        "new_order", "Nouvelle commande reçue !",
                        $"{clientName ?? "Un client"} vient de commander votre produit.",
                        $"/ent/orders", order?.IdOrder);
                }
            }
            catch { /* notification non-bloquante */ }

            return StatusCode(201, new { id = order?.IdOrder, message = "Commande créée." });
        }
        catch (Exception ex)
        {
            await tran.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── PATCH /api/orders/:id/status ────────────────────────
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "admin,vendor")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] StatusRequest req)
    {
        int? val = req.Status switch
        {
            "pending"   => 1,
            "confirmed" => 3,
            "delivered" => 2,
            "cancelled" => 0,
            _ => null
        };
        if (val == null) return BadRequest(new { message = "Statut invalide." });

        var rows = await _db.ExecuteAsync(
            "UPDATE Orders SET Active=@Status WHERE IdOrder=@Id",
            new { Status = val, Id = id }
        );
        if (rows == 0) return NotFound(new { message = "Commande introuvable." });
        return Ok(new { message = "Statut mis à jour.", id, status = req.Status });
    }

    // ─── Helpers ─────────────────────────────────────────────
    private static object MapOrder(Order o) => new
    {
        id             = o.IdOrder,
        user_id        = o.IdUser,
        deal_id        = o.IdDeal,
        client_name    = o.ClientName,
        client_email   = o.ClientEmail,
        deal_title     = o.DealTitle,
        total_amount   = o.DealPrice,
        vendor_name    = o.VendorName,
        status         = o.Active switch { 2 => "delivered", 3 => "confirmed", 0 => "cancelled", _ => "pending" },
        shipping_address = (string?)null,
        created_at     = o.DateTimeCommand?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
    };
}

public class CreateOrderRequest
{
    public long              IdDeal { get; set; }
    public OrderDetailRequest? Detail { get; set; }
}

public class OrderDetailRequest
{
    public string? Address   { get; set; }
    public string? Email     { get; set; }
    public string? Telephone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName  { get; set; }
    public int?    Quantity  { get; set; }
}
