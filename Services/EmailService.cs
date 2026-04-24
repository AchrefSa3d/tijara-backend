using System.Net;
using System.Net.Mail;

namespace TijaraApi.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string toName, string confirmationLink)
    {
        var cfg  = _config.GetSection("Email");
        var host = cfg["SmtpHost"];

        if (string.IsNullOrWhiteSpace(host))
        {
            // Mode développement — afficher le lien dans la console
            _logger.LogInformation(
                "\n╔══════════════════════════════════════════════════════════════╗\n" +
                "║  📧  CONFIRMATION EMAIL (mode dev — SMTP non configuré)     ║\n" +
                "║  Destinataire : {Email}                                      ║\n" +
                "║  Lien         : {Link}                                       ║\n" +
                "╚══════════════════════════════════════════════════════════════╝",
                toEmail, confirmationLink);
            return;
        }

        var port      = int.TryParse(cfg["SmtpPort"], out var p) ? p : 587;
        var ssl       = cfg["EnableSsl"]?.ToLower() != "false";
        var user      = cfg["Username"]  ?? "";
        var pass      = cfg["Password"]  ?? "";
        var fromAddr  = cfg["FromAddress"] ?? "noreply@tijara.tn";
        var fromName  = cfg["FromName"]    ?? "Tijara";

        var body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,sans-serif;background:#f8f9fc;margin:0;padding:32px'>
  <div style='max-width:520px;margin:0 auto;background:#fff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.08)'>
    <!-- Header -->
    <div style='background:linear-gradient(135deg,#405189,#0ab39c);padding:32px;text-align:center'>
      <div style='font-size:28px;font-weight:700;color:#fff;letter-spacing:0.5px'>
        🛍️ Tijara
      </div>
      <p style='color:rgba(255,255,255,.75);margin:8px 0 0;font-size:14px'>La marketplace tunisienne</p>
    </div>

    <!-- Body -->
    <div style='padding:40px 32px'>
      <h2 style='color:#1a2d6e;margin:0 0 16px;font-size:22px'>Confirmez votre adresse e-mail</h2>
      <p style='color:#555;line-height:1.7;font-size:15px'>
        Bonjour <strong>{toName}</strong>,<br><br>
        Merci de vous être inscrit sur <strong>Tijara</strong> ! Pour activer votre compte, veuillez cliquer sur le bouton ci-dessous.
      </p>

      <div style='text-align:center;margin:32px 0'>
        <a href='{confirmationLink}' style='background:linear-gradient(135deg,#405189,#0ab39c);color:#fff;text-decoration:none;padding:14px 36px;border-radius:10px;font-size:16px;font-weight:600;display:inline-block'>
          ✅ Confirmer mon e-mail
        </a>
      </div>

      <p style='color:#888;font-size:13px;line-height:1.6'>
        Ce lien expire dans <strong>24 heures</strong>. Si vous n'avez pas créé de compte sur Tijara, ignorez cet e-mail.
      </p>

      <div style='border-top:1px solid #eee;margin-top:32px;padding-top:16px;color:#aaa;font-size:12px;text-align:center'>
        &copy; {DateTime.Now.Year} Tijara — Marketplace Tunisienne
      </div>
    </div>
  </div>
</body>
</html>";

        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl   = ssl,
            Credentials = new NetworkCredential(user, pass),
        };

        using var message = new MailMessage
        {
            From       = new MailAddress(fromAddr, fromName),
            Subject    = "Confirmez votre e-mail — Tijara",
            Body       = body,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(toEmail, toName));

        await smtp.SendMailAsync(message);
        _logger.LogInformation("📧 Email de confirmation envoyé à {Email}", toEmail);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        var cfg  = _config.GetSection("Email");
        var host = cfg["SmtpHost"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation(
                "📧 [DEV] Réinitialisation mot de passe pour {Email}: {Link}", toEmail, resetLink);
            return;
        }

        var port      = int.TryParse(cfg["SmtpPort"], out var p) ? p : 587;
        var ssl       = cfg["EnableSsl"]?.ToLower() != "false";
        var fromAddr  = cfg["FromAddress"] ?? "noreply@tijara.tn";
        var fromName  = cfg["FromName"]    ?? "Tijara";
        var user      = cfg["Username"]    ?? "";
        var pass      = cfg["Password"]    ?? "";

        var body = $@"
<html><body style='font-family:Segoe UI,sans-serif'>
  <div style='max-width:520px;margin:0 auto'>
    <h2>Réinitialisation de votre mot de passe</h2>
    <p>Bonjour {toName},</p>
    <p>Cliquez ci-dessous pour réinitialiser votre mot de passe :</p>
    <a href='{resetLink}' style='background:#405189;color:#fff;padding:12px 28px;border-radius:8px;text-decoration:none;display:inline-block'>Réinitialiser</a>
    <p style='color:#888;font-size:12px;margin-top:24px'>Lien valide 1 heure. Si vous n'avez pas demandé ça, ignorez cet email.</p>
  </div>
</body></html>";

        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl   = ssl,
            Credentials = new NetworkCredential(user, pass),
        };
        using var msg = new MailMessage
        {
            From       = new MailAddress(fromAddr, fromName),
            Subject    = "Réinitialisation de mot de passe — Tijara",
            Body       = body,
            IsBodyHtml = true,
        };
        msg.To.Add(toEmail);
        await smtp.SendMailAsync(msg);
    }
}
