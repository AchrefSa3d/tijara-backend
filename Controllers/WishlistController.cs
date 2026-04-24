using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly DbService _db;
    public WishlistController(DbService db) => _db = db;

    private long UserId => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    // ── GET /api/wishlist/ads ─────────────────────────────────────
    [HttpGet("ads")]
    public async Task<IActionResult> GetWishlistAds()
    {
        var list = await _db.QueryAsync<WishlistItem>(@"
            SELECT w.IdWish, w.IdUser, a.IdAd AS TargetId, w.CreatedAt,
                   a.TitleAd AS Title, a.ImageAd AS Image, a.PriceAd AS Price
            FROM WishlistAds w
            JOIN Ads a ON w.IdAd = a.IdAd
            WHERE w.IdUser = @UserId
            ORDER BY w.CreatedAt DESC",
            new { UserId = UserId });
        return Ok(list);
    }

    // ── POST /api/wishlist/ads/:adId ──────────────────────────────
    [HttpPost("ads/{adId:long}")]
    public async Task<IActionResult> AddAdToWishlist(long adId)
    {
        try
        {
            await _db.ExecuteAsync(
                "INSERT INTO WishlistAds (IdUser, IdAd) VALUES (@UserId, @AdId)",
                new { UserId = UserId, AdId = adId });
            return Ok(new { message = "Annonce ajoutée aux favoris." });
        }
        catch
        {
            return Conflict(new { message = "Déjà dans les favoris." });
        }
    }

    // ── DELETE /api/wishlist/ads/:adId ────────────────────────────
    [HttpDelete("ads/{adId:long}")]
    public async Task<IActionResult> RemoveAdFromWishlist(long adId)
    {
        await _db.ExecuteAsync(
            "DELETE FROM WishlistAds WHERE IdUser=@UserId AND IdAd=@AdId",
            new { UserId = UserId, AdId = adId });
        return Ok(new { message = "Annonce retirée des favoris." });
    }

    // ── GET /api/wishlist/deals ───────────────────────────────────
    [HttpGet("deals")]
    public async Task<IActionResult> GetWishlistDeals()
    {
        var list = await _db.QueryAsync<WishlistItem>(@"
            SELECT w.IdWish, w.IdUser, d.IdDeal AS TargetId, w.CreatedAt,
                   d.TitleDeal AS Title, d.ImageDeal AS Image, d.PriceDeal AS Price
            FROM WishlistDeals w
            JOIN Deals d ON w.IdDeal = d.IdDeal
            WHERE w.IdUser = @UserId
            ORDER BY w.CreatedAt DESC",
            new { UserId = UserId });
        return Ok(list);
    }

    // ── POST /api/wishlist/deals/:dealId ──────────────────────────
    [HttpPost("deals/{dealId:long}")]
    public async Task<IActionResult> AddDealToWishlist(long dealId)
    {
        try
        {
            await _db.ExecuteAsync(
                "INSERT INTO WishlistDeals (IdUser, IdDeal) VALUES (@UserId, @DealId)",
                new { UserId = UserId, DealId = dealId });
            return Ok(new { message = "Deal ajouté aux favoris." });
        }
        catch
        {
            return Conflict(new { message = "Déjà dans les favoris." });
        }
    }

    // ── DELETE /api/wishlist/deals/:dealId ────────────────────────
    [HttpDelete("deals/{dealId:long}")]
    public async Task<IActionResult> RemoveDealFromWishlist(long dealId)
    {
        await _db.ExecuteAsync(
            "DELETE FROM WishlistDeals WHERE IdUser=@UserId AND IdDeal=@DealId",
            new { UserId = UserId, DealId = dealId });
        return Ok(new { message = "Deal retiré des favoris." });
    }

    // ── GET /api/wishlist/check ────────────────────────────────────
    // Returns which items the user has wishlisted (for UI state)
    [HttpGet("check")]
    public async Task<IActionResult> CheckWishlist([FromQuery] long? adId, [FromQuery] long? dealId)
    {
        bool adLiked = false, dealLiked = false;
        if (adId.HasValue)
        {
            var c = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM WishlistAds WHERE IdUser=@U AND IdAd=@Id",
                new { U = UserId, Id = adId.Value });
            adLiked = c > 0;
        }
        if (dealId.HasValue)
        {
            var c = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM WishlistDeals WHERE IdUser=@U AND IdDeal=@Id",
                new { U = UserId, Id = dealId.Value });
            dealLiked = c > 0;
        }
        return Ok(new { ad_liked = adLiked, deal_liked = dealLiked });
    }
}
