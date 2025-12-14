using LocustLogistics.Core;
using LocustLogistics.Logistics.Retrieval;
using LocustLogistics.Nests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LocustLogistics.Logistics.Storage
{
    public class StorageLogisticsModSystem : ModSystem
    {

        Dictionary<IHiveStorage, int> allTunedStorages = new Dictionary<IHiveStorage, int>();
        Dictionary<int, HashSet<IHiveStorage>> hiveStorages = new Dictionary<int, HashSet<IHiveStorage>>();

        public IReadOnlyDictionary<IHiveStorage, int> Membership => allTunedStorages;

        public IReadOnlySet<IHiveStorage> GetHiveStorages(int hive)
        {
            if (hiveStorages.TryGetValue(hive, out var nests)) return nests;
            else return new HashSet<IHiveStorage>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("HiveAccessPort", typeof(BEBehaviorHiveAccessPort));
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }

        public void UpdateStorageHiveMembership(IHiveStorage storage, int? prevHive, int? hive)
        {
            if (hive.HasValue) allTunedStorages[storage] = hive.Value;
            else allTunedStorages.Remove(storage);

            // Clean up prior caching
            if (prevHive.HasValue) {
                if (hiveStorages.TryGetValue(prevHive.Value, out var storages))
                {
                    storages.Remove(storage);
                    if (storages.Count == 0) hiveStorages.Remove(prevHive.Value);
                }
            }
            ;

            // Add new caching
            if (hive.HasValue)
            {
                if(!hiveStorages.TryGetValue(hive.Value, out var storages))
                {
                    storages = new HashSet<IHiveStorage>();
                    hiveStorages[hive.Value] = storages;
                }
                storages.Add(storage);
            }
        }
        
    }
}
