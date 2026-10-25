using Polly;
using Polly.Registry;

foreach (var typed in new[] { false, true })
{
    const int concurrency = 8;
    using var barrier = new Barrier(concurrency);
    using var reload = new CancellationTokenSource();
    var registry = new ResiliencePipelineRegistry<string>();
    var built = 0;
    var disposed = 0;

    void Configure(ResiliencePipelineBuilderBase builder, ConfigureBuilderContext<string> context)
    {
        var number = Interlocked.Increment(ref built);
        context.OnPipelineDisposed(() => Interlocked.Increment(ref disposed));
        context.AddReloadToken(reload.Token);
        // Force all callers to enter configuration before any can publish its pipeline.
        if (number <= concurrency && !barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Failed to synchronize the callers.");
        }
    }

    var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Factory.StartNew<object>(
        () => typed
            ? registry.GetOrAddPipeline<int>("same-key", (builder, context) => Configure(builder, context))
            : registry.GetOrAddPipeline("same-key", (builder, context) => Configure(builder, context)),
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

    var pipelines = await Task.WhenAll(tasks);
    var unique = pipelines.Distinct(ReferenceEqualityComparer.Instance).Count();
    await registry.DisposeAsync();
    Console.WriteLine($"{(typed ? "Generic" : "Non-generic")}: built={built}, distinct returned={unique}, disposed after registry shutdown={disposed}");

    var before = built;
    reload.Cancel();
    Console.WriteLine($"  Builds triggered AFTER registry disposal by reload signal: {built - before}; expected 0");
    Console.WriteLine($"  Total built={built}, total disposed={disposed}, undisposed={built - disposed}");

    if (built <= concurrency || disposed >= built || unique != 1)
    {
        throw new InvalidOperationException("The expected reproduction did not occur; investigate before claiming a bug.");
    }
}
