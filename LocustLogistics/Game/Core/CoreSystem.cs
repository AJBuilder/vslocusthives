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
    public class CoreSystem : ModSystem
    {
        uint nextHiveId;

        Dictionary<uint, HiveData> hivesById = new Dictionary<uint, HiveData>();
        Dictionary<IHiveMember, HiveData> hivesByMembers = new Dictionary<IHiveMember, HiveData>();
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
        public event Action<IHiveMember, HiveHandle?, HiveHandle?> MemberTuned;

        public Dictionary<uint, HiveMemberSaveData[]> unknownMemberships;

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));

            RegisterMembershipType<GenericBlockMembership>("locusthives:genericblock", GenericBlockMembership.ToBytes, (bytes) => GenericBlockMembership.FromBytes(bytes, api));
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
            sapi.Event.PlayerJoin += OnPlayerJoin;

            // Periodic cleanup of stale handles
            sapi.Event.RegisterGameTickListener((dt) =>
            {
                CleanupStaleHandles();
            }, 60000); // Every 60 seconds

            // Setup broadcasting updates
            serverChannel = sapi.Network.RegisterChannel("locusthivemembership");
            serverChannel.RegisterMessageType<MemberTunedPacket>();
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
            this.capi = capi;

            // Setup accepting updates
            clientChannel = capi.Network.RegisterChannel("locusthivemembership");
            clientChannel.RegisterMessageType<MemberTunedPacket>();
            clientChannel.SetMessageHandler<MemberTunedPacket>((packet) =>
            {
                if(deserializers.TryGetValue(packet.typeId, out var deserializer))
                {
                    IHiveMember member = deserializer(packet.bytes);
                    Tune(member, packet.hiveId.HasValue ? GetOrCreateHive(packet.hiveId.Value) : null);
                }
            });
        }

        /// <summary>
        /// Assigns the given member to the given hive.
        /// Fires MemberTuned event.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="hiveId"></param>
        private void Tune(IHiveMember member, HiveData hiveData)
        {
            var prevHive = AssignMembersip(member, hiveData);
            HiveHandle? prevHandle = prevHive == null ? null : MakeHiveHandle(prevHive);
            HiveHandle? handle = hiveData == null ? null : MakeHiveHandle(hiveData);

            // Fire events
            MemberTuned?.Invoke(member, prevHandle, handle);
            member.OnTuned(prevHandle, handle);

            // If server, send update packet
            if(sapi != null)
            {
                if(serializers.TryGetValue(member.GetType(), out var entry))
                {
                    var (typeId, serializer) = entry;
                    serverChannel.BroadcastPacket(new MemberTunedPacket
                    {
                        typeId = typeId,
                        hiveId = hiveData == null ? null : hiveData.id,
                        bytes = serializer(member)
                    });
                }
            }
        }

        public void Zero(IHiveMember member) => Tune(member, null);
        
        private HiveHandle MakeHiveHandle(HiveData hiveData)
        {
            return new HiveHandle(hiveData, Tune);
        }

        private HiveData AssignMembersip(IHiveMember member, HiveData hiveData)
        {
            
            // Remove any old membership
            if (hivesByMembers.TryGetValue(member, out var prevHive))
            {
                if (prevHive == hiveData) return hiveData; // Already a member.
                prevHive.members.Remove(member);
            }

            // Now assign new membership
            if (hiveData != null)
            {
                hivesByMembers[member] = hiveData;
                hiveData.members.Add(member);
            }
            else
            {
                hivesByMembers.Remove(member);
            }

            return prevHive;
        }

        public bool GetHiveOf(IHiveMember member, out HiveHandle hive)
        {
            var exists = hivesByMembers.TryGetValue(member, out var hiveData);
            hive = MakeHiveHandle(hiveData);
            return exists;
        }

        public bool GetHiveOf(uint id, out HiveHandle hive)
        {
            var exists = hivesById.TryGetValue(id, out var hiveData);
            hive = MakeHiveHandle(hiveData);
            return exists;
        }

        private HiveData GetOrCreateHive(uint hiveId)
        {
            if(!hivesById.TryGetValue(hiveId, out var hiveData))
            {
                hiveData = new HiveData
                {
                    id = hiveId,
                    name = $"#{nextHiveId}",
                    members = new HashSet<IHiveMember>()
                };
                hivesById[hiveId] = hiveData;
            }
            return hiveData;
        }

        /// <summary>
        /// Creates a new hive that doesn't exist yet.
        /// Should only be called server side.
        /// </summary>
        /// <returns></returns>
        public HiveHandle CreateHive(string name = null)
        {
            while(hivesById.ContainsKey(nextHiveId)) nextHiveId++;

            var hiveData = new HiveData
                {
                    id = nextHiveId,
                    name = name ?? $"#{nextHiveId}",
                    members = new HashSet<IHiveMember>()
                };

            hivesById[nextHiveId] = hiveData;

            // Post increment for the next time.
            nextHiveId++;

            return MakeHiveHandle(hiveData);
        }


        private void OnWorldSave()
        {
            var hiveSaveData = new Dictionary<uint, HiveSaveData>();
            foreach(var (id , hive) in hivesById)
            {
                hiveSaveData[id] = new HiveSaveData
                {
                    name=hive.name,
                    members=hive.members
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
            };

            sapi.WorldManager.SaveGame.StoreData("LocustHivesMembership", SerializerUtil.Serialize(saveData));
        }

        private void OnWorldLoad()
        {
            var saveData = sapi.WorldManager.SaveGame.GetData<CoreSaveData>("LocustHivesMembership");

            // Restore hives
            foreach(var (id, hiveSave) in saveData.hives)
            {
                // Create the hive data
                var hiveData = new HiveData
                {
                    name = hiveSave.name,
                    members = new HashSet<IHiveMember>()
                };
                hivesById[id] = hiveData;

                // Try to deserialize the members
                var unknown = new List<HiveMemberSaveData>(hiveSave.members.Length);
                foreach(var m in hiveSave.members)
                {
                    if(deserializers.TryGetValue(m.typeId, out var deserializer))
                    {
                        hiveData.members.Add(deserializer(m.data));
                    }
                    else
                    {
                        unknown.Add(m);
                        sapi.Logger.Warning($"The membership type {m.typeId} was not registered. Unable to deserialize/load...");
                    }
                }
                unknownMemberships[id] = unknown.ToArray();
            }

        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            // For now, just blast them with packets... should probably do a bulk packet
            foreach(var (member, hiveData) in hivesByMembers)
            {
                if(serializers.TryGetValue(member.GetType(), out var entry))
                {
                    var (typeId, serializer) = entry;
                    serverChannel.BroadcastPacket(new MemberTunedPacket
                    {
                        typeId = typeId,
                        hiveId = hiveData == null ? null : hiveData.id,
                        bytes = serializer(member)
                    });
                }
                else
                {
                    sapi.Logger.Error($"No serializer registered for {member.GetType().Name}. Unable to sync to client.");
                }
            }
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
