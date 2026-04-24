using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/follows")]
[Authorize]
public class FollowsController : ControllerBase
{
    private readonly DbService _db;
    public FollowsController(DbService db) => _db = db;

    // POST /api/follows/{vendorId} — toggle follow/unfollow
    [HttpPost("{vendorId:long}")]
    public async Task<IActionResult> ToggleFollow(long vendorId)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        if (userId == vendorId)
            return BadRequest(new { message = "Vous ne pouvez pas vous suivre vous-même." });

        var existing = await _db.QueryFirstOrDefaultAsync<UserFollow>(
            "SELECT IdFollow FROM UserFollows WHERE IdUser=@UserId AND IdVendor=@VendorId",
            new { UserId = userId, VendorId = vendorId });

        if (existing != null)
        {
            await _db.ExecuteAsync(
                "DELETE FROM UserFollows WHERE IdUser=@UserId AND IdVendor=@VendorId",
                new { UserId = userId, VendorId = vendorId });
            return Ok(new { following = false, message = "Abonnement retiré." });
        }

        await _db.ExecuteAsync(
            "INSERT INTO UserFollows (IdUser, IdVendor) VALUES (@UserId, @VendorId)",
            new { UserId = userId, VendorId = vendorId });
        return Ok(new { following = true, message = "Vous suivez maintenant ce vendeur." });
    }

    // GET /api/follows/check/{vendorId} — is current user following this vendor?
    [HttpGet("check/{vendorId:long}")]
    public async Task<IActionResult> CheckFollow(long vendorId)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var existing = await _db.QueryFirstOrDefaultAsync<UserFollow>(
            "SELECT IdFollow FROM UserFollows WHERE IdUser=@UserId AND IdVendor=@VendorId",
            new { UserId = userId, VendorId = vendorId });
        return Ok(new { following = existing != null });
    }

    // GET /api/follows/my-followers — vendor sees list of clients who follow them
    [HttpGet("my-followers")]
    public async Task<IActionResult> GetMyFollowers()
    {
        var vendorId = long.Parse(User.FindFirstValue("id") ?? "0");

        var followers = await _db.QueryAsync<User>(
            @"SELECT u.IdUser, u.FirstName, u.LastName, u.Username, u.ProfilePicture,
                     u.Telephone, u.Email,
                     CONVERT(NVARCHAR(20), uf.CreatedAt, 105) AS FollowedAt
              FROM UserFollows uf
              JOIN Users u ON uf.IdUser = u.IdUser
              WHERE uf.IdVendor = @VendorId AND u.Active = 1
              ORDER BY uf.CreatedAt DESC",
            new { VendorId = vendorId });

        var list = followers.ToList();
        return Ok(new
        {
            total     = list.Count,
            followers = list.Select(u => new
            {
                id              = u.IdUser,
                first_name      = u.FirstName,
                last_name       = u.LastName,
                full_name       = $"{u.FirstName} {u.LastName}".Trim(),
                username        = u.Username,
                profile_picture = u.ProfilePicture,
                phone           = u.Telephone,
                email           = u.Email,
                followed_at     = u.FollowedAt,
            })
        });
    }

    // GET /api/follows/mine — list vendors the current user follows
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyFollows()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        var vendors = await _db.QueryAsync<User>(
            @"SELECT u.IdUser, u.FirstName, u.LastName, u.Username, u.ProfilePicture,
                     u.Telephone, u.IsVerified,
                     (SELECT COUNT(*) FROM Deals WHERE idUser=u.IdUser AND active=1) AS ActiveDeals,
                     CONVERT(NVARCHAR(20), uf.CreatedAt, 105) AS FollowedAt
              FROM UserFollows uf
              JOIN Users u ON uf.IdVendor = u.IdUser
              WHERE uf.IdUser = @UserId AND u.Active = 1
              ORDER BY uf.CreatedAt DESC",
            new { UserId = userId });

        return Ok(vendors.Select(v => new
        {
            id              = v.IdUser,
            first_name      = v.FirstName,
            last_name       = v.LastName,
            shop_name       = v.Username,
            profile_picture = v.ProfilePicture,
            is_approved     = (v.IsVerified ?? 0) == 1,
            active_deals    = v.ActiveDeals ?? 0,
            followed_at     = v.FollowedAt,
        }));
    }
}
