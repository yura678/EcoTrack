using Application.Features.MonitoringDevices.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class MonitoringDeviceErrorFactory
{
    public static ObjectResult ToObjectResult(this MonitoringDeviceException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                MonitoringDeviceNumberAlreadyExistsException
                    or MonitoringDeviceHasDependenciesException
                    or ParentInstallationDecommissionedException => StatusCodes.Status409Conflict,
                InvalidEmissionSourceInstallationException => StatusCodes.Status400BadRequest,
                EmissionSourceNotFoundException
                    or MonitoringDeviceNotFoundException
                    or InstallationNotFoundException => StatusCodes.Status404NotFound,
                UnhandledMonitoringDeviceException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Monitoring device error handler does not implemented.")
            }
        };
    }
}