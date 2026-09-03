using System.Reflection;
using AstraExtera.Commands;
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

        var result = server.Execute("astraextera reroll -1234");

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
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll 42").Status);
        var previousSeed = server.Sync.Placement!.WorldSeed;
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll").Status);
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

        var result = server.Execute("astraextera reroll " + seed.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera reroll 42", player).Status);
        Assert.Same(previous, server.Sync.Sky);
        Assert.Equal(0, server.Writes);
        Assert.Empty(server.Broadcasts);

        player.CallerPrivileges = [Privilege.chat, Privilege.controlserver];
        Assert.Equal(EnumCommandStatus.Success, server.Execute("astraextera reroll 42", player).Status);
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
    public void Rerolling_Before_The_Save_Loads_Is_Rejected()
    {
        using var server = new TestServer(load: false);

        Assert.Equal(EnumCommandStatus.Error, server.Execute("astraextera reroll").Status);
        Assert.Throws<InvalidOperationException>(() => server.Sync.Reroll(42));
        Assert.Null(server.Sync.Sky);
        Assert.Empty(server.Stored);
        Assert.Empty(server.Broadcasts);
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

        public TestServer(Dictionary<string, byte[]>? stored = null, bool load = true)
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
            var api = ApiDouble.Create<ICoreServerAPI>((method, _) => method.Name switch
            {
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
            Sync = new GalaxyServerSync(api);
            Sync.Register();
            new GalaxyServerCommands(() => Sync.Sky, Sync.Reroll).Register(api);
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
