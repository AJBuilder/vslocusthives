using System.Collections.Generic;
using ProtoBuf;

namespace LocustHives.Game.Core
{

    [ProtoContract]
    public sealed class HiveMemberSaveData
    {
        [ProtoMember(1)]
        public string typeId;

        [ProtoMember(2)]
        // This field is serialized/deserialized using the registered handlers in the mod system.
        public byte[] data;

    }


    [ProtoContract]
    public sealed class CoreSaveData
    {
        [ProtoMember(1)]
        public Dictionary<int, HiveSaveData> hives;

        [ProtoMember(2)]
        public int nextHiveId;
    }

    [ProtoContract]
    public sealed class HiveSaveData
    {
        [ProtoMember(1)]
        public string name;

        [ProtoMember(2)]
        public HiveMemberSaveData[] members;
    }
    
}