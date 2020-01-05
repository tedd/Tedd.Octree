﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualBasic.CompilerServices;
using Tedd.SpanUtils;
using SType = System.UInt32;

namespace Tedd.Octree
{
    public class OctreeDev
    {
        public OctreeDev(int levels)
        {
            _levels = levels;
        }

        private Memory<byte> _data;
        private readonly int _levels;


        public ReadOnlyMemory<byte> Data => _data;

        public unsafe UInt32 Get(int x, int y, int z)
        {
            var span = _data.Span;

            var header = span.MoveReadByte();
            if (header.IsBitSet(0))
                // This is a monotype
                return span.ReadSize(out _);

            var ux = (UInt32)x;
            var uy = (UInt32)y;
            var uz = (UInt32)z;
            ux = ux.ReverseBitsCopy() >> (32 - _levels);
            uy = uy.ReverseBitsCopy() >> (32 - _levels);
            uz = uz.ReverseBitsCopy() >> (32 - _levels);

            for (var i = 0; i < _levels; i++)
            {
                var leafDesc = span.MoveReadByte();

                // Our target at this level
                var targetNode = (int)(((ux & 1) << 2) | ((uy & 1) << 1) | (uz & 1));
                // Then we dig into next level
                // Optimization: We do this here so we clump it with the other shift operations, hopefully get some reciprocal throughput (which for and/shift is 1/3)
                ux >>= 1;
                uy >>= 1;
                uz >>= 1;
                ;
                // Skip the other nodes
                for (var n = 0; n < targetNode; n++)
                {
                    System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref span[64]));
                    span.MoveSize();
                }

                // If this is a monotype our search ends here
                if (leafDesc.IsBitSet(targetNode))
                    return span.ReadSize(out _);

                // Not monotype, so this is a relative pointer and so we jump
                var jmp = span.MoveReadSize(out _);
                span.Move((int)jmp);
            }

            // We should never get this far
            throw new Exception("Error reading octree data structure.");
        }

        public void Build(Span<UInt32> memory)
        {
            // Each node theoretical max:
            //  - 4 bytes size
            //  - 4 bytes type descriptor
            //  - 1 byte subnode descriptor (8 bits indicating if subnode has subnodes)

            // Level 0: 1 node (special case)
            // Level 1: 8 nodes
            // Level 2: 8*8 nodes
            // Level 3: 8*8*8 nodes
            // Level 4: 8*8*8*8 nodes
            // Level 5: 8*8*8*8*8 nodes
            // (8^1+8^2+8^3+8^4+8^5)*9=337032 bytes
            var memSize = (int)(8 * ((Math.Pow(8, _levels) - 1)) / (8 - 1)) * 20;

            //var inGcRegion = GC.TryStartNoGCRegion(memSize, true);
            try
            {
                var rootNode = new Node() { MonoType = true };
                var size = BuildInt(memory, ref rootNode, _levels - 1, 0);

                if (rootNode.MonoType)
                    // Monotype
                    _data = new Memory<byte>(new byte[1 + rootNode.Value.MeasureWriteSize()]);
                else
                    // Other
                    _data = new Memory<byte>(new byte[1 + size]);

                var span = _data.Span;

                byte header = 0;
                // In case of root node being monotype then we simply store the header+monotype and nothing more. This means that a cube of any size with same types is stored as low as 2 bytes (and max 5 bytes): header+monotype
                header.SetBit(0, rootNode.MonoType);
                span.MoveWrite(header);

                // We added 1 byte extra as leafdescriptor
                CreateInt(ref span, ref rootNode);
            }
            finally
            {
                //if (inGcRegion)
                //    GC.EndNoGCRegion();
            }

        }

        private void CreateInt(ref Span<byte> data, ref Node node)
        {

            // The simple case of monotypes
            if (node.MonoType)
            {
                data.MoveWriteSize(node.Value);
                return;
            }

            var leafDesc = data.Slice(0, 1);
            byte leafDescByte = 0;
            data.Move(1);

            for (var i = 0; i < 8; i++)
            {
                var c = node.Children[i];
                if (c.MonoType)
                    data.MoveWriteSize(c.Value);
                else
                    data.MoveWriteSize(c.RelativePos);

                leafDescByte.SetBit(i, c.MonoType);
            }

            leafDesc[0] = leafDescByte;

            for (var i = 0; i < 8; i++)
            {
                var c = node.Children[i];
                if (!c.MonoType)
                    // Write body of this subnode
                    CreateInt(ref data, ref node.Children[i]);
            }
            // Release to GC asap
            node.Children = null;
        }

        [DebuggerDisplay("Monotype={MonoType}, Value={Value}, ChildrenCount={Children.Length}, Size={Size}, RelativePos={RelativePos}")]
        private struct Node
        {
            public Node[] Children;
            public UInt32 Value;
            public UInt32 Size;
            public bool MonoType;
            public UInt32 RelativePos;
        }

        private UInt32 BuildInt(in Span<SType> memory, ref Node node, int level, int pos)
        {
            UInt32 size = 0;
            SType cNodeType = 0;

            //node.Children = new Node[8];
            Node prevNode = default;
            for (var i = 0; i < 8; i++)
            {
                // Create child node
                var cNode = new Node() { MonoType = true };

                // calculate position in structure
                var x = ((i >> 2) & 1) << (_levels + _levels);
                var y = ((i >> 1) & 1) << _levels;
                var z = (i & 1);
                var childPos = (pos << 1) | x | y | z;
                //var childPos = (pos << 3) | i;

                if (level == 0)
                {
                    // Last level so we just get value
                    cNode.Value = memory[childPos];
                    cNode.Size = (UInt32)cNode.Value.MeasureWriteSize();
                }
                else
                {
                    // Recursively go down until level 0
                    cNode.Size = BuildInt(memory, ref cNode, level - 1, childPos);
                }

                // Check if all at this level is only one type, if not set parent type to not monotype
                if (i == 0)
                    cNodeType = cNode.Value;

                if (node.MonoType && (cNodeType != cNode.Value || !cNode.MonoType))
                {
                    node.MonoType = false;

                    // We need to create array + populate it up to this point
                    node.Children = new Node[8];
                    // Up until now all has been same type, fill in
                    for (var c = 0; c < i; c++)
                        node.Children[c] = prevNode;
                }

                if (!node.MonoType)
                    node.Children[i] = cNode;

                // Remember in case we need to fill in
                if (node.MonoType)
                    prevNode = cNode;
            }

            // If all children were monotype we can remove them and set our value to that
            if (node.MonoType)
            {
                node.Value = cNodeType;
                node.Children = null;
                // So our size is simply the value
                node.Size = 0;
                size = (UInt32)node.Value.MeasureWriteSize();
            }
            else
            {
                size = 1; // Leaf descriptor

                // This is not monotype, so we calculate the relative start address of each non-monotype child
                var cp = (UInt32)0;
                for (var i = 0; i < 8; i++)
                {
                    var c = node.Children[i];
                    if (!c.MonoType)
                    {
                        node.Children[i].RelativePos = cp;
                        cp += c.Size;
                    }
                }
                // The pointer also must contain the size of the following node pointers since it must jump across them
                var rh = 0;
                for (var i = 7; i >= 0; i--)
                {
                    // Measure size of all remaining pointers and values
                    var c = node.Children[i];
                    if (c.MonoType)
                        rh += c.Value.MeasureWriteSize();
                    else
                    {
                        // Add to current pos
                        node.Children[i].RelativePos += (UInt32)rh;
                        // And that number is then added to size of next
                        var sm = node.Children[i].RelativePos.MeasureWriteSize();
                        rh += sm;
                        size += (UInt32)sm;
                    }

                    size += c.Size;
                }
            }

            return size;
        }




    }
}
