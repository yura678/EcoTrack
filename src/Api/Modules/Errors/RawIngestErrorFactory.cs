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
                UnconvertibleUnitsException => StatusCodes.Status422UnprocessableEntity,
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
        UnconvertibleUnitsException c => new
        {
            error = c.Message,
            deviceId = c.DeviceId,
            failures = c.Failures.Select(f => new
            {
                rowIndex = f.RowIndex,
                pollutantId = f.PollutantId,
                fromUnitId = f.FromUnitId,
                fromUnitSymbol = f.FromUnitSymbol,
                canonicalUnitId = f.CanonicalUnitId,
                canonicalUnitSymbol = f.CanonicalUnitSymbol,
                reason = f.Reason
            })
        },
        _ => new { error = error.Message }
    };
}
