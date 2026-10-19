---
category: common-mistakes
tier: generalizable
sourceGame: SurvivalSample
phase: "3,5"
question: "Is your keep-the-capture hook a character DEATH event, or does it close the segment the moment the run ends? Find the game's single run-finalisation funnel (every ending passes through it, usually with a reason enum) and check whether a run-over / results screen then stays up with the world still loaded - keep on the funnel, close when the player leaves that screen."
sanitized: true
---

# Keep the run on its finalisation funnel, and close the segment when the results screen closes

"This character stopped living" and "this run is over" are different events, and "the run is over" is
not yet "the segment is closed". Hook the second for keeping, and close on the player leaving the
results screen.

## The death event misses every won run

The game ended runs through one method taking a reason enum (victory, death, objective failure,
abandon, disconnect) that emitted one "run finished" signal. The integration kept the capture on the
player-death event only. A **won** run emits no death, so the exit's scene-change backstop discarded
it - a 13-minute winning run lost. The only log line was reassuring:

```
gameplay aborted - the captured run was discarded (Success)
```

`(Success)` is the discard call's result code, not a verdict. Grep for which SDK call appears, end or
abort. A code comment claiming "completion is handled by the death path" was not a hook: check the
subscription list. Enumerate every way a run ends and confirm each reaches the keep path. In local
co-op, death is also too broad: one character can die mid-run.

## The results screen is inside the capture

The funnel fires when the run-over screen **opens**, and players mark highlights on it. Closing there,
five marks in an 11-minute run gave three `Success` during play, then `NetworkError` ~0.2 s after the
end call (end in flight - not a network fault) and `WrongState` ~2.5 s later (room closed).

Before moving the close point, confirm the funnel tears no world down (no scene change) and that every
per-tick writer tolerates a dead player (writes a `Possessed` flag and returns early, rather than
dereferencing the player). Then split the latch:

| Path | Behaviour |
|---|---|
| finalisation signal | set `runOver`, keep capturing, log it |
| results-screen exits ("play again", "main menu") | **submit**, set `runKept` |
| leaving the gameplay scene / `sceneUnloaded` backstop | submit if `runOver`, else discard |
| quit | submit, latch `runKept` so the unload backstop cannot abort it |

Backstops that *keep* make the button hooks belt-and-braces, so an exit route you missed still keeps
the run. `runKept` is also a race guard: room handles stay non-null until the submit callback lands,
and a discard in that window aborts the submit in flight. Reset it when the next capture opens.

**Remove the death hook** once the funnel covers death: a hook that submits at death closes the
segment before the results screen appears, and two keep paths at two times is a race the earlier wins.

Related: [[per-frame-open-needs-a-closed-latch]],
[[ending-a-recording-is-async-do-not-shut-down-on-the-next-line]].
