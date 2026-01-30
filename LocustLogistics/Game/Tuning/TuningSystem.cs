using HarmonyLib;
using LocustHives.Game.Logistics;
using LocustHives.Game.Nest;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Membership;
using LocustHives.Systems.Nests;
using Newtonsoft.Json.Linq;
using ProtoBuf;
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

        Dictionary<int, HashSet<IHiveMember>> membersByMembership = new Dictionary<int, HashSet<IHiveMember>>();
        Dictionary<IHiveMember, int> membershipByMembers = new Dictionary<IHiveMember, int>();
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

        public (string, int, byte[])[] unknownMemberships;

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));

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
            if (membershipByMembers.TryGetValue(member, out var old))
            {
                prevHiveId = old;
                if (prevHiveId == hiveId) return prevHiveId; // Already a member.
                membersByMembership[old].Remove(member);
            }
            else if (!hiveId.HasValue) return null; // Already has no membership

            if (hiveId.HasValue)
            {
                // Now assign new membership
                membershipByMembers[member] = hiveId.Value;

                // Cache reverse relationship
                if (!membersByMembership.TryGetValue(hiveId.Value, out var members))
                {
                    members = new HashSet<IHiveMember>();
                    membersByMembership[hiveId.Value] = members;
                }
                members.Add(member);
            }
            return prevHiveId;
        }

        public bool GetMembershipOf(IHiveMember member, out int hiveId)
        {
            if(membershipByMembers.TryGetValue(member, out hiveId)) return true;
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
            if (this.membersByMembership.TryGetValue(hiveId, out var members)) return members;
            else
            {
                members = new HashSet<IHiveMember>();
                membersByMembership[hiveId] = members;
                return members;
            }
        }

        /// <summary>
        /// Creates a new hive that doesn't exist yet.
        /// Should only be called server side.
        /// </summary>
        /// <returns></returns>
        public int CreateHive()
        {
            while(membersByMembership.ContainsKey(nextHiveId)) nextHiveId++;

            // Post increment for the next time.
            return nextHiveId++;
        }

        private void OnWorldSave()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write count
                writer.Write(membershipByMembers.Count + unknownMemberships.Length);

                foreach (var (handle, hiveId) in membershipByMembers)
                {
                    if(serializers.TryGetValue(handle.GetType(), out var entry))
                    {
                        var (typeId, serializer) = entry;

                        // Write type ID
                        writer.Write(typeId);

                        // Write hive ID
                        writer.Write(hiveId);

                        // Write handle bytes
                        var handleBytes = serializer(handle);
                        writer.Write(handleBytes.Length);
                        writer.Write(handleBytes);
                    }
                    else
                    {
                        sapi.Logger.Warning($"The membership type {handle.GetType().Name} was not registered. Unable to serialize/save...");
                    }
                }

                // And unknown memberships
                foreach(var (typeId, hiveId, data) in unknownMemberships)
                {
                    // Write type ID
                    writer.Write(typeId);

                    // Write hive ID
                    writer.Write(hiveId);

                    // Write unknown bytes
                    writer.Write(data.Length);
                    writer.Write(data);
                    
                }

                sapi.WorldManager.SaveGame.StoreData("LocustHivesMembership", ms.ToArray());
            }
        }

        private void OnWorldLoad()
        {
            var data = sapi.WorldManager.SaveGame.GetData<byte[]>("LocustHivesMembership");
            if(data == null) return;
            
            var unknown = new List<(string, int, byte[])>();

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                var count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        // Read type ID
                        var typeId = reader.ReadString();

                        // Read hive ID
                        var hiveId = reader.ReadInt32();

                        // Read handle bytes
                        byte[] handleBytes = null;
                        if(deserializers.TryGetValue(typeId, out var deserializer))
                        {
                            var length = reader.ReadInt32();
                            handleBytes = reader.ReadBytes(length);

                            // Deserialize handle
                            IHiveMember handle = null;
                            try
                            {
                                handle = deserializer(handleBytes);

                            }
                            catch (Exception ex)
                            {
                                sapi.Logger.Error($"Failed to deserialize membership of type {typeId}: {ex.Message}");
                            }
                            if (handle != null) AssignMembersip(handle, hiveId);
                        }
                        else
                        {
                            sapi.Logger.Warning($"The membership type of ID {typeId} was not registered. Unable to deserialize/load...");
                            if(handleBytes != null) unknown.Add((typeId, hiveId, handleBytes));
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Error($"Failed to deserialize handle membership: {ex.Message}");
                    }
                }
            }

            unknownMemberships = unknown.ToArray();
        }

        private void CleanupStaleHandles()
        {
            if (sapi == null) return;

            var staleHandles = new List<IHiveMember>();

            // Check all members for validity
            foreach (var member in membershipByMembers.Keys)
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
