namespace LocustHives.Game.Core
{

    public static class TunableExtensions
    {
        public static void TryTune(this TuningSystem system, IHiveTunable tunable, int? hiveId)
        {
            var member = tunable.GetHiveMemberHandle();
            if(member != null) system?.Tune(member, hiveId);
        }
    }
    /// <summary>
    /// Implemented by anything that can be assigned to hives.
    /// </summary>
    public interface IHiveTunable
    {
        /// <summary>
        /// Returns a handle representing this tunable's hive membership.
        /// </summary>
        IHiveMember GetHiveMemberHandle();
    }
}
