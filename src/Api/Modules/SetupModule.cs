using System.Text.Json.Serialization;
using Api.Filters;
using Application.Common.Settings;
using Application.Models.ApiResult;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules;

public static class SetupModule
{
    public const string CorsPolicyName = "EcoTrackCors";

    public static IServiceCollection SetupServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new() { Title = "API", Version = "v1" }); });

        services.AddControllers(options =>
        {
            options.Filters.Add(typeof(OkResultAttribute));
            options.Filters.Add(typeof(ErrorResultFilterAttribute));
            options.Filters.Add(typeof(ContentResultFilterAttribute));
            options.Filters.Add(typeof(ModelStateValidationAttribute));
            options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ApiResult<Dictionary<string, List<string>>>),
                StatusCodes.Status400BadRequest));
            options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ApiResult),
                StatusCodes.Status401Unauthorized));
            options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ApiResult),
                StatusCodes.Status403Forbidden));
            options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ApiResult),
                StatusCodes.Status500InternalServerError));
        }).AddJsonOptions(options =>
        {
            // Serialize enums as their string names instead of integers — the SPA's typed
            // unions ('Open'|'Closed'|...) rely on this, and number-encoded enums silently
            // mismatch frontend expectations (badges show "0" instead of "Open" etc.).
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }).ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
            options.SuppressMapClientErrors = true;
        });
        services.AddCors(configuration);
        services.AddRefreshCookieSettings(configuration);

        // CSRF protection for the cookie-authenticated refresh endpoint. The request token is
        // echoed back by the SPA in the X-XSRF-TOKEN header; the secret lives in an httpOnly
        // cookie. SameSite=None + Secure so it rides along on the cross-origin refresh POST.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "ecotrack_xsrf";
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        services.AddApplicationSettings(configuration);
        services.AddAdminSettings(configuration);
        services.AddFrontendSettings(configuration);

        return services;
    }

    private static void AddRefreshCookieSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RefreshCookie").Get<RefreshCookieSettings>()
                       ?? new RefreshCookieSettings();
        services.AddSingleton(settings);
    }

    private static void AddAdminSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var adminSettings = configuration.GetSection("Admin").Get<AdminSettings>() ?? new AdminSettings();
        services.AddSingleton(adminSettings);
    }

    private static void AddFrontendSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var frontendSettings = configuration.GetSection("Frontend").Get<FrontendSettings>() ?? new FrontendSettings();
        services.AddSingleton(frontendSettings);
    }

    private static void AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
        services.AddSingleton(corsSettings);

        services.AddCors(options =>
            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (corsSettings.AllowedOrigins.Length == 0)
                {
                    return;
                }

                policy.WithOrigins(corsSettings.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }));
    }


    private static void AddApplicationSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.Get<ApplicationSettings>();
        if (settings != null)
        {
            services.AddSingleton(settings);
        }
    }
}