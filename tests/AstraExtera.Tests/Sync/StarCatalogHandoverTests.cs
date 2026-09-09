using AstraExtera.Galaxy;
using AstraExtera.Sync;
using AstraTerra.Astronomy;
using Xunit;

namespace AstraExtera.Tests.Sync;

/// <summary>
/// The catalog both sides hand AstraTerra. A server reads its star catalog to validate the
/// constellations players submit, to fill prepared books, and to answer <c>/stars</c>, so what it
/// publishes has to be the same numbered sky its clients are drawing -- otherwise a figure a player
/// can see gets rejected, or accepted against different stars.
/// </summary>
public sealed class StarCatalogHandoverTests
{
    /// <summary>
    /// The server publishes from the field it holds in memory; a client publishes from the same field
    /// after it has been through the join packet. Both catalogs have to agree star for star, which is
    /// why the authored field is quantized to its stored precision before anything else sees it.
    /// </summary>
    [Fact]
    public void Both_Sides_Number_The_Same_Sky()
    {
        var authored = GalaxySky.Author(42);
        var received = new GalaxySky(
            GalaxyPlacementCodec.FromUtf8(GalaxyPlacementCodec.ToUtf8(authored.Placement)),
            StarFieldCodec.FromBytes(StarFieldCodec.ToBytes(authored.StarField)),
            LocalSystemSkyCodec.FromUtf8(LocalSystemSkyCodec.ToUtf8(authored.LocalSky)));

        var onTheServer = StarCatalogHandover.ToCatalog(authored);
        var onAClient = StarCatalogHandover.ToCatalog(received);

        Assert.NotEmpty(onTheServer.Stars);
        Assert.Equal(onTheServer.Stars.Count, onAClient.Stars.Count);
        for (var index = 0; index < onTheServer.Stars.Count; index++)
        {
            var expected = onTheServer.Stars[index];
            var actual = onAClient.Stars[index];
            Assert.Equal(expected.Hip, actual.Hip);
            Assert.Equal(expected.RightAscensionDeg, actual.RightAscensionDeg);
            Assert.Equal(expected.DeclinationDeg, actual.DeclinationDeg);
            Assert.Equal(expected.VisualMagnitude, actual.VisualMagnitude);
            Assert.Equal(expected.BvColorIndex, actual.BvColorIndex);
            Assert.Equal(expected.IsGuideStar, actual.IsGuideStar);
        }
    }

    /// <summary>
    /// Ids run 1..N by brightness rank with none missing, so every edge a player stores resolves to a
    /// star on the server as well as on their own screen. A gap would make server-side validation
    /// reject a figure that draws perfectly.
    /// </summary>
    [Fact]
    public void Every_Star_Id_Resolves()
    {
        var catalog = StarCatalogHandover.ToCatalog(GalaxySky.Author(7));

        Assert.Equal(
            Enumerable.Range(1, catalog.Stars.Count),
            catalog.Stars.Select(static star => star.Hip));
        Assert.Equal(catalog.Stars.Count, catalog.Stars.Select(static star => star.Hip).Distinct().Count());
    }

    /// <summary>
    /// Nothing of Earth's sky rides along. Its guide groups, cultures and deep-sky objects are keyed
    /// to Earth's own star ids, so keeping them would point named figures at unrelated stars.
    /// </summary>
    [Fact]
    public void Earths_Figures_Do_Not_Survive_The_Handover()
    {
        var catalog = StarCatalogHandover.ToCatalog(GalaxySky.Author(7));

        Assert.Empty(catalog.GuideGroups);
        Assert.Empty(catalog.SkyCultures);
        Assert.Empty(catalog.DeepSkyObjects);
        Assert.Contains(catalog.Stars, static star => star.IsGuideStar);
    }

    /// <summary>
    /// A reroll is a different sky under the same ids, which is exactly why the server has to publish
    /// too: left on Earth's catalog it would keep validating against a sky two rerolls old.
    /// </summary>
    [Fact]
    public void A_Reroll_Renumbers_The_Sky()
    {
        var before = StarCatalogHandover.ToCatalog(GalaxySky.Author(42));
        var after = StarCatalogHandover.ToCatalog(GalaxySky.Author(43));

        Assert.NotEqual(
            before.Stars.Select(static star => (star.RightAscensionDeg, star.DeclinationDeg)),
            after.Stars.Select(static star => (star.RightAscensionDeg, star.DeclinationDeg)));
    }
}
