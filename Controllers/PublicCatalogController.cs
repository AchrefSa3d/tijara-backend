using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

/// <summary>
/// Endpoints publics qui exposent les entités gérées par l'admin
/// vers les utilisateurs (coupons, prix) et vendeurs (packs boost).
/// </summary>
[ApiController]
public class PublicCatalogController : ControllerBase
{
    private readonly DbService _db;
    public PublicCatalogController(DbService db) => _db = db;

    // ─── GET /api/coupons — coupons actifs, non expirés ─────────────────
    [HttpGet("api/coupons")]
    public async Task<IActionResult> GetCoupons()
    {
        var rows = await _db.QueryAsync<dynamic>(@"
            SELECT IdCoupon, Title, Description, DateStart, DateEnd, Price, NumberOfCoupon, Used, Active
            FROM Coupons
            WHERE Active = 1
              AND (DateEnd IS NULL OR DateEnd >= GETDATE())
              AND (NumberOfCoupon = 0 OR Used < NumberOfCoupon)
            ORDER BY IdCoupon DESC");

        return Ok(rows.Select(r => new
        {
            id_coupon        = (long)r.IdCoupon,
            title            = (string)r.Title,
            description      = (string?)r.Description,
            date_start       = (DateTime?)r.DateStart,
            date_end         = (DateTime?)r.DateEnd,
            price            = (decimal)r.Price,
            number_of_coupon = (int)r.NumberOfCoupon,
            used             = (int)r.Used,
            active           = (bool)r.Active,
        }));
    }

    // ─── GET /api/prizes — prix actifs ──────────────────────────────────
    [HttpGet("api/prizes")]
    public async Task<IActionResult> GetPrizes()
    {
        var rows = await _db.QueryAsync<dynamic>(@"
            SELECT p.IdPrize, p.Title, p.Description, p.Image, p.DatePrize, p.IdUser, p.Active,
                   CONCAT(u.FirstName,' ',u.LastName) AS WinnerName
            FROM Prizes p
            LEFT JOIN Users u ON p.IdUser = u.IdUser
            WHERE p.Active = 1
            ORDER BY p.IdPrize DESC");

        return Ok(rows.Select(r => new
        {
            id_prize    = (long)r.IdPrize,
            title       = (string)r.Title,
            description = (string?)r.Description,
            image       = (string?)r.Image,
            date_prize  = (DateTime?)r.DatePrize,
            id_user     = (long?)r.IdUser,
            winner_name = (string?)r.WinnerName,
            active      = (bool)r.Active,
        }));
    }

    // ─── GET /api/boost-packs — packs actifs, accessibles aux vendeurs ──
    [HttpGet("api/boost-packs")]
    [Authorize]
    public async Task<IActionResult> GetBoostPacks()
    {
        var rows = await _db.QueryAsync<dynamic>(@"
            SELECT IdBoost, Title, Price, Discount, MaxDuration, OrdersCount,
                   Sliders, SideBar, Footer, RelatedPost, FirstLogin, Links, Active
            FROM BoostAdsPacks
            WHERE Active = 1
            ORDER BY Price ASC");

        return Ok(rows.Select(r => new
        {
            id_boost     = (long)r.IdBoost,
            title        = (string)r.Title,
            price        = (decimal)r.Price,
            discount     = (decimal)r.Discount,
            max_duration = (int)r.MaxDuration,
            orders_count = (int)r.OrdersCount,
            sliders      = (bool)r.Sliders,
            side_bar     = (bool)r.SideBar,
            footer       = (bool)r.Footer,
            related_post = (bool)r.RelatedPost,
            first_login  = (bool)r.FirstLogin,
            links        = (bool)r.Links,
            active       = (bool)r.Active,
        }));
    }

    // ─── GET /api/winners — gagnants publics (hall of fame) ─────────────
    [HttpGet("api/winners")]
    public async Task<IActionResult> GetWinners()
    {
        var rows = await _db.QueryAsync<dynamic>(@"
            SELECT w.IdWinner, w.FullName, w.IdPrize, p.Title AS PrizeTitle, p.Image AS PrizeImage
            FROM Winners w
            LEFT JOIN Prizes p ON w.IdPrize = p.IdPrize
            WHERE w.Active = 1
            ORDER BY w.IdWinner DESC");

        return Ok(rows.Select(r => new
        {
            id_winner    = (long)r.IdWinner,
            full_name    = (string?)r.FullName,
            id_prize     = (long?)r.IdPrize,
            prize_title  = (string?)r.PrizeTitle,
            prize_image  = (string?)r.PrizeImage,
        }));
    }
}
