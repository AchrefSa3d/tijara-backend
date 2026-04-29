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
    public async Task<IActionResult> Create([FromBody] TransportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Nom requis." });

        // Try both schema approaches - support both old (Transport) and new (TransportRequest) formats
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Transports (Name, Logo, Phone, Email, DeliveryFee, FreeFrom, Zones, Active, Description, Price, Duration)
            OUTPUT INSERTED.IdTransport
            VALUES (@Name, @Logo, @Phone, @Email, @DeliveryFee, @FreeFrom, @Zones, @Active, @Description, @Price, @Duration)",
            new { req.Name, req.Logo, req.Phone, req.Email, req.DeliveryFee, req.FreeFrom, req.Zones, req.Active, req.Description, req.Price, req.Duration });
        return Ok(new { id, name = req.Name, price = req.Price });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] TransportRequest req)
    {
        await _db.ExecuteAsync(@"
            UPDATE Transports SET Name=@Name, Logo=@Logo, Phone=@Phone, Email=@Email,
                                  DeliveryFee=@DeliveryFee, FreeFrom=@FreeFrom,
                                  Zones=@Zones, Active=@Active, Description=@Description,
                                  Price=@Price, Duration=@Duration
            WHERE IdTransport=@Id", 
            new { req.Name, req.Logo, req.Phone, req.Email, req.DeliveryFee, req.FreeFrom, req.Zones, req.Active, 
                  req.Description, req.Price, req.Duration, Id = id });
        return Ok(new { message = "Mis à jour." });
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

// DTOs
public class TransportRequest
{
    public string  Name        { get; set; } = "";
    public string? Logo        { get; set; }
    public string? Phone       { get; set; }
    public string? Email       { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal FreeFrom    { get; set; }
    public string? Zones       { get; set; }
    public bool    Active      { get; set; } = true;
    public string? Description { get; set; }
    public decimal Price       { get; set; }
    public string? Duration    { get; set; }
}