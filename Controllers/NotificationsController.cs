using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly DbService _db;
    public NotificationsController(DbService db) => _db = db;

    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var list = await _db.QueryAsync<Notification>(
            @"SELECT TOP 30 IdNotification, IdUser, Type, Title, Message, Link, IsRead, CreatedAt, IdReference
              FROM Notifications
              WHERE IdUser = @UserId
              ORDER BY CreatedAt DESC",
            new { UserId = userId });

        var items = list.Select(n => new
        {
            id         = n.IdNotification,
            type       = n.Type,
            title      = n.Title,
            message    = n.Message,
            link       = n.Link,
            is_read    = n.IsRead,
            created_at = n.CreatedAt,
            icon       = GetIcon(n.Type),
        }).ToList();

        return Ok(new
        {
            notifications = items,
            unread_count  = items.Count(n => !n.is_read),
        });
    }

    // PATCH /api/notifications/{id}/read
    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        await _db.ExecuteAsync(
            "UPDATE Notifications SET IsRead=1 WHERE IdNotification=@Id AND IdUser=@UserId",
            new { Id = id, UserId = userId });
        return Ok(new { message = "Notification lue." });
    }

    // PATCH /api/notifications/read-all
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        await _db.ExecuteAsync(
            "UPDATE Notifications SET IsRead=1 WHERE IdUser=@UserId",
            new { UserId = userId });
        return Ok(new { message = "Toutes les notifications lues." });
    }

    private static string GetIcon(string? type) => type switch
    {
        "new_product"  => "bx-store",
        "order_update" => "bx-package",
        "follow"       => "bx-user-plus",
        _              => "bx-bell",
    };

    // ─── Static helper used by other controllers ──────────────
    public static async Task CreateAsync(DbService db, long userId, string type,
        string title, string message, string? link = null, long? idRef = null)
    {
        try
        {
            await db.ExecuteAsync(
                @"INSERT INTO Notifications
                    (IdUser, Type, Title, Message, Link, IsRead, CreatedAt, IdReference)
                  VALUES
                    (@IdUser, @Type, @Title, @Message, @Link, 0, GETDATE(), @IdRef)",
                new { IdUser = userId, Type = type, Title = title,
                      Message = message, Link = link, IdRef = idRef });
        }
        catch { /* never fail the parent operation */ }
    }
}
