using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/annonces")]
public class AnnoncesController : ControllerBase
{
    private readonly DbService _db;
    public AnnoncesController(DbService db) => _db = db;

    // ─── GET /api/annonces  (public) ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categId,
        [FromQuery] string? type,
        [FromQuery] int  page  = 1,
        [FromQuery] int  limit = 20)
    {
        var offset = (page - 1) * limit;
        var where  = "a.Active = 1";
        if (categId.HasValue) where += " AND a.IdCateg = @CategId";
        if (!string.IsNullOrWhiteSpace(type)) where += " AND a.Type = @Type";

        var list = await _db.QueryAsync<Ad>(
            $@"SELECT a.*,
                      CONCAT(u.FirstName, ' ', u.LastName) AS AuthorName,
                      u.Username AS ShopName,
                      c.TitleFr  AS CategoryName,
                      ISNULL((SELECT COUNT(*) FROM AdLikes    WHERE IdAd = a.IdAd), 0)            AS LikesCount,
                      ISNULL((SELECT COUNT(*) FROM AdComments WHERE IdAd = a.IdAd AND Active = 1), 0) AS CommentsCount
               FROM Ads a
               LEFT JOIN Users      u ON a.IdUser  = u.IdUser
               LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
               WHERE {where}
               ORDER BY a.DatePublication DESC
               OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY",
            new { CategId = categId, Type = type, Offset = offset, Limit = limit }
        );
        return Ok(list.Select(MapAd));
    }

    // ─── GET /api/annonces/mine ───────────────────────────────────
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var list = await _db.QueryAsync<Ad>(
            @"SELECT a.*,
                     CONCAT(u.FirstName, ' ', u.LastName) AS AuthorName,
                     u.Username AS ShopName,
                     c.TitleFr  AS CategoryName,
                     ISNULL((SELECT COUNT(*) FROM AdLikes    WHERE IdAd = a.IdAd), 0)            AS LikesCount,
                     ISNULL((SELECT COUNT(*) FROM AdComments WHERE IdAd = a.IdAd AND Active = 1), 0) AS CommentsCount
              FROM Ads a
              LEFT JOIN Users      u ON a.IdUser  = u.IdUser
              LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
              WHERE a.IdUser = @UserId
              ORDER BY a.DatePublication DESC",
            new { UserId = userId }
        );
        return Ok(list.Select(MapAd));
    }

    // ─── GET /api/annonces/:id ────────────────────────────────────
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetOne(long id)
    {
        var ad = await _db.QueryFirstOrDefaultAsync<Ad>(
            @"SELECT a.*,
                     CONCAT(u.FirstName, ' ', u.LastName) AS AuthorName,
                     u.Username AS ShopName,
                     c.TitleFr  AS CategoryName,
                     ISNULL((SELECT COUNT(*) FROM AdLikes    WHERE IdAd = a.IdAd), 0)            AS LikesCount,
                     ISNULL((SELECT COUNT(*) FROM AdComments WHERE IdAd = a.IdAd AND Active = 1), 0) AS CommentsCount
              FROM Ads a
              LEFT JOIN Users      u ON a.IdUser  = u.IdUser
              LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
              WHERE a.IdAd = @Id",
            new { Id = id }
        );
        if (ad == null) return NotFound(new { message = "Annonce introuvable." });
        return Ok(MapAd(ad));
    }

    // ─── POST /api/annonces ───────────────────────────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateAdRequest req)
    {
        // Support both snake_case aliases (frontend) and PascalCase field names
        var title       = req.Title       ?? req.TitleAd;
        var description = req.Content     ?? req.DescriptionAd;
        var details     = req.DetailsAd;
        var price       = req.Price       ?? req.PriceAd;
        var image       = req.ImageUrl    ?? req.ImageAd;
        var categId     = req.CategoryId  ?? req.IdCateg;
        var type        = req.Type        ?? "annonce";

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { message = "Titre requis." });

        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");
        var role = User.FindFirstValue("role")
                ?? User.FindFirstValue(ClaimTypes.Role)
                ?? "user";

        // Auto-approve for admin; for vendors, approve if they are verified
        int active;
        if (role == "admin") {
            active = 1;
        } else if (role == "vendor") {
            var isVerified = await _db.ExecuteScalarAsync<int?>(
                "SELECT IsVerified FROM Users WHERE IdUser=@Id", new { Id = userId });
            active = (isVerified ?? 0) == 1 ? 1 : 0;
        } else {
            active = 0; // regular users need approval
        }

        var ad = await _db.QueryFirstOrDefaultAsync<Ad>(
            @"INSERT INTO Ads (TitleAd, DescriptionAd, DetailsAd, PriceAd, ImageAd,
                               IdCateg, IdUser, Active, Type, DatePublication)
              OUTPUT INSERTED.*
              VALUES (@TitleAd, @DescriptionAd, @DetailsAd, @PriceAd, @ImageAd,
                      @IdCateg, @UserId, @Active, @Type, GETDATE())",
            new {
                TitleAd       = title,
                DescriptionAd = description,
                DetailsAd     = details,
                PriceAd       = price,
                ImageAd       = image,
                IdCateg       = categId,
                UserId        = userId,
                Active        = active,
                Type          = type
            }
        );

        // Fetch joined fields for response
        var full = await _db.QueryFirstOrDefaultAsync<Ad>(
            @"SELECT a.*,
                     CONCAT(u.FirstName, ' ', u.LastName) AS AuthorName,
                     u.Username AS ShopName,
                     c.TitleFr  AS CategoryName,
                     0 AS LikesCount, 0 AS CommentsCount
              FROM Ads a
              LEFT JOIN Users      u ON a.IdUser  = u.IdUser
              LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
              WHERE a.IdAd = @Id",
            new { Id = ad!.IdAd }
        );

        return StatusCode(201, MapAd(full ?? ad));
    }

    // ─── PUT /api/annonces/:id ────────────────────────────────────
    [HttpPut("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Update(long id, [FromBody] CreateAdRequest req)
    {
        var role   = User.FindFirstValue("role")
                  ?? User.FindFirstValue(ClaimTypes.Role)
                  ?? "user";
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var existing = await _db.QueryFirstOrDefaultAsync<Ad>(
            "SELECT IdAd, IdUser FROM Ads WHERE IdAd = @Id",
            new { Id = id }
        );
        if (existing == null) return NotFound(new { message = "Annonce introuvable." });
        if (existing.IdUser != userId && role != "admin")
            return StatusCode(403, new { message = "Accès refusé." });

        var title       = req.Title    ?? req.TitleAd;
        var description = req.Content  ?? req.DescriptionAd;
        var price       = req.Price    ?? req.PriceAd;
        var image       = req.ImageUrl ?? req.ImageAd;
        var categId     = req.CategoryId ?? req.IdCateg;
        var type        = req.Type     ?? req.Type;

        var updated = await _db.QueryFirstOrDefaultAsync<Ad>(
            @"UPDATE Ads
              SET TitleAd       = COALESCE(@TitleAd,       TitleAd),
                  DescriptionAd = COALESCE(@DescriptionAd, DescriptionAd),
                  DetailsAd     = COALESCE(@DetailsAd,     DetailsAd),
                  PriceAd       = COALESCE(@PriceAd,       PriceAd),
                  ImageAd       = COALESCE(@ImageAd,       ImageAd),
                  IdCateg       = COALESCE(@IdCateg,       IdCateg),
                  Type          = COALESCE(@Type,          Type)
              OUTPUT INSERTED.*
              WHERE IdAd = @Id",
            new {
                TitleAd       = title,
                DescriptionAd = description,
                DetailsAd     = req.DetailsAd,
                PriceAd       = price,
                ImageAd       = image,
                IdCateg       = categId,
                Type          = type,
                Id            = id
            }
        );

        var full = await _db.QueryFirstOrDefaultAsync<Ad>(
            @"SELECT a.*,
                     CONCAT(u.FirstName, ' ', u.LastName) AS AuthorName,
                     u.Username AS ShopName,
                     c.TitleFr AS CategoryName
              FROM Ads a
              LEFT JOIN Users      u ON a.IdUser  = u.IdUser
              LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
              WHERE a.IdAd = @Id",
            new { Id = id }
        );
        return Ok(MapAd(full ?? updated!));
    }

    // ─── DELETE /api/annonces/:id ─────────────────────────────────
    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Delete(long id)
    {
        var role   = User.FindFirstValue("role")
                  ?? User.FindFirstValue(ClaimTypes.Role)
                  ?? "user";
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var existing = await _db.QueryFirstOrDefaultAsync<Ad>(
            "SELECT IdAd, IdUser FROM Ads WHERE IdAd = @Id",
            new { Id = id }
        );
        if (existing == null) return NotFound(new { message = "Annonce introuvable." });
        if (existing.IdUser != userId && role != "admin")
            return StatusCode(403, new { message = "Accès refusé." });

        await _db.ExecuteAsync(
            "UPDATE Ads SET Active = 0 WHERE IdAd = @Id",
            new { Id = id }
        );
        return Ok(new { message = "Annonce supprimée." });
    }

    // ─── POST /api/annonces/:id/like ─────────────────────────────
    [HttpPost("{id:long}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(long id)
    {
        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var existing = await _db.ExecuteScalarAsync<long?>(
            "SELECT IdLike FROM AdLikes WHERE IdAd=@AdId AND IdUser=@UserId",
            new { AdId = id, UserId = userId });

        if (existing.HasValue)
        {
            await _db.ExecuteAsync(
                "DELETE FROM AdLikes WHERE IdAd=@AdId AND IdUser=@UserId",
                new { AdId = id, UserId = userId });
            return Ok(new { liked = false });
        }
        else
        {
            await _db.ExecuteAsync(
                "INSERT INTO AdLikes (IdAd, IdUser, CreatedAt) VALUES (@AdId, @UserId, GETDATE())",
                new { AdId = id, UserId = userId });
            return Ok(new { liked = true });
        }
    }

    // ─── GET /api/annonces/:id/comments ──────────────────────────
    [HttpGet("{id:long}/comments")]
    public async Task<IActionResult> GetComments(long id)
    {
        var list = await _db.QueryAsync<dynamic>(
            @"SELECT c.IdComment AS id,
                     c.Content   AS content,
                     c.CreatedAt AS created_at,
                     CONCAT(u.FirstName,' ',u.LastName) AS author_name
              FROM AdComments c
              LEFT JOIN Users u ON c.IdUser = u.IdUser
              WHERE c.IdAd = @AdId AND c.Active = 1
              ORDER BY c.CreatedAt ASC",
            new { AdId = id }
        );
        return Ok(list);
    }

    // ─── POST /api/annonces/:id/comments ─────────────────────────
    [HttpPost("{id:long}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(long id, [FromBody] CommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "Commentaire vide." });

        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");

        var newId = await _db.ExecuteScalarAsync<long>(
            @"INSERT INTO AdComments (IdAd, IdUser, Content, CreatedAt, Active)
              OUTPUT INSERTED.IdComment
              VALUES (@AdId, @UserId, @Content, GETDATE(), 1)",
            new { AdId = id, UserId = userId, Content = req.Content.Trim() }
        );

        var authorName = await _db.ExecuteScalarAsync<string>(
            "SELECT CONCAT(FirstName,' ',LastName) FROM Users WHERE IdUser=@Id",
            new { Id = userId });

        return StatusCode(201, new
        {
            id          = newId,
            content     = req.Content.Trim(),
            author_name = authorName ?? "",
            created_at  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        });
    }

    // ─── Mapper ───────────────────────────────────────────────────
    // ─── Request model ────────────────────────────────────────────
    public class CommentRequest
    {
        public string Content { get; set; } = "";
    }

    // Returns all fields expected by the Angular frontend
    internal static object MapAd(Ad a) => new
    {
        id            = a.IdAd,
        title         = a.TitleAd,
        // Aliases for frontend compatibility
        content       = a.DescriptionAd,
        description   = a.DescriptionAd,
        details       = a.DetailsAd,
        price         = a.PriceAd,
        image_url     = a.ImageAd,
        image         = a.ImageAd,
        category_id   = a.IdCateg,
        category      = a.CategoryName,
        user_id       = a.IdUser,
        author        = a.AuthorName,
        author_name   = a.AuthorName,
        shop_name     = a.ShopName,
        type          = a.Type ?? "annonce",
        // Status: active=1 → "approved", active=0 → "pending"
        status        = (a.Active ?? 0) == 1 ? "approved" : "pending",
        active        = a.Active == 1,
        created_at    = a.DatePublication,
        date          = a.DatePublication,
        likes_count    = a.LikesCount    ?? 0,
        comments_count = a.CommentsCount ?? 0,
    };
}
