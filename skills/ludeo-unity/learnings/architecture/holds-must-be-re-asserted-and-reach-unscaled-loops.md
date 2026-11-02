---
category: architecture
tier: generalizable
sourceGame: TopDownRogueSample
phase: "5"
question: "Does your layer hold the game (restore freeze, a pause span, a hand-back under a dim) while game code can still re-enable input on its own (hub or menu panels, fades, popups) or run loops on unscaled time (Time.unscaledTime, WaitForSecondsRealtime)?"
sanitized: true
---

# Holds must be re-asserted every frame and must reach unscaled-time loops

A hold applied once, by input disable plus `Time.timeScale = 0`, leaks two ways: game code turns input
back on later, and unscaled-time loops never noticed the freeze.

## Re-assert the hold, and hold every player-controlled component

In a game with a hub or menu layer that stays alive, closing a panel, a fade finishing or a popup
dismissing writes "input enabled = true" after the layer's single disable, and the viewer controls a
game that is supposed to be parked.

- **Re-assert every frame** for the whole hold; a later game write cannot undo a per-frame hold.
- **Hold every player-controlled component**, not just the one the moment used - a hub or second mode
  often has its own movement and input components
  ([[normalize-the-whole-agency-suppressing-flag-family-not-just-the-input-one]]).
- Grep the **assignments** of every input-enable flag and confirm each writer is unreachable during the
  hold or overwritten by it.

## Park unscaled loops at the commit, not the timer

A loop waiting on `Time.unscaledTime` keeps producing through the restore freeze, a pause span and a
hand-back. Add one guard per loop, checked once per iteration, **just before it commits its result**
(the item is produced, the payment applied):

```csharp
while (true) {
    yield return new WaitForSecondsRealtime(interval);
    while (LudeoHolds.Active) yield return null;   // park here: the timer simply overruns
    Commit();
}
```

Parking at the commit means nothing completes while held and no timer state needs capturing; on
release the loop commits and carries on. **Derive the hold live** from its causes (restore frozen,
pause span open in the Player flow, hand-back in progress) so it cannot outlast them, and leave the
Creator flow alone.

## Prove it with work in flight

Seed the loops with items before the hold and count commits during it. On an idle world "nothing
committed" passes trivially; a single commit while held is a leak.
