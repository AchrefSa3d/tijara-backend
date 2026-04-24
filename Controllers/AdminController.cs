using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly DbService _db;
    public AdminController(DbService db) => _db = db;

    // ─── GET /api/admin/stats ─────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var users    = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE IdRole=2 AND Active=1");
        var vendors  = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE IdRole=3 AND Active=1");
        var pending  = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE IdRole=3 AND IsVerified=0 AND Active=1");
        var deals    = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Deals WHERE active=1");
        var orders   = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders WHERE Active=1");
        var reports  = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Reports WHERE State=0");
        var ads      = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Ads WHERE Active=1");

        return Ok(new
        {
            users,
            vendors,
            pending_vendors  = pending,
            products         = deals,
            orders,
            open_reclamations = reports,
            ads,
            pending_products = 0,
            revenue          = 0,
        });
    }

    // ─── GET /api/admin/users ─────────────────────────────────
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _db.QueryAsync<User>(
            @"SELECT u.IdUser, u.Email, u.FirstName, u.LastName, u.Username,
                     u.Telephone, u.IsVerified, u.Active, u.CreationDate, u.IdRole,
                     r.RoleUser
              FROM Users u LEFT JOIN Roles r ON u.IdRole = r.IdRole
              WHERE u.IdRole = 2
              ORDER BY u.IdUser DESC"
        );
        return Ok(users.Select(u => BuildUserResponse(u)));
    }

    // ─── GET /api/admin/vendors ───────────────────────────────
    [HttpGet("vendors")]
    public async Task<IActionResult> GetAllVendors()
    {
        var vendors = await _db.QueryAsync<User>(
            @"SELECT u.IdUser, u.Email, u.FirstName, u.LastName, u.Username,
                     u.Telephone, u.IsVerified, u.IsBusinessAccount, u.Active, u.CreationDate,
                     u.IdRole, r.RoleUser
              FROM Users u LEFT JOIN Roles r ON u.IdRole = r.IdRole
              WHERE u.IdRole = 3
              ORDER BY u.IdUser DESC"
        );
        return Ok(vendors.Select(u => BuildUserResponse(u)));
    }

    // ─── GET /api/admin/vendors/pending ──────────────────────
    [HttpGet("vendors/pending")]
    public async Task<IActionResult> GetPendingVendors()
    {
        var vendors = await _db.QueryAsync<User>(
            @"SELECT IdUser, Email, FirstName, LastName, Username, Telephone, Active, CreationDate
              FROM Users WHERE IdRole=3 AND IsVerified=0 AND Active=1
              ORDER BY IdUser DESC"
        );
        return Ok(vendors.Select(u => BuildUserResponse(u)));
    }

    // ─── GET /api/admin/vendors/:id ───────────────────────────
    [HttpGet("vendors/{id:long}")]
    public async Task<IActionResult> GetVendorDetails(long id)
    {
        var vendor = await _db.QueryFirstOrDefaultAsync<User>(
            @"SELECT IdUser, Email, FirstName, LastName, Username, Telephone,
                     IsVerified, Active, CreationDate
              FROM Users WHERE IdUser = @Id AND IdRole = 3",
            new { Id = id }
        );
        if (vendor == null) return NotFound(new { message = "Vendeur introuvable." });

        var deals = await _db.QueryAsync<Deal>(
            @"SELECT d.IdDeal, d.titleDeal, d.priceDeal, d.active, d.datePublication,
                     c.TitleFr AS CategoryName
              FROM Deals d LEFT JOIN Categories c ON d.idCateg = c.IdCateg
              WHERE d.idUser = @Id ORDER BY d.IdDeal DESC",
            new { Id = id }
        );

        var stats = await _db.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT COUNT(*) AS total_deals, SUM(CASE WHEN active=1 THEN 1 ELSE 0 END) AS active_deals
              FROM Deals WHERE idUser = @Id",
            new { Id = id }
        );

        return Ok(new { vendor = BuildUserResponse(vendor), products = deals, stats });
    }

    // ─── PATCH /api/admin/vendors/:id/approve ────────────────
    [HttpPatch("vendors/{id:long}/approve")]
    public async Task<IActionResult> ApproveVendor(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Users SET IsVerified=1 WHERE IdUser=@Id AND IdRole=3",
            new { Id = id }
        );
        if (rows == 0) return NotFound(new { message = "Vendeur introuvable." });
        return Ok(new { message = "Compte vendeur approuvé." });
    }

    // ─── PATCH /api/admin/vendors/:id/reject ─────────────────
    [HttpPatch("vendors/{id:long}/reject")]
    public async Task<IActionResult> RejectVendor(long id, [FromBody] ReasonRequest req)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Users SET IsVerified=0, Active=0 WHERE IdUser=@Id AND IdRole=3",
            new { Id = id }
        );
        if (rows == 0) return NotFound(new { message = "Vendeur introuvable." });
        return Ok(new { message = "Compte vendeur rejeté." });
    }

    // ─── PATCH /api/admin/users/:id/toggle ───────────────────
    [HttpPatch("users/{id:long}/toggle")]
    public async Task<IActionResult> ToggleUser(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Users SET Active = CASE WHEN Active=1 THEN 0 ELSE 1 END WHERE IdUser=@Id",
            new { Id = id }
        );
        if (rows == 0) return NotFound(new { message = "Utilisateur introuvable." });
        return Ok(new { message = "Statut modifié." });
    }

    // ─── GET /api/admin/orders ────────────────────────────────
    [HttpGet("orders")]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _db.QueryAsync<Order>(
            @"SELECT o.IdOrder, o.IdUser, o.IdDeal, o.DateTimeCommand, o.Active, o.IdState,
                     CONCAT(u.FirstName,' ',u.LastName) AS ClientName,
                     u.Email AS ClientEmail,
                     d.titleDeal AS DealTitle,
                     d.priceDeal AS DealPrice,
                     CONCAT(uv.FirstName,' ',uv.LastName) AS VendorName
              FROM Orders o
              LEFT JOIN Users u ON o.IdUser = u.IdUser
              LEFT JOIN Deals d ON o.IdDeal = d.IdDeal
              LEFT JOIN Users uv ON d.idUser = uv.IdUser
              ORDER BY o.IdOrder DESC"
        );
        return Ok(orders.Select(MapOrder));
    }

    // ─── PATCH /api/admin/orders/:id/status ──────────────────
    [HttpPatch("orders/{id:long}/status")]
    public async Task<IActionResult> UpdateOrderStatus(long id, [FromBody] StatusRequest req)
    {
        // Active: 1=actif, 0=annulé, 2=livré, 3=en cours
        int? statusVal = req.Status switch
        {
            "pending"   or "En attente" => 1,
            "confirmed" or "Confirmée"  => 3,
            "delivered" or "Livrée"     => 2,
            "cancelled" or "Annulée"    => 0,
            _ => null
        };
        if (statusVal == null) return BadRequest(new { message = "Statut invalide." });

        var rows = await _db.ExecuteAsync(
            "UPDATE Orders SET Active=@Status WHERE IdOrder=@Id",
            new { Status = statusVal, Id = id }
        );
        if (rows == 0) return NotFound(new { message = "Commande introuvable." });
        return Ok(new { message = "Statut mis à jour.", order = new { id, status = req.Status } });
    }

    // ─── GET /api/admin/all-products ─────────────────────────
    [HttpGet("all-products")]
    public async Task<IActionResult> GetAllProducts([FromQuery] string? approval_status)
    {
        var activeFilter = approval_status == "pending" ? "d.active=0" :
                           approval_status == "approved" ? "d.active=1" : "1=1";

        var deals = await _db.QueryAsync<Deal>(
            $@"SELECT d.*,
                      c.TitleFr AS CategoryName,
                      CONCAT(u.FirstName,' ',u.LastName) AS VendorName,
                      u.Username AS ShopName
               FROM Deals d
               LEFT JOIN Categories c ON d.idCateg = c.IdCateg
               LEFT JOIN Users u      ON d.idUser  = u.IdUser
               WHERE {activeFilter}
               ORDER BY d.IdDeal DESC"
        );

        return Ok(deals.Select(d => new
        {
            id              = d.IdDeal,
            name            = d.TitleDeal,
            price           = d.PriceDeal,
            image_url       = d.ImageDeal,
            category        = d.CategoryName,
            vendor_name     = d.VendorName,
            shop_name       = d.ShopName,
            approval_status = d.Active == 1 ? "approved" : "pending",
            is_active       = d.Active == 1,
        }));
    }

    // ─── PATCH /api/admin/products/:id/approve ────────────────
    [HttpPatch("products/{id:int}/approve")]
    public async Task<IActionResult> ApproveProduct(int id)
    {
        // Get deal info to send notifications
        var deal = await _db.QueryFirstOrDefaultAsync<Deal>(
            "SELECT IdDeal, idUser, titleDeal FROM Deals WHERE IdDeal=@Id",
            new { Id = id });

        var rows = await _db.ExecuteAsync(
            "UPDATE Deals SET active=1 WHERE IdDeal=@Id", new { Id = id });
        if (rows == 0) return NotFound(new { message = "Produit introuvable." });

        if (deal?.IdUser != null)
        {
            // 1. Notify the vendor
            await NotificationsController.CreateAsync(_db, (long)deal.IdUser,
                "order_update", "Produit approuvé",
                $"Votre produit \"{deal.TitleDeal}\" est maintenant visible sur la boutique.",
                $"/shop/product-detail/{id}", id);

            // 2. Notify followers of this vendor
            var vendorName = await _db.ExecuteScalarAsync<string>(
                "SELECT CONCAT(FirstName,' ',LastName) FROM Users WHERE IdUser=@Id",
                new { Id = deal.IdUser });

            var followers = await _db.QueryAsync<UserFollow>(
                "SELECT IdUser FROM UserFollows WHERE IdVendor=@VendorId",
                new { VendorId = deal.IdUser });

            foreach (var f in followers)
            {
                await NotificationsController.CreateAsync(_db, f.IdUser,
                    "new_product", "Nouveau produit disponible",
                    $"{vendorName} a publié un nouveau produit : {deal.TitleDeal}",
                    $"/shop/product-detail/{id}", id);
            }
        }

        return Ok(new { message = "Produit approuvé." });
    }

    // ─── PATCH /api/admin/products/:id/reject ────────────────
    [HttpPatch("products/{id:int}/reject")]
    public async Task<IActionResult> RejectProduct(int id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Deals SET active=0 WHERE IdDeal=@Id", new { Id = id });
        if (rows == 0) return NotFound(new { message = "Produit introuvable." });
        return Ok(new { message = "Produit rejeté." });
    }

    // ─── GET /api/admin/reports ───────────────────────────────
    [HttpGet("reports")]
    public async Task<IActionResult> GetReports([FromQuery] int? state)
    {
        var where = state.HasValue ? "r.State = @State" : "1=1";
        var list = await _db.QueryAsync<Report>(
            $@"SELECT r.*,
                      CONCAT(u.FirstName,' ',u.LastName) AS ClientName,
                      c.TitleCauseFr AS CauseTitle
               FROM Reports r
               LEFT JOIN Users u         ON r.IdUser        = u.IdUser
               LEFT JOIN CausesReports c ON r.IdCauseReport = c.IdCauseReport
               WHERE {where}
               ORDER BY r.IdReport DESC",
            new { State = state }
        );
        return Ok(list);
    }

    // ─── PATCH /api/admin/reports/:id ────────────────────────
    [HttpPatch("reports/{id:int}/close")]
    public async Task<IActionResult> CloseReport(int id)
    {
        await _db.ExecuteAsync("UPDATE Reports SET State=1 WHERE IdReport=@Id", new { Id = id });
        return Ok(new { message = "Signalement clôturé." });
    }

    // ─── GET /api/admin/annonces ──────────────────────────────
    [HttpGet("annonces")]
    public async Task<IActionResult> GetAllAnnonces([FromQuery] string? status)
    {
        // status: "pending" → Active=0, "approved" → Active=1, null → all
        int? activeFilter = status == "pending" ? 0 : status == "approved" ? 1 : (int?)null;
        var where = activeFilter.HasValue ? "a.Active = @Active" : "1=1";

        var list = await _db.QueryAsync<Ad>(
            $@"SELECT a.*,
                      CONCAT(u.FirstName,' ',u.LastName) AS AuthorName,
                      c.TitleFr AS CategoryName
               FROM Ads a
               LEFT JOIN Users u      ON a.IdUser  = u.IdUser
               LEFT JOIN Categories c ON a.IdCateg = c.IdCateg
               WHERE {where} ORDER BY a.IdAd DESC",
            new { Active = activeFilter }
        );
        return Ok(list.Select(a => new
        {
            id          = a.IdAd,
            title       = a.TitleAd,
            description = a.DescriptionAd,
            price       = a.PriceAd,
            image       = a.ImageAd,
            category    = a.CategoryName,
            category_id = a.IdCateg,
            user_id     = a.IdUser,
            author_name = a.AuthorName,
            active      = a.Active == 1,
            status      = a.Active == 1 ? "approved" : "pending",
            date        = a.DatePublication,
        }));
    }

    // ─── PATCH /api/admin/annonces/:id/approve ────────────────
    [HttpPatch("annonces/{id:long}/approve")]
    public async Task<IActionResult> ApproveAnnonce(long id)
    {
        await _db.ExecuteAsync("UPDATE Ads SET Active=1 WHERE IdAd=@Id", new { Id = id });
        return Ok(new { message = "Annonce approuvée." });
    }

    // ─── PATCH /api/admin/annonces/:id/reject ────────────────
    [HttpPatch("annonces/{id:long}/reject")]
    public async Task<IActionResult> RejectAnnonce(long id)
    {
        await _db.ExecuteAsync("UPDATE Ads SET Active=0 WHERE IdAd=@Id", new { Id = id });
        return Ok(new { message = "Annonce rejetée." });
    }

    // ─── Helpers ─────────────────────────────────────────────
    private static object BuildUserResponse(User u) => new
    {
        id           = u.IdUser,
        email        = u.Email,
        role         = AuthController.MapRole(u.IdRole ?? 2),
        first_name   = u.FirstName,
        last_name    = u.LastName,
        shop_name    = u.Username,
        phone        = u.Telephone,
        is_approved  = (u.IsVerified ?? 0) == 1,
        is_active    = (u.Active ?? 0) == 1,
        created_at   = u.CreationDate,
    };

    // ═══════════════════════════════════════════════════════════════════
    // ADMIN SETTINGS (key/value)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var rows = await _db.QueryAsync<AdminSettingRow>(
            "SELECT [Key], [Value] FROM AdminSettings");
        var dict = rows.ToDictionary(r => r.Key, r => (object?)r.Value);
        return Ok(dict);
    }

    public class SettingsUpdateRequest
    {
        public Dictionary<string, string?> Settings { get; set; } = new();
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] SettingsUpdateRequest req)
    {
        if (req?.Settings == null || req.Settings.Count == 0)
            return BadRequest(new { message = "Aucun paramètre à mettre à jour." });

        foreach (var kv in req.Settings)
        {
            await _db.ExecuteAsync(@"
                MERGE AdminSettings AS tgt
                USING (SELECT @Key AS [Key]) AS src
                ON tgt.[Key] = src.[Key]
                WHEN MATCHED THEN UPDATE SET [Value] = @Value, UpdatedAt = GETDATE()
                WHEN NOT MATCHED THEN INSERT ([Key],[Value]) VALUES (@Key, @Value);",
                new { Key = kv.Key, Value = kv.Value });
        }
        return Ok(new { message = "Paramètres mis à jour.", count = req.Settings.Count });
    }

    // ═══════════════════════════════════════════════════════════════════
    // POINT PACKETS (CRUD)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("point-packets")]
    public async Task<IActionResult> GetPointPackets()
    {
        var packets = await _db.QueryAsync<PointPacket>(
            "SELECT * FROM PointPackets ORDER BY IdPacket DESC");
        return Ok(packets);
    }

    [HttpPost("point-packets")]
    public async Task<IActionResult> CreatePointPacket([FromBody] PointPacketRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Le titre est requis." });

        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO PointPackets (Title, Description, PointsCount, Price, Discount, Active)
            OUTPUT INSERTED.IdPacket
            VALUES (@Title, @Description, @PointsCount, @Price, @Discount, @Active);",
            new {
                req.Title, req.Description, req.PointsCount,
                req.Price, req.Discount, req.Active
            });

        var created = await _db.QueryFirstOrDefaultAsync<PointPacket>(
            "SELECT * FROM PointPackets WHERE IdPacket=@Id", new { Id = id });
        return Ok(created);
    }

    [HttpPut("point-packets/{id}")]
    public async Task<IActionResult> UpdatePointPacket(long id, [FromBody] PointPacketRequest req)
    {
        var exists = await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PointPackets WHERE IdPacket=@Id", new { Id = id });
        if (exists == 0) return NotFound(new { message = "Packet introuvable." });

        await _db.ExecuteAsync(@"
            UPDATE PointPackets SET
                Title=@Title, Description=@Description, PointsCount=@PointsCount,
                Price=@Price, Discount=@Discount, Active=@Active
            WHERE IdPacket=@Id;",
            new {
                Id = id, req.Title, req.Description, req.PointsCount,
                req.Price, req.Discount, req.Active
            });

        var updated = await _db.QueryFirstOrDefaultAsync<PointPacket>(
            "SELECT * FROM PointPackets WHERE IdPacket=@Id", new { Id = id });
        return Ok(updated);
    }

    [HttpDelete("point-packets/{id}")]
    public async Task<IActionResult> DeletePointPacket(long id)
    {
        var rows = await _db.ExecuteAsync(
            "DELETE FROM PointPackets WHERE IdPacket=@Id", new { Id = id });
        if (rows == 0) return NotFound(new { message = "Packet introuvable." });
        return Ok(new { message = "Packet supprimé." });
    }

    // ═══════════════════════════════════════════════════════════════════
    // LOT 1 — CATALOG & MODERATION CRUD
    // ═══════════════════════════════════════════════════════════════════

    // ── Brands ─────────────────────────────────────────────────────────
    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands() =>
        Ok(await _db.QueryAsync<Brand>("SELECT * FROM Brands ORDER BY IdBrand DESC"));

    [HttpPost("brands")]
    public async Task<IActionResult> CreateBrand([FromBody] Brand b)
    {
        if (string.IsNullOrWhiteSpace(b.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Brands (Title, Description, Image, Active)
            OUTPUT INSERTED.IdBrand VALUES (@Title,@Description,@Image,@Active)", b);
        return Ok(await _db.QueryFirstOrDefaultAsync<Brand>("SELECT * FROM Brands WHERE IdBrand=@Id", new { Id = id }));
    }

    [HttpPut("brands/{id}")]
    public async Task<IActionResult> UpdateBrand(long id, [FromBody] Brand b)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Brands SET Title=@Title, Description=@Description, Image=@Image, Active=@Active
            WHERE IdBrand=@Id", new { Id = id, b.Title, b.Description, b.Image, b.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Brand>("SELECT * FROM Brands WHERE IdBrand=@Id", new { Id = id }));
    }

    [HttpDelete("brands/{id}")]
    public async Task<IActionResult> DeleteBrand(long id) =>
        await _db.ExecuteAsync("DELETE FROM Brands WHERE IdBrand=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Countries ──────────────────────────────────────────────────────
    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries() =>
        Ok(await _db.QueryAsync<Country>("SELECT * FROM Countries ORDER BY Title"));

    [HttpPost("countries")]
    public async Task<IActionResult> CreateCountry([FromBody] Country c)
    {
        if (string.IsNullOrWhiteSpace(c.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Countries (Title, Flag, Code, PhoneCode, Active)
            OUTPUT INSERTED.IdCountry VALUES (@Title,@Flag,@Code,@PhoneCode,@Active)", c);
        return Ok(await _db.QueryFirstOrDefaultAsync<Country>("SELECT * FROM Countries WHERE IdCountry=@Id", new { Id = id }));
    }

    [HttpPut("countries/{id}")]
    public async Task<IActionResult> UpdateCountry(long id, [FromBody] Country c)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Countries SET Title=@Title, Flag=@Flag, Code=@Code, PhoneCode=@PhoneCode, Active=@Active
            WHERE IdCountry=@Id", new { Id = id, c.Title, c.Flag, c.Code, c.PhoneCode, c.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Country>("SELECT * FROM Countries WHERE IdCountry=@Id", new { Id = id }));
    }

    [HttpDelete("countries/{id}")]
    public async Task<IActionResult> DeleteCountry(long id) =>
        await _db.ExecuteAsync("DELETE FROM Countries WHERE IdCountry=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Cities ─────────────────────────────────────────────────────────
    [HttpGet("cities")]
    public async Task<IActionResult> GetCities() =>
        Ok(await _db.QueryAsync<City>("SELECT * FROM Cities ORDER BY Title"));

    [HttpPost("cities")]
    public async Task<IActionResult> CreateCity([FromBody] City c)
    {
        if (string.IsNullOrWhiteSpace(c.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Cities (Title, IdCountry, TitleEn, TitleAr, Image, Active)
            OUTPUT INSERTED.IdCity VALUES (@Title,@IdCountry,@TitleEn,@TitleAr,@Image,@Active)", c);
        return Ok(await _db.QueryFirstOrDefaultAsync<City>("SELECT * FROM Cities WHERE IdCity=@Id", new { Id = id }));
    }

    [HttpPut("cities/{id}")]
    public async Task<IActionResult> UpdateCity(long id, [FromBody] City c)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Cities SET Title=@Title, IdCountry=@IdCountry, TitleEn=@TitleEn, TitleAr=@TitleAr, Image=@Image, Active=@Active
            WHERE IdCity=@Id", new { Id = id, c.Title, c.IdCountry, c.TitleEn, c.TitleAr, c.Image, c.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<City>("SELECT * FROM Cities WHERE IdCity=@Id", new { Id = id }));
    }

    [HttpDelete("cities/{id}")]
    public async Task<IActionResult> DeleteCity(long id) =>
        await _db.ExecuteAsync("DELETE FROM Cities WHERE IdCity=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Causes ─────────────────────────────────────────────────────────
    [HttpGet("causes")]
    public async Task<IActionResult> GetCauses() =>
        Ok(await _db.QueryAsync<Cause>("SELECT * FROM Causes ORDER BY IdCause DESC"));

    [HttpPost("causes")]
    public async Task<IActionResult> CreateCause([FromBody] Cause c)
    {
        if (string.IsNullOrWhiteSpace(c.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Causes (Title, Description, Email, Type, Active)
            OUTPUT INSERTED.IdCause VALUES (@Title,@Description,@Email,@Type,@Active)", c);
        return Ok(await _db.QueryFirstOrDefaultAsync<Cause>("SELECT * FROM Causes WHERE IdCause=@Id", new { Id = id }));
    }

    [HttpPut("causes/{id}")]
    public async Task<IActionResult> UpdateCause(long id, [FromBody] Cause c)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Causes SET Title=@Title, Description=@Description, Email=@Email, Type=@Type, Active=@Active
            WHERE IdCause=@Id", new { Id = id, c.Title, c.Description, c.Email, c.Type, c.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Cause>("SELECT * FROM Causes WHERE IdCause=@Id", new { Id = id }));
    }

    [HttpDelete("causes/{id}")]
    public async Task<IActionResult> DeleteCause(long id) =>
        await _db.ExecuteAsync("DELETE FROM Causes WHERE IdCause=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Coupons ────────────────────────────────────────────────────────
    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons() =>
        Ok(await _db.QueryAsync<Coupon>("SELECT * FROM Coupons ORDER BY IdCoupon DESC"));

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] Coupon c)
    {
        if (string.IsNullOrWhiteSpace(c.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Coupons (Title, Description, DateStart, DateEnd, Price, NumberOfCoupon, Used, Active)
            OUTPUT INSERTED.IdCoupon VALUES (@Title,@Description,@DateStart,@DateEnd,@Price,@NumberOfCoupon,@Used,@Active)", c);
        return Ok(await _db.QueryFirstOrDefaultAsync<Coupon>("SELECT * FROM Coupons WHERE IdCoupon=@Id", new { Id = id }));
    }

    [HttpPut("coupons/{id}")]
    public async Task<IActionResult> UpdateCoupon(long id, [FromBody] Coupon c)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Coupons SET Title=@Title, Description=@Description, DateStart=@DateStart, DateEnd=@DateEnd,
                Price=@Price, NumberOfCoupon=@NumberOfCoupon, Active=@Active
            WHERE IdCoupon=@Id", new { Id = id, c.Title, c.Description, c.DateStart, c.DateEnd, c.Price, c.NumberOfCoupon, c.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Coupon>("SELECT * FROM Coupons WHERE IdCoupon=@Id", new { Id = id }));
    }

    [HttpDelete("coupons/{id}")]
    public async Task<IActionResult> DeleteCoupon(long id) =>
        await _db.ExecuteAsync("DELETE FROM Coupons WHERE IdCoupon=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Prizes ─────────────────────────────────────────────────────────
    [HttpGet("prizes")]
    public async Task<IActionResult> GetPrizes() =>
        Ok(await _db.QueryAsync<Prize>("SELECT * FROM Prizes ORDER BY IdPrize DESC"));

    [HttpPost("prizes")]
    public async Task<IActionResult> CreatePrize([FromBody] Prize p)
    {
        if (string.IsNullOrWhiteSpace(p.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Prizes (Title, Description, Image, DatePrize, IdUser, Active)
            OUTPUT INSERTED.IdPrize VALUES (@Title,@Description,@Image,@DatePrize,@IdUser,@Active)", p);
        return Ok(await _db.QueryFirstOrDefaultAsync<Prize>("SELECT * FROM Prizes WHERE IdPrize=@Id", new { Id = id }));
    }

    [HttpPut("prizes/{id}")]
    public async Task<IActionResult> UpdatePrize(long id, [FromBody] Prize p)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Prizes SET Title=@Title, Description=@Description, Image=@Image, DatePrize=@DatePrize, IdUser=@IdUser, Active=@Active
            WHERE IdPrize=@Id", new { Id = id, p.Title, p.Description, p.Image, p.DatePrize, p.IdUser, p.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Prize>("SELECT * FROM Prizes WHERE IdPrize=@Id", new { Id = id }));
    }

    [HttpDelete("prizes/{id}")]
    public async Task<IActionResult> DeletePrize(long id) =>
        await _db.ExecuteAsync("DELETE FROM Prizes WHERE IdPrize=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ── Boost Ads Packs ────────────────────────────────────────────────
    [HttpGet("boost-packs")]
    public async Task<IActionResult> GetBoostPacks() =>
        Ok(await _db.QueryAsync<BoostPack>("SELECT * FROM BoostAdsPacks ORDER BY IdBoost DESC"));

    [HttpPost("boost-packs")]
    public async Task<IActionResult> CreateBoostPack([FromBody] BoostPack p)
    {
        if (string.IsNullOrWhiteSpace(p.Title)) return BadRequest(new { message = "Titre requis." });
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO BoostAdsPacks (Title, Price, Discount, MaxDuration, Sliders, SideBar, Footer, RelatedPost, FirstLogin, OrdersCount, Links, Active)
            OUTPUT INSERTED.IdBoost
            VALUES (@Title,@Price,@Discount,@MaxDuration,@Sliders,@SideBar,@Footer,@RelatedPost,@FirstLogin,@OrdersCount,@Links,@Active)", p);
        return Ok(await _db.QueryFirstOrDefaultAsync<BoostPack>("SELECT * FROM BoostAdsPacks WHERE IdBoost=@Id", new { Id = id }));
    }

    [HttpPut("boost-packs/{id}")]
    public async Task<IActionResult> UpdateBoostPack(long id, [FromBody] BoostPack p)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE BoostAdsPacks SET Title=@Title, Price=@Price, Discount=@Discount, MaxDuration=@MaxDuration,
                Sliders=@Sliders, SideBar=@SideBar, Footer=@Footer, RelatedPost=@RelatedPost,
                FirstLogin=@FirstLogin, OrdersCount=@OrdersCount, Links=@Links, Active=@Active
            WHERE IdBoost=@Id",
            new { Id = id, p.Title, p.Price, p.Discount, p.MaxDuration, p.Sliders, p.SideBar, p.Footer, p.RelatedPost, p.FirstLogin, p.OrdersCount, p.Links, p.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<BoostPack>("SELECT * FROM BoostAdsPacks WHERE IdBoost=@Id", new { Id = id }));
    }

    [HttpDelete("boost-packs/{id}")]
    public async Task<IActionResult> DeleteBoostPack(long id) =>
        await _db.ExecuteAsync("DELETE FROM BoostAdsPacks WHERE IdBoost=@Id", new { Id = id }) == 0
            ? NotFound() : Ok(new { message = "Supprimé." });

    // ═══════════════════════════════════════════════════════════════════
    // LOT 2 — WINNERS (CRUD)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("winners")]
    public async Task<IActionResult> GetWinners([FromQuery] string? search)
    {
        var where = string.IsNullOrWhiteSpace(search)
            ? "1=1"
            : "w.FullName LIKE @q OR w.Email LIKE @q OR p.Title LIKE @q";

        var list = await _db.QueryAsync<Winner>($@"
            SELECT w.*,
                   CONCAT(u.FirstName,' ',u.LastName) AS UserName,
                   p.Title AS PrizeTitle
            FROM Winners w
            LEFT JOIN Users  u ON w.IdUser  = u.IdUser
            LEFT JOIN Prizes p ON w.IdPrize = p.IdPrize
            WHERE {where}
            ORDER BY w.IdWinner DESC",
            new { q = $"%{search}%" });
        return Ok(list);
    }

    [HttpGet("winners/{id:long}")]
    public async Task<IActionResult> GetWinner(long id)
    {
        var w = await _db.QueryFirstOrDefaultAsync<Winner>(@"
            SELECT w.*,
                   CONCAT(u.FirstName,' ',u.LastName) AS UserName,
                   p.Title AS PrizeTitle
            FROM Winners w
            LEFT JOIN Users  u ON w.IdUser  = u.IdUser
            LEFT JOIN Prizes p ON w.IdPrize = p.IdPrize
            WHERE w.IdWinner=@Id", new { Id = id });
        if (w == null) return NotFound();
        return Ok(w);
    }

    [HttpPost("winners")]
    public async Task<IActionResult> CreateWinner([FromBody] WinnerRequest req)
    {
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Winners (IdUser, IdPrize, IdOrder, FullName, Email, Phone, Note, Active)
            OUTPUT INSERTED.IdWinner
            VALUES (@IdUser,@IdPrize,@IdOrder,@FullName,@Email,@Phone,@Note,@Active)", req);
        var created = await _db.QueryFirstOrDefaultAsync<Winner>(
            "SELECT * FROM Winners WHERE IdWinner=@Id", new { Id = id });
        return Ok(created);
    }

    [HttpPut("winners/{id:long}")]
    public async Task<IActionResult> UpdateWinner(long id, [FromBody] WinnerRequest req)
    {
        var ok = await _db.ExecuteAsync(@"
            UPDATE Winners SET IdUser=@IdUser, IdPrize=@IdPrize, IdOrder=@IdOrder,
                FullName=@FullName, Email=@Email, Phone=@Phone, Note=@Note, Active=@Active
            WHERE IdWinner=@Id",
            new { Id = id, req.IdUser, req.IdPrize, req.IdOrder, req.FullName, req.Email, req.Phone, req.Note, req.Active });
        if (ok == 0) return NotFound();
        return Ok(await _db.QueryFirstOrDefaultAsync<Winner>(
            "SELECT * FROM Winners WHERE IdWinner=@Id", new { Id = id }));
    }

    [HttpPatch("winners/{id:long}/toggle")]
    public async Task<IActionResult> ToggleWinner(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Winners SET Active = CASE WHEN Active=1 THEN 0 ELSE 1 END WHERE IdWinner=@Id",
            new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Statut modifié." });
    }

    [HttpDelete("winners/{id:long}")]
    public async Task<IActionResult> DeleteWinner(long id)
    {
        var rows = await _db.ExecuteAsync("DELETE FROM Winners WHERE IdWinner=@Id", new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Supprimé." });
    }

    // ═══════════════════════════════════════════════════════════════════
    // LOT 2 — DEALS MODERATION (dedicated endpoint)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("deals")]
    public async Task<IActionResult> GetAllDeals([FromQuery] string? status)
    {
        int? activeFilter = status == "pending" ? 0 : status == "active" ? 1 : (int?)null;
        var where = activeFilter.HasValue ? "d.active = @Active" : "1=1";

        var deals = await _db.QueryAsync<Deal>($@"
            SELECT d.IdDeal, d.titleDeal, d.descriptionDeal, d.priceDeal, d.discountDeal,
                   d.imageDeal, d.idCateg, d.idUser, d.active, d.datePublication, d.quantity,
                   c.TitleFr AS CategoryName,
                   CONCAT(u.FirstName,' ',u.LastName) AS VendorName,
                   u.Username AS ShopName
            FROM Deals d
            LEFT JOIN Categories c ON d.idCateg = c.IdCateg
            LEFT JOIN Users u      ON d.idUser  = u.IdUser
            WHERE {where}
            ORDER BY d.IdDeal DESC",
            new { Active = activeFilter });

        return Ok(deals.Select(d => new
        {
            id           = d.IdDeal,
            title        = d.TitleDeal,
            description  = d.DescriptionDeal,
            price        = d.PriceDeal,
            discount     = d.DiscountDeal,
            image        = d.ImageDeal,
            category     = d.CategoryName,
            vendor_name  = d.VendorName,
            shop_name    = d.ShopName,
            is_active    = d.Active == 1,
            status       = d.Active == 1 ? "active" : "pending",
            date         = d.DatePublication,
            quantity     = d.Quantity,
        }));
    }

    [HttpPatch("deals/{id:long}/toggle")]
    public async Task<IActionResult> ToggleDeal(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Deals SET active = CASE WHEN active=1 THEN 0 ELSE 1 END WHERE IdDeal=@Id",
            new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Statut modifié." });
    }

    [HttpDelete("deals/{id:long}")]
    public async Task<IActionResult> DeleteDeal(long id)
    {
        var rows = await _db.ExecuteAsync("DELETE FROM Deals WHERE IdDeal=@Id", new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Deal supprimé." });
    }

    // ═══════════════════════════════════════════════════════════════════
    // LOT 2 — ADMIN REVIEWS (lecture + modération)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews([FromQuery] string? type)
    {
        var where = !string.IsNullOrWhiteSpace(type) ? "r.TargetType=@Type" : "1=1";
        var list = await _db.QueryAsync<ReviewRecord>($@"
            SELECT r.*,
                   CONCAT(u.FirstName,' ',u.LastName) AS AuthorName
            FROM Reviews r
            LEFT JOIN Users u ON r.IdUser = u.IdUser
            WHERE {where}
            ORDER BY r.IdReview DESC",
            new { Type = type });
        return Ok(list);
    }

    [HttpPatch("reviews/{id:long}/toggle")]
    public async Task<IActionResult> ToggleReview(long id)
    {
        await _db.ExecuteAsync(
            "UPDATE Reviews SET Active = CASE WHEN Active=1 THEN 0 ELSE 1 END WHERE IdReview=@Id",
            new { Id = id });
        return Ok(new { message = "Statut modifié." });
    }

    [HttpDelete("reviews/{id:long}")]
    public async Task<IActionResult> DeleteReview(long id)
    {
        await _db.ExecuteAsync("DELETE FROM Reviews WHERE IdReview=@Id", new { Id = id });
        return Ok(new { message = "Avis supprimé." });
    }

    // ═══════════════════════════════════════════════════════════════════
    // LOT 2 — USERS MANAGEMENT (enhanced toggle premium/PRO)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("all-users")]
    public async Task<IActionResult> GetAllUsersEnhanced([FromQuery] string? role, [FromQuery] string? premium)
    {
        var conditions = new List<string> { "1=1" };
        if (role == "user")    conditions.Add("u.IdRole=2");
        if (role == "vendor")  conditions.Add("u.IdRole=3");
        if (premium == "true") conditions.Add("u.IsPremuim=1");
        var where = string.Join(" AND ", conditions);

        var users = await _db.QueryAsync<User>($@"
            SELECT u.IdUser, u.Email, u.FirstName, u.LastName, u.Username,
                   u.Telephone, u.IsVerified, u.IsPremuim, u.IsBusinessAccount,
                   u.Active, u.CreationDate, u.IdRole, r.RoleUser
            FROM Users u
            LEFT JOIN Roles r ON u.IdRole = r.IdRole
            WHERE {where}
            ORDER BY u.IdUser DESC",
            new { });
        return Ok(users.Select(u => new
        {
            id              = u.IdUser,
            email           = u.Email,
            first_name      = u.FirstName,
            last_name       = u.LastName,
            username        = u.Username,
            phone           = u.Telephone,
            role            = AuthController.MapRole(u.IdRole ?? 2),
            is_active       = (u.Active ?? 0) == 1,
            is_verified     = (u.IsVerified ?? 0) == 1,
            is_premium      = (u.IsPremuim ?? 0) == 1,
            is_business     = (u.IsBusinessAccount ?? 0) == 1,
            created_at      = u.CreationDate,
        }));
    }

    [HttpPatch("users/{id:long}/toggle-premium")]
    public async Task<IActionResult> TogglePremium(long id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Users SET IsPremuim = CASE WHEN IsPremuim=1 THEN 0 ELSE 1 END WHERE IdUser=@Id",
            new { Id = id });
        if (rows == 0) return NotFound();
        return Ok(new { message = "Statut premium modifié." });
    }

    private static object MapOrder(Order o) => new
    {
        id         = o.IdOrder,
        client     = o.ClientName ?? "Client",
        client_name = o.ClientName,
        email      = o.ClientEmail,
        vendor     = o.VendorName ?? "—",
        product    = o.DealTitle ?? "—",
        total      = o.DealPrice ?? "—",
        date       = o.DateTimeCommand?.ToString("dd/MM/yyyy") ?? "—",
        status     = o.Active switch { 2 => "Livrée", 3 => "Confirmée", 0 => "Annulée", _ => "En attente" },
        api_status = o.Active switch { 2 => "delivered", 3 => "confirmed", 0 => "cancelled", _ => "pending" },
    };
}
