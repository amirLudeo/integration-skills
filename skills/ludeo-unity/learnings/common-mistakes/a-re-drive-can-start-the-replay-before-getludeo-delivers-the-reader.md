---
category: common-mistakes
tier: generalizable
sourceGame: TopDownRogueSample
phase: "3,5"
question: "Does your layer re-try starting the play flow from more than one place (the Activate callback, a consent update, a readiness gate resolving, as well as the LudeoSelected handler)? Then every one of those paths must refuse to start until THIS selection's GetLudeo callback has delivered its reader."
sanitized: true
---

# A re-drive can start the replay before GetLudeo has delivered the reader

Every "try to start the play flow now" call site must wait for the reader that belongs to the current
selection, not for "a selection is pending".

On a preselected launch (`Activate` with `isLudeoSelected == true`, then `LudeoSelected` a moment
later) a layer usually re-drives the start from several places, because it waits on activation,
consent and readiness. The `LudeoSelected` handler meanwhile defers `GetLudeo` by a frame, since it
must not call into the SDK from inside an SDK dispatch. Observed:

```
Activate: Success isLudeoSelected=True
LudeoSelected: <ludeoId>
restored data: no data reader
replay failed: restore data unreadable or empty   -> handed back
GetLudeo issued (<ludeoId>)                       <- only now
GetLudeo: Success                                 <- a few hundred ms later, too late
```

The Activate and consent re-drives were queued before the deferred `GetLudeo`, ran first, saw a
selection pending and restored against no data. On a mid-app selection the same hole is quieter and
worse: the re-drive finds the **previous** Ludeo's cached reader and replays the wrong clip.

## SDK facts that shape the fix (observed on plugin 4.3.x; re-check on the installed version)

- `isLudeoSelected = true` only promises that a `LudeoSelected` will follow; `GetLudeo` needs the id
  that notification carries.
- A forced (auto-start) launch delivered `LudeoSelected` from inside another SDK dispatch, so
  `GetLudeo` had to be deferred a frame. That deferral opens the hole.
- `GetLudeo` had a **single static callback slot**: a second call in flight overwrote the first call's
  delegate, so the first result landed on the wrong handler. Serialize the calls.

## The fix shape

- Number every selection; remember which generation the current reader belongs to.
- The play-flow start refuses, logging "waiting for GetLudeo's reader", until
  `readerGen == selectionGen`. Every re-drive goes through that one check.
- One `GetLudeo` in flight. A newer selection waits and is issued when the current one lands; a result
  for an older selection is discarded and its reader disposed off the dispatch.
- Put a named deadline on `GetLudeo`, and hand back on timeout or failure.

Related: [[never-call-into-the-sdk-from-inside-an-sdk-callback]] (the deferral that creates the
window), [[a-guard-that-cannot-fire-is-not-evidence]] ("a selection is pending" was true for the wrong
reason).
