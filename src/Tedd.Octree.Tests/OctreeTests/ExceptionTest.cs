using System;
using Xunit;
using System.Reflection;

namespace Tedd.OctreeDev.Tests
{
    public class ExceptionTest
    {
        [Fact]
        public void ThrowsExceptionTest()
        {
            var octreeDev = new Octree.OctreeDev(1);
            var devField = typeof(Octree.OctreeDev).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            devField.SetValue(octreeDev, new Memory<byte>(new byte[] { 0, 0, 0, 0, 0 }));
            Assert.Throws<Exception>(() => octreeDev.Get(0, 0, 0));

            var octree = new Octree.Octree(1);
            var field = typeof(Octree.Octree).GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(octree, new Memory<byte>(new byte[] { 0, 0, 0, 0, 0 }));
            Assert.Throws<Exception>(() => octree.Get(0, 0, 0));
        }
    }
}
