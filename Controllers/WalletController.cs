using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly DbService _db;
    public WalletController(DbService db) => _db = db;

    private long UserId => long.Parse(User.FindFirstValue("id") ?? "0");

    // GET /api/wallet  — solde + historique
    [HttpGet]
    public async Task<IActionResult> GetWallet()
    {
        var wallet = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Wallets WHERE IdUser=@UserId",
            new { UserId = UserId });

        var transactions = await _db.QueryAsync<dynamic>(@"
            SELECT * FROM WalletTransactions
            WHERE IdUser=@UserId
            ORDER BY CreatedAt DESC",
            new { UserId = UserId });

        return Ok(new
        {
            wallet,
            transactions,
            balance      = wallet?.MoneyBudget ?? 0,
            points       = wallet?.NbrJeton ?? 0,
            blocked      = wallet?.MoneyBlocked ?? 0,
        });
    }

    // POST /api/wallet/buy-points  — acheter des points
    [HttpPost("buy-points")]
    public async Task<IActionResult> BuyPoints([FromBody] BuyPointsRequest req)
    {
        if (req.IdPacket <= 0)
            return BadRequest(new { message = "Packet requis." });

        var packet = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM PointPackets WHERE IdPacket=@Id AND Active=1",
            new { Id = req.IdPacket });

        if (packet == null)
            return NotFound(new { message = "Packet introuvable." });

        // Add points to wallet
        await _db.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM Wallets WHERE IdUser=@UserId)
                UPDATE Wallets SET NbrJeton = ISNULL(NbrJeton,0) + @Points WHERE IdUser=@UserId
            ELSE
                INSERT INTO Wallets (IdUser, NbrJeton, MoneyBudget) VALUES (@UserId, @Points, 0)",
            new { UserId = UserId, Points = (int)packet.PointsCount });

        // Log transaction
        var walletId = await _db.ExecuteScalarAsync<long>(
            "SELECT IdWallet FROM Wallets WHERE IdUser=@UserId", new { UserId = UserId });

        await _db.ExecuteAsync(@"
            INSERT INTO WalletTransactions (IdWallet, IdUser, Type, Amount, Description, Status)
            VALUES (@IdWallet, @IdUser, 'credit', @Amount, @Desc, 'completed')",
            new { IdWallet = walletId, IdUser = UserId,
                  Amount = (decimal)packet.Price,
                  Desc = $"Achat {packet.PointsCount} points - {packet.Title}" });

        return Ok(new { message = "Points achetés.", points_added = (int)packet.PointsCount });
    }

    // POST /api/wallet/transfer  — transférer de l'argent
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "Montant invalide." });
        if (req.IdUserTo == UserId)
            return BadRequest(new { message = "Vous ne pouvez pas vous transférer à vous-même." });

        // Check balance
        var wallet = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Wallets WHERE IdUser=@UserId", new { UserId = UserId });

        if (wallet == null || (decimal)wallet.MoneyBudget < req.Amount)
            return BadRequest(new { message = "Solde insuffisant." });

        // Get commission from settings
        var commissionPct = await _db.ExecuteScalarAsync<decimal?>(
            "SELECT CAST([Value] AS DECIMAL(5,2)) FROM AdminSettings WHERE [Key]='money_transfer_commission_pct'");
        var commission = req.Amount * ((commissionPct ?? 2.5m) / 100);
        var netAmount  = req.Amount - commission;

        // Debit sender
        await _db.ExecuteAsync(
            "UPDATE Wallets SET MoneyBudget = MoneyBudget - @Amount WHERE IdUser=@UserId",
            new { UserId = UserId, Amount = req.Amount });

        // Credit receiver
        await _db.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM Wallets WHERE IdUser=@To)
                UPDATE Wallets SET MoneyBudget = MoneyBudget + @Net WHERE IdUser=@To
            ELSE
                INSERT INTO Wallets (IdUser, NbrJeton, MoneyBudget) VALUES (@To, 0, @Net)",
            new { To = req.IdUserTo, Net = netAmount });

        // Log both sides
        var myWalletId = wallet?.IdWallet ?? 0L;
        await _db.ExecuteAsync(@"
            INSERT INTO WalletTransactions (IdWallet, IdUser, Type, Amount, Description, RefId)
            VALUES (@IdWallet, @IdUser, 'transfer', @Amount, @Desc, @To)",
            new { IdWallet = myWalletId, IdUser = UserId,
                  Amount = (decimal)(req?.Amount ?? 0),
                  Desc = $"Transfert vers utilisateur #{req.IdUserTo}",
                  To = req.IdUserTo });

        // Notify receiver
        await NotificationsController.CreateAsync(_db, req.IdUserTo,
            "wallet", "Transfert reçu",
            $"Vous avez reçu {netAmount:F2} DT.",
            "/wallet", UserId);

        return Ok(new { message = "Transfert effectué.", amount_sent = req.Amount, commission, net_amount = netAmount });
    }

    // GET /api/wallet/transactions
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] string? type)
    {
        var where = !string.IsNullOrWhiteSpace(type) ? "AND Type=@Type" : "";
        var list = await _db.QueryAsync<dynamic>($@"
            SELECT * FROM WalletTransactions
            WHERE IdUser=@UserId {where}
            ORDER BY CreatedAt DESC",
            new { UserId = UserId, Type = type });
        return Ok(list);
    }

    // POST /api/wallet/block/:idUser  — bloquer un utilisateur
    [HttpPost("block/{idUser:long}")]
    public async Task<IActionResult> BlockUser(long idUser)
    {
        if (idUser == UserId) return BadRequest(new { message = "Impossible de se bloquer soi-même." });
        try
        {
            await _db.ExecuteAsync(
                "INSERT INTO BlockedUsers (IdUser, IdBlocked) VALUES (@UserId, @Blocked)",
                new { UserId = UserId, Blocked = idUser });
            return Ok(new { message = "Utilisateur bloqué." });
        }
        catch { return Conflict(new { message = "Déjà bloqué." }); }
    }

    // DELETE /api/wallet/block/:idUser  — débloquer
    [HttpDelete("block/{idUser:long}")]
    public async Task<IActionResult> UnblockUser(long idUser)
    {
        await _db.ExecuteAsync(
            "DELETE FROM BlockedUsers WHERE IdUser=@UserId AND IdBlocked=@Blocked",
            new { UserId = UserId, Blocked = idUser });
        return Ok(new { message = "Utilisateur débloqué." });
    }

    // GET /api/wallet/blocked  — liste des bloqués
    [HttpGet("blocked")]
    public async Task<IActionResult> GetBlocked()
    {
        var list = await _db.QueryAsync<dynamic>(@"
            SELECT b.IdBlock, b.IdBlocked, b.CreatedAt,
                   CONCAT(u.FirstName,' ',u.LastName) AS BlockedName,
                   u.Email AS BlockedEmail
            FROM BlockedUsers b
            JOIN Users u ON b.IdBlocked = u.IdUser
            WHERE b.IdUser=@UserId
            ORDER BY b.CreatedAt DESC",
            new { UserId = UserId });
        return Ok(list);
    }
}

public class BuyPointsRequest  { public long    IdPacket { get; set; } }
public class TransferRequest   { public long    IdUserTo { get; set; }
                                 public decimal Amount   { get; set; } }