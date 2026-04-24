using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using TijaraApi.Services;

// DealsDB26 uses PascalCase columns — Dapper default matching is case-insensitive

var builder = WebApplication.CreateBuilder(args);

// ─── Services ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Sérialisation snake_case pour correspondre au frontend Angular
        opts.JsonSerializerOptions.PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower;
        opts.JsonSerializerOptions.DictionaryKeyPolicy         = null; // garder les clés dict telles quelles
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddSingleton<DbService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<EmailService>();

// ─── CORS : accepte tous les localhost (dev) ─────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("TijaraCors", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
                new Uri(origin).Host == "localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── JWT Authentication ──────────────────────────────────────
// Disable default claim-type remapping so "role"→"role" and "id"→"id" as-is
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "tijara_secret_key_2026_very_secure";
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ClockSkew                = TimeSpan.Zero,
            // Keep claim names exactly as written in the token
            NameClaimType = "id",
            RoleClaimType = "role",
        };
        opts.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync("{\"message\":\"Token manquant ou invalide.\"}");
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode  = 403;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync("{\"message\":\"Accès refusé.\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// ─── Build ────────────────────────────────────────────────────
var app = builder.Build();

// Initialize additional tables (Follows, Notifications)
var db = app.Services.GetRequiredService<DbService>();
await db.InitializeTablesAsync();

app.UseCors("TijaraCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/api/health", () => new
{
    status    = "OK",
    app       = "Tijara API (.NET)",
    version   = "2.0.0",
    timestamp = DateTime.UtcNow
});

Console.WriteLine("\n╔══════════════════════════════════════════════╗");
Console.WriteLine("║  🚀 Tijara API .NET  →  http://localhost:5000  ║");
Console.WriteLine("╚══════════════════════════════════════════════╝\n");

app.Run();
