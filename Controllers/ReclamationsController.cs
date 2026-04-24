using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/reclamations")]
[Authorize]
public class ReclamationsController : ControllerBase
{
    private readonly DbService _db;
    public ReclamationsController(DbService db) => _db = db;

    // ─── GET /api/reclamations/causes  (public) ───────────────────
    [HttpGet("causes")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCauses()
    {
        var causes = await _db.QueryAsync<CauseReport>(
            "SELECT IdCauseReport, TitleCauseFr, TitleCauseEn, TitleCauseAr, GroupName, Active FROM CausesReports WHERE Active = 1"
        );
        return Ok(causes.Select(c => new {
            id        = c.IdCauseReport,
            title     = c.TitleCauseFr,
            title_en  = c.TitleCauseEn,
            title_ar  = c.TitleCauseAr,
            group     = c.GroupName
        }));
    }

    // ─── GET /api/reclamations ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var role   = User.FindFirstValue("role") ?? "user";
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        IEnumerable<Report> list;
        if (role == "admin")
        {
            list = await _db.QueryAsync<Report>(
                @"SELECT r.*,
                         CONCAT(u.FirstName, ' ', u.LastName) AS ClientName,
                         cr.TitleCauseFr AS CauseTitle
                  FROM Reports r
                  LEFT JOIN Users         u  ON r.IdUser        = u.IdUser
                  LEFT JOIN CausesReports cr ON r.IdCauseReport = cr.IdCauseReport
                  ORDER BY r.Date DESC"
            );
        }
        else
        {
            list = await _db.QueryAsync<Report>(
                @"SELECT r.*, cr.TitleCauseFr AS CauseTitle
                  FROM Reports r
                  LEFT JOIN CausesReports cr ON r.IdCauseReport = cr.IdCauseReport
                  WHERE r.IdUser = @UserId
                  ORDER BY r.Date DESC",
                new { UserId = userId }
            );
        }
        return Ok(list.Select(MapReport));
    }

    // ─── POST /api/reclamations ───────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { message = "Sujet et description requis." });

        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        var report = await _db.QueryFirstOrDefaultAsync<Report>(
            @"INSERT INTO Reports (IdUser, IdCauseReport, Subject, Description, Date, State, TypeTable, IdTable)
              OUTPUT INSERTED.*
              VALUES (@IdUser, @IdCauseReport, @Subject, @Description, GETDATE(), 1, @TypeTable, @IdTable)",
            new {
                IdUser        = userId,
                req.IdCauseReport,
                req.Subject,
                req.Description,
                req.TypeTable,
                req.IdTable
            }
        );
        return StatusCode(201, MapReport(report!));
    }

    // ─── PATCH /api/reclamations/:id  (admin only) ────────────────
    [HttpPatch("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusRequest req)
    {
        // State : 1=open, 2=in_progress, 3=resolved, 0=closed
        int stateVal;
        if (!int.TryParse(req.Status, out stateVal))
        {
            stateVal = req.Status?.ToLower() switch
            {
                "open"        => 1,
                "in_progress" => 2,
                "resolved"    => 3,
                "closed"      => 0,
                _             => -1
            };
        }
        if (stateVal < 0)
            return BadRequest(new { message = "Statut invalide. Valeurs acceptées: open, in_progress, resolved, closed (ou 0-3)." });

        var report = await _db.QueryFirstOrDefaultAsync<Report>(
            @"UPDATE Reports SET State = @State OUTPUT INSERTED.* WHERE IdReport = @Id",
            new { State = stateVal, Id = id }
        );
        if (report == null) return NotFound(new { message = "Réclamation introuvable." });
        return Ok(MapReport(report));
    }

    // ─── Mapper ───────────────────────────────────────────────────
    private static object MapReport(Report r) => new
    {
        id          = r.IdReport,
        user_id     = r.IdUser,
        cause_id    = r.IdCauseReport,
        cause       = r.CauseTitle,
        subject     = r.Subject,
        description = r.Description,
        date        = r.Date,
        state       = r.State,
        state_label = r.State switch { 0 => "closed", 2 => "in_progress", 3 => "resolved", _ => "open" },
        type_table  = r.TypeTable,
        id_table    = r.IdTable,
        client_name = r.ClientName
    };
}
