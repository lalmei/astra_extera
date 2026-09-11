using System.Reflection;
using AstraExtera.Commands;
using AstraExtera.Config;
using AstraExtera.Galaxy;
using AstraExtera.Sync;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Xunit;

namespace AstraExtera.Tests.Commands;

public sealed class GalaxyServerCommandsTests
{
    static GalaxyServerCommandsTests()
    {
        // The real command framework localizes permission errors even in a headless test.
        var assets = ApiDouble.Create<IAssetManager>((method, _) => method.Name switch
        {
            "get_Origins" => new List<IAssetOrigin>(),
            _ => throw new NotSupportedException(method.Name)
        });
        Lang.LoadLanguage(ApiDouble.Create<ILogger>((_, _) => null), assets);
        Lang.ChangeLanguage("en");
    }

    [Fact]
    public void Console_Reroll_Replaces_All_Stored_Data_And_Broadcasts_It()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky!;

        var result = server.Execute("astraextera reroll -1234 confirm");

        Assert.Equal(EnumCommandStatus.Success, result.Status);
        Assert.Contains("7 -> -1234", result.StatusMessage);
        Assert.Equal(7, server.World.Seed);
        var replacement = server.Sync.Sky!;
        Assert.Equal(-1234, replacement.Placement.WorldSeed);
        Assert.NotEqual(previous.Placement, replacement.Placement);
        Assert.NotEqual(previous.StarField.Stars, replacement.StarField.Stars);
        Assert.NotEqual(previous.LocalSky, replacement.LocalSky);
        Assert.Equal(GalaxyGenerator.Generate(-1234), replacement.Placement);

        var packet = Assert.Single(server.Broadcasts);
        Assert.Equal(3, server.Writes);
        Assert.Equal(server.Stored[AstraExteraModMetadata.GalaxySaveKey], packet.Payload);
        Assert.Equal(server.Stored[AstraExteraModMetadata.StarFieldSaveKey], packet.StarFieldPayload);
        Assert.Equal(server.Stored[AstraExteraModMetadata.LocalSkySaveKey], packet.LocalSkyPayload);
        Assert.Equal(replacement.Placement, GalaxyPlacementCodec.FromUtf8(packet.Payload));
        Assert.Equal(replacement.StarField.Stars, StarFieldCodec.FromBytes(packet.StarFieldPayload).Stars);
        Assert.Equal(LocalSystemSkyCodec.ToUtf8(replacement.LocalSky), packet.LocalSkyPayload);

        server.Join();
        Assert.Equal(packet.Payload, Assert.Single(server.JoinPackets).Payload);
        Assert.Equal(packet.StarFieldPayload, server.JoinPackets[0].StarFieldPayload);
        Assert.Equal(packet.LocalSkyPayload, server.JoinPackets[0].LocalSkyPayload);

        using var restarted = new TestServer(server.Stored);
        Assert.Equal(0, restarted.LoadWrites);
        Assert.Equal(replacement.Placement, restarted.Sync.Sky!.Placement);
        Assert.Equal(replacement.StarField.Stars, restarted.Sync.Sky.StarField.Stars);
        Assert.Equal(packet.LocalSkyPayload, LocalSystemSkyCodec.ToUtf8(restarted.Sync.Sky.LocalSky));
        restarted.Join();
        Assert.Equal(packet.Payload, Assert.Single(restarted.JoinPackets).Payload);
        Assert.Contains("seed=-1234", restarted.Execute("astraextera galaxy").StatusMessage);
    }

    [Fact]
    public void Omitting_The_Seed_Chooses_A_Different_Cosmology_Each_Time()
    {
        using var server = new TestServer();
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll 42 confirm").Status);
        var previousSeed = server.Sync.Placement!.WorldSeed;
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll confirm").Status);
            Assert.NotEqual(previousSeed, server.Sync.Placement!.WorldSeed);
            previousSeed = server.Sync.Placement.WorldSeed;
        }

        Assert.Equal(3, server.Broadcasts.Count);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(0L)]
    public void Explicit_Seeds_Keep_Their_Full_64_Bit_Value(long seed)
    {
        using var server = new TestServer();

        var result = server.Execute("astraextera reroll " + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + " confirm");

        Assert.Equal(EnumCommandStatus.Success, result.Status);
        Assert.Equal(seed, server.Sync.Placement!.WorldSeed);
        Assert.Equal(seed, GalaxyPlacementCodec.FromUtf8(server.Stored[AstraExteraModMetadata.GalaxySaveKey]).WorldSeed);
    }

    [Fact]
    public void Inspecting_Is_Public_But_Rerolling_Requires_Controlserver()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;
        var player = new Caller { Type = EnumCallerType.Player, CallerPrivileges = [Privilege.chat] };

        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera galaxy", player).Status);
        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera reroll 42 confirm", player).Status);

        // Preview and find apply nothing, but both author skies on the server thread on demand, so
        // they are held to the same privilege as the command they exist to make safe.
        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera preview 42", player).Status);
        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera find kind=moon", player).Status);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);

        player.CallerPrivileges = [Privilege.chat, Privilege.controlserver];
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera preview 42", player).Status);
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll 42 confirm", player).Status);
        Assert.Equal(42, server.Sync.Placement!.WorldSeed);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    public void Invalid_Seeds_Leave_The_Saved_Sky_Alone(string seed)
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var result = server.Execute("astraextera reroll " + seed);

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains("64-bit integer", result.StatusMessage);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);
    }

    [Fact]
    public void Requesting_The_Current_Seed_Is_A_No_Op()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var result = server.Execute("astraextera reroll 7");

        Assert.Equal(EnumCommandStatus.Success, result.Status);
        Assert.Contains("already 7", result.StatusMessage);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);
    }

    [Fact]
    public void A_Configured_Server_Authors_And_Rerolls_Under_Its_Constraints_And_Says_So()
    {
        var config = new AstraExteraConfig { WorldKind = "moon", StarClass = "M", ParentGiantRings = "required" };
        var expected = config.GetGalaxyConstraints();
        using var server = new TestServer(config: config);

        var authored = server.Sync.Placement!;
        Assert.Equal(ObserverWorldKind.TerrestrialMoon, authored.WorldKind);
        Assert.Equal(StarSpectralClass.M, authored.System.StarClass);
        Assert.NotNull(authored.System.ParentGiantAppearance!.Ring);
        Assert.Equal(expected, authored.AuthoredUnder);
        Assert.Contains(
            "constraints=kind=moon,star=M,rings=required",
            server.Execute("astraextera galaxy").StatusMessage);

        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll -1234 confirm").Status);

        var rerolled = server.Sync.Placement!;
        Assert.Equal(GalaxyGenerator.Generate(-1234, expected), rerolled);
        Assert.Equal(ObserverWorldKind.TerrestrialMoon, rerolled.WorldKind);
        Assert.Equal(StarSpectralClass.M, rerolled.System.StarClass);
        Assert.NotNull(rerolled.System.ParentGiantAppearance!.Ring);

        // The sky a save is holding is the sky it keeps: a restart under a changed config reloads
        // the stored placement rather than authoring the world someone has since asked for.
        using var restarted = new TestServer(server.Stored, config: new AstraExteraConfig { WorldKind = "planet" });
        Assert.Equal(0, restarted.LoadWrites);
        Assert.Equal(rerolled, restarted.Sync.Placement);
    }

    [Fact]
    public void An_Unconfigured_Server_Authors_Exactly_What_It_Always_Did()
    {
        using var server = new TestServer();

        Assert.Equal(GalaxyGenerator.Generate(7), server.Sync.Placement);
        Assert.Null(server.Sync.Placement!.AuthoredUnder);
        Assert.True(server.Sync.Constraints.IsUnconstrained);
        Assert.Contains("constraints=none", server.Execute("astraextera galaxy").StatusMessage);
    }

    [Fact]
    public void Previewing_Changes_Nothing_And_Reroll_Then_Produces_The_Previewed_Sky()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var preview = server.Execute("astraextera preview -1234");

        Assert.Equal(EnumCommandStatus.Success, preview.Status);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);
        server.Join();
        Assert.Equal(
            GalaxyPlacementCodec.ToUtf8(previous!.Placement),
            Assert.Single(server.JoinPackets).Payload);

        // The point of a preview is that it is the sky you get, so the readout has to survive the
        // reroll word for word.
        var previewed = Describes(preview.StatusMessage);
        Assert.Contains("Nothing was saved", preview.StatusMessage);
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll -1234 confirm").Status);
        Assert.Equal(previewed, server.Execute("astraextera galaxy").StatusMessage);
        Assert.Equal(-1234, server.Sync.Placement!.WorldSeed);
    }

    [Fact]
    public void Previewing_Authors_Under_The_Server_Constraints_A_Reroll_Would_Use()
    {
        var config = new AstraExteraConfig { WorldKind = "moon", ParentGiantRings = "required" };
        using var server = new TestServer(config: config);

        var preview = server.Execute("astraextera preview -99");

        Assert.Equal(EnumCommandStatus.Success, preview.Status);
        Assert.Contains("constraints=kind=moon,rings=required", preview.StatusMessage);
        Assert.Equal(0, server.Writes);

        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll -99 confirm").Status);
        Assert.Equal(Describes(preview.StatusMessage), server.Execute("astraextera galaxy").StatusMessage);
    }

    [Fact]
    public void A_Found_Seed_Is_One_That_Reroll_Turns_Into_The_World_That_Was_Asked_For()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var found = server.Execute("astraextera find kind=moon rings=required");

        Assert.Equal(EnumCommandStatus.Success, found.Status);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);

        var seed = SeedIn(found.StatusMessage);
        Assert.Equal(EnumCommandStatus.Success, server.Execute($"astraextera reroll {seed} confirm").Status);
        var applied = server.Sync.Placement!;
        Assert.Equal(ObserverWorldKind.TerrestrialMoon, applied.WorldKind);
        Assert.NotNull(GalaxyConstraints.DominantGiant(applied.System)!.Ring);
    }

    [Fact]
    public void Finding_Refuses_A_Contradiction_By_Name_Rather_Than_Searching_For_It()
    {
        using var server = new TestServer();

        var result = server.Execute("astraextera find kind=moon moons=required");

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains("kind=moon,moons=required", result.StatusMessage);
        Assert.Contains("belongs to its giant", result.StatusMessage);
        Assert.DoesNotContain("seeds", result.StatusMessage);
    }

    [Fact]
    public void Finding_Names_The_Constraint_A_Configured_Server_Can_Never_Produce()
    {
        // The server config wins, because the seed reported has to be one a reroll would honour.
        // Asking this server for a moon world is therefore asking for something it cannot make.
        using var server = new TestServer(config: new AstraExteraConfig { WorldKind = "planet" });

        var result = server.Execute("astraextera find kind=moon");

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains($"{GalaxySkySearch.DefaultAttempts} seeds", result.StatusMessage);
        Assert.Contains("Nothing drawn satisfied kind=moon", result.StatusMessage);
        Assert.Contains("configured constraints 'kind=planet'", result.StatusMessage);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);
    }

    [Theory]
    [InlineData("astraextera find", "Constraints are kind=")]
    [InlineData("astraextera find star=Q", "star 'Q' is not one of")]
    [InlineData("astraextera find colour=blue", "'colour' is not a constraint")]
    [InlineData("astraextera find moon", "not a key=value constraint")]
    public void Finding_Rejects_What_It_Cannot_Read(string command, string expected)
    {
        using var server = new TestServer();

        var result = server.Execute(command);

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains(expected, result.StatusMessage);
        Assert.Equal(0, server.Writes);
    }

    [Fact]
    public void An_Unconfirmed_Reroll_Says_What_It_Would_Cost_And_Does_Nothing()
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var result = server.Execute("astraextera reroll -1234");

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains("cannot be migrated", result.StatusMessage);
        Assert.Contains("/astraextera preview -1234", result.StatusMessage);
        Assert.Contains("/astraextera reroll -1234 confirm", result.StatusMessage);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);

        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera reroll").Status);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Empty(server.Broadcasts);
    }

    [Theory]
    [InlineData("astraextera reroll -1234 yes")]
    [InlineData("astraextera reroll confirm -1234")]
    public void A_Misplaced_Confirmation_Is_A_Usage_Error_Rather_Than_A_Reroll(string command)
    {
        using var server = new TestServer();
        var previous = server.Sync.Sky;

        var result = server.Execute(command);

        Assert.Equal(EnumCommandStatus.Error, result.Status);
        Assert.Contains("Usage: /astraextera reroll", result.StatusMessage);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);
    }

    [Fact]
    public void Rerolling_Before_The_Save_Loads_Is_Rejected()
    {
        using var server = new TestServer(load: false);

        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera reroll confirm").Status);
        Assert.Throws<InvalidOperationException>(() => server.Sync.Reroll(42));
        Assert.Null(server.Sync.Sky);
        Assert.Empty(server.Stored);
        Assert.Empty(server.Broadcasts);
    }

    /// <summary>The one line of a multi-line reply that is the galaxy readout itself.</summary>
    private static string Describes(string message)
        => Assert.Single(
            message.Split('\n'),
            static line => line.StartsWith("AstraExtera galaxy:", StringComparison.Ordinal));

    private static long SeedIn(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, @"at seed (-?\d+)");
        Assert.True(match.Success, message);
        return long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TestServer : IDisposable
    {
        public Dictionary<string, byte[]> Stored { get; }
        public List<GalaxyPlacementPacket> Broadcasts { get; } = [];
        public List<GalaxyPlacementPacket> JoinPackets { get; } = [];
        public int Writes { get; private set; }
        public int LoadWrites { get; }
        public GalaxyServerSync Sync { get; }
        public IServerWorldAccessor World { get; }
        private readonly ChatCommandApi commands;
        private readonly Dictionary<string, Delegate?> events = [];

        public TestServer(
            Dictionary<string, byte[]>? stored = null,
            bool load = true,
            AstraExteraConfig? config = null)
        {
            Stored = stored ?? [];
            var save = ApiDouble.Create<ISaveGame>((method, args) => method.Name switch
            {
                "GetData" => Stored.GetValueOrDefault((string)args[0]!),
                "StoreData" => Store((string)args[0]!, (byte[])args[1]!),
                _ => throw new NotSupportedException(method.Name)
            });
            World = ApiDouble.Create<IServerWorldAccessor>((method, _) => method.Name switch
            {
                "get_Seed" => 7,
                _ => throw new NotSupportedException(method.Name)
            });
            var manager = ApiDouble.Create<IWorldManagerAPI>((method, _) => method.Name switch
            {
                "get_SaveGame" => save,
                _ => throw new NotSupportedException(method.Name)
            });
            IServerNetworkChannel? channel = null;
            channel = ApiDouble.Create<IServerNetworkChannel>((method, args) =>
            {
                if (method.Name == "RegisterMessageType") return channel;
                if (method.Name == "BroadcastPacket")
                {
                    Assert.Empty((IServerPlayer[])args[1]!);
                    Broadcasts.Add((GalaxyPlacementPacket)args[0]!);
                    return null;
                }
                if (method.Name == "SendPacket")
                {
                    Assert.Single((IServerPlayer[])args[1]!);
                    JoinPackets.Add((GalaxyPlacementPacket)args[0]!);
                    return null;
                }
                throw new NotSupportedException(method.Name);
            });
            var network = ApiDouble.Create<IServerNetworkAPI>((method, _) => method.Name switch
            {
                "RegisterChannel" => channel,
                _ => throw new NotSupportedException(method.Name)
            });
            var eventApi = ApiDouble.Create<IServerEventAPI>((method, args) =>
            {
                var name = method.Name[(method.Name.IndexOf('_') + 1)..];
                var handler = (Delegate)args[0]!;
                events[name] = method.Name.StartsWith("add_", StringComparison.Ordinal)
                    ? Delegate.Combine(events.GetValueOrDefault(name), handler)
                    : Delegate.Remove(events.GetValueOrDefault(name), handler);
                return null;
            });
            var logger = ApiDouble.Create<ILogger>((_, _) => null);

            // No AstraTerra in a test process, which is the same answer a server gets when the
            // dependency is missing: the world bridge warns and publishes nothing.
            var modLoader = ApiDouble.Create<IModLoader>((method, _) => method.Name switch
            {
                "GetModSystem" => null,
                _ => throw new NotSupportedException(method.Name)
            });
            var api = ApiDouble.Create<ICoreServerAPI>((method, _) => method.Name switch
            {
                "get_ModLoader" => modLoader,
                "get_ChatCommands" => commands,
                "get_World" => World,
                "get_WorldManager" => manager,
                "get_Network" => network,
                "get_Event" => eventApi,
                "get_Logger" => logger,
                "get_Side" => EnumAppSide.Server,
                _ => throw new NotSupportedException(method.Name)
            });
            commands = new ChatCommandApi(api);
            Sync = new GalaxyServerSync(api, config ?? new AstraExteraConfig());
            Sync.Register();
            new GalaxyServerCommands(() => Sync.Sky, Sync.Reroll, () => Sync.Constraints).Register(api);
            if (load) events["SaveGameLoaded"]!.DynamicInvoke();
            LoadWrites = Writes;
            Writes = 0;
        }

        private object? Store(string key, byte[] bytes)
        {
            Stored[key] = bytes;
            Writes++;
            return null;
        }

        public TextCommandResult Execute(string text, Caller? caller = null)
        {
            TextCommandResult? result = null;
            commands.ExecuteUnparsed("/" + text, new TextCommandCallingArgs
            {
                Caller = caller ?? new Caller { Type = EnumCallerType.Console, CallerPrivileges = ["*"] },
                RawArgs = new CmdArgs("")
            }, value => result = value);
            return Assert.IsType<TextCommandResult>(result);
        }

        public void Join() => ((PlayerDelegate)events["PlayerJoin"]!)(null!);

        public void Dispose() => Sync.Unregister();
    }

    // Only the save, network and command APIs used above are available; touching world generation
    // or any other game state fails the test instead of silently succeeding on a loose mock.
    public class ApiDouble : DispatchProxy
    {
        private System.Func<MethodInfo, object?[], object?> handler = null!;

        public static T Create<T>(System.Func<MethodInfo, object?[], object?> handler) where T : class
        {
            var proxy = Create<T, ApiDouble>();
            ((ApiDouble)(object)proxy).handler = handler;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => handler(targetMethod!, args ?? []);
    }
}
