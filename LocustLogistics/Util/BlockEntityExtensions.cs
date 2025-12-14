using LocustLogistics.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace LocustLogistics.Util
{
    public static class BlockEntityExtensions
    {
        /// <summary>
        /// Get the block entity as the given type by casting it, OR it's behaviors.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="blockEntity"></param>
        /// <returns></returns>
        public static T GetAs<T>(this BlockEntity blockEntity) where T : class
        {
            // Direct cast
            if (blockEntity is T t)
                return t;

            // Fallback: search behaviors
            return blockEntity.GetBehavior<T>();
        }
    }
}
