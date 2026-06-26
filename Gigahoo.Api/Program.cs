using System.Text;
using Gigahoo.Api.BackgroundServices;
using Gigahoo.Api.Data;
using Gigahoo.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Gigahoo API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/gigahoo-.log", rollingInterval: RollingInterval.Day));

    // Database
    builder.Services.AddDbContext<GigahooDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sql =>
            {
                sql.MigrationsAssembly("Gigahoo.Api");
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                sql.CommandTimeout(60);
            }));

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]!;
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero,
            };
        });

    builder.Services.AddAuthorization();

    // Services
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
    builder.Services.AddScoped<IOtpService, OtpService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IStripeService, StripeService>();
    builder.Services.AddScoped<ITwilioService, TwilioService>();
    builder.Services.AddScoped<IPhoneNumberCleanupService, PhoneNumberCleanupService>();

    // HttpClient factory (used by Telnyx providers)
    builder.Services.AddHttpClient();

    // Telephony + SMS providers (config-selectable: Twilio | Telnyx)
    var telephonyProvider = builder.Configuration["Telephony:Provider"] ?? "Twilio";
    if (string.Equals(telephonyProvider, "Telnyx", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddScoped<Gigahoo.Api.Services.Providers.ITelephonyProvider, Gigahoo.Api.Services.Providers.TelnyxTelephonyProvider>();
    else
        builder.Services.AddScoped<Gigahoo.Api.Services.Providers.ITelephonyProvider, Gigahoo.Api.Services.Providers.TwilioTelephonyProvider>();

    var smsProvider = builder.Configuration["Sms:Provider"] ?? "Twilio";
    if (string.Equals(smsProvider, "Telnyx", StringComparison.OrdinalIgnoreCase))
        builder.Services.AddScoped<Gigahoo.Api.Services.Providers.ISmsProvider, Gigahoo.Api.Services.Providers.TelnyxSmsProvider>();
    else
        builder.Services.AddScoped<Gigahoo.Api.Services.Providers.ISmsProvider, Gigahoo.Api.Services.Providers.TwilioSmsProvider>();

    // OTP delivery (delegates to ISmsProvider)
    builder.Services.AddScoped<ISmsService, SmsService>();

    // Background services
    builder.Services.AddHostedService<PhoneNumberCleanupBackgroundService>();

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        // Rate limiting effectively disabled for now (very high limit, no queue so
        // requests never wait). Re-tighten these when going to real production volume.
        options.AddFixedWindowLimiter("api", config =>
        {
            config.Window = TimeSpan.FromMinutes(1);
            config.PermitLimit = 1_000_000;
            config.QueueLimit = 0;
            config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.AddFixedWindowLimiter("auth", config =>
        {
            config.Window = TimeSpan.FromMinutes(1);
            config.PermitLimit = 1_000_000;
            config.QueueLimit = 0;
            config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.OnRejected = async (context, ct) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests" }, ct);
        };
    });

    // CORS
    // The allowed Gigahoo domain hosts come from the DB (the Domain table is the
    // single source of truth). The list is loaded ONCE at startup (after the app
    // is built, below) and captured by the policy predicate.
    var gigahooDomains = new List<string>();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            // Allowed: configured origins (e.g. localhost for dev) plus every
            // Gigahoo regional domain and its www host (loaded from the DB).
            policy.SetIsOriginAllowed(origin =>
                  {
                      if (origins.Contains(origin)) return true;
                      try
                      {
                          var host = new Uri(origin).Host.ToLowerInvariant();
                          if (host.StartsWith("www.")) host = host[4..];
                          return gigahooDomains.Contains(host);
                      }
                      catch { return false; }
                  })
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Gigahoo API",
            Version = "v1",
            Description = "AI Phone Receptionist API for Home Service Businesses"
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<GigahooDbContext>();

    // Security headers
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });

    var app = builder.Build();

    // Load the Gigahoo regional domain hosts from the DB ONCE at startup so the
    // CORS predicate (registered above) can allow those origins. Strip a leading
    // "www." so the predicate's www-stripped host comparison matches.
    using (var scope = app.Services.CreateScope())
    {
        var domainDb = scope.ServiceProvider.GetRequiredService<GigahooDbContext>();
        var hosts = await domainDb.Domains.Select(d => d.Host).ToListAsync();
        gigahooDomains.AddRange(hosts.Select(h => h.ToLowerInvariant().StartsWith("www.") ? h.ToLowerInvariant()[4..] : h.ToLowerInvariant()));
    }

    // Middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
        app.UseExceptionHandler("/error");
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // Global error handler
    app.Map("/error", () => Results.Problem());

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
