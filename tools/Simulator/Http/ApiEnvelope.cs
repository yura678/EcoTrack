namespace Simulator.Http;

public sealed class ApiEnvelope<T>
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public string? RequestId { get; set; }
    public T? Data { get; set; }
}
