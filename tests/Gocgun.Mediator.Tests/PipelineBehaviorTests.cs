using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Mediator.Tests;

/// <summary>Pipeline order, short-circuit, and exceptions.</summary>
[Collection(PipelineCacheCollection.Name)]
public sealed class PipelineBehaviorTests
{
    /// <summary>With no behaviors the handler runs alone.</summary>
    [Fact]
    public async Task No_behaviors_invokes_handler_directly()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
            services.AddSingleton<IRequestHandler<TracedRequest, string>, TracedHandler>());
        var result = await provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a"));
        Assert.Equal("a", result);
        Assert.Equal(["handler"], trace.Entries);
    }

    /// <summary>Behaviors run in registration order and unwind in reverse.</summary>
    [Fact]
    public async Task Behaviors_run_in_registration_order()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
        {
            services.AddSingleton<IRequestHandler<TracedRequest, string>, TracedHandler>();
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new TracedBehavior("b1", trace));
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new TracedBehavior("b2", trace));
        });
        var result = await provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a"));
        Assert.Equal("a/b2/b1", result);
        Assert.Equal(["b1:before", "b2:before", "handler", "b2:after", "b1:after"], trace.Entries);
    }

    /// <summary>A behavior can skip the handler.</summary>
    [Fact]
    public async Task Behavior_can_short_circuit()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
        {
            services.AddSingleton<IRequestHandler<TracedRequest, string>, TracedHandler>();
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new ShortCircuitBehavior(trace));
        });
        var result = await provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a"));
        Assert.Equal("short", result);
        Assert.Equal(["short-circuit"], trace.Entries);
    }

    /// <summary>A handler exception propagates and later steps do not run.</summary>
    [Fact]
    public async Task Handler_exception_propagates()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
        {
            services.AddSingleton<IRequestHandler<TracedRequest, string>, FaultyHandler>();
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new TracedBehavior("b1", trace));
        });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a")).AsTask());
        Assert.Equal("handler boom", exception.Message);
        Assert.Equal(["b1:before"], trace.Entries);
    }

    /// <summary>A behavior exception propagates and the handler is not called.</summary>
    [Fact]
    public async Task Behavior_exception_propagates()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
        {
            services.AddSingleton<IRequestHandler<TracedRequest, string>, TracedHandler>();
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new ThrowingBehavior());
        });
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a")).AsTask());
        Assert.Equal("behavior boom", exception.Message);
        Assert.Empty(trace.Entries);
    }

    /// <summary>Open behaviors run in registration order.</summary>
    [Fact]
    public async Task Open_behaviors_run_in_registration_order()
    {
        Sender.ClearPipelineCache();
        FirstOpenBehavior<PingRequest, string>.Trace.Clear();
        var services = new ServiceCollection();
        services.AddOpenBehavior(typeof(FirstOpenBehavior<,>));
        services.AddOpenBehavior(typeof(SecondOpenBehavior<,>));
        services.AddSingleton<IRequestHandler<PingRequest, string>, PingHandler>();
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        Assert.Equal("a", await provider.GetRequiredService<ISender>().SendAsync(new PingRequest("a")));
        Assert.Equal(["first", "second"], FirstOpenBehavior<PingRequest, string>.Trace);
    }

    /// <summary>A behavior calling next twice re-runs the inner behaviors and the handler each time.</summary>
    [Fact]
    public async Task Calling_next_twice_reruns_inner_pipeline()
    {
        var trace = new CallTrace();
        await using var provider = Build(trace, services =>
        {
            services.AddSingleton<IRequestHandler<TracedRequest, string>, TracedHandler>();
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new TwiceBehavior());
            services.AddSingleton<IPipelineBehavior<TracedRequest, string>>(new TracedBehavior("inner", trace));
        });
        await provider.GetRequiredService<ISender>().SendAsync(new TracedRequest("a"));
        Assert.Equal(["inner:before", "handler", "inner:after", "inner:before", "handler", "inner:after"], trace.Entries);
    }

    private static ServiceProvider Build(CallTrace trace, Action<IServiceCollection> configure)
    {
        Sender.ClearPipelineCache();
        var services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddSingleton<ISender, Sender>();
        configure(services);
        return services.BuildServiceProvider();
    }
}
