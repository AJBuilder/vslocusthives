namespace LocustHives.Game.Core
{

    /// <summary>
    /// Implemented by anything that can be assigned to hives.
    /// </summary>
    public interface IHiveTunable
    {
        /// <summary>
        /// A handle representing this tunable's membership.
        ///
        /// The returned value must have a stable canonical identity throughout the loading/unloading
        /// of chunks and the world. (i.e block position, entity id, etc.)
        ///
        /// If this IHiveTunable doesn't want to maintain a reference to the returned membership,
        /// it can return a boxed struct with overrides for Equals(object obj) and GetHashCode().
        /// </summary>
        IHiveMember HiveMembershipHandle { get; }
    }
}
