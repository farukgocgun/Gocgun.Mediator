using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Mediator;

/// <summary>Default <see cref="ISender"/>. The pipeline for each request type is compiled once.</summary>
internal sealed class Sender(IServiceProvider provider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> PipelineCache = new();

    private delegate ValueTask<TResponse> PipelineDelegate<TResponse>(
        IServiceProvider serviceProvider,
        IRequest<TResponse> request,
        CancellationToken cancellationToken);

    internal static int CachedPipelineCount => PipelineCache.Count;

    /// <summary>Invokes the handler after the behaviors registered for this request.</summary>
    /// <typeparam name="TRequest">Request type.</typeparam>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="serviceProvider">Scope that contains the handler.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Token passed to the handler.</param>
    /// <returns>The response.</returns>
    public static ValueTask<TResponse> InvokeAsync<TRequest, TResponse>(
        IServiceProvider serviceProvider,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var resolved = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var behaviors = resolved as IPipelineBehavior<TRequest, TResponse>[] ?? [.. resolved];
        if (behaviors.Length == 0)
        {
            return handler.HandleAsync(request, cancellationToken);
        }

        return new PipelineInvocation<TRequest, TResponse>(behaviors, handler, request, cancellationToken).Continue();
    }

    /// <inheritdoc />
    public ValueTask<TResponse> SendAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pipeline = (PipelineDelegate<TResponse>)PipelineCache.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => BuildPipeline<TResponse>(requestType, responseType),
            typeof(TResponse));

        return pipeline(provider, request, cancellationToken);
    }

    internal static void ClearPipelineCache() => PipelineCache.Clear();

    [RequiresDynamicCode("Compiles one expression per request type.")]
    [RequiresUnreferencedCode("Binds a generic method per request type.")]
    private static PipelineDelegate<TResponse> BuildPipeline<TResponse>(Type requestType, Type responseType)
    {
        if (responseType != typeof(TResponse))
        {
            throw new InvalidOperationException("The cached response type does not match the request.");
        }

        var invoke = typeof(Sender)
            .GetMethod(nameof(InvokeAsync), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(requestType, typeof(TResponse));

        var providerParameter = Expression.Parameter(typeof(IServiceProvider), "provider");
        var requestParameter = Expression.Parameter(typeof(IRequest<TResponse>), "request");
        var tokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        var body = Expression.Call(
            invoke,
            providerParameter,
            Expression.Convert(requestParameter, requestType),
            tokenParameter);

        return Expression
            .Lambda<PipelineDelegate<TResponse>>(body, providerParameter, requestParameter, tokenParameter)
            .Compile();
    }

    private sealed class PipelineInvocation<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IPipelineBehavior<TRequest, TResponse>[] behaviors;
        private readonly IRequestHandler<TRequest, TResponse> handler;
        private readonly TRequest request;
        private readonly CancellationToken cancellationToken;

        public PipelineInvocation(
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            IRequestHandler<TRequest, TResponse> handler,
            TRequest request,
            CancellationToken cancellationToken)
        {
            this.behaviors = behaviors;
            this.handler = handler;
            this.request = request;
            this.cancellationToken = cancellationToken;
        }

        public ValueTask<TResponse> Continue() => Run(0);

        // Each next() is bound to its own position, so a behavior may call it more than once.
        private ValueTask<TResponse> Run(int position)
        {
            if (position >= behaviors.Length)
            {
                return handler.HandleAsync(request, cancellationToken);
            }

            return behaviors[position].HandleAsync(request, () => Run(position + 1), cancellationToken);
        }
    }
}
