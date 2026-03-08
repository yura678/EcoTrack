using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Api.Swagger;

public class CustomTokenRequiredOperationFilter : IOperationProcessor
{
    
    public bool Process(OperationProcessorContext context)
    {
        var hasAttribute = context.MethodInfo
            .GetCustomAttributes(typeof(RequireTokenWithoutAuthorizationAttribute), false).Any();

        if (hasAttribute)
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = OpenApiSecurityApiKeyLocation.Header,
                Name = "Authorization"
            };

            var securityRequirement = new OpenApiSecurityRequirement { { securityScheme.Scheme, new List<string>() } };

            
            context.OperationDescription.Operation.Security=[securityRequirement];
        }

        return true;
    }
}
