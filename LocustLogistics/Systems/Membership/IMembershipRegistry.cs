using System;
using System.Collections.Generic;

namespace LocustHives.Systems.Membership
{
    public interface IMembershipRegistry<T>
    {

        public bool GetMembershipOf(T member, out int membership);
        public IReadOnlySet<T> GetMembersOf(int membership);

        /// <summary>
        /// Assign the member to a new membership.
        /// 
        /// Returns the old id if there was one.
        /// </summary>
        /// <param name="member"></param>
        /// <param name="membership"></param>
        /// <returns></returns>
        public int? AssignMembership(T member, int? membership);
    }
}
