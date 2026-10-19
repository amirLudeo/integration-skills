---
category: architecture
tier: generalizable
sourceGame: multiple
phase: "5"
question: "Does the game persist player-profile state (currencies, items, unlocks, progression) on its own, for example when a run ends or on a front-end autosave? Then a replay writes the clip's state into whoever replayed it, unless the game's save is refused for the whole time a Ludeo is playing and only re-enabled after a full reload."
sanitized: true
---

# Block the game's save while a Ludeo is replaying

A restore writes the clip's run state (currencies, items, skill points) into the game's player-stats
store. In many games that is the store the profile persists, saved by the game itself at run end.
Without a guard, every replay writes the clip's account into the player's save.

**Reads can write too.** In one game, asking whether content was unlocked unlocked it, into the same
store: a replay session that never reached a level wrote a full unlocked-content list into the save.

## Where to put the guard

- At the **single funnel** every save passes through, not beside the restore or on the public save
  overloads - deferred and retried saves reach the funnel by their own path.
- Keyed on **"this session is replaying a Ludeo"**, not "the restore is running": the damaging save
  happens at run end, long after the rebuild finished.
- Report the refused save as **success** with one log line. Nothing in a replay depends on it, and an
  error would surface storage failures to a viewer who never asked to save.

## Lift the block only after a full reload

A local replay can end in a front end where autosave is on and only part of the save is reloaded.
Lifting the block there lets the next autosave write the replay's state, or a wiped profile, over the
player's run. Before lifting it, **reload the entire save through the game's own load path** (the call
launch-then-Continue uses), and lift only if that succeeded; otherwise keep saving blocked for the rest
of the session.

## Clear replay overrides when the player leaves the Ludeo

Overrides that answer game questions from the clip - unlocked content, which can decide what a run even
offers ([[persisted-progression-can-be-a-generation-input]]) - must be cleared when the player
**leaves the Ludeo**, not at process shutdown. Cleared on shutdown, the whole main menu showed the
recording's account as the player's, and a normal run from it would have used the clip's unlocks.

## Proving it

The guard's log line is not proof. The first test clip carried none of the guarded currency, so there
was nothing to write - a pass for the wrong reason. Test with a clip that carries the guarded state,
then decode the save file (or byte-compare it before and after a replay-to-title cycle) rather than
trusting the game's menus.

Related: [[forcing-a-setting-for-a-replay-must-not-persist-it]] - the same leak through a settings
setter that writes the save slot.
