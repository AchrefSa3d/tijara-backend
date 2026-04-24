using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

/// <summary>
/// Maps Deals table to /api/products — the main shop listings.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly DbService _db;
    public ProductsController(DbService db) => _db = db;

    // ─── GET /api/products ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page  = 1,
        [FromQuery] int limit = 12)
    {
        var offset = (page - 1) * limit;
        var where  = "d.active = 1";
        if (!string.IsNullOrWhiteSpace(category)) where += " AND c.TitleFr = @Category";
        if (!string.IsNullOrWhiteSpace(search))   where += " AND d.titleDeal LIKE @Search";

        var data = await _db.QueryAsync<Deal>(
            $@"SELECT d.*,
                      c.TitleFr  AS CategoryName,
                      u.Username AS ShopName,
                      CONCAT(u.FirstName,' ',u.LastName) AS VendorName,
                      ISNULL((SELECT AVG(CAST(r.Rating AS FLOAT))
                              FROM Ratings r
                              WHERE r.TableName = 'Deals' AND r.IdTable = d.IdDeal), 0) AS AvgRating,
                      ISNULL((SELECT COUNT(*) FROM Ratings r
                              WHERE r.TableName = 'Deals' AND r.IdTable = d.IdDeal), 0) AS ReviewCount
               FROM Deals d
               LEFT JOIN Categories c ON d.idCateg = c.IdCateg
               LEFT JOIN Users u      ON d.idUser  = u.IdUser
               WHERE {where}
               ORDER BY d.IdDeal DESC
               OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY",
            new
            {
                Category = category,
                Search   = search != null ? $"%{search}%" : null,
                Offset   = offset,
                Limit    = limit
            }
        );

        var total = await _db.ExecuteScalarAsync<int>(
            $@"SELECT COUNT(*) FROM Deals d
               LEFT JOIN Categories c ON d.idCateg = c.IdCateg
               WHERE {where}",
            new { Category = category, Search = search != null ? $"%{search}%" : null }
        );

        return Ok(new { data = data.Select(MapDeal), total, page, limit });
    }

    // ─── GET /api/products/mine ───────────────────────────────
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var deals  = await _db.QueryAsync<Deal>(
            @"SELECT d.*, c.TitleFr AS CategoryName
              FROM Deals d
              LEFT JOIN Categories c ON d.idCateg = c.IdCateg
              WHERE d.idUser = @UserId
              ORDER BY d.IdDeal DESC",
            new { UserId = userId }
        );
        return Ok(deals.Select(d => new
        {
            id             = d.IdDeal,
            name           = d.TitleDeal,
            description    = d.DescriptionDeal,
            price          = ParsePrice(d.PriceDeal),
            price_raw      = d.PriceDeal,
            stock          = TryParseInt(d.Quantity),
            image          = d.ImageDeal,
            image_url      = d.ImageDeal,
            category       = d.CategoryName,
            category_id    = d.IdCateg,
            status         = d.Active == 1 ? "actif" : "inactif",
            approval_status = d.Active == 1 ? "approved" : "pending",
            brand          = d.Brand,
            colors         = d.Colors,
            discount       = d.DiscountDeal,
            date_end       = d.DateEnd,
        }));
    }

    // ─── GET /api/products/vendor/:vendorId ───────────────────
    [HttpGet("vendor/{vendorId:long}")]
    public async Task<IActionResult> GetVendorProfile(long vendorId)
    {
        var vendor = await _db.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.IdUser, u.Email, u.FirstName, u.LastName, u.Username,
                     u.Telephone, u.Location, u.City,
                     u.IsVerified, u.ProfilePicture, u.CreationDate,
                     s.NameFR AS StateName
              FROM Users u
              LEFT JOIN States s ON u.IdState = s.IdState
              WHERE u.IdUser = @VendorId AND u.IdRole = 3 AND u.Active = 1",
            new { VendorId = vendorId }
        );
        if (vendor == null) return NotFound(new { message = "Vendeur introuvable." });

        var deals = await _db.QueryAsync<Deal>(
            @"SELECT d.IdDeal, d.titleDeal, d.priceDeal, d.imageDeal, d.active, d.IdCateg,
                     c.TitleFr AS CategoryName,
                     ISNULL((SELECT AVG(CAST(r.Rating AS FLOAT))
                             FROM Ratings r WHERE r.TableName='Deals' AND r.IdTable=d.IdDeal),0) AS AvgRating,
                     ISNULL((SELECT COUNT(*) FROM Ratings r
                             WHERE r.TableName='Deals' AND r.IdTable=d.IdDeal),0) AS ReviewCount
              FROM Deals d
              LEFT JOIN Categories c ON d.idCateg = c.IdCateg
              WHERE d.idUser = @UserId AND d.active = 1
              ORDER BY d.IdDeal DESC",
            new { UserId = vendorId }
        );

        var fullName = $"{vendor.FirstName} {vendor.LastName}".Trim();
        var shopName = string.IsNullOrWhiteSpace(vendor.Username) ? null : vendor.Username;

        return Ok(new
        {
            vendor = new
            {
                id              = vendor.IdUser,
                email           = vendor.Email,
                first_name      = vendor.FirstName,
                last_name       = vendor.LastName,
                shop_name       = shopName,
                display_name    = shopName ?? fullName,
                phone           = vendor.Telephone,
                city            = vendor.City ?? vendor.StateName,
                address         = vendor.Location,
                profile_picture = vendor.ProfilePicture,
                is_approved     = (vendor.IsVerified ?? 0) == 1,
                created_at      = vendor.CreationDate,
                active_deals    = deals.Count(),
            },
            products = deals.Select(MapDeal)
        });
    }

    // ─── GET /api/products/:id ────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var deal = await _db.QueryFirstOrDefaultAsync<Deal>(
            @"SELECT d.*,
                     c.TitleFr AS CategoryName,
                     u.Username AS ShopName,
                     CONCAT(u.FirstName,' ',u.LastName) AS VendorName,
                     ISNULL((SELECT AVG(CAST(r.Rating AS FLOAT))
                             FROM Ratings r WHERE r.TableName='Deals' AND r.IdTable=d.IdDeal),0) AS AvgRating,
                     ISNULL((SELECT COUNT(*) FROM Ratings r
                             WHERE r.TableName='Deals' AND r.IdTable=d.IdDeal),0) AS ReviewCount
              FROM Deals d
              LEFT JOIN Categories c ON d.idCateg = c.IdCateg
              LEFT JOIN Users u      ON d.idUser  = u.IdUser
              WHERE d.IdDeal = @Id",
            new { Id = id }
        );
        if (deal == null) return NotFound(new { message = "Produit introuvable." });
        return Ok(MapDeal(deal));
    }

    // ─── POST /api/products ───────────────────────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateDealRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TitleDeal))
            return BadRequest(new { message = "Titre requis." });

        var userId = long.Parse(
            User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "0");
        var role = User.FindFirstValue("role")
                ?? User.FindFirstValue(ClaimTypes.Role)
                ?? "user";
        // Admin products → active immediately
        // Verified vendor products → active immediately (already approved account)
        // Unverified vendor → active=0 pending admin approval
        int active;
        if (role == "admin")
        {
            active = 1;
        }
        else
        {
            var isVerified = await _db.ExecuteScalarAsync<int?>(
                "SELECT IsVerified FROM Users WHERE IdUser=@Id", new { Id = userId });
            active = (isVerified ?? 0) == 1 ? 1 : 0;
        }

        var now = DateTime.Now.ToString("yyyy-MM-dd");

        var deal = await _db.QueryFirstOrDefaultAsync<Deal>(
            @"INSERT INTO Deals
                (titleDeal, descriptionDeal, detailsDeal, priceDeal, discountDeal,
                 quantity, imageDeal, idCateg, idUser, active, colors, brand,
                 telephone, dateEnd, datePublication, startDate)
              OUTPUT INSERTED.*
              VALUES
                (@TitleDeal, @DescriptionDeal, @DetailsDeal, @PriceDeal, @DiscountDeal,
                 @Quantity, @ImageDeal, @IdCateg, @IdUser, @Active, @Colors, @Brand,
                 @Telephone, @DateEnd, @DatePublication, @StartDate)",
            new
            {
                TitleDeal       = req.TitleDeal?.Trim(),
                DescriptionDeal = req.DescriptionDeal?.Trim(),
                DetailsDeal     = req.DetailsDeal?.Trim(),
                PriceDeal       = req.PriceDeal?.Trim(),
                DiscountDeal    = req.DiscountDeal?.Trim(),
                Quantity        = req.Quantity?.Trim(),
                ImageDeal       = req.ImageDeal,
                IdCateg         = req.IdCateg,
                IdUser          = userId,
                Active          = active,
                Colors          = req.Colors?.Trim(),
                Brand           = req.Brand?.Trim(),
                Telephone       = req.Telephone?.Trim(),
                DateEnd         = req.DateEnd?.Trim(),
                DatePublication = now,
                StartDate       = now,
            }
        );

        return StatusCode(201, deal == null ? new { message = "Créé." } : (object)MapDeal(deal));
    }

    // ─── PUT /api/products/:id ────────────────────────────────
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDealRequest req)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var role   = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "user";

        var whereClause = role == "admin" ? "IdDeal = @Id" : "IdDeal = @Id AND idUser = @UserId";

        var deal = await _db.QueryFirstOrDefaultAsync<Deal>(
            $@"UPDATE Deals SET
                titleDeal       = COALESCE(@TitleDeal,       titleDeal),
                descriptionDeal = COALESCE(@DescriptionDeal, descriptionDeal),
                priceDeal       = COALESCE(@PriceDeal,       priceDeal),
                discountDeal    = COALESCE(@DiscountDeal,    discountDeal),
                quantity        = COALESCE(@Quantity,        quantity),
                imageDeal       = COALESCE(@ImageDeal,       imageDeal),
                idCateg         = COALESCE(@IdCateg,         idCateg),
                colors          = COALESCE(@Colors,          colors),
                brand           = COALESCE(@Brand,           brand),
                telephone       = COALESCE(@Telephone,       telephone),
                dateEnd         = COALESCE(@DateEnd,         dateEnd)
               OUTPUT INSERTED.*
               WHERE {whereClause}",
            new
            {
                Id              = id,
                UserId          = userId,
                TitleDeal       = req.TitleDeal?.Trim(),
                DescriptionDeal = req.DescriptionDeal?.Trim(),
                PriceDeal       = req.PriceDeal?.Trim(),
                DiscountDeal    = req.DiscountDeal?.Trim(),
                Quantity        = req.Quantity?.Trim(),
                ImageDeal       = req.ImageDeal,
                IdCateg         = req.IdCateg,
                Colors          = req.Colors?.Trim(),
                Brand           = req.Brand?.Trim(),
                Telephone       = req.Telephone?.Trim(),
                DateEnd         = req.DateEnd?.Trim(),
            }
        );

        if (deal == null) return NotFound(new { message = "Produit introuvable ou accès refusé." });
        return Ok(MapDeal(deal));
    }

    // ─── PATCH /api/products/:id/status ──────────────────────
    [HttpPatch("{id:int}/status")]
    [Authorize]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var role   = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "user";
        var where  = role == "admin" ? "IdDeal = @Id" : "IdDeal = @Id AND idUser = @UserId";

        var rows = await _db.ExecuteAsync(
            $"UPDATE Deals SET active = CASE WHEN active = 1 THEN 0 ELSE 1 END WHERE {where}",
            new { Id = id, UserId = userId }
        );
        if (rows == 0) return NotFound(new { message = "Produit introuvable." });
        return Ok(new { message = "Statut modifié." });
    }

    // ─── DELETE /api/products/:id ─────────────────────────────
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var role   = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "user";

        int rows;
        if (role == "admin")
            rows = await _db.ExecuteAsync("DELETE FROM Deals WHERE IdDeal = @Id", new { Id = id });
        else
            rows = await _db.ExecuteAsync(
                "DELETE FROM Deals WHERE IdDeal = @Id AND idUser = @UserId",
                new { Id = id, UserId = userId });

        if (rows == 0) return NotFound(new { message = "Produit introuvable." });
        return Ok(new { message = "Produit supprimé." });
    }

    // ─── GET /api/products/:id/reviews ───────────────────────
    [HttpGet("{id:int}/reviews")]
    public async Task<IActionResult> GetReviews(int id)
    {
        var ratings = await _db.QueryAsync<RatingRecord>(
            @"SELECT r.IdRating, r.IdUser, r.Rating, r.CommentRating, r.Date,
                     r.TableName, r.IdTable, r.Active,
                     CONCAT(u.FirstName,' ',u.LastName) AS AuthorName
              FROM Ratings r JOIN Users u ON r.IdUser = u.IdUser
              WHERE r.TableName = 'Deals' AND r.IdTable = @Id AND r.Active = 1
              ORDER BY r.Date DESC",
            new { Id = id }
        );
        return Ok(ratings.Select(r => new
        {
            id      = r.IdRating,
            user_id = r.IdUser,
            rating  = r.Rating,
            comment = r.CommentRating,
            date    = r.Date,
            author  = r.AuthorName
        }));
    }

    // ─── POST /api/products/:id/reviews ──────────────────────
    [HttpPost("{id:int}/reviews")]
    [Authorize]
    public async Task<IActionResult> AddReview(int id, [FromBody] RatingRequest body)
    {
        if (body.Rating < 1 || body.Rating > 5)
            return BadRequest(new { message = "Note entre 1 et 5 requise." });

        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var today  = DateTime.Now.ToString("yyyy-MM-dd");

        var rating = await _db.QueryFirstOrDefaultAsync<RatingRecord>(
            @"INSERT INTO Ratings (IdUser, Rating, CommentRating, Date, TableName, IdTable, Active)
              OUTPUT INSERTED.*
              VALUES (@IdUser, @RatingVal, @Comment, @Date, 'Deals', @IdTable, 1)",
            new { IdUser = userId, RatingVal = body.Rating, Comment = body.Comment, Date = today, IdTable = id }
        );
        return StatusCode(201, new
        {
            id      = rating?.IdRating,
            user_id = rating?.IdUser,
            rating  = rating?.Rating,
            comment = rating?.CommentRating
        });
    }

    // ─── DELETE /api/products/:id/reviews/:reviewId ──────────
    [HttpDelete("{id:int}/reviews/{reviewId:long}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(int id, long reviewId)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var role   = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "user";

        // Admin can delete any review; users can only delete their own
        var whereClause = role == "admin"
            ? "IdRating = @ReviewId AND TableName = 'Deals' AND IdTable = @DealId"
            : "IdRating = @ReviewId AND IdUser = @UserId AND TableName = 'Deals' AND IdTable = @DealId";

        var rows = await _db.ExecuteAsync(
            $"UPDATE Ratings SET Active = 0 WHERE {whereClause}",
            new { ReviewId = reviewId, UserId = userId, DealId = id });

        if (rows == 0) return NotFound(new { message = "Avis introuvable ou accès refusé." });
        return Ok(new { message = "Avis supprimé." });
    }

    // ─── Helpers ─────────────────────────────────────────────
    private static object MapDeal(Deal d) => new
    {
        id             = d.IdDeal,
        name           = d.TitleDeal,
        description    = d.DescriptionDeal,
        details        = d.DetailsDeal,
        price          = ParsePrice(d.PriceDeal),
        price_raw      = d.PriceDeal,
        discount       = d.DiscountDeal,
        stock          = TryParseInt(d.Quantity),
        image          = d.ImageDeal,
        image_url      = d.ImageDeal,
        category       = d.CategoryName,
        category_name  = d.CategoryName,
        category_id    = d.IdCateg,
        vendor         = d.ShopName ?? d.VendorName,
        vendor_name    = d.VendorName,
        shop_name      = d.ShopName,
        vendor_id      = d.IdUser,
        avg_rating     = d.AvgRating ?? 0,
        review_count   = d.ReviewCount ?? 0,
        rating         = d.AvgRating ?? 0,
        review_count_n = d.ReviewCount ?? 0,
        status         = d.Active == 1 ? "actif" : "inactif",
        approval_status = d.Active == 1 ? "approved" : "pending",
        brand          = d.Brand,
        colors         = d.Colors,
        location       = d.LocationDeal,
        date_end       = d.DateEnd,
        start_date     = d.StartDate,
        date_publication = d.DatePublication,
        likes          = d.Likes ?? 0,
    };

    private static decimal ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        var digits = new string(raw.Where(c => c == '.' || c == ',' || char.IsDigit(c)).ToArray())
                         .Replace(',', '.');
        return decimal.TryParse(digits, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static int TryParseInt(string? s) =>
        int.TryParse(s, out var v) ? v : 0;
}
