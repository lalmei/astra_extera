using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace AstraExtera.Tests.Smoke;

public sealed class DocumentationAssetTests
{
    [Fact]
    public void Handbook_Page_Uses_Astronomy_Category_And_Resolved_Lang_Keys()
    {
        var handbookPath = Path.Combine(
            RepositoryRoot,
            "assets/astraextera/config/handbook/00-generated-sky.json");
        using var handbook = JsonDocument.Parse(File.ReadAllText(handbookPath));
        var root = handbook.RootElement;
        var lang = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepositoryRoot, "assets/astraextera/lang/en.json")))!;

        Assert.Equal("astraextera-generated-sky", root.GetProperty("pageCode").GetString());
        Assert.Equal("astraterra", root.GetProperty("categoryCode").GetString());

        foreach (var property in new[] { "title", "text" })
        {
            var key = root.GetProperty(property).GetString()!;
            Assert.StartsWith("game:astraextera-handbook-", key);
            Assert.True(lang.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                $"Missing lang entry '{key}'.");
        }
    }

    [Fact]
    public void Handbook_Links_Only_To_Existing_AstraTerra_Pages()
    {
        var lang = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepositoryRoot, "assets/astraextera/lang/en.json")))!;
        var text = lang["game:astraextera-handbook-generated-sky-text"];
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "astraterra-astrolabe",
            "astraterra-sextant",
            "astraterra-skydisc",
            "astraterra-telescopes"
        };
        var linked = Regex.Matches(text, "handbook://([^\"]+)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected.Order(), linked.Order());
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
