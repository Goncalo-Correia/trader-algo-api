using System.Threading.Channels;

namespace TraderAlgoApi.Services.Jobs;

public sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    // Unbounded: job ids are tiny and enqueue must never block a request thread.
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(long jobId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(jobId, cancellationToken);

    public ValueTask<long> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
