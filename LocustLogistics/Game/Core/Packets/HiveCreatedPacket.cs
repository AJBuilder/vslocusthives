using ProtoBuf;

namespace LocustHives.Game.Core
{
    [ProtoContract]
    public class HiveCreatedPacket
    {
        [ProtoMember(1)]
        public uint hiveId;
    }

}