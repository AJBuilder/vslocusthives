using HarmonyLib;
using LocustHives.Game.Logistics;
using LocustHives.Game.Nest;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Membership;
using LocustHives.Systems.Nests;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustHives.Game.Core
{

    /// <summary>
    /// This mod system tracks things that are tuned to a hive.
    /// </summary>
    public class TuningSystem : ModSystem
    {
        int nextHiveId;

        Dictionary<int, HiveData> allHiveData = new Dictionary<int, HiveData>();
        Dictionary<IHiveMember, int> hivesByMembers = new Dictionary<IHiveMember, int>();
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        IClientNetworkChannel clientChannel;
        IServerNetworkChannel serverChannel;
        
        Dictionary<string, System.Func<byte[], IHiveMember>> deserializers = new Dictionary<string, System.Func<byte[], IHiveMember>>();
        Dictionary<Type, (string, System.Func<IHiveMember, byte[]>)> serializers = new Dictionary<Type, (string, System.Func<IHiveMember, byte[]>)>();

        /// <summary>
        /// Event fired when membership changes.
        /// Parameters: (member, previousHiveId, newHiveId)
        /// </summary>
        public event Action<IHiveMember, int?, int?> MemberTuned;

        public Dictionary<int, HiveMemberSaveData[]> unknownMemberships;

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));

            var test = RuntimeTypeModel.Create();


            // Register built-in handle types
            // Notify member
            MemberTuned += (member, prevHiveId, hiveId) => member.OnTuned(prevHiveId, hiveId);
        }

        /// <summary>
        /// Register a membership type for serialization/deserialization.
        /// Call this in your mod's Start() method.
        /// </summary>
        /// <param name="typeId">Unique type identifier (e.g., "yourmod:customhandle")</param>
        /// <param name="deserializer">Function to deserialize bytes into handle</param>
        public void RegisterMembershipType<T>(
            string typeId,
            System.Func<T, byte[]> serializerFunc,
            System.Func<byte[], T> deserializerFunc)
            where T : IHiveMember
        {
            deserializers[typeId] = (bytes) => deserializerFunc(bytes);
            serializers[typeof(T)] = (typeId, (member) => serializerFunc((T)member));
        }


        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
            this.sapi = sapi;

            sapi.Event.GameWorldSave += OnWorldSave;

            sapi.Event.SaveGameLoaded += OnWorldLoad;

            // Periodic cleanup of stale handles
            sapi.Event.RegisterGameTickListener((dt) =>
            {
                CleanupStaleHandles();
            }, 60000); // Every 60 seconds

            // Setup broadcasting updates
            serverChannel = sapi.Network.RegisterChannel("locusthivemembership");
            serverChannel.RegisterMessageType<MembershipUpdatePacket>();
            MemberTuned += (member, prevHiveId, hiveId) =>
            {
                if(serializers.TryGetValue(member.GetType(), out var entry))
                {
                    var (typeId, serializer) = entry;
                    serverChannel.BroadcastPacket(new MembershipUpdatePacket
                    {
                        updates = [
                            new MembershipUpdate
                            {
                                typeId = typeId,
                                prevHiveId = prevHiveId,
                                hiveId = hiveId,
                                bytes = serializer(member)
                            }
                        ]
                    });
                }
            };
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            this.capi = capi;

            // Setup accepting updates
            clientChannel = capi.Network.RegisterChannel("locusthivemembership");
            clientChannel.RegisterMessageType<MembershipUpdatePacket>();
            clientChannel.SetMessageHandler<MembershipUpdatePacket>((packet) =>
            {
                if(packet.updates == null) return;

                foreach(var update in packet.updates)
                {
                    if(deserializers.TryGetValue(update.typeId, out var deserializer))
                    {
                        IHiveMember member = deserializer(update.bytes);
                        AssignMembersip(member, update.hiveId);
                        if (update.isSync)
                        {
                            MemberTuned?.Invoke(member, update.prevHiveId, update.hiveId);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Assigns the given member to the given hive.
        /// Fires MemberTuned event and calls the member's OnTuned method.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="hiveId"></param>
        public void Tune(IHiveMember member, int? hiveId)
        {
            var prevHiveId = AssignMembersip(member, hiveId);
            MemberTuned?.Invoke(member, prevHiveId, hiveId);
        }

        /// <summary>
        /// Assigns the given member to the given membership.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="hiveId"></param>
        /// <returns></returns>
        private int? AssignMembersip(IHiveMember member, int? hiveId)
        {
            
            // Remove any old membership
            int? prevHiveId = null;
            if (hivesByMembers.TryGetValue(member, out var old))
            {
                prevHiveId = old;
                if (prevHiveId == hiveId) return prevHiveId; // Already a member.
                allHiveData[old].members.Remove(member);
            }
            else if (!hiveId.HasValue) return null; // Already has no membership

            if (hiveId.HasValue)
            {
                // Now assign new membership
                hivesByMembers[member] = hiveId.Value;

                // Cache reverse relationship
                if (!allHiveData.TryGetValue(hiveId.Value, out var hiveData))
                {
                    hiveData = new HiveData();
                    allHiveData[hiveId.Value] = hiveData;
                }
                hiveData.members.Add(member);
            }
            return prevHiveId;
        }

        public bool GetMembershipOf(IHiveMember member, out int hiveId)
        {
            if(hivesByMembers.TryGetValue(member, out hiveId)) return true;
            return false;
        }

        /// <summary>
        /// Get the set of all members assigned to the given hive.
        /// 
        /// The lifetime of the set is from the first member being tuned to
        /// that hive, till the end of this mod system.
        /// </summary>
        /// <param name="hiveId"></param>
        /// <returns></returns>
        public IReadOnlySet<IHiveMember> GetMembersOf(int hiveId)
        {
            if (!this.allHiveData.TryGetValue(hiveId, out var hiveData))
            {
                hiveData = new HiveData
                {
                    name = $"#{hiveId}",
                    members = new HashSet<IHiveMember>()
                };
                allHiveData[hiveId] = hiveData;
            }
            return hiveData.members;
        }

        public bool GetNameOf(int hiveId, out string name)
        {
            if(allHiveData.TryGetValue(hiveId, out var hiveData))
            {
                name = hiveData.name;
                return true;
            }
            name = null;
            return false;
        }

        /// <summary>
        /// Creates a new hive that doesn't exist yet.
        /// Should only be called server side.
        /// </summary>
        /// <returns></returns>
        public int CreateHive(string name = null)
        {
            while(allHiveData.ContainsKey(nextHiveId)) nextHiveId++;

            var hiveData = new HiveData
                {
                    name = name ?? $"#{nextHiveId}",
                    members = new HashSet<IHiveMember>()
                };

            allHiveData[nextHiveId] = hiveData;

            // Post increment for the next time.
            return nextHiveId++;
        }


        private void OnWorldSave()
        {
            var hiveSaveData = new Dictionary<int, HiveSaveData>();
            foreach(var (id, h) in allHiveData)
            {
                hiveSaveData[id] = new HiveSaveData
                {
                    name=h.name,
                    members=h.members
                        .Select(m =>
                            {
                                // Try to serialize each member
                                if(serializers.TryGetValue(m.GetType(), out var entry))
                                {
                                    var (typeId, serializer) = entry;
                                    return new HiveMemberSaveData{
                                            typeId=typeId,
                                            data=serializer(m)
                                        };
                                }
                                else
                                {
                                    // On failure, we can't do anything but drop it
                                    sapi.Logger.Error($"The membership type {m.GetType().Name} was not registered. Unable to serialize/save...");
                                    return null;
                                }
                            })
                        .Where(entry => entry != null)
                        // Make sure to include the unknown for that hive.
                        .Concat(unknownMemberships.Get(id, Array.Empty<HiveMemberSaveData>())) 
                        .ToArray()
                };
            }

            var saveData = new CoreSaveData
            {
                hives = hiveSaveData,
                nextHiveId = nextHiveId
            };

            sapi.WorldManager.SaveGame.StoreData("LocustHivesMembership", SerializerUtil.Serialize(saveData));
        }

        private void OnWorldLoad()
        {
            var bytes = sapi.WorldManager.SaveGame.GetData<byte[]>("LocustHivesMembership");
            if(bytes == null) return;

            var saveData = SerializerUtil.Deserialize<CoreSaveData>(bytes);

            // Restore hives
            foreach(var (id, h) in saveData.hives)
            {
                // Create the hive data
                var hive = new HiveData
                {
                    name = h.name,
                    members = new HashSet<IHiveMember>()
                };
                allHiveData[id] = hive;

                // Try to deserialize the members
                var unknown = new List<HiveMemberSaveData>(h.members.Length);
                foreach(var m in h.members)
                {
                    if(deserializers.TryGetValue(m.typeId, out var deserializer))
                    {
                        hive.members.Add(deserializer(m.data));
                    }
                    else
                    {
                        unknown.Add(m);
                        sapi.Logger.Warning($"The membership type {m.typeId} was not registered. Unable to deserialize/load...");
                    }
                }
                unknownMemberships[id] = unknown.ToArray();
            }

            nextHiveId = saveData.nextHiveId;
        }

        private void CleanupStaleHandles()
        {
            if (sapi == null) return;

            var staleHandles = new List<IHiveMember>();

            // Check all members for validity
            foreach (var member in hivesByMembers.Keys)
            {
                if (!member.IsValid(sapi))
                {
                    staleHandles.Add(member);
                }
            }

            // Remove stale handles
            foreach (var handle in staleHandles)
            {
                Tune(handle, null);
                sapi.Logger.Warning($"[LocustHives] Removed stale handle of type '{handle.GetType().Name}' from hive membership");
            }
        }

    }
}
