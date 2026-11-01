namespace Gocgun.Mediator.Tests;

/// <summary>Request used by dispatch tests.</summary>
public sealed record PingRequest(string Value) : IRequest<string>;

/// <summary>Returns the request value.</summary>
public sealed class PingHandler : IRequestHandler<PingRequest, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(PingRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(request.Value);
}

/// <summary>Request used to observe the cancellation token.</summary>
public sealed record TokenProbeRequest : IRequest<bool>;

/// <summary>Stores the token it was called with.</summary>
public sealed class TokenProbeHandler : IRequestHandler<TokenProbeRequest, bool>
{
    /// <summary>Gets the token from the last call.</summary>
    public CancellationToken Token { get; private set; }

    /// <inheritdoc />
    public ValueTask<bool> HandleAsync(TokenProbeRequest request, CancellationToken cancellationToken)
    {
        Token = cancellationToken;
        return ValueTask.FromResult(cancellationToken.IsCancellationRequested);
    }
}

/// <summary>Shared call order.</summary>
public sealed class CallTrace
{
    private readonly List<string> entries = [];

    /// <summary>Gets a snapshot of the recorded entries.</summary>
    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (entries)
            {
                return entries.ToArray();
            }
        }
    }

    /// <summary>Records <paramref name="entry"/>.</summary>
    /// <param name="entry">A step name.</param>
    public void Add(string entry)
    {
        lock (entries)
        {
            entries.Add(entry);
        }
    }
}

/// <summary>Request whose handler records a trace.</summary>
/// <param name="Value">Payload echoed by the handler.</param>
public sealed record TracedRequest(string Value) : IRequest<string>;

/// <summary>Records "handler" and returns the request value.</summary>
public sealed class TracedHandler(CallTrace trace) : IRequestHandler<TracedRequest, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TracedRequest request, CancellationToken cancellationToken)
    {
        trace.Add("handler");
        return ValueTask.FromResult(request.Value);
    }
}

/// <summary>Throws from the handler.</summary>
public sealed class FaultyHandler : IRequestHandler<TracedRequest, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TracedRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("handler boom");
}

/// <summary>Records before and after the rest of the pipeline.</summary>
public sealed class TracedBehavior(string name, CallTrace trace) : IPipelineBehavior<TracedRequest, string>
{
    /// <inheritdoc />
    public async ValueTask<string> HandleAsync(
        TracedRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        trace.Add($"{name}:before");
        var response = await next().ConfigureAwait(false);
        trace.Add($"{name}:after");
        return $"{response}/{name}";
    }
}

/// <summary>Does not call the rest of the pipeline.</summary>
public sealed class ShortCircuitBehavior(CallTrace trace) : IPipelineBehavior<TracedRequest, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(
        TracedRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        trace.Add("short-circuit");
        return ValueTask.FromResult("short");
    }
}

/// <summary>Throws before the handler.</summary>
public sealed class ThrowingBehavior : IPipelineBehavior<TracedRequest, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(
        TracedRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("behavior boom");
}

/// <summary>Open behavior that records its place in the pipeline.</summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class FirstOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Gets the steps recorded by open behaviors.</summary>
    public static List<string> Trace { get; } = [];

    /// <inheritdoc />
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Trace.Add("first");
        return next();
    }
}

/// <summary>Second open behavior.</summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class SecondOpenBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        FirstOpenBehavior<TRequest, TResponse>.Trace.Add("second");
        return next();
    }
}

/// <summary>Calls the rest of the pipeline twice.</summary>
public sealed class TwiceBehavior : IPipelineBehavior<TracedRequest, string>
{
    /// <inheritdoc />
    public async ValueTask<string> HandleAsync(
        TracedRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        await next().ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }
}
