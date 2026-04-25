using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/sms")]
[Authorize(Roles = "admin")]
public class SmsController : ControllerBase
{
    private readonly DbService _db;
    public SmsController(DbService db) => _db = db;

    // POST /api/sms — envoie un SMS (mock — loggue seulement)
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SmsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Recipient) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { message = "Destinataire et message requis." });

        // TODO : intégrer un vrai provider (Twilio, Tunisie SMS, etc.)
        // Pour l'instant on log simplement.
        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO SmsLogs (Recipient, Message, Status, Provider)
            OUTPUT INSERTED.IdSms
            VALUES (@Recipient, @Message, 'sent', 'mock')",
            new { req.Recipient, req.Message });

        return Ok(new { idSms = id, status = "sent", message = "SMS envoyé (mock)." });
    }

    // GET /api/sms
    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var list = await _db.QueryAsync<SmsLog>(
            "SELECT TOP 100 * FROM SmsLogs ORDER BY IdSms DESC");
        return Ok(list);
    }
}
