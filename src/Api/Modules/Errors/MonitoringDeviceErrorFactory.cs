using Application.Features.MonitoringDevices.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class MonitoringDeviceErrorFactory
{
    public static ObjectResult ToObjectResult(this MonitoringDeviceException error)
    {
        var (statusCode, fieldName) = MapError(error);
        return ErrorBodyBuilder.Build(statusCode, error.Message, fieldName);
    }

    private static (int StatusCode, string? FieldName) MapError(MonitoringDeviceException error) => error switch
    {
        MonitoringDeviceNumberAlreadyExistsException => (StatusCodes.Status409Conflict, "SerialNumber"),
        MonitoringDeviceHasDependenciesException
            or ParentInstallationDecommissionedException => (StatusCodes.Status409Conflict, null),
        InvalidEmissionSourceInstallationException => (StatusCodes.Status400BadRequest, null),
        EmissionSourceNotFoundException
            or MonitoringDeviceNotFoundException
            or InstallationNotFoundException => (StatusCodes.Status404NotFound, null),
        UnhandledMonitoringDeviceException => (StatusCodes.Status500InternalServerError, null),
        _ => throw new NotImplementedException(
            $"Monitoring device error handler is not implemented for {error.GetType().Name}.")
    };
}
