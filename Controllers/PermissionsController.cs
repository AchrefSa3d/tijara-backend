using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize(Roles = "admin")]
public class PermissionsController : ControllerBase
{
    private readonly DbService _db;
    public PermissionsController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.QueryAsync<dynamic>(
            "SELECT IdPermission, IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete FROM Permissions ORDER BY IdRole, Resource");
        return Ok(list);
    }

    [HttpGet("role/{idRole:int}")]
    public async Task<IActionResult> GetByRole(int idRole)
    {
        var list = await _db.QueryAsync<dynamic>(
            "SELECT IdPermission, IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete FROM Permissions WHERE IdRole=@IdRole ORDER BY Resource",
            new { IdRole = idRole });
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body)
    {
        var p = ParseBody(body);
        if (p.IdRole <= 0 || string.IsNullOrWhiteSpace(p.Resource))
            return BadRequest(new { message = "id_role et resource requis." });

        try
        {
            // Upsert by (IdRole, Resource)
            var existing = await _db.QueryFirstOrDefaultAsync<int?>(
                "SELECT IdPermission FROM Permissions WHERE IdRole=@IdRole AND Resource=@Resource",
                new { p.IdRole, p.Resource });

            if (existing.HasValue && existing.Value > 0)
            {
                await _db.ExecuteAsync(@"
                    UPDATE Permissions SET CanRead=@CanRead, CanCreate=@CanCreate,
                                           CanUpdate=@CanUpdate, CanDelete=@CanDelete
                    WHERE IdPermission=@Id",
                    new { p.CanRead, p.CanCreate, p.CanUpdate, p.CanDelete, Id = existing.Value });
                return Ok(new { id_permission = existing.Value, updated = true });
            }

            var id = await _db.ExecuteScalarAsync<int>(@"
                INSERT INTO Permissions (IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete)
                OUTPUT INSERTED.IdPermission
                VALUES (@IdRole, @Resource, @CanRead, @CanCreate, @CanUpdate, @CanDelete)", p);
            return Ok(new { id_permission = id, created = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Permissions.Create] {ex.Message}");
            return StatusCode(500, new { message = "Enregistrement impossible : " + ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] JsonElement body)
    {
        var p = ParseBody(body);
        await _db.ExecuteAsync(@"
            UPDATE Permissions SET CanRead=@CanRead, CanCreate=@CanCreate,
                                   CanUpdate=@CanUpdate, CanDelete=@CanDelete
            WHERE IdPermission=@Id",
            new { p.CanRead, p.CanCreate, p.CanUpdate, p.CanDelete, Id = id });
        return Ok(new { id_permission = id, updated = true });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _db.ExecuteAsync("DELETE FROM Permissions WHERE IdPermission=@Id", new { Id = id });
        return Ok(new { message = "Supprimé." });
    }

    // ─── Helpers ─────────────────────────────────────────────
    private record PermDto(int IdRole, string Resource, bool CanRead, bool CanCreate, bool CanUpdate, bool CanDelete);

    private static PermDto ParseBody(JsonElement b) => new(
        IdRole:   ReadInt (b, "idRole",   "id_role"),
        Resource: ReadStr (b, "resource") ?? "",
        CanRead:  ReadBool(b, "canRead",   "can_read"),
        CanCreate:ReadBool(b, "canCreate", "can_create"),
        CanUpdate:ReadBool(b, "canUpdate", "can_update"),
        CanDelete:ReadBool(b, "canDelete", "can_delete"));

    private static bool TryGet(JsonElement el, out JsonElement v, params string[] keys)
    {
        foreach (var k in keys)
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(k, out v)) return true;
        v = default; return false;
    }
    private static string? ReadStr(JsonElement el, params string[] keys) =>
        TryGet(el, out var v, keys) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()) : null;
    private static int ReadInt(JsonElement el, params string[] keys)
    {
        if (!TryGet(el, out var v, keys)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s)) return s;
        return 0;
    }
    private static bool ReadBool(JsonElement el, params string[] keys)
    {
        if (!TryGet(el, out var v, keys)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.GetInt32() != 0,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : v.GetString() == "1",
            _ => false,
        };
    }
}
