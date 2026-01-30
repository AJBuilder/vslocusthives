using LocustHives.Game.Core;
using LocustHives.Game.Logistics.Lattice;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LocustHives.Systems.Logistics
{
    /// <summary>
    /// Handle for groups of lattices that function as one storage.
    /// Identity is based on the block positions of the members.
    /// </summary>
    public struct LatticeStorageGroup : IHiveMember, ILogisticsStorage
    {
        private readonly HashSet<BlockPos> positions;
        private readonly ICoreAPI api;

        public LatticeStorageGroup(IEnumerable<BlockPos> positions, ICoreAPI api)
        {
            this.positions = positions.ToHashSet();
            this.api = api;
        }

        // IHiveMember implementation
        public bool IsValid(ICoreAPI api)
        {
            return positions.All(pos => api.World.BlockAccessor.GetBlockEntity(pos)?.GetAs<IStorageLattice>() != null);
        }

        public static byte[] ToBytes(LatticeStorageGroup handle)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write length
                writer.Write(handle.positions.Count);

                foreach(var pos in handle.positions)
                {
                    // Write positions
                    writer.Write(pos.X);
                    writer.Write(pos.Y);
                    writer.Write(pos.Z);
                    writer.Write(pos.dimension);
                }

                return ms.ToArray();
            }
        }

        public static LatticeStorageGroup FromBytes(byte[] data, ICoreAPI api)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Read number of positions
                var count = reader.ReadInt32();

                var positions = new BlockPos[count];
                for (int i = 0; i < count; i++)
                {
                    // Read positions
                    var x = reader.ReadInt32();
                    var y = reader.ReadInt32();
                    var z = reader.ReadInt32();
                    var dimension = reader.ReadInt32();

                    positions[i] = new BlockPos(x, y, z, dimension);
                }

                return new LatticeStorageGroup(positions, api);
            }
        }

        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                foreach(var lattice in GetLattices())
                {
                    foreach (var stack in lattice.Stacks)
                    {
                        yield return stack;
                    } 
                }
            }
        }

        public IEnumerable<IStorageAccessMethod> AccessMethods
        {
            get
            {
                var group = this;
                foreach(var pos in positions)
                {
                    var lattice = api.World.BlockAccessor.GetBlockEntity(pos)?.GetAs<IStorageLattice>();
                    foreach (var face in lattice.AvailableFaces)
                    {
                        yield return new BlockFaceAccessible(
                            pos,
                            face,
                            0,
                            CanDo,
                            (stack, sink) => group.TryTakeOut(lattice, stack, sink),
                            (stack, quantity) => group.TryPutInto(lattice, stack, quantity),
                            (stack) => group.TryReserve(lattice, stack));
                    }    
                }
            }
        }

        public uint CanDo(ItemStack stack) => (uint)GetLattices().Sum(l => l.CanDo(stack));

        public uint TryTakeOut(IStorageLattice origin, ItemStack stack, ItemSlot sinkSlot)
        {
            uint remaining = (uint)Math.Max(0, stack.StackSize);
            if(remaining <= 0) return 0;
            uint transfered = 0;

            foreach(var l in TraverseConnected(origin, api))
            {
                var clone = stack.CloneWithSize((int)remaining);
                var moved = l.TryTakeOut(clone, sinkSlot);
                remaining -= moved;
                if(remaining <= 0) break;
                transfered += moved;
            }

            return transfered;
        }

        public uint TryPutInto(IStorageLattice origin, ItemSlot sourceSlot, uint quantity)
        {
            var stack = sourceSlot.Itemstack;
            if (stack == null) return 0;

            uint remaining = quantity;
            if(remaining <= 0) return 0;
            uint transfered = 0;

            foreach(var l in TraverseConnected(origin, api))
            {
                var clone = stack.CloneWithSize((int)remaining);
                var moved = l.TryPutInto(sourceSlot, quantity);
                remaining -= moved;
                if(remaining <= 0) break;
                transfered += moved;
            }

            return transfered;
            
        }

        LogisticsReservation TryReserve(IStorageLattice origin, ItemStack stack)
        {
            var needed = Math.Max(0, stack.StackSize);
            var reservations = new List<LogisticsReservation>(needed);

            foreach(var lattice in TraverseConnected(origin, api))
            {
                var clone = stack.CloneWithSize(needed);
                var r = lattice.TryReserve(clone);
                if(r != null && r.Stack.StackSize != 0) {
                    reservations.Add(r);
                    needed -= r.Stack.StackSize;

                    // If zero or sign has flipped, then we are done
                    if(needed * stack.StackSize <= 0) break;
                }
            }

            var parent = new LogisticsReservation(stack);
            var arr = reservations.ToArray();
            parent.ReleasedEvent += () => arr.Foreach(a => a.Release());
            foreach(var a in arr)
            {
                a.ReleasedEvent += parent.Release;
            }
            return parent;
        }

        public static LatticeStorageGroup FromConnected(IStorageLattice origin, ICoreAPI api)
        {
            var positions = TraverseConnected(origin, api).Select(l => l.Pos);
            return new LatticeStorageGroup(positions, api);
        }

        /// <summary>
        /// BFS on touching lattices.
        /// Will also yield it's origin.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<IStorageLattice> TraverseConnected(IStorageLattice lattice, ICoreAPI api, ISet<BlockPos> visited = null)
        {
            if(visited == null) visited = new HashSet<BlockPos>();
            visited.Add(lattice.Pos);
            var queue = new Queue<IStorageLattice>();
            queue.Enqueue(lattice);

            var blockAccessor = api.World.BlockAccessor;

            // BFS traversal
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                yield return current;

                foreach (var face in BlockFacing.ALLFACES)
                {
                    var pos = current.Pos.AddCopy(face);

                    // Pretty sure it is cheaper to check this first than checking
                    // for if it is a lattice and has matching membership first?
                    if(visited.Contains(pos)) continue;

                    var otherLatttice = blockAccessor.GetBlockEntity(pos)?.GetAs<IStorageLattice>();
                    if(otherLatttice != null && visited.Add(otherLatttice.Pos))
                    {
                        queue.Enqueue(otherLatttice);
                    }
                }
            }
        }

        private IEnumerable<IStorageLattice> GetLattices()
        {
            foreach(var pos in positions)
            {
                var lattice = api.World.BlockAccessor.GetBlockEntity(pos)?.GetAs<IStorageLattice>();
                if(lattice != null) yield return lattice;
            }
        }

        // Equality based on canonical position
        public bool Equals(IHiveMember other)
        {
            return other is LatticeStorageGroup handle &&
                   positions != null && handle.positions != null &&
                   positions.SetEquals(handle.positions);
        }

        public override int GetHashCode()
        {
            return positions?.GetHashCode() ?? 0;
        }
    }
}
