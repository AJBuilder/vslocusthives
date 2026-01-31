using ProtoBuf;

namespace LocustHives.Game.Core
{
    [ProtoContract]
    public class MemberTunedPacket
    {
        [ProtoMember(1)]
        public string typeId;
        [ProtoMember(2)]
        public uint? hiveId;
        [ProtoMember(3)]
        public byte[] bytes;
    }

}