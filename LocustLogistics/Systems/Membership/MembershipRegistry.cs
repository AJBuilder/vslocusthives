using System;
using System.Collections.Generic;

namespace LocustHives.Systems.Membership
{
    /// <summary>
    /// A membership registry where each member can only belong to one membership at a time.
    /// </summary>
    public class MembershipRegistry<T> : IMembershipRegistry<T>
    {

        // Events
        public event Action<T, int?, int?> MemberAssigned;


        Dictionary<int, HashSet<T>> membersByMembership = new Dictionary<int, HashSet<T>>();
        Dictionary<T, int> membershipByMembers = new Dictionary<T, int>();

        public bool GetHiveOf(T member, out int membership)
        {
            if(membershipByMembers.TryGetValue(member, out membership)) return true;
            return false;
        }

        public IReadOnlySet<T> GetMembersOf(int membership)
        {
            if (this.membersByMembership.TryGetValue(membership, out var members)) return members;
            else
            {
                members = new HashSet<T>();
                membersByMembership[membership] = members;
                return members;
            }
        }

        /// <summary>
        /// Assign the member to a new membership.
        /// 
        /// Returns the old id if there was one.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="membership"></param>
        /// <returns></returns>
        public int? AssignMembership(T member, int? membership)
        {
            // Remove any old membership
            int? oldMembership = null;
            if (membershipByMembers.TryGetValue(member, out var old))
            {
                oldMembership = old;
                if (oldMembership == membership) return oldMembership; // Already a member.
                membersByMembership[old].Remove(member);
            }
            else if (!membership.HasValue) return null; // Already has no membership

            if (membership.HasValue)
            {
                // Now assign new membership
                membershipByMembers[member] = membership.Value;

                // Cache reverse relationship
                if (!membersByMembership.TryGetValue(membership.Value, out var members))
                {
                    members = new HashSet<T>();
                    membersByMembership[membership.Value] = members;
                }
                members.Add(member);
            }

            MemberAssigned?.Invoke(member, oldMembership, membership);
            return oldMembership;
        }

        /// <summary>
        /// Gets all member-membership pairs.
        /// Used for serialization.
        /// </summary>
        public IEnumerable<(T member, int membership)> GetAllMemberships()
        {
            foreach (var kvp in membershipByMembers)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Gets all members across all memberships.
        /// Used for cleanup operations.
        /// </summary>
        public IEnumerable<T> GetAllMembers()
        {
            return membershipByMembers.Keys;
        }
    }
}
