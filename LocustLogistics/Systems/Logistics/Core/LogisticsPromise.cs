using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace LocustHives.Systems.Logistics.Core
{
    /// <summary>
    /// The state of a LogisticsPromise.
    /// Unfulfilled: still waiting on operations
    /// Fulfilled: all promised operations were complete
    /// Cancelled: this promise no longer needs to be fulfilled
    /// </summary>
    public enum LogisticsPromiseState
    {
        Unfulfilled,
        Fulfilled,
        Cancelled
    }

    /// <summary>
    /// An operation to perform on storage.
    /// Give: Give the target storage the stack
    /// Take: Take the stack from the target storage
    /// </summary>
    public enum LogisticsOperation
    {
        Give,
        Take,
    }

    public static class ItemStackLogisticsExtensions
    {
        /// <summary>
        /// Normalize the stack and operation so that the stack size is non-zero and positive.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="operation"></param>
        public static void Normalize(this ItemStack stack, ref LogisticsOperation operation)
        {
            // Flip the operation if the requested count is negative for some reason.
            if (stack.StackSize < 0)
            {
                stack.StackSize = -stack.StackSize;
                operation = operation == LogisticsOperation.Give ? LogisticsOperation.Take : LogisticsOperation.Give;
            }
            else
            {
                // Normalize to 1? Not sure how to handle this case.
                if (stack.StackSize == 0) stack.StackSize = 1; 
            }
        }
    }

    /// <summary>
    /// A promise is a handle object that is used to communicate intent to perform a logistics operation.
    /// </summary>
    public class LogisticsPromise
    {
        LogisticsPromiseState state;
        uint fulfilled;

        /// <summary>
        /// Event that is fired when the promise is either fulfilled or cancelled.
        /// Check the state (which is passed) to determine if it was fulfilled.
        /// </summary>
        public event Action<LogisticsPromiseState> CompletedEvent;

        /// <summary>
        /// Event that is fired whenever a worker fulfills any amount of a promise.
        /// </summary>
        public event Action<uint, ILogisticsWorker> FulfillmentEvent;

        /// <summary>
        /// The stack to be given/taken.
        /// 
        /// On construction the stack size is normalized to always be positive.
        /// Flipping the operation if necessary.
        /// </summary>
        public ItemStack Stack { get; }
        public ILogisticsStorage Target { get; }
        public LogisticsPromiseState State => state;
        public LogisticsOperation Operation { get; }

        public uint Fulfilled => fulfilled;

        public LogisticsPromise(ItemStack stack, ILogisticsStorage target, LogisticsOperation operation)
        {
            stack = stack.Clone();
            stack.Normalize(ref operation);
            Stack = stack;
            Target = target;
            state = LogisticsPromiseState.Unfulfilled;
            Operation = operation;
            fulfilled = 0;
        }

        /// <summary>
        /// Called to indicate how much of the promised operation was fulfilled.
        /// </summary>
        public void Fulfill(uint count, ILogisticsWorker byWorker)
        {
            if (state == LogisticsPromiseState.Unfulfilled)
            {
                fulfilled += count;

                // We have to set the state before triggering the fulfillment event because if this fulfillment
                // completely fulfills another promise whose completion would cause this promise to be cancelled,
                // this promise needs to know whether this is actually cancelled or completed. (See AddChild)
                if (fulfilled >= Stack.StackSize) state = LogisticsPromiseState.Fulfilled;

                FulfillmentEvent?.Invoke(count, byWorker);
                if (fulfilled >= Stack.StackSize) CompletedEvent?.Invoke(LogisticsPromiseState.Fulfilled);
            }
        }

        /// <summary>
        /// Mark the promise as cancelled.
        /// This signals to anything waiting on the promise that it will never be fulfilled.
        /// It also signals to the thing responsible for the promise to drop it.
        /// </summary>
        public void Cancel()
        {
            if (state == LogisticsPromiseState.Unfulfilled)
            {
                state = LogisticsPromiseState.Cancelled;
                CompletedEvent?.Invoke(LogisticsPromiseState.Cancelled);
            }
        }

        /// <summary>
        /// Add events to this promise and the given promise so that the given's fulfillment contributes
        /// towards this promises fulfillment and cancellation of this promise will
        /// result in the given cancelling.
        /// 
        /// This makes the given a "child" of this parent.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="childCount"></param>
        /// <returns></returns>
        public void AddChild(LogisticsPromise child)
        {
            // If the child receives some fulfillment, propogate the fulfillment to the parent.
            child.FulfillmentEvent += Fulfill;

            // If the parent promise was completed, cancel the child.
            var propogateCompletion = (LogisticsPromiseState state) => child.Cancel();
            CompletedEvent += propogateCompletion;

            // Finally, if the child is cancelled remove it's event handler
            child.CompletedEvent += (state) =>
            {
                CompletedEvent -= propogateCompletion;
            };
        }

    }
}
