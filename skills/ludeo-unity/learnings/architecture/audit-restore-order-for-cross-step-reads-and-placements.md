---
category: architecture
tier: generalizable
sourceGame: SurvivalSample
phase: "5"
question: "Does your restore apply in an order built from per-entity rules ('clock last', 'health after level', 'objectives after cursors') and call the game's own accept/spawn entry points for some steps? Check what each step READS from another step's value and what each game call PLACES that another step already restored - and when a value drifts, sample it after its own write before guessing which."
sanitized: true
---

# Audit the restore order for cross-step reads and placements, not just per-entity rules

Ordering rules written per entity do not compose. Two steps can each be right on their own and still
collide because one reads, or moves, what the other writes.

## A late write that an earlier step reads

"Write the clock last" (the level-entry trigger zeroes it) met "write health after levelling" (max
health is derived, so health is captured as a percentage). Levelling read the clock -
`FloorToInt(elapsedTime / interval)` - so with the clock still at zero, several hundred enemies eleven
minutes into a run came back level 1 and died to one hit. Fix: **pre-load** the clock before the pass
that derives from it, and still write the full clock last. Safe because the apply is one synchronous
block inside the freeze, so no frame observes the half-state. Wrap the apply and zero the clock on a
throw: a mid-run clock over unrestored wave cursors walks the whole fight in one frame and ends the
run a second in.

Before committing an order, walk every "deferred until after X" note and ask what X's inputs are.

## A game entry point that places what another step restored

Driving the game's own "accept this objective" path is right (it registers HUD, counters and quest
state that field writes skip), but accepting also **spawns and places the objective's props** - and
one prop type owns a miniboss the enemy pass had restored three steps earlier. Two of 39 named threats
moved, one of them 68 m. Ask of every step that calls into the game: what does it *touch*, not what
does it restore. Narrow fix: re-assert **position only** for affected entities after the owning step,
and only for entities the clip had on the field (a parked one belongs to its fight).

## Diagnose with three samples, not one drift figure

The check that reported it compared the clip's value to the live transform after the settle:
`enemies after the settle: 2 of 973 wrong, worst drift 68.12m`. That number is identical whether the
write never ran, ran and did not take, or took and was overwritten - three sessions of confident wrong
guesses. Sample recorded, **right after the restore's own write**, and end of pass:

| samples | verdict |
|---|---|
| after-write ≠ recorded | the write did not take - look at the setter |
| after-write = recorded, end-of-pass ≠ after-write | a later step overwrote it |
| end-of-pass = recorded, post-settle ≠ recorded | a live system moved it during the settle |

Print only subjects that moved, per step; one bracketed run named the accept call. Async spawns that
land late look like the last row (see [[restore-async-stage-waits-frozen-behind-a-watchdog]]).

Same ownership question asked at write time: [[write-restored-state-to-whoever-owns-it]].
