using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstraExtera.Galaxy;

public static class LocalSystemSkyCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] ToUtf8(LocalSystemSky sky)
        => JsonSerializer.SerializeToUtf8Bytes(sky, Options);

    public static LocalSystemSky FromUtf8(byte[] utf8)
        => JsonSerializer.Deserialize<LocalSystemSky>(utf8, Options)
           ?? throw new InvalidOperationException("Local system sky JSON was empty.");
}
