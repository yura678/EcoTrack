using Application.Features.RawIngest.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Modules.Errors;

public static class RawIngestErrorFactory
{
    public static ObjectResult ToObjectResult(this RawIngestException error)
    {
        return new ObjectResult(BuildBody(error))
        {
            StatusCode = error switch
            {
                UnconfiguredDevicePollutantsException => StatusCodes.Status400BadRequest,
                UnhandledRawIngestException => StatusCodes.Status500InternalServerError,
                _ => throw new NotImplementedException("RawIngest error handler is not implemented.")
            }
        };
    }

    private static object BuildBody(RawIngestException error) => error switch
    {
        UnconfiguredDevicePollutantsException u => new
        {
            error = u.Message,
            deviceId = u.DeviceId,
            unconfiguredPollutantIds = u.UnconfiguredPollutantIds
        },
        _ => new { error = error.Message }
    };
}
