using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Tests.Common;

public record CreatedJob(Job Job, IState State);

/// <summary>
/// Test double for <see cref="IBackgroundJobClient"/> that records every Create call so tests
/// can assert which Hangfire jobs the production code attempted to enqueue. State transitions
/// always succeed — there's no real worker behind this.
/// </summary>
public class RecordingBackgroundJobClient : IBackgroundJobClient
{
    public ConcurrentQueue<CreatedJob> Created { get; } = new();

    public string Create(Job job, IState state)
    {
        Created.Enqueue(new CreatedJob(job, state));
        return Guid.NewGuid().ToString();
    }

    public bool ChangeState(string jobId, IState state, string? expectedState) => true;

    public void Clear() => Created.Clear();
}
