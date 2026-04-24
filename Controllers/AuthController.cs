using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;
using TijaraApi.Models;
using TijaraApi.Services;

namespace TijaraApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DbService     _db;
    private readonly TokenService  _tokens;
    private readonly EmailService  _email;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(DbService db, TokenService tokens,
                          EmailService email, IConfiguration config,
                          ILogger<AuthController> logger)
    {
        _db     = db;
        _tokens = tokens;
        _email  = email;
        _config = config;
        _logger = logger;
    }

    // ─── POST /api/auth/login ─────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Email et mot de passe requis." });

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.*, r.RoleUser
              FROM Users u LEFT JOIN Roles r ON u.IdRole = r.IdRole
              WHERE u.Email = @Email AND u.Active = 1",
            new { Email = req.Email.Trim().ToLower() }
        );

        if (user == null)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        bool passwordOk;
        try { passwordOk = BCrypt.Net.BCrypt.Verify(req.Password, user.Password); }
        catch { passwordOk = false; }
        if (!passwordOk)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        var role = MapRole(user.IdRole ?? 2);

        // Vendor must be verified
        if (role == "vendor" && (user.IsVerified ?? 0) == 0)
            return StatusCode(403, new
            {
                message = "Votre compte vendeur est en attente de validation.",
                status  = "pending_approval"
            });

        var token = _tokens.GenerateToken(user.IdUser, user.Email ?? "", role,
                                          user.FirstName ?? "", user.LastName ?? "");
        return Ok(new { token, user = BuildUserResponse(user, role) });
    }

    // ─── POST /api/auth/register ──────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Email et mot de passe requis." });
        if (req.Password.Length < 6)
            return BadRequest(new { message = "Mot de passe : minimum 6 caractères." });

        var email = req.Email.Trim().ToLower();
        var exists = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT IdUser FROM Users WHERE Email = @Email", new { Email = email });
        if (exists != null)
            return Conflict(new { message = "Cet email est déjà utilisé." });

        // Role: 1=Admin, 2=Visitor(user), 3=Advertiser(vendor)
        var roleStr  = req.Role == "vendor" ? "vendor" : "user";
        var idRole   = roleStr == "vendor" ? 3 : 2;
        var isVerif  = roleStr == "vendor" ? 0 : 1; // vendors need admin approval
        // For vendors: prefer ShopName, then Username, then FirstName+LastName
        var username = roleStr == "vendor"
            ? (req.ShopName?.Trim() ?? req.Username?.Trim() ?? $"{req.FirstName?.Trim()} {req.LastName?.Trim()}".Trim())
            : (req.Username?.Trim() ?? $"{req.FirstName?.Trim()} {req.LastName?.Trim()}".Trim());

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var now  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var telephone = req.Telephone?.Trim() ?? req.Phone?.Trim();

        var newUser = await _db.QueryFirstOrDefaultAsync<User>(
            @"INSERT INTO Users
                (Username, FirstName, LastName, Email, Password, IdRole,
                 Telephone, City, Active, IsVerified, CreationDate, BirthDate, Gender)
              OUTPUT
                INSERTED.IdUser, INSERTED.Email, INSERTED.IdRole,
                INSERTED.FirstName, INSERTED.LastName, INSERTED.Username,
                INSERTED.IsVerified, INSERTED.Active, INSERTED.BirthDate, INSERTED.Gender
              VALUES
                (@Username, @FirstName, @LastName, @Email, @Password, @IdRole,
                 @Telephone, @City, 1, @IsVerified, @CreationDate, @BirthDate, @Gender)",
            new
            {
                Username     = username,
                FirstName    = req.FirstName?.Trim() ?? "",
                LastName     = req.LastName?.Trim()  ?? "",
                Email        = email,
                Password     = hash,
                IdRole       = idRole,
                Telephone    = telephone,
                City         = req.City?.Trim(),
                IsVerified   = isVerif,
                CreationDate = now,
                BirthDate    = req.BirthDate?.Trim(),
                Gender       = req.Gender?.Trim(),
            }
        );

        if (newUser == null)
            return StatusCode(500, new { message = "Erreur lors de la création du compte." });

        // ── Envoyer email de confirmation (clients uniquement) ─────────────
        if (roleStr == "user")
        {
            try
            {
                var confirmToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                var appUrl       = _config["AppUrl"] ?? "http://localhost:4200";
                var confirmLink  = $"{appUrl}/auth/confirm-email?token={confirmToken}";

                await _db.ExecuteAsync(
                    @"INSERT INTO EmailTokens (IdUser, Token, Type, ExpiresAt)
                      VALUES (@IdUser, @Token, 'email_confirm', DATEADD(HOUR, 24, GETDATE()))",
                    new { IdUser = newUser.IdUser, Token = confirmToken });

                _ = Task.Run(() => _email.SendConfirmationEmailAsync(
                    newUser.Email ?? "", newUser.FirstName ?? "Utilisateur", confirmLink));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Email confirmation non envoyé : {Msg}", ex.Message);
            }
        }

        if (roleStr == "vendor")
            return StatusCode(201, new
            {
                message = "Compte vendeur créé. En attente de validation.",
                status  = "pending_approval",
                user    = new { id = newUser.IdUser, email = newUser.Email, role = "vendor" }
            });

        var token = _tokens.GenerateToken(newUser.IdUser, newUser.Email ?? "", "user",
                                          newUser.FirstName ?? "", newUser.LastName ?? "");
        return StatusCode(201, new
        {
            token,
            user        = BuildUserResponse(newUser, "user"),
            email_sent  = true,
            message     = "Compte créé ! Un email de confirmation a été envoyé."
        });
    }

    // ─── GET /api/auth/confirm-email?token=xxx ────────────────
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "Token manquant." });

        var record = await _db.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT IdToken, IdUser, ExpiresAt, Used
              FROM EmailTokens
              WHERE Token = @Token AND Type = 'email_confirm'",
            new { Token = token });

        if (record == null)
            return BadRequest(new { message = "Lien invalide ou expiré." });

        DateTime expiresAt = record.ExpiresAt;
        if (expiresAt < DateTime.Now)
            return BadRequest(new { message = "Ce lien a expiré. Veuillez en demander un nouveau." });

        bool used = record.Used;
        if (used)
            return Ok(new { message = "Email déjà confirmé.", already_confirmed = true });

        long userId = record.IdUser;
        await _db.ExecuteAsync(
            "UPDATE Users        SET EmailConfirmed = 1 WHERE IdUser  = @Id",
            new { Id = userId });
        await _db.ExecuteAsync(
            "UPDATE EmailTokens  SET Used = 1           WHERE IdToken = @Id",
            new { Id = (long)record.IdToken });

        return Ok(new { message = "Email confirmé avec succès ! Vous pouvez maintenant vous connecter.", confirmed = true });
    }

    // ─── POST /api/auth/resend-confirm ────────────────────────
    [HttpPost("resend-confirm")]
    public async Task<IActionResult> ResendConfirm([FromBody] ResendConfirmRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Email requis." });

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT IdUser, Email, FirstName, EmailConfirmed, Active FROM Users WHERE Email = @Email AND Active = 1",
            new { Email = req.Email.Trim().ToLower() });

        if (user == null) return Ok(new { message = "Si ce compte existe, un email a été envoyé." });
        if ((user.EmailConfirmed ?? 1) == 1)
            return Ok(new { message = "Votre email est déjà confirmé." });

        var confirmToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var appUrl       = _config["AppUrl"] ?? "http://localhost:4200";
        var confirmLink  = $"{appUrl}/auth/confirm-email?token={confirmToken}";

        await _db.ExecuteAsync(
            @"INSERT INTO EmailTokens (IdUser, Token, Type, ExpiresAt)
              VALUES (@IdUser, @Token, 'email_confirm', DATEADD(HOUR, 24, GETDATE()))",
            new { IdUser = user.IdUser, Token = confirmToken });

        _ = Task.Run(() => _email.SendConfirmationEmailAsync(
            user.Email ?? "", user.FirstName ?? "Utilisateur", confirmLink));

        return Ok(new { message = "Un email de confirmation a été envoyé.", email_sent = true });
    }

    // ─── POST /api/auth/facebook ──────────────────────────────
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.AccessToken))
            return BadRequest(new { message = "Token Facebook requis." });

        // Vérifier le token avec l'API Graph Facebook
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var graphUrl = $"https://graph.facebook.com/me?fields=id,first_name,last_name,email,picture&access_token={Uri.EscapeDataString(req.AccessToken)}";
        HttpResponseMessage fbResponse;
        try { fbResponse = await http.GetAsync(graphUrl); }
        catch { return StatusCode(503, new { message = "Impossible de contacter Facebook." }); }

        if (!fbResponse.IsSuccessStatusCode)
            return Unauthorized(new { message = "Token Facebook invalide ou expiré." });

        var json      = await fbResponse.Content.ReadAsStringAsync();
        var fbData    = JsonSerializer.Deserialize<JsonElement>(json);

        var fbId      = fbData.TryGetProperty("id",         out var idEl)   ? idEl.GetString()   : null;
        var fbEmail   = fbData.TryGetProperty("email",      out var emEl)   ? emEl.GetString()   : null;
        var fbFirst   = fbData.TryGetProperty("first_name", out var fnEl)   ? fnEl.GetString()   : null;
        var fbLast    = fbData.TryGetProperty("last_name",  out var lnEl)   ? lnEl.GetString()   : null;
        string? fbPic = null;
        if (fbData.TryGetProperty("picture", out var picEl) &&
            picEl.TryGetProperty("data",     out var picData) &&
            picData.TryGetProperty("url",    out var picUrl))
            fbPic = picUrl.GetString();

        if (string.IsNullOrWhiteSpace(fbId))
            return Unauthorized(new { message = "Impossible de récupérer l'identifiant Facebook." });

        // Chercher par FacebookId ou par email
        User? user = null;
        if (!string.IsNullOrWhiteSpace(fbEmail))
            user = await _db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Email = @Email AND Active = 1",
                new { Email = fbEmail.Trim().ToLower() });

        if (user == null)
            user = await _db.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE FacebookId = @FbId AND Active = 1",
                new { FbId = fbId });

        if (user == null)
        {
            // Créer un nouveau compte
            var now      = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var username = $"{fbFirst?.Trim()} {fbLast?.Trim()}".Trim();
            var hashPass = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

            user = await _db.QueryFirstOrDefaultAsync<User>(
                @"INSERT INTO Users
                    (Username, FirstName, LastName, Email, Password, IdRole,
                     ProfilePicture, FacebookId, Active, IsVerified, EmailConfirmed, CreationDate)
                  OUTPUT INSERTED.*
                  VALUES
                    (@Username, @FirstName, @LastName, @Email, @Password, 2,
                     @ProfilePicture, @FacebookId, 1, 1, 1, @CreationDate)",
                new
                {
                    Username       = username,
                    FirstName      = fbFirst ?? "",
                    LastName       = fbLast  ?? "",
                    Email          = fbEmail?.Trim().ToLower() ?? $"fb_{fbId}@facebook.com",
                    Password       = hashPass,
                    ProfilePicture = fbPic,
                    FacebookId     = fbId,
                    CreationDate   = now,
                });

            if (user == null) return StatusCode(500, new { message = "Erreur création du compte." });
        }
        else
        {
            // Mettre à jour FacebookId si pas encore enregistré
            if (string.IsNullOrWhiteSpace(user.FacebookId))
                await _db.ExecuteAsync(
                    "UPDATE Users SET FacebookId = @FbId WHERE IdUser = @Id",
                    new { FbId = fbId, Id = user.IdUser });
        }

        var role = MapRole(user.IdRole ?? 2);
        var jwtToken = _tokens.GenerateToken(user.IdUser, user.Email ?? "", role,
                                             user.FirstName ?? "", user.LastName ?? "");
        return Ok(new { token = jwtToken, user = BuildUserResponse(user, role) });
    }

    // ─── GET /api/auth/me ─────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var user   = await _db.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.*, r.RoleUser,
                     s.NameFR AS StateName, c.country_enName AS CountryName
              FROM Users u
              LEFT JOIN Roles r   ON u.IdRole   = r.IdRole
              LEFT JOIN States s  ON u.IdState  = s.IdState
              LEFT JOIN Countries c ON u.IdCountry = c.IdCountry
              WHERE u.IdUser = @Id",
            new { Id = userId }
        );
        if (user == null) return NotFound(new { message = "Utilisateur introuvable." });
        var role = MapRole(user.IdRole ?? 2);
        return Ok(BuildUserResponse(user, role));
    }

    // ─── PUT /api/auth/profile ────────────────────────────────
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = long.Parse(User.FindFirstValue("id") ?? "0");

        // Support alias field names sent by the Angular frontend
        var telephone = req.Telephone?.Trim() ?? req.Phone?.Trim();
        var location  = req.Location?.Trim()  ?? req.Address?.Trim();
        // shop_name (vendors) → Username column
        var username  = req.Username?.Trim()  ?? req.ShopName?.Trim();

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            @"UPDATE Users SET
                FirstName      = COALESCE(@FirstName,      FirstName),
                LastName       = COALESCE(@LastName,       LastName),
                Telephone      = COALESCE(@Telephone,      Telephone),
                Location       = COALESCE(@Location,       Location),
                City           = COALESCE(@City,           City),
                ProfilePicture = COALESCE(@ProfilePicture, ProfilePicture),
                Username       = COALESCE(@Username,       Username),
                IdState        = COALESCE(@IdState,        IdState),
                IdCountry      = COALESCE(@IdCountry,      IdCountry),
                BirthDate      = COALESCE(@BirthDate,      BirthDate),
                Gender         = COALESCE(@Gender,         Gender)
              OUTPUT
                INSERTED.IdUser, INSERTED.Email, INSERTED.IdRole,
                INSERTED.FirstName, INSERTED.LastName, INSERTED.Username,
                INSERTED.Telephone, INSERTED.ProfilePicture, INSERTED.Location,
                INSERTED.City, INSERTED.IdState, INSERTED.IdCountry,
                INSERTED.IsVerified, INSERTED.Active,
                INSERTED.BirthDate, INSERTED.Gender
              WHERE IdUser = @Id",
            new
            {
                Id             = userId,
                FirstName      = req.FirstName?.Trim(),
                LastName       = req.LastName?.Trim(),
                Telephone      = telephone,
                Location       = location,
                City           = req.City?.Trim(),
                ProfilePicture = req.ProfilePicture,
                Username       = username,
                IdState        = req.IdState,
                IdCountry      = req.IdCountry,
                BirthDate      = req.BirthDate?.Trim(),
                Gender         = req.Gender?.Trim(),
            }
        );

        if (user == null) return NotFound(new { message = "Utilisateur introuvable." });
        var role = MapRole(user.IdRole ?? 2);
        return Ok(new { message = "Profil mis à jour.", user = BuildUserResponse(user, role) });
    }

    // ─── POST /api/auth/change-password ──────────────────────
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "Mot de passe requis." });
        if (req.NewPassword.Length < 6)
            return BadRequest(new { message = "Minimum 6 caractères." });

        var userId = long.Parse(User.FindFirstValue("id") ?? "0");
        var user   = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT IdUser, Password FROM Users WHERE IdUser = @Id", new { Id = userId });
        if (user == null) return NotFound(new { message = "Utilisateur introuvable." });

        bool ok;
        try { ok = BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.Password); }
        catch { ok = false; }
        if (!ok) return BadRequest(new { message = "Mot de passe actuel incorrect." });

        var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.ExecuteAsync(
            "UPDATE Users SET Password = @Hash WHERE IdUser = @Id",
            new { Hash = newHash, Id = userId });

        return Ok(new { message = "Mot de passe modifié avec succès." });
    }

    // ─── POST /api/auth/dev-reset ────────────────────────────
    // DEV ONLY: réinitialise le mot de passe d'un utilisateur par email.
    // À retirer ou protéger en production.
    [HttpPost("dev-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> DevResetPassword([FromBody] DevResetRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "Email et nouveau mot de passe requis." });
        if (req.NewPassword.Length < 6)
            return BadRequest(new { message = "Minimum 6 caractères." });

        var email = req.Email.Trim().ToLower();
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT IdUser FROM Users WHERE Email = @Email", new { Email = email });
        if (user == null)
            return NotFound(new { message = "Aucun utilisateur avec cet email." });

        var hash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.ExecuteAsync(
            "UPDATE Users SET Password = @Hash, Active = 1, IsVerified = 1 WHERE IdUser = @Id",
            new { Hash = hash, Id = user.IdUser });

        return Ok(new { message = "Mot de passe réinitialisé.", email, password = req.NewPassword });
    }

    public record DevResetRequest(string Email, string NewPassword);

    // ─── Helpers ─────────────────────────────────────────────
    internal static string MapRole(int idRole) => idRole switch
    {
        1 => "admin",
        3 => "vendor",
        _ => "user"
    };

    private static object BuildUserResponse(User u, string role) => new
    {
        id              = u.IdUser,
        email           = u.Email,
        role,
        first_name      = u.FirstName,
        last_name       = u.LastName,
        // Pour les vendeurs : afficher le nom d'entreprise (Username), sinon prénom+nom
        display_name    = role == "vendor"
                            ? (u.Username ?? $"{u.FirstName} {u.LastName}".Trim())
                            : $"{u.FirstName} {u.LastName}".Trim(),
        phone           = u.Telephone,
        shop_name       = u.Username,
        profile_picture = u.ProfilePicture,
        is_approved     = (u.IsVerified ?? 0) == 1,
        city            = u.City ?? u.StateName,
        address         = u.Location,
        country         = u.CountryName,
        is_premium      = (u.IsPremuim ?? 0) == 1,
        birth_date      = u.BirthDate,
        gender          = u.Gender,
    };
}
