using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocustHives.Game.Core
{
    public interface IHiveMember
    {

        /// <summary>
        /// The local copy of this member's hive id.
        /// Used client side to determine membership.
        /// </summary>
        int? LocalHiveId { get; }

        /// <summary>
        /// Action that will be invoked by the hives mod system when this member is tuned.
        ///
        /// First argument is the previous hive id, second is the new hive id.
        /// </summary>
        public Action<int?, int?> OnTuned { get; set; }
    }
}
