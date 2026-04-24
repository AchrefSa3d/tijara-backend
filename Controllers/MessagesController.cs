using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly DbService _db;
    public MessagesController(DbService db) => _db = db;

    // ─── GET /api/messages/conversations ─────────────────────────
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        var list = await _db.QueryAsync<Chat>(
            @"SELECT c.IdChat, c.IdUserSender, c.IdUserReciver, c.CreatedAt, c.Active,
                     CONCAT(s.FirstName, ' ', s.LastName) AS SenderName,
                     CONCAT(r.FirstName, ' ', r.LastName) AS ReceiverName,
                     (SELECT TOP 1 Message FROM ChatMessages
                      WHERE IdChat = c.IdChat AND Active = 1
                      ORDER BY CreateDate DESC) AS LastMessage,
                     (SELECT COUNT(*) FROM ChatMessages
                      WHERE IdChat = c.IdChat
                        AND IdUserSender <> @UserId
                        AND Active = 1) AS UnreadCount
              FROM Chats c
              JOIN Users s ON c.IdUserSender  = s.IdUser
              JOIN Users r ON c.IdUserReciver = r.IdUser
              WHERE (c.IdUserSender = @UserId OR c.IdUserReciver = @UserId)
                AND c.Active = 1
              ORDER BY c.CreatedAt DESC",
            new { UserId = userId }
        );

        var result = list.Select(c => new
        {
            id            = c.IdChat,
            sender_id     = c.IdUserSender,
            receiver_id   = c.IdUserReciver,
            sender_name   = c.SenderName,
            receiver_name = c.ReceiverName,
            last_message  = c.LastMessage,
            unread_count  = c.UnreadCount,
            created_at    = c.CreatedAt,
            // Convenient: who is the "other" person
            other_id      = c.IdUserSender == userId ? c.IdUserReciver : c.IdUserSender,
            other_name    = c.IdUserSender == userId ? c.ReceiverName  : c.SenderName
        });

        return Ok(result);
    }

    // ─── GET /api/messages/conversations/:id ─────────────────────
    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> GetMessages(int id)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        var chat = await _db.QueryFirstOrDefaultAsync<Chat>(
            @"SELECT IdChat, IdUserSender, IdUserReciver FROM Chats
              WHERE IdChat = @Id AND Active = 1
                AND (IdUserSender = @Uid OR IdUserReciver = @Uid)",
            new { Id = id, Uid = userId }
        );
        if (chat == null) return StatusCode(403, new { message = "Accès refusé." });

        var messages = await _db.QueryAsync<ChatMessage>(
            @"SELECT cm.IdChatMessage, cm.IdChat, cm.Message, cm.CreateDate,
                     cm.IdUserSender, cm.Active,
                     CONCAT(u.FirstName, ' ', u.LastName) AS SenderName
              FROM ChatMessages cm
              JOIN Users u ON cm.IdUserSender = u.IdUser
              WHERE cm.IdChat = @Id AND cm.Active = 1
              ORDER BY cm.CreateDate ASC",
            new { Id = id }
        );

        var result = messages.Select(m => new
        {
            id          = m.IdChatMessage,
            chat_id     = m.IdChat,
            message     = m.Message,
            sender_id   = m.IdUserSender,
            sender_name = m.SenderName,
            created_at  = m.CreateDate,
            is_mine     = m.IdUserSender == userId
        });

        return Ok(new { chat_id = chat.IdChat, messages = result });
    }

    // ─── POST /api/messages/conversations/:id ────────────────────
    [HttpPost("conversations/{id:int}")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] MessageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "Message vide." });

        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        var chat = await _db.QueryFirstOrDefaultAsync<Chat>(
            @"SELECT IdChat FROM Chats
              WHERE IdChat = @Id AND Active = 1
                AND (IdUserSender = @Uid OR IdUserReciver = @Uid)",
            new { Id = id, Uid = userId }
        );
        if (chat == null) return StatusCode(403, new { message = "Accès refusé." });

        var msg = await _db.QueryFirstOrDefaultAsync<ChatMessage>(
            @"INSERT INTO ChatMessages (IdChatMessage, IdChat, Message, CreateDate, IdUserSender, Active)
              OUTPUT INSERTED.*
              VALUES (
                (SELECT ISNULL(MAX(IdChatMessage),0)+1 FROM ChatMessages),
                @IdChat, @Message, GETDATE(), @SenderId, 1)",
            new { IdChat = id, Message = req.Content.Trim(), SenderId = userId }
        );

        return StatusCode(201, new
        {
            id         = msg?.IdChatMessage,
            chat_id    = msg?.IdChat,
            message    = msg?.Message,
            sender_id  = msg?.IdUserSender,
            created_at = msg?.CreateDate
        });
    }

    // ─── POST /api/messages/start ─────────────────────────────────
    [HttpPost("start")]
    public async Task<IActionResult> StartChat([FromBody] StartChatRequest req)
    {
        // Accept both vendor_id (frontend alias) and id_user_reciver
        var receiverId = req.IdUserReciver != 0 ? req.IdUserReciver : req.VendorId;

        if (receiverId == 0 || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "Destinataire et message requis." });

        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        if (userId == receiverId)
            return BadRequest(new { message = "Vous ne pouvez pas vous envoyer un message à vous-même." });

        // Chercher une conversation existante (bidirectionnel)
        var existing = await _db.QueryFirstOrDefaultAsync<Chat>(
            @"SELECT IdChat FROM Chats
              WHERE Active = 1
                AND ((IdUserSender = @Uid AND IdUserReciver = @Rid)
                  OR (IdUserSender = @Rid AND IdUserReciver = @Uid))",
            new { Uid = userId, Rid = receiverId }
        );

        long chatId;
        if (existing != null)
        {
            chatId = existing.IdChat;
        }
        else
        {
            var newChat = await _db.QueryFirstOrDefaultAsync<Chat>(
                @"INSERT INTO Chats (IdUserSender, IdUserReciver, Active)
                  OUTPUT INSERTED.*
                  VALUES (@Uid, @Rid, 1)",
                new { Uid = userId, Rid = receiverId }
            );
            chatId = newChat!.IdChat;
        }

        // Envoyer le message
        await _db.ExecuteAsync(
            @"INSERT INTO ChatMessages (IdChatMessage, IdChat, Message, CreateDate, IdUserSender, Active)
              VALUES (
                (SELECT ISNULL(MAX(IdChatMessage),0)+1 FROM ChatMessages),
                @IdChat, @Message, GETDATE(), @SenderId, 1)",
            new { IdChat = chatId, Message = req.Content.Trim(), SenderId = userId }
        );

        return StatusCode(201, new { chat_id = chatId, message = "Message envoyé." });
    }
}
