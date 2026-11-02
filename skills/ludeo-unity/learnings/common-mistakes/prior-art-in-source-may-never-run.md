---
category: common-mistakes
tier: generalizable
sourceGame: SurvivalSample
phase: "1,2,4,5"
question: "Are you planning capture, restore or test gates around prior art found in source - a record/replay or autotest framework, a late-join / host-migration apply path, gameplay classes implementing the save interface, the game's save call? Before it reaches the plan, prove it runs in the mode you ship: a concrete subclass, live callers, a live writer, registration, and an availability gate that allows the moment you target."
sanitized: true
---

# Prior art in source may never run

An implementation being present, correct and complete says nothing about whether it runs. Recorded
as validated, it produces a plan that reads well-supported while resting on nothing. Check the call
graph first; keep what is dead as a reading list.

## An inherited test framework with no live implementation

A complete input record/replay framework (start/stop recording and replay, a fixed global seed,
an action library, console commands, a CI entry point) could not run: the manager was `abstract`,
`Instance` was assigned in a subclass's `Awake`, and **no concrete subclass existed**. No recorded data
was on disk and the one gameplay call into it was commented out; it came from a sibling title. Check
for a concrete subclass by type, where `Instance` is set and whether a shipped scene creates it, and
whether call sites sit behind a define ([[verify-the-define-fence-before-citing-a-hook]]). Cost a
revival in tiers - does it run, does it **capture** (nothing constructed actions while recording), what
determinism it gives (goal-based bot commands at 5x timescale) - and check conflicts: its forced global
seed overrode the world generator's seed the capture records.

## Late-join code: read it twice, never call it

Netcode that brings a joiner up to a running match solves the Ludeo problem. Its state packet (one
method with names like `CollectGameState`, `BuildStatePacket`, 24 categories in one case) is the best
phase-4 run inventory; its apply method is the best phase-5 write-order reference. But in one game the
loader's only call site was commented out, a single-player loader had zero callers, the writer was
commented out, and the only live caller was the out-of-scope late-join handler. Grep call sites of the
entry point and read every hit (commented calls still match), check the writer as well as the reader,
and ask which mode the live caller belongs to. Mirror the order; write to the owning primitives and
verify each value, since you are its first caller in this mode.

## A save interface implementation is not proof the save writes it

Every actor implemented the save interface, but the run-level `Serialize(SaveData)` body was commented
out (runs were deliberately not resumable), and the save iterated a set filled by `AddSerializable(this)`
that only six non-actor classes ever joined. Read the top-level `Serialize` body, follow the
registration path and count, then classify the group. Per-entity `Serialize` bodies stay good field-list
prior art.

## A strong save may refuse exactly the moment you want

A full-state, named-field save had an availability gate: `CanSave => savingEnabled && !inActiveFight &&
!inDialogue && !inCutscene && … && Time.timeSinceLevelLoad > 3.0f`. The clip concept was mid-fight, the
one moment it refuses. The gate blocks *initiating* a save, not representing the state: port the data
model and its restore order, never use the save call as the capture mechanism. Record the gate's
exclusions next to the group (grep `CanSave`, `IsSaveAllowed`, `DisableSaveForDuration`).

Related: [[find-the-studio-s-own-repro-tooling-first]] - a repro tool that sets a seed and changes
scene has no save gate, which made it the right model for the replay boot.
