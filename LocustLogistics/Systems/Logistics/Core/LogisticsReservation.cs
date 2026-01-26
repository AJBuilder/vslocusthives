using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LocustHives.Systems.Logistics.Core
{
    /// <summary>
    /// Active: The stack is actively reserved
    /// Cancelled: The stack is no longer being reserved
    /// </summary>
    public enum LogisticsReservationState
    {
        Active,
        Released
    }

    public class LogisticsReservation
    {
        LogisticsReservationState state;

        public event Action ReleasedEvent;

        /// <summary>
        /// The stack and stack size being reserved.
        ///
        /// Positive stack size = Give reservation (room is reserved)
        /// Negative stack size = Take reservation (items are reserved)
        /// </summary>
        public ItemStack Stack { get; }
        public LogisticsReservationState State => state;


        public LogisticsReservation(ItemStack stack)
        {
            Stack = stack;
        }

        /// <summary>
        /// Indicate that the items are no longer reserved or no longer need to be reserved.
        /// </summary>
        public void Release()
        {
            if(state == LogisticsReservationState.Active)
            {
                state = LogisticsReservationState.Released;
                ReleasedEvent?.Invoke();
            }
        }

    }
}
