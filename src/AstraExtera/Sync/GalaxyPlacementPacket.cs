using ProtoBuf;

namespace AstraExtera.Sync;

[ProtoContract]
public sealed class GalaxyPlacementPacket
{
    [ProtoMember(1)]
    public byte[] Payload = Array.Empty<byte>();

    [ProtoMember(2)]
    public byte[] StarFieldPayload = Array.Empty<byte>();

    [ProtoMember(3)]
    public byte[] LocalSkyPayload = Array.Empty<byte>();
}
