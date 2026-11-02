---
category: architecture
tier: generalizable
sourceGame: multiple
phase: "5"
question: "Are you about to test replay -> replay (a second LudeoSelected in one process) on a LOCAL player-flow run started with LudeoSettings autoStartInLudeo / ludeoToAutoStart? The auto-start injects exactly one selection; a second one only comes from the platform, after the Ludeo ends, through the overlay's end-of-run screen, whose focused Play Again a posted Confirm key presses. Let a run reach its natural end and read the overlay lines before designing the test."
sanitized: true
---

# The next Ludeo comes from the end-of-run screen, not from the auto-start

**Plugin 4.3.3.** The local player flow is started with `LudeoSettings.autoStartInLudeo` +
`ludeoToAutoStart`. The plugin injects that id as `LudeoSelected` in exactly one place: the video-state
handler in `LudeoSession_Notifications.cs`, guarded by `LudeoManager.Instance.IsActivationCallbackWaiting`.
So it fires once per process, during activation. Nothing in the plugin can produce a second local
selection. Any later `LudeoSelected` comes from the backend.

## What actually happens when a played Ludeo ends (observed locally)

Every earlier harness run left the replay after ~20 s, which is why nobody had seen this. A run that
sends no input and waits past the Ludeo's length (`onLudeoReady … durationMs=` in the overlay log) shows:

1. At `durationMs` the overlay logs `LudeoEnded intercepted -- forwarding to HTTP /ludeo-player/end-ludeo`,
   then dispatches `EndLudeoPlay`, `ludeo-player.ludeo-win` (or lose), `gameplays.gameplay-ended`, `FreezeGame`.
2. The SDK raises **`PauseGameRequested`**. This is the "Ludeo is over" pause the docs describe. It is
   **terminal**: no `ResumeGameRequested` follows. Your pause handling must freeze and stay frozen.
3. The overlay requests **end-of-run recommendations**
   (`GET /api/v3/recommendations/get-eor?count=3&…`), which are the "play next" Ludeos (Other Moments).
   **A second `LudeoSelected` is the viewer picking Play Again or one of these.** `GameBackToMenuRequested`
   is not sent on its own. In our run, 95 s after the end, neither Back-to-Menu nor a second selection had
   arrived: nothing had pressed anything.
4. The recommendation service can fail (`503 ServiceUnavailableError`). In the first game the end screen
   then had nothing to offer, and the game correctly sat frozen behind the overlay; harness screenshots
   showed a black frame there (the overlay is drawn into the game's frame, and the game view under it
   was intact at the moment of the pause). On the overlay seen in two later games the end screen
   (`SummaryScreen`) falls back to the current Ludeo (`backend returned no rows -- inserting current
   ludeoId`), with **Play Again** focused.
5. Play Again: `sendInput dispatch action='Confirm' screen='SummaryScreen'` → `"eventType":"ReplayLudeo"`
   → `POST /api/v3/ludeos/replay` → `NewLudeoSelected`. The SDK ends the gameplay session and closes the
   room, the layer opens a new room for the same Ludeo, and the overlay starts the run itself
   (`auto-firing PlayLudeo`, no pre-play screen) → a second `ludeo_GameplaySession_Begin succeeded`.

## How to apply

- Replay -> replay of the **same** Ludeo runs locally with no person: let the replay reach its end, then
  post the overlay's `Confirm` key (from its `bindings rebuilt` line) to the game window, which presses
  the focused Play Again (`scripts/play-game.ps1 -Again`). It proves the teardown and re-entry, and that
  the end-of-run state doesn't leak into the restore. It caught a restore exception that only the
  second run threw and that its screenshot didn't show, so judge it from the log.
- A **different** second Ludeo still needs (a) another published Ludeo for the game, (b) the
  recommendation service up, and (c) a person to move the focus to it: the overlay binds no key for that.
  Leave it to the user's sign-off, or to a cloud check (phase 7), rather than simulating the
  notification and calling it proven.
- A harness scenario that only observes (no input after Play, watch the SDK notification lines and the
  overlay's `dispatchEvent name=` lines, screenshot on the first of each) is cheap and turns guesses
  about the end-of-Ludeo sequence into a log.
- The end-of-Ludeo `PauseGameRequested` is also your only locally reachable proof of the SDK -> game
  pause direction (CR-011). Check the freeze and your pause/non-ludeoable bookkeeping fired exactly
  once there.

See also [[room-ready-is-the-players-play-click]] (the pre-play button, which a posted `Confirm` also
presses: `sendInput dispatch action='Confirm' screen='PrePlayScreen'`).
