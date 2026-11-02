---
category: architecture
tier: generalizable
sourceGame: TopDownRogueSample
phase: "6"
question: "Does the integrator want an amount-based Studio Lab goal ('earn X currency', 'deal X damage', 'score X points') rather than a count of discrete events?"
sanitized: true
---

# An amount goal needs threshold actions, and they must re-arm in the Creator flow

`SendAction` takes an action name and nothing else; there is no amount parameter, and Studio Lab goals
are built from actions. A numeric attribute holding a running total did not give the integrator an
amount goal. So "earn 50K" has to become "the action `Score_50K` happened".

## The ladder

- **A closed ladder of threshold actions, one name per rung.** For an exponential economy (one event
  can pay single digits or 10^12) use a 1-2-5 ladder: `Score_10, Score_20, Score_50, Score_100 …
  Score_1K …`, up to the value type's ceiling, and saturate there instead of wrapping.
- **One event can cross several rungs.** Send each crossed rung, lowest first.
- **Keep your own counter**, fed from the same event and value the game uses, reset in
  `BeginGameplay`'s success callback. A restored moment's existing total must never count.
- Name rungs with the game's own compact-number suffixes so goal text matches the HUD. The name set is
  closed, so the per-version action list does not grow with play.

## Player flow vs Creator flow

- **Player flow:** `BeginGameplay` is the Ludeo's start, so each rung is sent once when the total
  first reaches it.
- **Creator flow:** the cut is chosen after recording, so a once-per-capture rung can fire minutes
  before the cut and the goal is not offered for a cut that clearly earned it. Re-arm each rung: give
  it **its own counter of value since it was last sent**, send when that reaches the rung, keep the
  remainder. Any cut that earned at least X then contains an `X` send.
- **Bound the volume with a per-rung minimum send interval** (for example 1 s); a crossing inside the
  interval stays pending and goes out from a per-frame tick. Cost: a cut ending less than one interval
  after a crossing can miss it.

Caveat: a ladder on a single event's size ("one hit worth at least X") supports "reach X" only; if a
rung is sent at most once, a goal with count > 1 on it can never complete. Say so when you hand the
rows to the studio.

## How to apply

1. Verify the ladder on fixed inputs: 0, 9 to 10, 49 to 51, one huge jump, near the ceiling.
2. Log (time, value) events and rung sends; slide trial cuts of several lengths across the session and
   assert every rung up to the amount earned in each cut was sent inside it, with one interval of slack.
3. Every rung must be sent once by a build before Studio Lab lists it
   ([[studio-lab-lists-an-action-only-after-a-build-sent-it]]); a harness run that sends each rung once
   through the real action path makes the high rungs selectable.
