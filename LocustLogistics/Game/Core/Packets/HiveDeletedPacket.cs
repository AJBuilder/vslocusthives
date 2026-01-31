using ProtoBuf;

namespace LocustHives.Game.Core
{
    [ProtoContract]
    public class HiveDeletedPacket
    {
        [ProtoMember(1)]
        public uint hiveId;
    }

}