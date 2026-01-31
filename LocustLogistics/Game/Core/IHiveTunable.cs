namespace LocustHives.Game.Core
{

    /// <summary>
    /// Implemented by anything that can be assigned to hives.
    /// </summary>
    public interface IHiveTunable
    {
        /// <summary>
        /// A handle representing this tunable's membership.
        /// </summary>
        IHiveMember HiveMembershipHandle { get; }
    }
}
