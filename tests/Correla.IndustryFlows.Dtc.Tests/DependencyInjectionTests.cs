using Correla.IndustryFlows.Dtc.DependencyInjection;
using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>Tests for <see cref="ServiceCollectionExtensions.AddDtcParser"/>.</summary>
public sealed class DependencyInjectionTests
{
    private static string BundlePath()
    {
        var dir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(dir);
        while (current is not null && !current.GetFiles("*.sln").Any())
        {
            current = current.Parent;
        }

        return Path.Combine(current!.FullName, "docs", "elec", "15.4", "schemas");
    }

    [Fact]
    public void AddDtcParser_ResolvesDtcProcessor()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts => opts.BundlePath = BundlePath());

        var provider = services.BuildServiceProvider();
        var processor = provider.GetService<DtcProcessor>();

        Assert.NotNull(processor);
    }

    [Fact]
    public void AddDtcParser_RegistryIsPopulated()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts => opts.BundlePath = BundlePath());

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ISchemaRegistry>();

        Assert.True(registry.Manifest.Count >= 206);
    }

    [Fact]
    public void AddDtcParser_DefaultPredicatesRegistered()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts =>
        {
            opts.BundlePath = BundlePath();
            opts.RegisterDefaultPredicates = true;
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPredicateRegistry>();

        Assert.True(registry.TryGet("mpanCheckDigit", out _));
        Assert.True(registry.TryGet("dtcDateTime", out _));
        Assert.True(registry.TryGet("dtcMidnightHh", out _));
    }

    [Fact]
    public void AddDtcParser_RegisterDefaultPredicatesFalse_NoneRegistered()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts =>
        {
            opts.BundlePath = BundlePath();
            opts.RegisterDefaultPredicates = false;
        });

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IPredicateRegistry>();

        Assert.False(registry.TryGet("mpanCheckDigit", out _));
    }

    [Fact]
    public void AddDtcParser_DtcProcessorIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts => opts.BundlePath = BundlePath());

        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<DtcProcessor>();
        var second = provider.GetRequiredService<DtcProcessor>();

        Assert.Same(first, second);
    }
}

