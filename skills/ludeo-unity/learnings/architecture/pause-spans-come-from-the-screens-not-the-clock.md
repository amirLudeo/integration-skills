---
category: architecture
tier: generalizable
sourceGame: multiple
phase: "3,6"
question: "Are you deriving PauseLudeo/ResumeLudeo (or non-ludeoable) spans from 'the time scale reached zero', and can gameplay actions be raised while an input-waiting screen (dialog, reward pick, results, level select) is up? Drive the spans from the screens themselves, one latch for the whole stretch, and defer actions raised under an open pause."
sanitized: true
---

# Pause spans come from the screens, not the clock

What the backend needs is "is the game waiting on the player to choose something", not "is time
stopped". Answer it from the screens, keep one latch across nested screens, and hold back actions
raised while a pause is open.

## A zero time scale answers the wrong question

A game's `Time.timeScale` arbiter is one public funnel with a change event, so it looks ideal. It
disagreed with "waiting on the player" three ways:

1. **Zero with nobody waiting:** kill-cam and finisher ramps, a dev free camera, `time_scale 0`.
2. **Modals that never zero it, or never call the pause primitive:** a confirm dialog held its own
   arbiter request; a chat window blocked input and registered nothing.
3. **No event on hand-over:** a dialog over a paused menu is 0 → 0, so the arbiter fires nothing; a
   loading screen registered a running value above every pause.

Keep the arbiter watch for "is the sim stopped" (the restore freeze needs it), but retire its reporting
role and say so where it used to be wired.

## Drive the span off the screens, never stuck by construction

- Poll an ordered predicate per frame ("which screen is in the player's way, or none") from real
  visibility (`activeInHierarchy`, owner enabled); `Update` runs at zero time scale. Return the
  holder's *name* so the log reads "waiting on the player (a confirm dialog)".
- **One latch for the whole stretch**, plus a short release debounce (~0.15 s unscaled) on the way out
  only, so a screen swapping to the next sends no Resume/Pause pair. Teardown never waits for it.
- Wrap each screen read in its own `try/catch`, a throw counting as closed; a destroyed canvas would
  otherwise throw every frame and leave the pause open.
- Build the screen list from your own census: the game's "is a blocking screen open" helpers (four
  disagreeing copies in one game) are not authoritative. Prefer a GUI latch over a window's
  `activeSelf` - tabbing to a side panel deactivated the window while the pick was still owed.
- **Close the open span as the first step of every teardown**, before `End`.
- A frozen selection screen needs both pairs, nested
  ([[pause-and-non-ludeoable-triggers-split-by-flow-a-real-pause-needs-both]]).

## Defer actions raised under an open pause

The backend saves no data for a pause span, so an action sent inside one is inferred to fall in the
dropped stretch and a goal built on it may never count, silently. UI pause sources make this common: a
reward claim from a popup's OK button, a run-start action from a level-select Start button.

- In the single action path, queue gameplay actions while a pause is open; send them right after
  `ResumeLudeo`. Never defer `PauseLudeo`, `ResumeLudeo`, `StartNoneLudeable`, `StopNoneLudeable`.
- Flush the queue in teardown after closing the span, before `End` or `Abort`.
- Bound the queue and count overflow. Assert per run that the log shows `ResumeLudeo` before the
  deferred action and that nothing was lost or sent while paused.

Related: [[non-ludeoable-spans-as-a-state-machine-not-paired-calls]].
