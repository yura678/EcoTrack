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

    // Each body is an ApiError: Message carries the reason (so it lands in the envelope's
    // `message`), Data keeps the structured context the SPA needs — without the redundant
    // `error` text that used to duplicate the message.
    private static ApiError BuildBody(RawIngestException error) => error switch
    {
        UnconfiguredDevicePollutantsException u => new ApiError(u.Message, new
        {
            deviceId = u.DeviceId,
            unconfiguredPollutantIds = u.UnconfiguredPollutantIds
        }),
        UnconvertibleUnitsException c => new ApiError(c.Message, new
        {
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
        }),
        _ => new ApiError(error.Message)
    };
}
