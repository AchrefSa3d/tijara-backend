using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly DbService _db;
    public PaymentsController(DbService db) => _db = db;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
    private bool IsAdmin => User.IsInRole("admin");

    // POST /api/payments — crée un paiement (mock — pas de vrai gateway)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] PaymentRequest req)
    {
        var userId = CurrentUserId;
        if (userId == null) return Unauthorized();
        if (req.Amount <= 0) return BadRequest(new { message = "Montant invalide." });

        // Mock : simulate transaction success
        var txId = "TIJ-" + DateTime.Now.Ticks.ToString()[^10..];
        var reference = "PAY-" + Guid.NewGuid().ToString("N")[..8].ToUpper();

        var id = await _db.ExecuteScalarAsync<long>(@"
            INSERT INTO Payments (IdUser, IdOrder, Amount, Method, Status, Reference, TransactionId, PaidAt)
            OUTPUT INSERTED.IdPayment
            VALUES (@IdUser, @IdOrder, @Amount, @Method, 'paid', @Reference, @TxId, GETDATE())",
            new { IdUser = userId, req.IdOrder, req.Amount, req.Method, Reference = reference, TxId = txId });

        // Optionnel : mettre la commande en payée
        if (req.IdOrder.HasValue)
        {
            await _db.ExecuteAsync(
                "UPDATE Orders SET PaymentStatus='paid' WHERE IdOrder=@Id",
                new { Id = req.IdOrder.Value });
        }

        return Ok(new
        {
            idPayment     = id,
            reference,
            transactionId = txId,
            status        = "paid",
            message       = "Paiement confirmé."
        });
    }

    // GET /api/payments/mine
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine()
    {
        var list = await _db.QueryAsync<PaymentRecord>(
            "SELECT * FROM Payments WHERE IdUser=@UserId ORDER BY IdPayment DESC",
            new { UserId = CurrentUserId });
        return Ok(list);
    }

    // GET /api/payments (admin only)
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> All([FromQuery] string? status)
    {
        var sql = "SELECT p.*, u.Email, CONCAT(u.FirstName,' ',u.LastName) AS UserName FROM Payments p JOIN Users u ON p.IdUser=u.IdUser"
                + (string.IsNullOrEmpty(status) ? "" : " WHERE p.Status=@Status")
                + " ORDER BY p.IdPayment DESC";
        var list = await _db.QueryAsync<dynamic>(sql, new { Status = status });
        return Ok(list);
    }

    // POST /api/payments/:id/refund (admin)
    [HttpPost("{id:long}/refund")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Refund(long id)
    {
        await _db.ExecuteAsync(
            "UPDATE Payments SET Status='refunded' WHERE IdPayment=@Id", new { Id = id });
        return Ok(new { message = "Paiement remboursé." });
    }
}
