## 2025-02-18 - Target Framework & Dependency Modernization

**Observation:** The codebase exclusively targets `netcoreapp3.1`, which is out of support. Dependencies across all projects (Octree, Tests, Benchmark) are outdated (e.g., `Tedd.BitUtils 1.0.5`, `Microsoft.NET.Test.Sdk 16.4.0`, `BenchmarkDotNet 0.12.0`). Attempting to upgrade `Microsoft.NET.Test.Sdk` without `<SuppressTfmSupportBuildErrors>true</SuppressTfmSupportBuildErrors>` fails for `netcoreapp3.1`.

**Strategic Action:** Update `TargetFramework` to `TargetFrameworks` including `netcoreapp3.1;net8.0;net9.0` to preserve the original consumer compatibility while enabling modern platform capabilities. Apply explicit property `SuppressTfmSupportBuildErrors` in the test project. Upgrade all outdated NuGet packages across all projects to the latest stable versions compatible with these targets. Ensure `PackageReadmeFile` is mapped correctly in the main project for NuGet publication.
