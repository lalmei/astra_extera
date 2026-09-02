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
