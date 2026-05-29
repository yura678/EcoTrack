namespace Simulator.Http;

public sealed class ApiEnvelope<T>
{
    public bool IsSuccess { get; set; }

    // The API serializes ApiResultStatusCode through JsonStringEnumConverter (SetupModule),
    // so this arrives as the enum name ("Success"), not a number. Typing it as int makes
    // System.Text.Json (with AllowReadingFromString) try to parse "Success" as an integer
    // and throw. The simulator never reads this field — keep it as the string the API sends.
    public string? StatusCode { get; set; }
    public string? Message { get; set; }
    public string? RequestId { get; set; }
    public T? Data { get; set; }
}
