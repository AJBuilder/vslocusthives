using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common.Entities;

namespace LocustHives.Game.Util
{
    public static class EntityExtensions
    {
        /// <summary>
        /// Get the entity as the given type by casting it, OR it's sided behaviors.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static T GetAs<T>(this Entity entity) where T : class
        {
            // Direct cast
            if (entity is T t)
                return t;

            // Fallback: search behaviors
            return entity
                .SidedProperties
                .Behaviors
                .OfType<T>()
                .FirstOrDefault();
        }
    }
}
