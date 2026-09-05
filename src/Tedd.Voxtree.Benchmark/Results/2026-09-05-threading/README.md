# World synchronization measurements

Measured on 2026-09-05 using the working-tree threading changes based on
`8e89ccf0fa2eab793a830fc40d72b1a120553da3`. The report retains the BenchmarkDotNet, OS, CPU,
SDK/runtime, confidence interval, and allocation metadata. No commit is implied
for the uncommitted changes. SHA-256 identities of measured implementation files:

- `src/Tedd.Voxtree/OctreeWorld.cs`: `6b999074dacd285c2efd5191aade553e81868b1474791aef71605eb64526c532`
- `src/Tedd.Voxtree/OctreeWorld.Queries.cs`: `1d0abcf1c425a7ef819e1691041e1ac07a720ffc1acb05257f64aefaea3e01f0`
- `src/Tedd.Voxtree/OctreeWorld.Threading.cs`: `c62ae7b56dbb674839c8fa47dc7819218d77b08086ec80bc0ac89f80fddb4e4b`
- `src/Tedd.Voxtree.Benchmark/Tests/WorldSynchronization.cs`: `de6ccdeb1df3f2b30496343b74c4413ae240ea211ff62fd4d9fd13e515ef6217`

| Operation | Individual locking | Batch of 1,024 | Reduction per operation |
| --- | ---: | ---: | ---: |
| World point read | 26.130 ns | 19.874 ns | 24% |
| Resident chunk replacement | 27.335 ns | 19.648 ns | 28% |

All cases measured zero managed bytes per operation after setup/warmup. Batch
acquisition/disposal is included and amortized across 1,024 operations. Both
paths execute the same public operations; batched calls borrow the existing lock.
The world is depth 10, chunks depth 3, with a single resident uniform channel.
Read coordinates vary inside that chunk. Writes alternate two prebuilt nonempty
snapshots and include the path lookup, reference replacement, and revision change.
They exclude encoding, branch splitting/collapse, queueing, and I/O.

Capturing a retained immutable chunk once and reading its channel measured
1.887 ns per voxel. This skips outer-tree traversal as well as repeated locks,
and intentionally keeps a historical snapshot if the world changes. It is not
an unlocked-world control, nor representative of heterogeneous tree decoding.

This screening run uses one launch, three warmups, and five measurements.
It measures warm, uncontended operation cost, not parallel scaling, writer
fairness, or tail latency. The shared-reader/exclusive-writer behavior is covered
by concurrency tests; real contention still requires application measurements.
First use on a worker and initial contention may allocate lock bookkeeping or
wait handles. Results do not establish that reader/writer locking is universally
faster than a monitor.

Reproduce from the repository root:

```shell
dotnet run -c Release -f net10.0 --project src/Tedd.Voxtree.Benchmark -- --filter '*WorldSynchronization*' --job short --warmupCount 3 --iterationCount 5 --artifacts BenchmarkDotNet.Artifacts/threading-net10
```
