using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize(Roles = "admin")]
public class PermissionsController : ControllerBase
{
    private readonly DbService _db;
    public PermissionsController(DbService db) => _db = db;

    // GET /api/permissions
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.QueryAsync<Permission>(
            "SELECT * FROM Permissions ORDER BY IdRole, Resource");
        return Ok(list);
    }

    // GET /api/permissions/role/2
    [HttpGet("role/{idRole:int}")]
    public async Task<IActionResult> GetByRole(int idRole)
    {
        var list = await _db.QueryAsync<Permission>(
            "SELECT * FROM Permissions WHERE IdRole=@IdRole ORDER BY Resource",
            new { IdRole = idRole });
        return Ok(list);
    }

    // POST /api/permissions
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Permission p)
    {
        try
        {
            var id = await _db.ExecuteScalarAsync<int>(@"
                INSERT INTO Permissions (IdRole, Resource, CanRead, CanCreate, CanUpdate, CanDelete)
                OUTPUT INSERTED.IdPermission
                VALUES (@IdRole, @Resource, @CanRead, @CanCreate, @CanUpdate, @CanDelete)", p);
            return Ok(new { idPermission = id });
        }
        catch
        {
            await _db.ExecuteAsync(@"
                UPDATE Permissions SET CanRead=@CanRead, CanCreate=@CanCreate,
                                       CanUpdate=@CanUpdate, CanDelete=@CanDelete
                WHERE IdRole=@IdRole AND Resource=@Resource", p);
            return Ok(new { message = "Updated" });
        }
    }

    // PUT /api/permissions/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Permission p)
    {
        await _db.ExecuteAsync(@"
            UPDATE Permissions SET CanRead=@CanRead, CanCreate=@CanCreate,
                                   CanUpdate=@CanUpdate, CanDelete=@CanDelete
            WHERE IdPermission=@Id",
            new { p.CanRead, p.CanCreate, p.CanUpdate, p.CanDelete, Id = id });
        return Ok(new { message = "Updated" });
    }

    // DELETE /api/permissions/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _db.ExecuteAsync("DELETE FROM Permissions WHERE IdPermission=@Id", new { Id = id });
        return Ok();
    }
}
