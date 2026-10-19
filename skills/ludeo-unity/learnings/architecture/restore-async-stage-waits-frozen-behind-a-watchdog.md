---
category: architecture
tier: generalizable
sourceGame: SurvivalSample
phase: "5"
question: "Does part of your restore ask the game to BUILD something (load a prefab, assign through a callback, spawn via a void-returning call that resolves an asset inside) - and does one coroutine own the freeze, that wait, the unfreeze and the 'restore applied' notification? Then wait frozen on a counted signal with a real-time deadline, and fence that coroutine so a throw or a hang still hands over."
sanitized: true
---

# The restore's asynchronous stage waits frozen, on a real signal, behind a fence and a watchdog

When a restore asks the game to build things, keep the world frozen while you wait, end the wait on
what actually arrived, and make sure a throw or a hang inside that wait still unfreezes and notifies.

## Wait frozen - loads advance at a stopped clock

Unfreezing to wait lets hundreds of already-placed entities walk and fight for as long as the loads
take, so the moment drifts before anyone sees it. Freezing only deadlocks work gated on a physics step.
Which callbacks still tick at a stopped clock also decides whether the SDK's notification pump survives
(see [[verify-the-timescale-pump-claim-against-the-installed-plugin]]).

| Advances at `timeScale = 0` | Does **not** |
|---|---|
| asset / addressable loads and their callbacks | `WaitForSeconds`, any scaled timer |
| `yield return null`, `Time.realtimeSinceStartup` | `FixedUpdate`, `Time.time` deadlines |

Shape: frozen stage 1 writes everything in one pass; frozen stage 2 starts the builds and waits
(`yield return null` + real-time deadline); unfreeze a few frames to settle; frozen stage 3 re-asserts
derived values (health stored as a percentage of a maximum that depends on equipment and buffs).

## End the wait on what arrived, not on a duration

- A callback is an exact signal: count up when you start, down inside each callback.
- **A void-returning call can still be async.** `SpawnPickup(type, pos)` reads like a command, but if
  its body (or the overload it wraps) resolves an asset by name or address, it returns before the thing
  exists. One restore's gate showed apply "ok" at 33.1036s and verify "FAILED, slot 0 empty" at
  33.1056s: the wait loop counted only the callback path. Read every game call's body for an asset
  resolve before treating the next line as done.
- No callback? Watch the world: `Registry.Count < before + requested` is outstanding.
- Bound every wait with a real-time deadline; on expiry log requested vs arrived and **continue**.
  Make sure whatever you count is counted back down, or every replay pays the full deadline.

## Fence it: hand-driven `MoveNext`, a `finally`, a watchdog

If anything in the owning coroutine throws, Unity drops it: the world stays frozen, the SDK is never
told the restore applied, and the begin gate refuses `RoomReady` for the rest of the process.

```csharp
IEnumerator stage = InnerStage(ctx);
while (true) {
    object current;
    try { if (!stage.MoveNext()) break; current = stage.Current; }
    catch (Exception e) { ReportAndKeepGoing(ctx, e); break; }   // do not rethrow
    yield return current;                                         // outside the try: legal C#
}
```

Wrap the whole handshake in `try { … } finally { … }` doing only the non-yielding hand-over (unfreeze,
release input, notify), guarded by a "normal path finished" flag set **after** the notify returns. A
`finally` cannot catch a hang, so add a real-time watchdog budgeted against the platform's load
timeout that finishes degraded through the same hand-over and names the last stage reached.

Prove it with fault injection placed where only the defence under test can save it, and read the log
lines, not the verdict: a "throw on the 3rd `MoveNext`" never fired in a two-step stage and still passed.
