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

        // Toujours joindre Users (acheteur), Vendor (Deals.idUser) et Deals pour enrichir l'affichage
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
        // Projection explicite en snake_case pour le frontend
        var result = rows.Select(r => new
        {
            id_invoice    = (long)r.IdInvoice,
            number        = (string)r.Number,
            id_order      = (long)r.IdOrder,
            id_user       = (long)r.IdUser,
            id_vendor     = (long?)r.IdVendor,
            subtotal      = (decimal)r.Subtotal,
            tax           = (decimal)r.Tax,
            delivery_fee  = (decimal)r.DeliveryFee,
            total         = (decimal)r.Total,
            status        = (string)r.Status,
            issued_at     = (DateTime?)r.IssuedAt,
            paid_at       = (DateTime?)r.PaidAt,
            client_name   = (string?)r.ClientName,
            client_email  = (string?)r.ClientEmail,
            vendor_name   = (string?)r.VendorName,
            deal_title    = (string?)r.DealTitle,
        });
        return Ok(result);
    }

    // GET /api/invoices (alias for GetList, mine or all for admin)
    public async Task<IActionResult> GetAll()
    {
        return await GetList();
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

    // POST /api/invoices/from-order/:idOrder — génère une facture à partir d'une commande
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

    // POST /api/invoices (admin/vendor — create invoice directly)
    [HttpPost]
    [Authorize(Roles = "admin,vendor")]
    public async Task<IActionResult> Create([FromBody] InvoiceRequest req)
    {
        var tax   = req.Amount * 0.19m; // 19% TVA
        var total = req.Amount + tax;
        var ref_  = "INV-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);

        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Invoices (IdOrder, IdUser, Amount, TaxAmount, TotalAmount, InvoiceRef, Status)
            OUTPUT INSERTED.IdInvoice
            VALUES (@IdOrder, @IdUser, @Amount, @Tax, @Total, @Ref, 'pending')",
            new { req.IdOrder, req.IdUser, req.Amount, Tax = tax, Total = total, Ref = ref_ });

        return StatusCode(201, new { id, invoice_ref = ref_, total_amount = total });
    }

    // PATCH /api/invoices/:id/paid
    [HttpPatch("{id:long}/paid")]
    public async Task<IActionResult> MarkPaid(long id)
    {
        await _db.ExecuteAsync(
            "UPDATE Invoices SET Status='paid', PaidAt=GETDATE() WHERE IdInvoice=@Id",
            new { Id = id });
        return Ok(new { message = "Facture marquée comme payée." });
    }

    // PATCH /api/invoices/:id/pay (alternate route)
    [HttpPatch("{id:long}/pay")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> MarkPaidAlt(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Invoices SET Status='paid', PaidAt=GETDATE() WHERE IdInvoice=@Id",
            new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Facture marquée comme payée." });
    }
}

public class InvoiceRequest
{
    public long    IdOrder { get; set; }
    public long    IdUser  { get; set; }
    public decimal Amount  { get; set; }
}
