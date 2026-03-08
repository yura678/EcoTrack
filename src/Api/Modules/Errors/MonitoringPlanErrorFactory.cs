using Application.Features.MonitoringPlans.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class MonitoringPlanErrorFactory
{
    public static ObjectResult ToObjectResult(this MonitoringPlanException error)
    {
        return new ObjectResult(error.Message)
        {
            StatusCode = error switch
            {
                MonitoringPlanNotFoundException
                    or InstallationNotFoundException
                    or EmissionSourceNotFoundException
                    or PollutantNotFoundException
                    or MonitoringRequirementNotFoundException => StatusCodes.Status404NotFound,
                MonitoringPlanInvalidStatusException
                    or ActiveMonitoringPlanAlreadyExistsException => StatusCodes.Status409Conflict,
                UnhandledMonitoringPlanException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("Monitoring plan error handler does not implemented.")
            }
        };
    }
}