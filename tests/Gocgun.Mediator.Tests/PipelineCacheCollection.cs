namespace Gocgun.Mediator.Tests;

/// <summary>
/// Serializes every test that sends through <see cref="Sender"/>. The compiled pipeline cache is
/// static, so a parallel test class would add or clear entries while another test counts them.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PipelineCacheCollection
{
    /// <summary>Collection name.</summary>
    public const string Name = "PipelineCache";

    private PipelineCacheCollection()
    {
    }
}
