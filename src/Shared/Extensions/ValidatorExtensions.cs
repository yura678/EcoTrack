using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Shared.ValidationBase;
using Shared.ValidationBase.Interfaces;

namespace Shared.Extensions;

public static class ValidatorExtensions
{
    public static IServiceCollection RegisterValidatorsAsServices(this IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(c => c != typeof(ValidatorExtensions).Assembly)
            .ToArray();

       
        services.AddValidatorsFromAssemblies(assemblies);

        var customValidatableTypes = assemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidatableModel<>)))
            .ToList();

        foreach (var type in customValidatableTypes)
        {
            var constructors = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructors == null) continue;

            var parametersLength = constructors.GetParameters().Length;

            var dummyArgs = new object?[parametersLength];

            
            var parameters = constructors.GetParameters();
            for (int i = 0; i < parametersLength; i++)
            {
                dummyArgs[i] = parameters[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(parameters[i].ParameterType)
                    : null;
            }

            var model = Activator.CreateInstance(type, dummyArgs);
            var methodInfo = type.GetMethod(nameof(IValidatableModel<object>.ValidateApplicationModel));

            if (model != null)
            {
                var methodArgument =
                    Activator.CreateInstance(typeof(ApplicationBaseValidationModelProvider<>).MakeGenericType(type));
                var validator = methodInfo?.Invoke(model, new[] { methodArgument });

                if (validator != null)
                {
                    var validatorInterface = validator.GetType().GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));

                    if (validatorInterface != null)
                    {
                        services.AddScoped(validatorInterface, _ => validator);
                    }
                }
            }
        }

        return services;
    }
}