using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly DbService _db;
    public ReportsController(DbService db) => _db = db;

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
    private bool IsAdmin => CurrentRole == "admin" || User.IsInRole("admin");

    // GET /api/reports/overview — admin: global, vendor: own
    // Schéma réel: Orders(IdOrder, IdUser, IdDeal, DateTimeCommand, Active)
    // - "Total" vient de Deals.priceDeal via JOIN
    // - "Vendor" vient de Deals.idUser via JOIN
    // - "Status" vient de Deliveries.Status (sinon 'pending')
    // - "PaymentStatus" vient de Invoices.Status (paid/unpaid)
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var vendorJoin   = IsAdmin ? "" : "AND dl.idUser=@UserId";
        var param        = new { UserId = CurrentUserId };

        var stats = await _db.QueryFirstOrDefaultAsync<dynamic>($@"
            ;WITH OrdersView AS (
                SELECT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active,
                       TRY_CAST(REPLACE(dl.priceDeal,',','.') AS DECIMAL(18,3)) AS Total, dl.idUser AS IdVendor,
                       ISNULL(dv.Status,'pending') AS Status,
                       ISNULL(iv.Status,'unpaid')  AS PaymentStatus
                FROM Orders o
                LEFT JOIN Deals dl ON o.IdDeal = dl.IdDeal
                LEFT JOIN Deliveries dv ON dv.IdOrder = o.IdOrder
                LEFT JOIN Invoices  iv ON iv.IdOrder = o.IdOrder
                WHERE 1=1 {vendorJoin}
            )
            SELECT
                (SELECT COUNT(*) FROM OrdersView) AS TotalOrders,
                (SELECT COUNT(*) FROM OrdersView WHERE Status='delivered') AS DeliveredOrders,
                (SELECT COUNT(*) FROM OrdersView WHERE Status='pending')   AS PendingOrders,
                (SELECT ISNULL(SUM(Total),0) FROM OrdersView) AS TotalRevenue,
                (SELECT ISNULL(SUM(Total),0) FROM OrdersView WHERE PaymentStatus='paid') AS PaidRevenue,
                (SELECT COUNT(*) FROM Deals)                          AS TotalProducts,
                (SELECT COUNT(*) FROM Users WHERE Active=1)           AS ActiveUsers
        ", param);

        return Ok(stats);
    }

    // GET /api/reports/sales-by-month
    [HttpGet("sales-by-month")]
    public async Task<IActionResult> SalesByMonth()
    {
        var vendorFilter = IsAdmin ? "" : "AND dl.idUser=@UserId";
        var list = await _db.QueryAsync<dynamic>($@"
            SELECT TOP 12
                   FORMAT(o.DateTimeCommand,'yyyy-MM') AS Month,
                   COUNT(*) AS Orders,
                   ISNULL(SUM(TRY_CAST(REPLACE(dl.priceDeal,',','.') AS DECIMAL(18,3))),0) AS Revenue
            FROM Orders o
            LEFT JOIN Deals dl ON o.IdDeal = dl.IdDeal
            WHERE o.DateTimeCommand >= DATEADD(MONTH, -12, GETDATE()) {vendorFilter}
            GROUP BY FORMAT(o.DateTimeCommand,'yyyy-MM')
            ORDER BY Month", new { UserId = CurrentUserId });
        return Ok(list);
    }

    // GET /api/reports/top-products
    // OrderDetails utilise IdProduct (pas IdDeal)
    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] int limit = 10)
    {
        var vendorFilter = IsAdmin ? "" : "WHERE d.idUser=@UserId";
        var list = await _db.QueryAsync<dynamic>($@"
            SELECT TOP (@Limit) d.IdDeal AS Id, d.titleDeal AS Title, d.priceDeal AS Price,
                   d.imageDeal AS Image,
                   (SELECT COUNT(*) FROM Orders o WHERE o.IdDeal = d.IdDeal) AS Sold
            FROM Deals d
            {vendorFilter}
            ORDER BY Sold DESC", new { Limit = limit, UserId = CurrentUserId });
        return Ok(list);
    }

    // GET /api/reports/top-customers (admin only)
    [HttpGet("top-customers")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> TopCustomers([FromQuery] int limit = 10)
    {
        var list = await _db.QueryAsync<dynamic>(@"
            SELECT TOP (@Limit) u.IdUser, u.Email,
                   CONCAT(u.FirstName,' ',u.LastName) AS Name,
                   COUNT(o.IdOrder) AS Orders,
                   ISNULL(SUM(TRY_CAST(REPLACE(dl.priceDeal,',','.') AS DECIMAL(18,3))),0) AS TotalSpent
            FROM Users u
            LEFT JOIN Orders o  ON o.IdUser=u.IdUser
            LEFT JOIN Deals  dl ON o.IdDeal=dl.IdDeal
            GROUP BY u.IdUser, u.Email, u.FirstName, u.LastName
            ORDER BY TotalSpent DESC", new { Limit = limit });
        return Ok(list);
    }
}
