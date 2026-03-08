using Api.Filters;
using Application.Common.Settings;
using Application.Models.ApiResult;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules;

public static class SetupModule
{
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
        }).ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
            options.SuppressMapClientErrors = true;
        });
        services.AddCors();
        services.AddApplicationSettings(configuration);

        return services;
    }

    private static void AddCors(this IServiceCollection services)
    {
        services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()));
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