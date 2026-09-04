using System;
using System.Runtime.CompilerServices;

namespace Tedd.Octree;

internal static class OctreeThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ThrowUnbuilt() =>
        throw new InvalidOperationException("The octree has not been built.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ThrowInvalidView() =>
        throw new InvalidOperationException("The octree view is not initialized.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ThrowCoordinate(string parameterName, int value) =>
        throw new ArgumentOutOfRangeException(parameterName, value, "Coordinate is outside the octree.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static uint ThrowMalformed() =>
        throw new FormatException("The octree data is malformed.");
}
