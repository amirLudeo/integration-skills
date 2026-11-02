---
category: common-mistakes
tier: generalizable
sourceGame: multiple
phase: "4,5"
question: "Before trusting any capture or restore gate: can it actually FAIL? Name the defect it exists to catch and describe what a failing run would look like. Check the subject is not degenerate, 'not applicable' is not counted as pass, the getter you verify through has no fallback, the probe does not mutate the precondition, and the field you discriminate on has a runtime writer."
sanitized: true
---

# A restore gate must be able to fail

A green verdict is worth nothing until you can say what a failing run would have looked like. Five
disguises of the same hole; an agent that had read the first one ninety minutes earlier still wrote it
twice, because each looks like a different question.

## 1. A degenerate subject or a pre-satisfied world

A harness that took "the first unclaimed objective" landed on one with **one** part: every progress
assertion failed on correct behaviour (the implementation deliberately hides one fewer than the total),
and "1 of 1 parts on its recorded place" passed while unable to tell right order from "all moved to one
spot". A sibling gate ran on a clip already inside a boss fight, where the real restore had done the
work, and passed testing nothing. Rank candidates by element count, encode the implementation's
deliberate clamps in one shared expectation helper, print `N of M checkable`, assert the starting
precondition, and when no non-degenerate subject exists, say so as an unproven half. Same for guards
and holds: a popup that arms after N attempts cannot fire on a fresh profile (log refusal counts and
assert > 0); "nothing progressed under the hold" needs work in flight; rate proofs need a
low-progression profile.

## 2. "Not applicable" returned as pass

A visual check returned `null` both for "correct" and for "nothing to compare", reporting
`3 destroyed (3 visibly changed), 0 complaints`. Split, the same run said
`0 VISIBLY changed, 3 unchecked [unchecked-keepIfNull ×3]` and exposed a real bug: the restore re-applied
a presence flag **after** the game's destroy method, so smashed props came back intact. Count correct,
wrong and could-not-check separately, print each subject's branch, and fail when
`confirmed == 0 && subjects > 0`.

## 3. A getter's fallback verifies as real state

`TargetPosition => target != null ? target.position : Vector3.zero` captures `(0,0,0)` with no target;
the setter no-ops, and a verify through the same getter reports a match. Look for a fallback branch in
every captured getter; capture validity too (`HasTarget`), treat "precondition absent" as not
checkable, and recognise the baked-in fallback in old Ludeos as absent.

## 4. The probe destroys the precondition

To prove a replay answers from the clip, a gate locked content then asserted "locked" with the game's
`IsContentHidden(id)`, which calls `IsContentUnlocked(id, unlockIfEligible: true)` and re-unlocks it.
Assert preconditions with the pure read underneath; smells are flags like `unlockIfEligible`,
`autoCreate`, `ensure`, and lazily-creating getters. Bonus: comparing profile size around the mutating
call proves the override returns before the write path runs.

## 5. A discriminator field with no runtime writer

A census of `enemyType == Boss` returned 0 of 723 and "refuted" coverage; the boss was in the pool all
along. The field's only assignment was its initialiser and nothing read it; the health bar ran off a
different authored flag. Grep writers (`\.field\s*=`) and readers first; discriminate on the data that
drives the feature (the encounter's named prefab), and print the old answer beside the new one.

Related: [[verify-the-world-not-the-flag-you-just-wrote]], [[ask-what-your-check-cannot-see]],
[[a-guard-that-cannot-fire-is-not-evidence]].
