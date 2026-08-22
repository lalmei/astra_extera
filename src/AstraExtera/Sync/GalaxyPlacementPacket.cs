using ProtoBuf;

namespace AstraExtera.Sync;

[ProtoContract]
public sealed class GalaxyPlacementPacket
{
    [ProtoMember(1)]
    public byte[] Payload = Array.Empty<byte>();
}
