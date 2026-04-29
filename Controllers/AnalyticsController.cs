using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "admin")]
public class AnalyticsController : ControllerBase
{
    private readonly DbService _db;
    public AnalyticsController(DbService db) => _db = db;

    // GET /api/admin/analytics/overview
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var users     = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Active=1");
        var vendors   = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE IdRole=3 AND Active=1");
        var orders    = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders WHERE Active > 0");
        var delivered = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders WHERE Active=2");
        var revenue   = await _db.ExecuteScalarAsync<decimal?>(
            "SELECT ISNULL(SUM(TotalAmount),0) FROM Invoices WHERE Status='paid'");
        var ads       = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Ads WHERE Active=1");
        var deals     = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Deals WHERE active=1");

        return Ok(new
        {
            total_users     = users,
            total_vendors   = vendors,
            total_orders    = orders,
            delivered,
            total_revenue   = revenue ?? 0,
            total_ads       = ads,
            total_deals     = deals,
            conversion_rate = orders > 0
                ? Math.Round((double)delivered / orders * 100, 1)
                : 0,
        });
    }

    // GET /api/admin/analytics/users-by-month
    [HttpGet("users-by-month")]
    public async Task<IActionResult> UsersByMonth()
    {
        var data = await _db.QueryAsync<dynamic>(@"
            SELECT
                FORMAT(TRY_CAST(CreationDate AS DATETIME), 'yyyy-MM') AS Month,
                COUNT(*) AS Count
            FROM Users
            WHERE TRY_CAST(CreationDate AS DATETIME) >= DATEADD(MONTH, -12, GETDATE())
            GROUP BY FORMAT(TRY_CAST(CreationDate AS DATETIME), 'yyyy-MM')
            ORDER BY Month ASC");
        return Ok(data);
    }

    // GET /api/admin/analytics/orders-by-month
    [HttpGet("orders-by-month")]
    public async Task<IActionResult> OrdersByMonth()
    {
        var data = await _db.QueryAsync<dynamic>(@"
            SELECT
                FORMAT(DateTimeCommand, 'yyyy-MM') AS Month,
                COUNT(*) AS TotalOrders,
                SUM(CASE WHEN Active=2 THEN 1 ELSE 0 END) AS Delivered,
                SUM(CASE WHEN Active=0 THEN 1 ELSE 0 END) AS Cancelled
            FROM Orders
            WHERE DateTimeCommand >= DATEADD(MONTH, -12, GETDATE())
            GROUP BY FORMAT(DateTimeCommand, 'yyyy-MM')
            ORDER BY Month ASC");
        return Ok(data);
    }

    // GET /api/admin/analytics/revenue-by-month
    [HttpGet("revenue-by-month")]
    public async Task<IActionResult> RevenueByMonth()
    {
        var data = await _db.QueryAsync<dynamic>(@"
            SELECT
                FORMAT(IssuedAt, 'yyyy-MM')   AS Month,
                ISNULL(SUM(TotalAmount), 0)   AS Revenue,
                ISNULL(SUM(TaxAmount), 0)     AS Tax,
                COUNT(*)                      AS InvoiceCount
            FROM Invoices
            WHERE IssuedAt >= DATEADD(MONTH, -12, GETDATE())
            GROUP BY FORMAT(IssuedAt, 'yyyy-MM')
            ORDER BY Month ASC");
        return Ok(data);
    }

    // GET /api/admin/analytics/top-vendors
    [HttpGet("top-vendors")]
    public async Task<IActionResult> TopVendors()
    {
        var data = await _db.QueryAsync<dynamic>(@"
            SELECT TOP 10
                u.IdUser,
                CONCAT(u.FirstName,' ',u.LastName) AS VendorName,
                u.Username                         AS ShopName,
                COUNT(DISTINCT d.IdDeal)           AS TotalDeals,
                COUNT(DISTINCT o.IdOrder)          AS TotalOrders
            FROM Users u
            LEFT JOIN Deals  d ON d.idUser = u.IdUser AND d.active = 1
            LEFT JOIN Orders o ON o.IdDeal = d.IdDeal
            WHERE u.IdRole = 3 AND u.Active = 1
            GROUP BY u.IdUser, u.FirstName, u.LastName, u.Username
            ORDER BY TotalOrders DESC");
        return Ok(data);
    }

    // GET /api/admin/analytics/top-categories
    [HttpGet("top-categories")]
    public async Task<IActionResult> TopCategories()
    {
        var data = await _db.QueryAsync<dynamic>(@"
            SELECT TOP 10
                c.IdCateg,
                c.TitleFr              AS Category,
                COUNT(DISTINCT d.IdDeal) AS TotalDeals,
                COUNT(DISTINCT a.IdAd)   AS TotalAds
            FROM Categories c
            LEFT JOIN Deals d ON d.idCateg = c.IdCateg AND d.active = 1
            LEFT JOIN Ads   a ON a.IdCateg = c.IdCateg AND a.Active = 1
            WHERE c.Active = 1
            GROUP BY c.IdCateg, c.TitleFr
            ORDER BY TotalDeals DESC");
        return Ok(data);
    }

    // GET /api/admin/analytics/alerts  — IA simple
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var alerts = new List<object>();

        var pendingVendors = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE IdRole=3 AND IsVerified=0 AND Active=1");
        if (pendingVendors > 0)
            alerts.Add(new { type = "warning", message = $"{pendingVendors} vendeur(s) en attente d'approbation." });

        var openReclamations = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Reports WHERE State=1");
        if (openReclamations > 5)
            alerts.Add(new { type = "danger", message = $"{openReclamations} réclamations ouvertes non traitées." });

        var pendingOrders = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Orders WHERE Active=1 AND DateTimeCommand < DATEADD(DAY,-3,GETDATE())");
        if (pendingOrders > 0)
            alerts.Add(new { type = "warning", message = $"{pendingOrders} commande(s) en attente depuis plus de 3 jours." });

        if (!alerts.Any())
            alerts.Add(new { type = "success", message = "Tout est en ordre. Aucune alerte active." });

        return Ok(alerts);
    }
}