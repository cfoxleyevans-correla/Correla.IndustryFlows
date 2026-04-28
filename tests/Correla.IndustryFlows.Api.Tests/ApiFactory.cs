using Correla.IndustryFlows.Dtc.Schema;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Correla.IndustryFlows.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that replaces <see cref="ISchemaRegistry"/> with an
/// instance bound to the absolute schema bundle path found by walking up from the
/// test output directory to the solution root. This sidesteps the timing issue where
/// <c>Program.cs</c> reads configuration (and resolves the bundle path) before
/// <c>ConfigureAppConfiguration</c> callbacks have a chance to run.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        var bundlePath = Path.Combine(FindSolutionRoot(), "docs", "elec", "15.4", "schemas");

        // ConfigureServices runs after Program.cs has registered its services, so we
        // can remove the registry registered with the development bundle path and replace
        // it with one pointing to the absolute path resolved from the solution root.
        builder.ConfigureServices(services =>
        {
            var existing = services.SingleOrDefault(s => s.ServiceType == typeof(ISchemaRegistry));
            if (existing is not null)
            {
                services.Remove(existing);
            }

            services.AddSingleton<ISchemaRegistry>(_ => new FileSchemaRegistry(bundlePath));
        });
    }

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> until a directory
    /// containing a <c>.sln</c> file is found. Throws if the solution root
    /// cannot be located.
    /// </summary>
    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !current.GetFiles("*.sln").Any())
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                "Could not locate the solution root from the test output directory.");
        }

        return current.FullName;
    }
}
