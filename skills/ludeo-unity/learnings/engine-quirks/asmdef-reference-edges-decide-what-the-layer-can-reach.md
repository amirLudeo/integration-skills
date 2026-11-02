---
category: engine-quirks
tier: generalizable
sourceGame: SurvivalSample
phase: "1,3,5"
question: "Does the game keep its own code in assembly definitions (.asmdef) rather than Assembly-CSharp, possibly split across several? Then 'auto-referenced' will not make LudeoSDK visible to it, and the assembly you place the Ludeo layer in decides which game types it can EVER reference."
sanitized: true
---

# Asmdef reference edges decide what the layer can reach, in both directions

An `.asmdef` references exactly what its `references` array lists. That breaks the SDK's visibility
outbound, and caps which game types the layer can see inbound.

## Outbound: "auto-referenced" does not reach asmdef game code

Both SDK asmdefs ship `"autoReferenced": true`, which only means Unity's **predefined** assemblies
(`Assembly-CSharp`, `Assembly-CSharp-Editor`) reference them. Game code in its own asmdef gets:

```
error CS0246: The type or namespace name 'LudeoSDK' could not be found
```

even though `Library/ScriptAssemblies/LudeoSDK.dll` exists and the assembly is loaded in the domain.
Add the edge to every consuming assembly - one array entry each, required, not optional:

```json
{ "name": "Game.Core",   "references": [ "LudeoSDK", ... ] }
{ "name": "Game.Editor", "references": [ "LudeoSDK", "LudeoSDKUnityEditor", ... ] }
```

Prove "SDK references resolve" with a throwaway `using LudeoSDK;` file **inside the game's runtime
asmdef folder**; the built DLL or reflecting over `AppDomain` proves nothing about reference edges. An
agent editor bridge that compiles snippets has its own reference set, so its `CS0246` is not evidence
about the project.

## Inbound: the layer's assembly decides which half of the game it can see

With `Game.Gameplay` referencing `Game.Core`, a layer placed in `Core` compiled its whole capture side
for weeks, then the restore needed a presentation component in the upper assembly:

```
error CS0246: The type or namespace name 'MapMarker' could not be found
```

Adding `Gameplay` to `Core`'s references is a cycle Unity refuses: the type is unreachable, not merely
unreferenced. It surfaces late because capture reads broad lower-assembly types while restore needs
specific upper-assembly components. Folder names often mislead; check where each assembly's sources are.

1. **Map the assemblies before placing the layer**, and place it where it can see what the *restore*
   will need - usually the assembly at the top of the graph that nothing else references.
2. When already stuck, cheapest first: hold it through a shared base type in your assembly (match
   `GetType().Name` fail-safe, with a comment); ask for a one-line accessor in the upper assembly; move
   the layer.
3. **Never call a virtual through the base type without reading the other overrides.** The base's
   `OnInteract` was cosmetic on the marker and load-bearing on five siblings (finishing objectives,
   dropping loot, starting map-wide effects).
