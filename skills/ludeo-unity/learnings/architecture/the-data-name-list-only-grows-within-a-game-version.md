---
category: architecture
tier: generalizable
sourceGame: SurvivalSample
phase: "4,5"
question: "Is any attribute NAME built from a value that changes during play (a wave or level index, a learned skill or item id, a pool slot, a live object's name), or are you about to capture a profile-wide id list (unlocks, owned items, completed objectives) as one indexed attribute per entry? Every name persists for the game version; name only from closed sets that ship with the build."
sanitized: true
---

# The data-name list only grows within a game version

The platform keeps one append-only list of attribute **names** per game version. A name sent once stays
for the life of that version, and every unseen name rewrites the whole list. Deleting the writer stops
new names but does not shrink the list; renaming registers a second name. A name's type is fixed by its
first write (see [[attribute-writes-the-sdk-refuses-silently]]).

## What it cost

An integration encoded a wave index, a learned skill id and a pool queue position into names; 94% of
names had been minted by play. Fifteen days in, the list held 18,491 names (1.9 MB) and the platform
stopped accepting updates inside the SDK's timeout. The SDK retried every ~11 s indefinitely (about 170
backend errors an hour from one PC) and those attributes were never recorded. A platform alert caught
it, not the integration. The only escape was a new `gameVersion`; the redesign cut 2,213 real names to
684. The worst family was keyed by a live object's display name and had no ceiling at all.

## The rule: the attribute path is the schema, runtime values go in the value

- A name keyed by a **closed set that ships with the build** (an enum, a fixed slot list, authored
  resource types) registers once and is fine.
- Anything that **varies with play** becomes its own object type with fixed attribute names (one object
  per wave, per learned skill, per pooled enemy) carrying the index or id as a value.

## A profile-wide id list is a bitmask over authored content

`listCount` + `list{0}` suits bounded collections (equipped items, a run's modifiers), not "everything
this account ever unlocked". Measured on a maxed profile: 436 ids held, **817** authored, 162 able to
affect a run; the plan had been sized on a 7-entry sample. Since recorder and replayer run the same
build, capture which authored positions are set, 32 per int: 817 ids became 26 mask words + a count + a
fingerprint = 28 names, whatever a profile holds.

- Take the order from the game's own content walk (the function that resolves an id across content
  arrays); do not invent one.
- Write the count truthfully so "nothing unlocked" differs from "feature never ran".
- A bitmask is an **ordinal** encoding: one new authored entry shifts every later bit. Write a hash over
  the ordered id list beside it; the restore recomputes it and **declines the whole mask on mismatch**.
- Only for authored universes; ids minted by play are an object type.

## Measure it

- Count **real names**, not format strings: one report said "279 attributes" while the backend held
  18,491, because `wave{0}.*` counted once. Record the count at every schema change; warn past a budget
  (the redesign used 1,000). Any family that grows with playtime is the bug.
- A sweep right after capture opens is a floor: families keyed by list position have not grown yet.
  Measure again after a long run.
