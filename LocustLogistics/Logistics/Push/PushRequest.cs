using LocustLogistics.Logistics.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace LocustLogistics.Logistics.Retrieval
{
    /// <summary>
    /// A handle object used to simplify the interaction between the push logistics system and storages.
    /// </summary>
    public class PushRequest
    {
        bool completed;
        bool failed;
        bool cancelled;

        public event Action CompletedEvent;
        public event Action AbandonedEvent;
        public event Action FailedEvent;
        public event Action CancelledEvent;
        public ItemStack Stack { get; }
        public IHiveStorage From { get; }

        public bool Completed => completed;
        public bool Failed => failed;
        public bool Cancelled => cancelled;

        public bool Active => !(completed || failed || cancelled);

        public PushRequest(ItemStack stack, IHiveStorage from)
        {
            Stack = stack;
            From = from;
        }

        /// <summary>
        /// Called to indicate that request was completed.
        /// </summary>
        public void Complete()
        {
            if (Active)
            {
                CompletedEvent?.Invoke();
                completed = true;
            }
        }
        public void Abandon()
        {
            if (Active)
            {
                AbandonedEvent?.Invoke();
            }
        }
        public void Fail()
        {
            if (Active)
            {
                FailedEvent?.Invoke();
                failed = true;
            }
        }

        public void Cancel()
        {
            if (Active)
            {
                CancelledEvent?.Invoke();
                cancelled = true;
            }
        }

    }
}
