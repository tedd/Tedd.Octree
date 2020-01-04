using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualBasic.CompilerServices;
using Tedd.SpanUtils;
using SType = System.UInt32;

namespace Tedd.Octree
{
    public class Octree
    {
        private Memory<byte> _data;
        private readonly int _levels;

        public Octree(int levels)
        {
            _levels = levels;
        }

        public ReadOnlyMemory<byte> Data => _data;

        public UInt32 Get(int x, int y, int z)
        {
            var span = _data.Span;

            var ux = (UInt32)x;
            var uy = (UInt32)y;
            var uz = (UInt32)z;
            ux = ux.ReverseBitsCopy() >> (32 - _levels);
            uy = uy.ReverseBitsCopy() >> (32 - _levels);
            uz = uz.ReverseBitsCopy() >> (32 - _levels);

            return GetInt(ref span, ux, uy, uz);
        }

        private static SType GetInt(ref Span<byte> span, UInt32 x, UInt32 y, UInt32 z)
        {
            var size = (Int32)span.MoveReadSize(out _);    // Read size of this leaf.
            span = span.Slice(0, size - 3); // Lock us to this leaf.

            // Read node header
            var leafs = span.MoveReadByte();
            var type = span.MoveReadSize(out _); // TODO: This is used for mipmap.

            // Node number we are looking for at this level
            var targetNode = (((x & 1) << 2) | ((y & 1) << 1) | (z & 1));

            var jumpLength = 0;
            var nodeSize = 0;
            // Loop through leaf index.
            // Index is stored before leaf data for cache locality. This results in a single jump as opposed to up to 8 jumps.
            for (var i = 0; i <= 8; i++)
            {
                // Does this subnode contain leafs?
                if (((leafs >> i) & 1) == 1)
                {
                    // We count jump length up until our target node
                    if (i < targetNode)
                        jumpLength += (Int32)span.MoveReadSize(out _);
                    // We remember size of our target node so we can limit span to that (used as a free bounds verification)
                    else if (i == targetNode)
                        nodeSize = (Int32)span.MoveReadSize(out _);
                    // After that we don't count, but we need to move forward until end of index before we make final jump.
                    else
                        _ = span.MoveReadSize(out _);
                }
                // No leafs in this subnode
                else
                {
                    // If this is our target node we simply return value.
                    if (i == targetNode)
                        return (UInt32)span.MoveReadSize(out _);
                    // Move forward
                    _ = span.MoveReadSize(out _);
                }
            }
            // We have now added all nodes up to our subnode, so we jump to correct location and limit span to that node
            span = span.Slice(jumpLength, nodeSize);

            // Then we dig into next level
            return GetInt(ref span, x >> 1, y >> 1, z >> 1);
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
            //var size = (int)(8 * ((Math.Pow(8, _levels) - 1)) / (8 - 1)) * 9 + 9;
            var rootNode = new Node();
            var l = _levels - 1;
            var size = BuildInt(memory, rootNode, l, 0);

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
            CreateInt(ref span, rootNode);

        }

        private void CreateInt(ref Span<byte> data, Node node)
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
                    CreateInt(ref data, c);
            }
        }

        private class Node
        {
            // TODO: Could be struct?
            public Node[] Children;
            public UInt32 Value;
            public UInt32 Size;
            public bool MonoType = true;
            public UInt32 RelativePos;
        }

        private UInt32 BuildInt(in Span<SType> memory, Node node, int level, int pos)
        {
            UInt32 size = 0;
            SType cNodeType = 0;
            node.Children = new Node[8];
            for (var i = 0; i < 8; i++)
            {
                // Create child node
                var cNode = new Node();
                node.Children[i] = cNode;

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

                    // Check if all at this level is only one type, if not set parent type to not monotype
                    if (i == 0)
                        cNodeType = cNode.Value;
                    if (cNodeType != cNode.Value)
                        node.MonoType = false;
                }
                else
                {
                    // Recursively go down until level 0
                    // TODO: We could use stackalloc here and only copy out if not monotype, would saves all allocations that are monotype
                    var l = level - 1;
                    cNode.Size = BuildInt(memory, cNode, l, childPos);

                    // If level above us is not monotype then we are not too
                    if (!cNode.MonoType)
                        node.MonoType = false;
                }

                // Accumulated size of all children
                //size += cNode.Size;

            }

            // If all children were monotype we can remove them and set our value to that
            if (node.MonoType)
            {
#if DEBUG
                //if (node.Children != null)
                //    throw new Exception("Shold be null");
#endif

                node.Value = node.Children[0].Value;
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
                        c.RelativePos = cp;
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
                        c.RelativePos += (UInt32)rh;
                        // And that number is then added to size of next
                        var sm = c.RelativePos.MeasureWriteSize();
                        rh += sm;
                        size += (UInt32)sm;
                    }

                    size += c.Size;
                }


                // If we have children we also need a pointer
                //size++;
            }


            return size;
        }


    }
}
