using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly DbService _db;
    public ReviewsController(DbService db) => _db = db;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    // ── GET /api/reviews?type=deal&targetId=5 ────────────────────
    [HttpGet]
    public async Task<IActionResult> GetReviews([FromQuery] string type, [FromQuery] long targetId)
    {
        var list = await _db.QueryAsync<ReviewRecord>(@"
            SELECT r.*,
                   CONCAT(u.FirstName,' ',u.LastName) AS AuthorName
            FROM Reviews r
            JOIN Users u ON r.IdUser = u.IdUser
            WHERE r.TargetType=@Type AND r.TargetId=@Id AND r.Active=1
            ORDER BY r.IdReview DESC",
            new { Type = type, Id = targetId });
        return Ok(list);
    }

    // ── GET /api/reviews/summary?type=deal&targetId=5 ────────────
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string type, [FromQuery] long targetId)
    {
        var data = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT COUNT(*) AS total,
                   AVG(CAST(Rating AS FLOAT)) AS average,
                   SUM(CASE WHEN Rating=5 THEN 1 ELSE 0 END) AS r5,
                   SUM(CASE WHEN Rating=4 THEN 1 ELSE 0 END) AS r4,
                   SUM(CASE WHEN Rating=3 THEN 1 ELSE 0 END) AS r3,
                   SUM(CASE WHEN Rating=2 THEN 1 ELSE 0 END) AS r2,
                   SUM(CASE WHEN Rating=1 THEN 1 ELSE 0 END) AS r1
            FROM Reviews
            WHERE TargetType=@Type AND TargetId=@Id AND Active=1",
            new { Type = type, Id = targetId });
        return Ok(data);
    }

    // ── POST /api/reviews?type=deal&targetId=5 ───────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddReview(
        [FromQuery] string type,
        [FromQuery] long targetId,
        [FromBody] ReviewRequest req)
    {
        var userId = CurrentUserId;
        if (userId == null) return Unauthorized();

        if (req.Rating < 1 || req.Rating > 5)
            return BadRequest(new { message = "Note entre 1 et 5." });

        try
        {
            var id = await _db.ExecuteScalarAsync<long>(@"
                INSERT INTO Reviews (IdUser, TargetType, TargetId, Rating, Comment)
                OUTPUT INSERTED.IdReview
                VALUES (@IdUser, @Type, @TargetId, @Rating, @Comment)",
                new { IdUser = userId, Type = type, TargetId = targetId, req.Rating, req.Comment });

            var created = await _db.QueryFirstOrDefaultAsync<ReviewRecord>(
                @"SELECT r.*, CONCAT(u.FirstName,' ',u.LastName) AS AuthorName
                  FROM Reviews r JOIN Users u ON r.IdUser=u.IdUser
                  WHERE r.IdReview=@Id", new { Id = id });
            return Ok(created);
        }
        catch
        {
            // Unique constraint violation — update existing
            await _db.ExecuteAsync(@"
                UPDATE Reviews SET Rating=@Rating, Comment=@Comment, CreatedAt=GETDATE()
                WHERE IdUser=@IdUser AND TargetType=@Type AND TargetId=@TargetId",
                new { IdUser = userId, Type = type, TargetId = targetId, req.Rating, req.Comment });
            return Ok(new { message = "Avis mis à jour." });
        }
    }

    // ── DELETE /api/reviews/:id ───────────────────────────────────
    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(long id)
    {
        var userId = CurrentUserId;
        var rows = await _db.ExecuteAsync(
            "DELETE FROM Reviews WHERE IdReview=@Id AND IdUser=@UserId",
            new { Id = id, UserId = userId });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Avis supprimé." });
    }

    // ── GET /api/reviews/my ───────────────────────────────────────
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyReviews()
    {
        var userId = CurrentUserId;
        var list = await _db.QueryAsync<ReviewRecord>(@"
            SELECT r.* FROM Reviews r
            WHERE r.IdUser=@UserId AND r.Active=1
            ORDER BY r.CreatedAt DESC",
            new { UserId = userId });
        return Ok(list);
    }
}
