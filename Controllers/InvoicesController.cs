using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly DbService _db;
    public InvoicesController(DbService db) => _db = db;

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

    // GET /api/invoices — admin: all, vendor: mine as vendor, user: mine as buyer
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        string sql;
        object? param = null;

        // Toujours joindre Users (acheteur) et Deals pour enrichir l'affichage
        var commonSelect = @"
            SELECT i.IdInvoice, i.Number, i.IdOrder, i.IdUser, i.IdVendor,
                   i.Subtotal, i.Tax, i.DeliveryFee, i.Total, i.Status,
                   i.IssuedAt, i.PaidAt,
                   LTRIM(RTRIM(CONCAT(u.FirstName,' ',u.LastName))) AS ClientName,
                   u.Email AS ClientEmail,
                   d.titleDeal AS DealTitle
            FROM Invoices i
            LEFT JOIN Users u ON i.IdUser = u.IdUser
            LEFT JOIN Orders o ON i.IdOrder = o.IdOrder
            LEFT JOIN Deals  d ON o.IdDeal  = d.IdDeal";

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

        var list = await _db.QueryAsync<dynamic>(sql, param);
        return Ok(list);
    }

    // GET /api/invoices/:id
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOne(long id)
    {
        var inv = await _db.QueryFirstOrDefaultAsync<Invoice>(
            "SELECT * FROM Invoices WHERE IdInvoice=@Id", new { Id = id });
        if (inv == null) return NotFound();
        // Access check
        if (!IsAdmin && inv.IdUser != CurrentUserId && inv.IdVendor != CurrentUserId)
            return Forbid();
        return Ok(inv);
    }

    // POST /api/invoices — génère une facture à partir d'une commande
    [HttpPost("from-order/{idOrder:long}")]
    public async Task<IActionResult> FromOrder(long idOrder)
    {
        // Anti-doublon
        var existing = await _db.QueryFirstOrDefaultAsync<Invoice>(
            "SELECT * FROM Invoices WHERE IdOrder=@Id", new { Id = idOrder });
        if (existing != null) return Ok(existing);

        // Orders schema: IdOrder, IdUser, IdDeal, DateTimeCommand, Active
        // Deals.PriceDeal est NVARCHAR(50) — on parse côté SQL avec TRY_CAST
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

        var dict = (IDictionary<string, object>)order;
        object? Get(string k) => dict.TryGetValue(k, out var x) && x != DBNull.Value ? x : null;

        decimal SafeDec(object? o) => o == null ? 0m : Convert.ToDecimal(o, System.Globalization.CultureInfo.InvariantCulture);
        long    SafeLong(object? o) => o == null ? 0L : Convert.ToInt64(o);

        var number   = "INV-" + DateTime.Now.Year + "-" + DateTime.Now.Ticks.ToString()[^6..];
        decimal total    = SafeDec(Get("Total"));
        decimal delivery = 0m;
        decimal subtotal = total - delivery;
        decimal tax      = Math.Round(subtotal * 0.07m, 3); // TVA 7% Tunisie
        long    idUser   = SafeLong(Get("IdBuyer"));
        long?   idVendor = Get("IdVendor") is object v1 ? Convert.ToInt64(v1) : (long?)null;

        // Garde-fou: l'utilisateur courant doit être l'acheteur, le vendeur, ou admin
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

    // PATCH /api/invoices/:id/paid
    [HttpPatch("{id:long}/paid")]
    public async Task<IActionResult> MarkPaid(long id)
    {
        await _db.ExecuteAsync(
            "UPDATE Invoices SET Status='paid', PaidAt=GETDATE() WHERE IdInvoice=@Id",
            new { Id = id });
        return Ok();
    }
}
