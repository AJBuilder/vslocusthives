using LocustLogistics.Logistics.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace LocustLogistics.Logistics
{
    public class LogisticsWorkersModSystem : ModSystem
    {

        Dictionary<ILogisticsWorker, int> allTunedWorkers = new Dictionary<ILogisticsWorker, int>();
        Dictionary<int, HashSet<ILogisticsWorker>> hiveWorkers = new Dictionary<int, HashSet<ILogisticsWorker>>();

        public IReadOnlyDictionary<ILogisticsWorker, int> Membership => allTunedWorkers;

        public IReadOnlySet<ILogisticsWorker> GetHiveWorkers(int hive)
        {
            if (hiveWorkers.TryGetValue(hive, out var workers)) return workers;
            else return new HashSet<ILogisticsWorker>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("hivelogisticsworker", typeof(EntityBehaviorLogisticsWorker));
        }

        public void UpdateLogisticsWorkerHiveMembership(ILogisticsWorker worker, int? prevHive, int? hive)
        {
            if (hive.HasValue) allTunedWorkers[worker] = hive.Value;
            else allTunedWorkers.Remove(worker);

            // Clean up prior caching
            if (prevHive.HasValue)
            {
                if (hiveWorkers.TryGetValue(prevHive.Value, out var workers))
                {
                    workers.Remove(worker);
                    if (workers.Count == 0) hiveWorkers.Remove(prevHive.Value);
                }
            };

            // Add new caching
            if (hive.HasValue)
            {
                if (!hiveWorkers.TryGetValue(hive.Value, out var workers))
                {
                    workers = new HashSet<ILogisticsWorker>();
                    hiveWorkers[hive.Value] = workers;
                }
                workers.Add(worker);
            }
        }

    }
}
