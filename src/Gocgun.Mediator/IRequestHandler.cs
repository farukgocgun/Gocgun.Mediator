namespace Gocgun.Mediator;

/// <summary>Handles one request type.</summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles <paramref name="request"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token that cancels the handler.</param>
    /// <returns>The response.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
