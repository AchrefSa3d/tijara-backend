using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/transports")]
public class TransportsController : ControllerBase
{
    private readonly DbService _db;
    public TransportsController(DbService db) => _db = db;

    // GET /api/transports — public (liste des transporteurs actifs)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool onlyActive = false)
    {
        var sql = onlyActive
            ? "SELECT * FROM Transports WHERE Active=1 ORDER BY Name"
            : "SELECT * FROM Transports ORDER BY Name";
        var list = await _db.QueryAsync<Transport>(sql);
        return Ok(list);
    }

    // GET /api/transports/:id
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.QueryFirstOrDefaultAsync<Transport>(
            "SELECT * FROM Transports WHERE IdTransport=@Id", new { Id = id });
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] Transport t)
    {
        var id = await _db.ExecuteScalarAsync<int>(@"
            INSERT INTO Transports (Name, Logo, Phone, Email, DeliveryFee, FreeFrom, Zones, Active)
            OUTPUT INSERTED.IdTransport
            VALUES (@Name, @Logo, @Phone, @Email, @DeliveryFee, @FreeFrom, @Zones, @Active)", t);
        return Ok(new { idTransport = id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Transport t)
    {
        await _db.ExecuteAsync(@"
            UPDATE Transports SET Name=@Name, Logo=@Logo, Phone=@Phone, Email=@Email,
                                  DeliveryFee=@DeliveryFee, FreeFrom=@FreeFrom,
                                  Zones=@Zones, Active=@Active
            WHERE IdTransport=@Id", new { t.Name, t.Logo, t.Phone, t.Email, t.DeliveryFee, t.FreeFrom, t.Zones, t.Active, Id = id });
        return Ok();
    }

    [HttpPatch("{id:int}/toggle")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Toggle(int id)
    {
        await _db.ExecuteAsync(
            "UPDATE Transports SET Active = 1 - Active WHERE IdTransport=@Id", new { Id = id });
        return Ok();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _db.ExecuteAsync("DELETE FROM Transports WHERE IdTransport=@Id", new { Id = id });
        return Ok();
    }
}
