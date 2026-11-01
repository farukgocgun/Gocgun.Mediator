using Microsoft.Extensions.DependencyInjection;

namespace Gocgun.Mediator.Tests;

/// <summary>Dispatch without a behavior.</summary>
[Collection(PipelineCacheCollection.Name)]
public sealed class SenderTests
{
    /// <summary>The handler receives the request and returns its value.</summary>
    [Fact]
    public async Task Send_dispatches_handler()
    {
        Sender.ClearPipelineCache();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<PingRequest, string>, PingHandler>();
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISender>().SendAsync(new PingRequest("a"));
        Assert.Equal("a", result);
    }

    /// <summary>A null request is rejected.</summary>
    [Fact]
    public async Task Null_request_throws()
    {
        Sender.ClearPipelineCache();
        var services = new ServiceCollection();
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            provider.GetRequiredService<ISender>().SendAsync<string>(null!).AsTask());
    }

    /// <summary>A missing handler fails when the request is sent.</summary>
    [Fact]
    public async Task Missing_handler_throws()
    {
        Sender.ClearPipelineCache();
        var services = new ServiceCollection();
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<ISender>().SendAsync(new PingRequest("x")).AsTask());
    }

    /// <summary>The second send reuses the compiled pipeline.</summary>
    [Fact]
    public async Task Second_call_uses_cache()
    {
        Sender.ClearPipelineCache();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<PingRequest, string>, PingHandler>();
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        await sender.SendAsync(new PingRequest("1"));
        var countAfterFirst = Sender.CachedPipelineCount;
        await sender.SendAsync(new PingRequest("2"));
        Assert.Equal(countAfterFirst, Sender.CachedPipelineCount);
        Assert.Equal(1, Sender.CachedPipelineCount);
    }

    /// <summary>The cancellation token reaches the handler.</summary>
    [Fact]
    public async Task Cancellation_reaches_handler()
    {
        Sender.ClearPipelineCache();
        var handler = new TokenProbeHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<TokenProbeRequest, bool>>(handler);
        services.AddSingleton<ISender, Sender>();
        await using var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await provider.GetRequiredService<ISender>().SendAsync(new TokenProbeRequest(), cts.Token);
        Assert.True(handler.Token.IsCancellationRequested);
    }
}
