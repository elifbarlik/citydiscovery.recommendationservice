using CityDiscovery.RecommendationService.Api.Models;
using CityDiscovery.RecommendationService.Application.Common.Interfaces;
using CityDiscovery.RecommendationService.Infrastructure;
using CityDiscovery.RecommendationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;

// .env dosyasından ortam değişkenlerini yükle
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Environment variable'ları konfigürasyona ekle (.env dosyasındaki değerler appsettings.json'ı override eder)
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "CityDiscovery Öneri Servisi API",
        Version = "v1",
        Description = "Kullanıcılara kişiselleştirilmiş mekan önerileri sunan, etkileşim tabanlı öneri motoru API'sidir. " +
                      "Cosine similarity, popülerlik, zaman bozunumu ve oturum yakınlığı gibi hibrit skorlama yöntemleri kullanır.",
        Contact = new() { Name = "CityDiscovery Ekibi" }
    });

    // JWT Bearer auth desteği — Swagger UI'da "Authorize" butonu
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT token girin. Örnek: eyJhbGciOiJIUzI1NiIs..."
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JWT Authentication — değerler .env dosyasından okunur
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key yapılandırılmamış. .env dosyasını kontrol edin.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "citydiscovery";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// Register Infrastructure services (no messaging — consumers run in Workers only)
builder.Services.AddInfrastructure(builder.Configuration, addMessaging: false);

// Rate Limiting — 30 istek/dakika per kullanıcı
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("per-user", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });
});

var app = builder.Build();

// ==========================================================================
// Database Migration at Startup
// ==========================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Starting database migration for RecommendationService...");
        var dbContext = services.GetRequiredService<RecommendationDbContext>();
        dbContext.Database.Migrate();
        logger.LogInformation("Database migration completed successfully for RecommendationService.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the RecommendationService database.");
        throw; // Uygulamanın bozuk DB ile ayağa kalkmasını engelle
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Correlation ID middleware — her istekte benzersiz izleme ID'si
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    using (context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CorrelationId").BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        await next();
    }
});

// Health check endpoint — anonim
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "CityDiscovery.RecommendationService" }))
    .WithName("HealthCheck")
    .WithOpenApi(opt =>
    {
        opt.Summary = "Sağlık Kontrolü";
        opt.Description = "Servisin ayakta olup olmadığını kontrol eder.\n\n" +
                          "**Çıktı:** `{ status: 'healthy', service: 'CityDiscovery.RecommendationService' }`\n\n" +
                          "Kimlik doğrulama gerektirmez.";
        return opt;
    })
    .Produces<object>(200)
    .WithTags("Sistem")
    .AllowAnonymous();

// Temporary Test Endpoint for Gemini Embedding — anonim (development only)
app.MapPost("/test-embedding", async (string text, IEmbeddingProvider provider) =>
{
    var vector = await provider.GetVectorAsync(text);
    return Results.Ok(new { text, vectorLength = vector.Length, sample = vector.Take(5) });
})
.WithName("TestEmbedding")
.WithOpenApi(opt =>
{
    opt.Summary = "Embedding Test (Geliştirme Amaçlı)";
    opt.Description = "Verilen metni embedding vektörüne dönüştürür. Sadece geliştirme ortamında kullanılmak içindir.\n\n" +
                      "**Girdi:** Query string üzerinden `text` parametresi (metin)\n\n" +
                      "**Çıktı:** `{ text, vectorLength, sample: [ilk 5 değer] }`";
    return opt;
})
.Produces<object>(200)
.WithTags("Geliştirme")
.AllowAnonymous();

// Recommendations endpoint: GET /recommendations/{userId}?cityId=...&limit=20&offset=0&categories=restaurant,cafe
app.MapGet("/recommendations/{userId:guid}", async (
    Guid userId,
    int cityId,
    HttpContext httpContext,
    IRecommendationCacheService cacheService,
    ISessionService sessionService,
    CancellationToken ct,
    int limit = 20,
    int offset = 0,
    string? categories = null) =>
{
    var sw = Stopwatch.StartNew();
    // Validation
    if (cityId <= 0)
        return Results.BadRequest(new { error = "cityId must be a positive integer" });
    if (limit < 1 || limit > 50)
        return Results.BadRequest(new { error = "limit must be between 1 and 50" });
    if (offset < 0)
        return Results.BadRequest(new { error = "offset must be >= 0" });

    // Parse categories
    List<string>? categoryList = null;
    if (!string.IsNullOrWhiteSpace(categories))
        categoryList = categories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // SessionId: header X-Session-Id, then query sessionId, then get/create from session service
    Guid? sessionId = null;
    var headerSession = httpContext.Request.Headers["X-Session-Id"].FirstOrDefault();
    if (Guid.TryParse(headerSession, out var hs))
        sessionId = hs;
    else if (Guid.TryParse(httpContext.Request.Query["sessionId"].FirstOrDefault(), out var qs))
        sessionId = qs;

    var hadActiveSession = sessionService.GetActiveSession(userId).HasValue;
    if (!sessionId.HasValue)
        sessionId = sessionService.GetActiveSession(userId) ?? sessionService.StartSession(userId);

    var result = await cacheService.GetOrComputeAsync(userId, cityId, limit, offset, sessionId, hadActiveSession, categoryList, ct);
    if (result == null)
        return Results.Problem("Failed to compute recommendations");

    var response = new RecommendationResponse(
        UserId: userId,
        CityId: cityId,
        SessionId: sessionId.Value,
        Venues: result.Venues.Select(v => new VenueRecommendationDto(v.VenueId, v.Score, v.Strategy, v.SessionInfluenced)).ToList(),
        GeneratedAt: DateTime.UtcNow,
        Strategy: result.Strategy,
        TotalCount: result.Venues.Count);

    sw.Stop();
    httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Recommendations").LogInformation(
            "Recommendations computed for User {UserId} in {ElapsedMs}ms — Strategy: {Strategy}, Count: {Count}",
            userId, sw.ElapsedMilliseconds, response.Strategy, response.TotalCount);

    return Results.Ok(response);
})
.WithName("GetRecommendations")
.WithOpenApi(opt =>
{
    opt.Summary = "Kullanıcıya Özel Mekan Önerileri";
    opt.Description = "Kullanıcı profiline göre kişiselleştirilmiş mekan önerileri döndürür.\n\n" +
                      "**Zorunlu Parametreler:**\n" +
                      "- `userId` (path): Kullanıcı ID'si (GUID)\n" +
                      "- `cityId` (query): Şehir ID'si (int)\n\n" +
                      "**Opsiyonel Parametreler:**\n" +
                      "- `limit` (query): Döndürülecek öneri sayısı (1-50, varsayılan: 20)\n" +
                      "- `offset` (query): Sayfalama için atlama sayısı (>= 0, varsayılan: 0)\n" +
                      "- `categories` (query): Virgülle ayrılmış kategori filtresi (örn: `restaurant,cafe`)\n" +
                      "- `X-Session-Id` (header) veya `sessionId` (query): Oturum ID'si\n\n" +
                      "**Çıktı:** `{ userId, cityId, sessionId, venues: [{ venueId, score, strategy, sessionInfluenced }], generatedAt, strategy, totalCount }`\n\n" +
                      "**Stratejiler:** `hybrid` (profil tabanlı), `popularity_fallback` (yeni kullanıcı), `preference_fallback` (tercih tabanlı)";
    return opt;
})
.Produces<RecommendationResponse>(200)
.Produces(400)
.WithTags("Öneriler")
.RequireAuthorization()
.RequireRateLimiting("per-user");

// === Dismiss Endpoints ===
app.MapPost("/recommendations/{userId:guid}/dismiss/{venueId:guid}", async (
    Guid userId,
    Guid venueId,
    IDismissedVenueRepository dismissRepo,
    IRecommendationCacheInvalidator cacheInvalidator) =>
{
    await dismissRepo.DismissAsync(userId, venueId);
    cacheInvalidator.InvalidateForUser(userId);
    return Results.Ok(new { message = "Venue dismissed", userId, venueId });
})
.WithName("DismissVenue")
.WithOpenApi(opt =>
{
    opt.Summary = "Mekanı Önerilerden Kaldır";
    opt.Description = "Kullanıcının beğenmediği mekanı öneri listesinden kalıcı olarak çıkarır.\n\n" +
                      "**Girdi:** `userId` ve `venueId` (path, GUID)\n\n" +
                      "**Çıktı:** `{ message, userId, venueId }`\n\n" +
                      "Kaldırılan mekan bir sonraki öneri isteğinde artık gösterilmez. Geri almak için DELETE endpoint'ini kullanın.";
    return opt;
})
.Produces<object>(200)
.WithTags("Öneriler")
.RequireAuthorization();

app.MapDelete("/recommendations/{userId:guid}/dismiss/{venueId:guid}", async (
    Guid userId,
    Guid venueId,
    IDismissedVenueRepository dismissRepo,
    IRecommendationCacheInvalidator cacheInvalidator) =>
{
    await dismissRepo.UndismissAsync(userId, venueId);
    cacheInvalidator.InvalidateForUser(userId);
    return Results.Ok(new { message = "Venue undismissed", userId, venueId });
})
.WithName("UndismissVenue")
.WithOpenApi(opt =>
{
    opt.Summary = "Mekan Kaldırma İşlemini Geri Al";
    opt.Description = "Daha önce önerilerden kaldırılmış bir mekanı tekrar önerilere dahil eder.\n\n" +
                      "**Girdi:** `userId` ve `venueId` (path, GUID)\n\n" +
                      "**Çıktı:** `{ message, userId, venueId }`";
    return opt;
})
.Produces<object>(200)
.WithTags("Öneriler")
.RequireAuthorization();

// === Trending Endpoint ===
app.MapGet("/venues/trending", async (
    int cityId,
    IInteractionRepository interactionRepo,
    CancellationToken ct,
    int limit = 10,
    int days = 7) =>
{
    if (cityId <= 0) return Results.BadRequest(new { error = "cityId must be a positive integer" });
    if (limit < 1 || limit > 50) return Results.BadRequest(new { error = "limit must be between 1 and 50" });
    if (days < 1 || days > 30) return Results.BadRequest(new { error = "days must be between 1 and 30" });

    var trending = await interactionRepo.GetTrendingVenueIdsAsync(cityId, days, limit, ct);
    return Results.Ok(new { cityId, days, venues = trending });
})
.WithName("GetTrendingVenues")
.WithOpenApi(opt =>
{
    opt.Summary = "Trend Mekanları Listele";
    opt.Description = "Belirli bir şehirdeki son N gün içinde en çok etkileşim alan mekanları döndürür.\n\n" +
                      "**Zorunlu Parametreler:**\n" +
                      "- `cityId` (query): Şehir ID'si (int)\n\n" +
                      "**Opsiyonel Parametreler:**\n" +
                      "- `limit` (query): Döndürülecek mekan sayısı (1-50, varsayılan: 10)\n" +
                      "- `days` (query): Kaç günlük veri kullanılacak (1-30, varsayılan: 7)\n\n" +
                      "**Çıktı:** `{ cityId, days, venues: [venueId1, venueId2, ...] }`\n\n" +
                      "Kimlik doğrulama gerektirmez. Etkileşim sayısına göre sıralanır (beğeni, kaydetme, favori, inceleme vb.)";
    return opt;
})
.Produces<object>(200)
.Produces(400)
.WithTags("Mekanlar")
.AllowAnonymous();

// === User Preferences Endpoints ===
app.MapGet("/users/{userId:guid}/preferences", async (
    Guid userId,
    IUserPreferenceRepository prefRepo,
    CancellationToken ct) =>
{
    var pref = await prefRepo.GetByUserIdAsync(userId, ct);
    if (pref == null) return Results.Ok(new { userId, preferredCategories = new List<string>() });
    return Results.Ok(new { userId, pref.PreferredCategories, pref.UpdatedAt });
})
.WithName("GetUserPreferences")
.WithOpenApi(opt =>
{
    opt.Summary = "Kullanıcı Tercihlerini Getir";
    opt.Description = "Kullanıcının kayıtlı kategori tercihlerini döndürür.\n\n" +
                      "**Girdi:** `userId` (path, GUID)\n\n" +
                      "**Çıktı:** `{ userId, preferredCategories: ['restaurant', 'cafe', ...], updatedAt }`\n\n" +
                      "Tercih kaydı yoksa boş liste döner.";
    return opt;
})
.Produces<object>(200)
.WithTags("Kullanıcı Tercihleri")
.RequireAuthorization();

app.MapPut("/users/{userId:guid}/preferences", async (
    Guid userId,
    PreferenceRequest request,
    IUserPreferenceRepository prefRepo,
    IRecommendationCacheInvalidator cacheInvalidator,
    CancellationToken ct) =>
{
    if (request.PreferredCategories == null || request.PreferredCategories.Count == 0)
        return Results.BadRequest(new { error = "At least one category is required" });

    var pref = new CityDiscovery.RecommendationService.Domain.Entities.UserPreference
    {
        UserId = userId,
        PreferredCategories = request.PreferredCategories,
        UpdatedAt = DateTime.UtcNow
    };
    await prefRepo.UpsertAsync(pref, ct);
    cacheInvalidator.InvalidateForUser(userId);
    return Results.Ok(new { message = "Preferences saved", userId, request.PreferredCategories });
})
.WithName("SetUserPreferences")
.WithOpenApi(opt =>
{
    opt.Summary = "Kullanıcı Tercihlerini Kaydet / Güncelle";
    opt.Description = "Kullanıcının kategori tercihlerini kaydeder veya günceller. Onboarding sırasında veya sonrasında kullanılır.\n\n" +
                      "**Girdi:** `userId` (path, GUID) + JSON body: `{ preferredCategories: ['restaurant', 'museum', ...] }`\n\n" +
                      "**Çıktı:** `{ message, userId, preferredCategories }`\n\n" +
                      "En az bir kategori belirtilmelidir. Tercihler, yeni kullanıcılar için cold-start önerilerinde kullanılır.";
    return opt;
})
.Produces<object>(200)
.Produces(400)
.WithTags("Kullanıcı Tercihleri")
.RequireAuthorization();

// Session endpoints — authenticated
app.MapPost("/sessions/start", (Guid userId, ISessionService sessionService) =>
{
    var sessionId = sessionService.StartSession(userId);
    return Results.Ok(new { sessionId });
})
.WithName("StartSession")
.WithOpenApi(opt =>
{
    opt.Summary = "Oturum Başlat";
    opt.Description = "Kullanıcı için yeni bir tarama oturumu başlatır.\n\n" +
                      "**Girdi:** `userId` (query, GUID)\n\n" +
                      "**Çıktı:** `{ sessionId }`\n\n" +
                      "Oturum ID'si, önerilerde oturum yakınlığı skoru hesaplamak için kullanılır.";
    return opt;
})
.Produces<object>(200)
.WithTags("Oturum Yönetimi")
.RequireAuthorization();

app.MapGet("/sessions/active", (Guid userId, ISessionService sessionService) =>
{
    var sessionId = sessionService.GetActiveSession(userId);
    return Results.Ok(new { sessionId });
})
.WithName("GetActiveSession")
.WithOpenApi(opt =>
{
    opt.Summary = "Aktif Oturumu Sorgula";
    opt.Description = "Kullanıcının şu an aktif olan oturumunu döndürür.\n\n" +
                      "**Girdi:** `userId` (query, GUID)\n\n" +
                      "**Çıktı:** `{ sessionId }` — Aktif oturum yoksa `sessionId: null`";
    return opt;
})
.Produces<object>(200)
.WithTags("Oturum Yönetimi")
.RequireAuthorization();

app.MapPost("/sessions/end", (Guid userId, ISessionService sessionService) =>
{
    sessionService.EndSession(userId);
    return Results.Ok();
})
.WithName("EndSession")
.WithOpenApi(opt =>
{
    opt.Summary = "Oturumu Sonlandır";
    opt.Description = "Kullanıcının aktif oturumunu sonlandırır.\n\n" +
                      "**Girdi:** `userId` (query, GUID)\n\n" +
                      "**Çıktı:** HTTP 200 (body yok)";
    return opt;
})
.Produces(200)
.WithTags("Oturum Yönetimi")
.RequireAuthorization();

app.Run();
