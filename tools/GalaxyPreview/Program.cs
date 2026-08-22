using AstraExtera.Galaxy;

long? seed = null;
var output = Path.GetFullPath("dist/galaxy-preview.html");
var openAfterWrite = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed" when i + 1 < args.Length:
            seed = long.Parse(args[++i]);
            break;
        case "--out" when i + 1 < args.Length:
            output = Path.GetFullPath(args[++i]);
            break;
        case "--open":
            openAfterWrite = true;
            break;
        default:
            Console.Error.WriteLine("Usage: GalaxyPreview [--seed 42] [--out dist/galaxy-preview.html] [--open]");
            return 2;
    }
}

var chosenSeed = seed ?? Random.Shared.Next(1, int.MaxValue);
var placement = GalaxyGenerator.Generate(chosenSeed);
var directory = Path.GetDirectoryName(output);
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

var samplingTimer = System.Diagnostics.Stopwatch.StartNew();
var sky = StarFieldSampler.Sample(placement);
samplingTimer.Stop();
File.WriteAllText(output, GalaxyDebugHtml.Render(placement, sky));

var catalogPath = Path.Combine(
    Path.GetDirectoryName(output) ?? ".",
    "star-catalog.v1.json");
File.WriteAllText(catalogPath, StarCatalogExport.ToJson(placement, sky, new StarCatalogExportOptions { Indent = true }));
File.WriteAllText(
    Path.Combine(Path.GetDirectoryName(output) ?? ".", "guide-stars.v1.json"),
    StarCatalogExport.EmptyGuideGroupsJson());

Console.WriteLine(GalaxyPlacementCodec.Describe(placement));
Console.WriteLine(
    $"Visible sky: naked-eye stars={sky.ExpectedVisibleCount:N0}; drawn={sky.SampledCount:N0}; " +
    $"catalog={sky.Stars.Count:N0}; brightest m={(sky.Stars.Count > 0 ? sky.Stars[0].ApparentMagnitude : double.NaN):0.00}; " +
    $"pole {placement.Orientation.PoleTiltFromGalacticPoleDeg:0.0}° off the galactic pole; " +
    $"sampled in {samplingTimer.ElapsedMilliseconds} ms.");
Console.WriteLine(output);
Console.WriteLine(catalogPath);

if (openAfterWrite)
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "open",
        Arguments = output,
        UseShellExecute = false
    });
}

return 0;
