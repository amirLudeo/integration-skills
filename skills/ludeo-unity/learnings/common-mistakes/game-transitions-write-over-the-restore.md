---
category: common-mistakes
tier: generalizable
sourceGame: multiple
phase: "4,5"
question: "Does your restore write state and then let (or make) a game transition run - re-driving a 'start' trigger, leaving a level via the game's scene change, loading a gameplay scene whose initializer runs on every load? Read each transition's body for what it RESETS or GRANTS, and write or re-assert your values after it."
sanitized: true
---

# Game transitions write over the restore

Start methods, level teardowns and per-load initializers own some state at the moment they run. Any
restore value written before one of them runs is theirs to overwrite, and none of them errors.

## Re-driving a "suppressed" trigger resets what you restored

The plan re-drove the map's event-loop start, reasoning that suppressing the run start (which zeroes
wave cursors and the clock) had suppressed its neighbour. Wrong twice: the suppression guarded one
call, not the loader, so the loop was already running; and the start method's first lines zero the
event cursors the apply had just written. The run's first side objective was offered again over a run
that had accepted it. Before re-driving any trigger:

1. Grep the loader: if it still calls the trigger on the replay path, do not add a second call.
2. Read its first ten lines. If it zeroes or re-news anything you restore, write after it instead.
3. "Not captured" is not "false" - check the flag live.

Register a read-back for every cursor a start method can reset
([[make-the-restore-verify-every-value-it-writes]]); here none existed, so nothing caught it.

## A replay started inside a level runs that level's teardown over step 0

Step 0 writes generation inputs (character choice plus a "chosen from menu" flag, seed) into statics,
then changes scene. From the menu that works. From the platform's "play again", the old level's
end-of-level hook runs after step 0 and does, reasonably:

```csharp
GameSession.cameFromMenu = false;   // synthetic: leaving a map for anywhere
```

The new map took its default-character branch; everything else matched, so it looked like a capture
fault. Fresh-boot harness runs never reach it. Re-assert flag and names together in a window after the
teardown and before the consumer: the loader's "scene load starting" signal, or `sceneLoaded` (after
`Awake`, before `Start`). Same path, other fault: [[replaying-from-inside-the-same-level-needs-a-hop-out]].

## A per-load run-start initializer re-grants on top of a pre-load apply

Copying the game's Continue shape (fill persistent services, then load the scene), the scene's
initializer ran a run-start grant on every load: starting currency was granted again on top of the
restored value, and an intro rewrote a captured camera-lock flag. Read the initializer top to bottom
for writes to restored services (`InitializeNewRun`, `ApplyStartingBonuses`, intro sequencers). Prefer
a second apply pass after the scene settles over suppressing the grant, and end with a verify pass that
logs every mismatch. Related: [[a-path-that-skips-the-menus-skips-what-the-menus-initialise]],
[[jumping-straight-into-a-level-skips-setup-the-restore-needs]].
