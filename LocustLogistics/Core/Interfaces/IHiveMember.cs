using Vintagestory.API.MathTools;

#nullable enable

namespace LocustLogistics.Core.Interfaces
{
    public interface IHiveMember
    {
        Vec3d Position { get; }
        int Dimension { get; }
        public int? HiveId { get; set; }

    }
}
