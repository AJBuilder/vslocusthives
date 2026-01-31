using Vintagestory.API.Common;

namespace LocustHives.Game.Logistics.Lattice
{
    public interface IStorageRegulator
    {
        ItemStack TrackedItem { get; set; }

        uint CurrentLevel { get; }
    }
}
