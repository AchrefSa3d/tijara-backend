using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly DbService _db;
    public CategoriesController(DbService db) => _db = db;

    // ─── GET /api/categories  (public) ───────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cats = await _db.QueryAsync<Category>(
            @"SELECT c.*, t.Title AS TypeTitle
              FROM Categories c
              LEFT JOIN TypeCategory t ON c.idtypecat = t.Idtypecat
              WHERE c.Active = 1
              ORDER BY c.TitleFr"
        );
        return Ok(cats.Select(MapCategory));
    }

    // ─── GET /api/categories/all  (admin — includes inactive) ────
    [HttpGet("all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllAdmin()
    {
        var cats = await _db.QueryAsync<Category>(
            @"SELECT c.*, t.Title AS TypeTitle
              FROM Categories c
              LEFT JOIN TypeCategory t ON c.idtypecat = t.Idtypecat
              ORDER BY c.IdCateg"
        );
        return Ok(cats.Select(MapCategory));
    }

    // ─── GET /api/categories/types ────────────────────────────────
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes()
    {
        var types = await _db.QueryAsync<TypeCategory>("SELECT * FROM TypeCategory ORDER BY Idtypecat");
        return Ok(types.Select(t => new { id = t.Idtypecat, name = t.Title }));
    }

    // ─── GET /api/categories/:id ──────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var cat = await _db.QueryFirstOrDefaultAsync<Category>(
            @"SELECT c.*, t.Title AS TypeTitle
              FROM Categories c
              LEFT JOIN TypeCategory t ON c.idtypecat = t.Idtypecat
              WHERE c.IdCateg = @Id",
            new { Id = id });
        if (cat == null) return NotFound(new { message = "Catégorie introuvable." });
        return Ok(MapCategory(cat));
    }

    // ─── POST /api/categories  (admin) ───────────────────────────
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CategoryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NameFr) && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Nom de la catégorie requis." });

        var nameFr = req.NameFr?.Trim() ?? req.Name?.Trim();
        var nameEn = req.NameEn?.Trim() ?? nameFr;
        var nameAr = req.NameAr?.Trim();

        var cat = await _db.QueryFirstOrDefaultAsync<Category>(
            @"INSERT INTO Categories (TitleFr, TitleEn, TitleAr, Description, Image, idtypecat, Active)
              OUTPUT INSERTED.*
              VALUES (@TitleFr, @TitleEn, @TitleAr, @Description, @Image, @TypeId, 1)",
            new {
                TitleFr     = nameFr,
                TitleEn     = nameEn,
                TitleAr     = nameAr,
                Description = req.Description?.Trim(),
                Image       = req.Image?.Trim(),
                TypeId      = req.TypeId ?? req.IdTypecat
            }
        );
        return StatusCode(201, MapCategory(cat!));
    }

    // ─── PUT /api/categories/:id  (admin) ────────────────────────
    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest req)
    {
        var nameFr = req.NameFr?.Trim() ?? req.Name?.Trim();

        var cat = await _db.QueryFirstOrDefaultAsync<Category>(
            @"UPDATE Categories
              SET TitleFr     = COALESCE(@TitleFr,     TitleFr),
                  TitleEn     = COALESCE(@TitleEn,     TitleEn),
                  TitleAr     = COALESCE(@TitleAr,     TitleAr),
                  Description = COALESCE(@Description, Description),
                  Image       = COALESCE(@Image,       Image),
                  idtypecat   = COALESCE(@TypeId,      idtypecat)
              OUTPUT INSERTED.*
              WHERE IdCateg = @Id",
            new {
                Id          = id,
                TitleFr     = nameFr,
                TitleEn     = req.NameEn?.Trim(),
                TitleAr     = req.NameAr?.Trim(),
                Description = req.Description?.Trim(),
                Image       = req.Image?.Trim(),
                TypeId      = req.TypeId ?? req.IdTypecat
            }
        );
        if (cat == null) return NotFound(new { message = "Catégorie introuvable." });
        return Ok(MapCategory(cat));
    }

    // ─── PATCH /api/categories/:id/toggle  (admin) ───────────────
    [HttpPatch("{id:int}/toggle")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Toggle(int id)
    {
        var rows = await _db.ExecuteAsync(
            "UPDATE Categories SET Active = CASE WHEN Active=1 THEN 0 ELSE 1 END WHERE IdCateg=@Id",
            new { Id = id });
        if (rows == 0) return NotFound(new { message = "Catégorie introuvable." });
        var active = await _db.ExecuteScalarAsync<int>("SELECT Active FROM Categories WHERE IdCateg=@Id", new { Id = id });
        return Ok(new { message = "Statut modifié.", active = active == 1 });
    }

    // ─── DELETE /api/categories/:id  (admin) ─────────────────────
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        // Soft delete — just deactivate
        var rows = await _db.ExecuteAsync(
            "UPDATE Categories SET Active=0 WHERE IdCateg=@Id",
            new { Id = id });
        if (rows == 0) return NotFound(new { message = "Catégorie introuvable." });
        return Ok(new { message = "Catégorie désactivée." });
    }

    // ─── Mapper ───────────────────────────────────────────────────
    private static object MapCategory(Category c) => new
    {
        id          = c.IdCateg,
        name        = c.TitleFr ?? c.TitleEn ?? c.TitleAr,
        name_fr     = c.TitleFr,
        name_en     = c.TitleEn,
        name_ar     = c.TitleAr,
        slug        = (c.TitleFr ?? c.TitleEn ?? "").ToLower().Replace(" ", "-"),
        description = c.Description,
        image       = c.Image,
        type_id     = c.Idtypecat,
        type_title  = c.TypeTitle,
        active      = c.Active == 1,
    };
}

// ─── Request DTO ─────────────────────────────────────────────────────────
public class CategoryRequest
{
    public string? Name        { get; set; }   // alias
    public string? NameFr      { get; set; }
    public string? NameEn      { get; set; }
    public string? NameAr      { get; set; }
    public string? Description { get; set; }
    public string? Image       { get; set; }
    public int?    TypeId      { get; set; }
    public int?    IdTypecat   { get; set; }   // alias
}
