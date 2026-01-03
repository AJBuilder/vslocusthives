using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LocustHives.Systems.Nests
{
    public interface ILocustNest
    {
        Vec3d Position { get; }
        int Dimension { get; }
        // TODO: Add storing of locusts inside the nest
        // Punting for now since it got complicated storing them
        // once they had inventories. Need a way to keep them "in the world"
        // but not alive/moving around etc. Perhaps disabling their controls,
        // removing the animation, removing the collision boxes, and teleporting them
        // inside the nest? Or moving them to another dimension?
        // There is too many edge cases for any solution where I don't think this is possible.
        //public int MaxCapacity { get; }
        //public bool HasRoom { get; }
        //public bool TryStoreLocust(EntityLocust locust);
        //public bool TryUnstoreLocust(int index);
    }
}
