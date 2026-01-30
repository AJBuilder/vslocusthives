using ProtoBuf;

namespace LocustHives.Game.Core
{
    [ProtoContract]
    public class MembershipUpdate
    {
        [ProtoMember(1)]
        public string typeId;
        [ProtoMember(2)]
        public int? prevHiveId;
        [ProtoMember(3)]
        public int? hiveId;
        [ProtoMember(4)]
        public byte[] bytes;
        [ProtoMember(5)]
        public bool isSync;
        
    }
    [ProtoContract]
    public class MembershipUpdatePacket
    {
        [ProtoMember(1)]
        public MembershipUpdate[] updates;
    }

}