using System.Text.Json;
using System.Text.RegularExpressions;
using AstraExtera;
using Xunit;

namespace AstraExtera.Tests.Smoke;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public void Runtime_Version_Stays_In_Sync_With_Modinfo()
    {
        using var stream = File.OpenRead(Path.Combine(RepositoryRoot, "modinfo.json"));
        using var document = JsonDocument.Parse(stream);
        var modinfoVersion = document.RootElement.GetProperty("version").GetString();

        Assert.NotNull(modinfoVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+$", modinfoVersion);
        Assert.Equal(AstraExteraModMetadata.Version, modinfoVersion);
        Assert.Equal(
            "AstraExtera " + modinfoVersion + ": procedural sky engine on AstraTerra.",
            AstraExteraModMetadata.StartupLogMessage);
    }

    [Fact]
    public void Makefile_Exposes_Version_Bump_Target()
    {
        var makefile = File.ReadAllText(Path.Combine(RepositoryRoot, "Makefile"));

        Assert.Contains("bump-version:", makefile);
        Assert.Contains("bump-patch-version:", makefile);
        Assert.Matches(new Regex(@"make bump-version\s+VERSION=0\.1\.2"), makefile);
        Assert.Matches(new Regex(@"make bump-patch-version\s+Increment patch version"), makefile);
    }

    /// <summary>
    /// The AstraTerra version this mod needs is written down in four places, and the bump tooling
    /// updates none of them.
    /// </summary>
    /// <remarks>
    /// <c>make bump-version-files</c> rewrites AstraExtera's own version wherever it appears, but
    /// the dependency is a different number with a different reason to change -- it moves when
    /// AstraExtera starts relying on something new over the boundary, not when AstraExtera ships.
    /// So it is maintained by hand, and this is what stops the hand from missing one: a README that
    /// asks for an older AstraTerra than modinfo actually requires sends a player to a download
    /// that will not load.
    /// </remarks>
    [Fact]
    public void The_Required_AstraTerra_Version_Is_The_Same_In_Every_Place_It_Is_Written()
    {
        using var stream = File.OpenRead(Path.Combine(RepositoryRoot, "modinfo.json"));
        using var document = JsonDocument.Parse(stream);
        var required = document.RootElement
            .GetProperty("dependencies")
            .GetProperty("astraterra")
            .GetString();

        Assert.NotNull(required);
        Assert.Matches(@"^\d+\.\d+\.\d+$", required);

        foreach (var (relativePath, expected) in new[]
        {
            ("README.md", $"AstraTerra {required} or newer"),
            (Path.Combine("docs", "player-guide.md"), $"AstraTerra {required} or newer"),
            (Path.Combine(".github", "ISSUE_TEMPLATE", "bug_report.yml"), $"placeholder: v{required}"),
        })
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
            Assert.True(
                text.Contains(expected, StringComparison.Ordinal),
                $"{relativePath} does not say \"{expected}\"; modinfo.json requires AstraTerra {required}");
        }
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraExtera.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
