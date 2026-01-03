using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocustHives.Systems.Logistics.Core
{
    public readonly struct WorkerEffort
    {
        readonly Func<uint, LogisticsPromise> onTryStart;
        public uint CountAvailable { get; }

        public float Time { get; }

        public WorkerEffort(uint count, float time, Func<uint, LogisticsPromise> onTryStart)
        {
            CountAvailable = count;
            Time = time;
            this.onTryStart = onTryStart;
        }

        /// <summary>
        /// Try to get a promise for this effort.
        /// 
        /// If null, then the effort has not been promised. This could mean that
        /// the effort is no longer possible or valid.
        /// </summary>
        /// <returns></returns>
        public LogisticsPromise TryStart(uint count)
        {
            return onTryStart.Invoke(count);
        }
    }
}
