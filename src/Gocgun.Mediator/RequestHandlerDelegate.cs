namespace Gocgun.Mediator;

/// <summary>Continues the pipeline. The delegate does not take a token; the behavior already has one.</summary>
/// <typeparam name="TResponse">Response type.</typeparam>
/// <returns>The response from the rest of the pipeline.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();
