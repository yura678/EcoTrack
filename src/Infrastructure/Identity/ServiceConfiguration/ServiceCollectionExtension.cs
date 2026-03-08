using System.Security.Claims;
using System.Text;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Identity;
using Application.Models.ApiResult;
using Domain.Entities.User;
using Infrastructure.Identity.Dtos;
using Infrastructure.Identity.Extensions;
using Infrastructure.Identity.Jwt;
using Infrastructure.Identity.Manager;
using Infrastructure.Identity.PermissionManager;
using Infrastructure.Identity.SeedDatabaseService;
using Infrastructure.Identity.Store;
using Infrastructure.Identity.UserManager;
using Infrastructure.Identity.validator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Extensions;

namespace Infrastructure.Identity.ServiceConfiguration;

public static class ServiceCollectionExtension
{
    public static IServiceCollection RegisterIdentityServices(this IServiceCollection services,
        IdentitySettings identitySettings)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAppUserManager, AppUserManagerImplementation>();
        services.AddScoped<ISeedDataBase, SeedDataBase>();

        services.AddScoped<IUserValidator<User>, AppUserValidator>();
        services.AddScoped<UserValidator<User>, AppUserValidator>();

        services.AddScoped<IUserClaimsPrincipalFactory<User>, AppUserClaimsPrincipleFactory>();

        services.AddScoped<IRoleValidator<Role>, AppRoleValidator>();
        services.AddScoped<RoleValidator<Role>, AppRoleValidator>();

        services.AddScoped<IAuthorizationHandler, DynamicPermissionHandler>();
        services.AddScoped<IDynamicPermissionService, DynamicPermissionService>();
        services.AddScoped<IRoleStore<Role>, RoleStore>();
        services.AddScoped<IUserStore<User>, AppUserStore>();
        services.AddScoped<IRoleManagerService, RoleManagerService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        services.AddIdentity<User, Role>(options =>
            {
                options.Stores.ProtectPersonalData = false;

                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireUppercase = false;

                options.SignIn.RequireConfirmedEmail = true;
                options.SignIn.RequireConfirmedPhoneNumber = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = false;
                options.User.RequireUniqueEmail = false;

                //options.Stores.ProtectPersonalData = true;
            }).AddUserStore<AppUserStore>()
            .AddRoleStore<RoleStore>().
            //.AddUserValidator<AppUserValidator>().
            //AddRoleValidator<AppRoleValidator>().
            AddUserManager<AppUserManager>()
            .AddRoleManager<AppRoleManager>()
            .AddErrorDescriber<AppErrorDescriber>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipleFactory>()
            .AddDefaultTokenProviders().AddSignInManager<AppSignInManager>()
            .AddDefaultTokenProviders()
            .AddPasswordlessLoginTotpTokenProvider();


        //For [ProtectPersonalData] Attribute In Identity

        //services.AddScoped<ILookupProtectorKeyRing, KeyRing>();

        //services.AddScoped<ILookupProtector, LookupProtector>();

        //services.AddScoped<IPersonalDataProtector, PersonalDataProtector>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ConstantPolicies.DynamicPermission, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new DynamicPermissionRequirement());
            });
        });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            var secretkey = Encoding.UTF8.GetBytes(identitySettings.SecretKey);
            var encryptionkey = Encoding.UTF8.GetBytes(identitySettings.EncryptKey);

            var validationParameters = new TokenValidationParameters
            {
                ClockSkew = TimeSpan.Zero, // default: 5 min
                RequireSignedTokens = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(secretkey),

                RequireExpirationTime = true,
                ValidateLifetime = true,

                ValidateAudience = true, //default : false
                ValidAudience = identitySettings.Audience,

                ValidateIssuer = true, //default : false
                ValidIssuer = identitySettings.Issuer,

                TokenDecryptionKey = new SymmetricSecurityKey(encryptionkey),
            };

            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = validationParameters;
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    //var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(JwtBearerEvents));
                    //logger.LogError("Authentication failed.", context.Exception);

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var signInManager = context.HttpContext.RequestServices.GetRequiredService<AppSignInManager>();

                    var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
                    if (claimsIdentity.Claims?.Any() != true)
                        context.Fail("This token has no claims.");

                    var securityStamp =
                        claimsIdentity.FindFirstValue(new ClaimsIdentityOptions().SecurityStampClaimType);
                    if (!securityStamp.HasValue())
                        context.Fail("This token has no security stamp");

                    //Find user and token from database and perform your custom validation
                    var userId = claimsIdentity.GetUserId<Guid>();
                    // var user = await userRepository.GetByIdAsync(context.HttpContext.RequestAborted, userId);

                    //if (user.SecurityStamp != Guid.Parse(securityStamp))
                    //    context.Fail("Token security stamp is not valid.");

                    var validatedUser = await signInManager.ValidateSecurityStampAsync(context.Principal);
                    if (validatedUser == null)
                        context.Fail("Token security stamp is not valid.");
                },
                OnChallenge = async context =>
                {
                    //var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(JwtBearerEvents));
                    //logger.LogError("OnChallenge error", context.Error, context.ErrorDescription);
                    if (context.AuthenticateFailure is SecurityTokenExpiredException)
                    {
                        context.HandleResponse();

                        var response = new ApiResult(false,
                            ApiResultStatusCode.UnAuthorized, "Token is expired. refresh your token");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(response);
                    }

                    else if (context.AuthenticateFailure != null)
                    {
                        context.HandleResponse();

                        var response = new ApiResult(false,
                            ApiResultStatusCode.UnAuthorized, "Token is Not Valid");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(response);
                    }

                    else
                    {
                        context.HandleResponse();

                        context.Response.StatusCode = (int)StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ApiResult(false, ApiResultStatusCode.UnAuthorized,
                            "Invalid access token. Please login"));
                    }
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = (int)StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new ApiResult(false,
                        ApiResultStatusCode.Forbidden, ApiResultStatusCode.Forbidden.ToDisplay()));
                }
            };
        });

        return services;
    }

    public static async Task SeedDefaultUsersAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var seedService = scope.ServiceProvider.GetRequiredService<ISeedDataBase>();
        await seedService.Seed();
    }
}