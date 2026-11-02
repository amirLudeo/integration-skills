---
category: common-mistakes
tier: generalizable
sourceGame: SurvivalSample
phase: "1,2,3,4,5,6,7,8"
question: "Are you driving the Unity Editor through an agent/MCP bridge that COMPILES a C# snippet per call (a RunCommand-style tool), or sharing one open Editor with other agent sessions? Every such compile is a domain reload - mid-play it kills the session, and in a shared Editor it hits every other session. Use the bridge only in stopped mode, on a stop signal the current run created, and ask peers before any reload."
sanitized: true
---

# Every Editor compile is a domain reload, for every session using that Editor

A snippet-compiling bridge call, an `AssetDatabase.Refresh` or entering play mode reloads the domain of
the one Editor process. During play mode that kills the run; with several sessions it hits all of them.

## The bridge is a stopped-mode tool

A `RunCommand`-style tool compiles an assembly per call, so an in-play call forces a hot reload. On a
large HDRP project, in-play calls ended in `Hotreload` + `PostProcessAllAssets` and a frozen Editor.
Drive play mode from inside the game, with the filesystem as the interface:

```
agent  -> (bridge, stopped) write job.json, open boot scene, EnterPlaymode()
game   -> [RuntimeInitializeOnLoadMethod] spawns a runner iff job.json exists; consumes it at start
runner -> runs the scenario, writes result.json, exits play mode
agent  -> reads result.json off disk, then the log
```

The same runner works under `-batchmode` and in a built player
([[agent-can-run-unity-compile-gates-headlessly]]).

## Only believe play stopped on a signal this run created

A run configured not to exit play mode (so a human could look around) removed the only deterministic
stop signal. Every inferable marker (a session-release line, the backup-scene reload) recurs once per
run, so a bridge call made on that inference reloaded a live session: statics wiped, per-frame
`NullReferenceException`, the log growing 4 MB to 112 MB in two minutes, a crash. Prefer: the run's own
result file; else the human who kept the session owns the stop and says so; else a marker carrying the
current run's id. Also: an error-level log during a bridge call can be reported as tool failure, and a
modal (a dirty-scene save prompt) hangs the bridge - check every open scene's `isDirty` first.

## Parallel sessions share one Editor

Up to five sessions on one project exchanged about 100 messages negotiating the Editor (64 in a day);
one session's half-written files broke another's compile, and even waking the bridge after its idle
shutdown triggered a reload. A **human** capture writes no job or result file, so it is invisible to
"is a run in flight?" checks and is lost to a reload.

1. Partition files up front; concurrent text edits coexisted, reloads did not.
2. Before any reload, message the peer and wait for a reply; never infer from a missing result file.
3. Compile without the Editor where possible
   ([[dotnet-build-of-the-unity-csproj-cannot-catch-player-build-breaks]] for its limits).
4. Keep shared state in one notes file every session reads.
5. If every step needs the Editor, prefer one session at a time.
