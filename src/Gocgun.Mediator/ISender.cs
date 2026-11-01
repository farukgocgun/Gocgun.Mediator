namespace Gocgun.Mediator;

/// <summary>Sends a request to its single handler.</summary>
public interface ISender
{
    /// <summary>Dispatches <paramref name="request"/>.</summary>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token passed through the pipeline.</param>
    /// <returns>The response.</returns>
    ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
