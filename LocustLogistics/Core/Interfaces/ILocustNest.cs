using System.Collections.Generic;
using Vintagestory.GameContent;

namespace LocustLogistics.Core.Interfaces
{
    public interface ILocustNest : IHiveMember
    {
        IReadOnlyCollection<EntityLocust> StoredLocusts { get; }
        int MaxCapacity { get; }
        bool HasRoom { get; }

        bool TryStoreLocust(EntityLocust locust);
        bool TryUnstoreLocust(EntityLocust locust);
    }
}
