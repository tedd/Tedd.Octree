# Archived v1 implementation

OctreeV1.cs preserves the construction, storage, and lookup algorithms from
src/Tedd.Octree/Octree.cs at commit 3ac31f8.

The archived type was renamed and made internal so it can coexist with the
current package. Its former Tedd.BitUtils and Tedd.SpanUtils calls are
implemented locally in LegacyExtensions.cs; the legacy byte encoding remains
unchanged. This archive is benchmark-only and is not shipped by the library.
