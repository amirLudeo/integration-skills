---
category: common-mistakes
tier: generalizable
sourceGame: IdleSample
phase: "1,5,7,8"
question: "Was the Ludeo integration branch cut from the trunk while the shipped game is built from a separate release/content branch - and does any restore module rebuild a game save object (new XSaveData { ... }) that a merge or update could have changed? Compare the integration branch's base with the shipped branch's head, and diff the save DTOs after any merge."
sanitized: true
---

# Check the branch base against the shipped game, and re-audit rebuilt save DTOs after merging

Confirm the integration branch contains the game that actually ships; when you merge the real game in,
every rebuilt save object silently defaults the fields it gained.

## The integration branch may hold a pre-release game

The branch was cut from the trunk weeks before release while the studio shipped from a separate
content/release branch (balancing, new content, the release candidate, hotfixes). Every Ludeo build,
cloud uploads included, was a pre-release game; it surfaced only when a "bug" was already fixed on the
release branch.

- **Ask the integrator which branch is the shipped game**; do not assume the trunk.
- **Check at phase 1 and before every upload:** compare the integration base with the head of the
  branch the store build comes from (`git merge-base`, or your VCS's equivalent).
- **If they diverged and the integrator wants the real game,** merge the release head in and treat it
  as a game update: re-scope every wave against the diff, re-run every gate, re-check every one-line
  hook the merge touched, and expect the game to refuse or archive saves from old builds.

## Rebuilt save DTOs default every field the capture never wrote

Restoring through the game's own `LoadFromSaveData(new XSaveData { … })` is the right pattern, but a
field added to `XSaveData` later is restored at its C# default, and the round trip cannot see it: never
captured, never verified, drift 0. One merge brought dozens of new save fields across several modules.
After any game update:

1. **Diff** every save DTO class and load method the modules touch between the old and new revision.
2. **Classify each new field:** capture (persistent, or shapes the moment), skip (short-lived), or
   derived (the load recomputes it - then make sure your fresh DTO does not zero something the game
   set on scene load that a later re-assert would wipe).
3. **Re-check** reset paths the restore relies on, mechanics that changed (a counter that became
   derived), and fixture ids the update deleted. Compare per-module written counts before and after;
   a big drop is a stale fixture, not a pass.
4. **Re-measure the attribute-name count**; updates push it up.
