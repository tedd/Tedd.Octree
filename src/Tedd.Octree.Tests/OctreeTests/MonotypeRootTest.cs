using System;
using Xunit;
using System.Reflection;

namespace Tedd.OctreeDev.Tests
{
    public class MonotypeRootTest
    {
        [Fact]
        public void MonotypeRootTest1()
        {
            var octreeDev = new Octree.OctreeDev(1);
            var devField = typeof(Octree.OctreeDev).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            devField.SetValue(octreeDev, new Memory<byte>(new byte[] { 1, 42 }));
            var val = octreeDev.Get(0, 0, 0);
            Assert.Equal(42u, val);

            var octree = new Octree.Octree(1);
            var field = typeof(Octree.Octree).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(octree, new Memory<byte>(new byte[] { 1, 42 }));
            var val2 = octree.Get(0, 0, 0);
            Assert.Equal(42u, val2);
        }
    }
}
