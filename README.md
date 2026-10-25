# Polly discarded-pipeline reproduction

Reproduction for [App-vNext/Polly#3286](https://github.com/App-vNext/Polly/issues/3286).

Run with .NET SDK 9 and the .NET 9 runtime:

```powershell
dotnet run --project PollyRegistryRepro.csproj --configuration Release
```

The dependency is pinned to Polly.Core 8.8.0. The test uses eight dedicated worker tasks and a barrier inside configuration to force concurrent construction for the same key. It checks both generic and non-generic registries.

Expected output on the affected version:

```text
Non-generic: built=8, distinct returned=1, disposed after registry shutdown=1
  Builds triggered AFTER registry disposal by reload signal: 7; expected 0
  Total built=15, total disposed=8, undisposed=7
Generic: built=8, distinct returned=1, disposed after registry shutdown=1
  Builds triggered AFTER registry disposal by reload signal: 7; expected 0
  Total built=15, total disposed=8, undisposed=7
```

The program exits successfully when it reproduces the bug and throws if the expected observations do not occur. It is a diagnostic harness, not a passing regression test for a fixed library. On a fixed implementation, all constructed instances should be cleaned up and no reload should occur after shutdown.

Related historical discussion: https://github.com/App-vNext/Polly/issues/2539. Duplicate construction was discussed there; the proposed contribution focuses on cleanup of discarded instances.

Verified 24 October 2026 with SDK 9.0.316. An isolated build of unmodified current Polly.Core source at commit 3ab3ec00b74e4816f5a4759dadbb6997fe175c17 produced the same output. The full upstream test suite was not run.