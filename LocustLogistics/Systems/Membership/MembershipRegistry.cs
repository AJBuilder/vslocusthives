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

        public bool GetMembershipOf(T member, out int membership)
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
                membersByMembership[old].Remove(member);
            }

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

        private void AddMemberNoCheck(T member, int membership)
        {
        }


        public bool AddMember(T member, int membership)
        {
            if (membershipByMembers.TryGetValue(member, out var currentMembership))
            {
                // Fail if already has a different membership
                if(currentMembership != membership) return false;
                return true; // no op if already a member
            }
            AddMemberNoCheck(member, membership);
            return true;
        }


    }
}
