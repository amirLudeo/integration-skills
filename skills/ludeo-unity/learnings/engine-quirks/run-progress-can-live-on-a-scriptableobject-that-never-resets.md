---
category: engine-quirks
tier: generalizable
sourceGame: SurvivalSample
phase: "4,5"
question: "Are you about to capture run progress by reading it off authored data - a ScriptableObject config, or a [Serializable] class inside one, that the game hands out by reference? Check whether the game writes runtime progress BACK into those objects. If it does, the values survive between runs in one process and a sweep of the asset reports the previous run's progress, not this one's."
sanitized: true
---

# Run progress can live on a ScriptableObject that never resets

The census assumption is that authored data is static and run state lives on managers and actors. One
game handed out a `ScriptableObject`'s array of objective definitions **by reference** (a plain
`=> _field` property), and its manager wrote each objective's live progress straight back onto them:
parts remaining, total, finished, the accepted choice.

Those members were auto-properties without `[SerializeField]`, so they look transient. They are not,
within a process: the asset stays loaded, so the values persist across a run ending, a scene change and
a fresh map.

## Why the game gets away with it and capture does not

The game reads progress through a **per-run registry on the manager** (a `Dictionary<string,
Definition>` of objectives started this run, rebuilt with the scene); anything absent is treated as
never having happened, so leaked flags are invisible in play. Capture has no such shield: sweep the
asset and you record objectives from whatever run came before.

**Capture through the per-run registry**, adding the accessor if there is none:

```csharp
public IEnumerable<Definition> LudeoStartedObjectives => _registry.Values;
```

## Consequences for the restore

- **Registering the objective on the manager is the load-bearing half**, not setting the numbers. The
  HUD chose between "2/8" and a countdown purely on whether a quest was active, which only the game's
  accept path sets; restoring counters without it read as a wrong value but was a wrong display.
- **Register finished items as completed, without replaying them.** The "all objectives complete" test
  walks the registry, so an absent finished objective counts as never happened and end-of-map progress
  comes out short.

## Same root: arrays narrowed in place

The same asset's choice-pool array was narrowed in place before being offered, so an index into it is
only as stable as the last narrowing. Capture the item's own stable id (here a localisation key, the
same lookup the game's completion handler uses) and keep the index only as a fallback, warning loudly
when the fallback resolved.
