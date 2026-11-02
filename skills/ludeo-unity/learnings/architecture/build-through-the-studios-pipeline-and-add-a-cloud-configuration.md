---
category: architecture
tier: generalizable
sourceGame: multiple
phase: "1,2,3,4,7"
question: "Does the studio build players through its own pipeline (build window, build-configuration assets, Unity 6 Build Profiles, CI entry points), and are you about to build with Unity's Build Settings dialog or a bare BuildPipeline.BuildPlayer, change settings in place for the cloud, or trust EditorBuildSettings and the Editor's defines as the build's scenes and defines?"
sanitized: true
---

# Build through the studio's pipeline, and make the cloud build one more configuration in it

The studio's pipeline does work Unity's standard build does not, and owns the real scene lists and
defines. Build through it, read the target configuration rather than the Editor, and add the cloud
build as a new configuration instead of editing the one players buy.

## A player built outside the pipeline launches, then breaks

One player built through Unity's own dialog failed three ways that pointed nowhere near the build:

| Symptom | Actual cause |
|---|---|
| Closes right after the splash | the pipeline copies the store's app-id file beside the exe; without it the store client relaunches the game and it quits |
| `Invalid path in TextDataProvider .../aa/settings.json`, `No Location found for Key=...` | the pipeline builds Addressables as a separate step; `BuildPipeline.BuildPlayer` never does, and does not fail |
| Loading never finishes | the baked development flag disagreed with the dialog's Development Build box, so a plugin asked for a library variant that did not ship |

Triage the output folder before reading code: app-id file present, `StreamingAssets/aa/` holds
`settings.json` and bundles, a known key appears in the built catalog. See
[[buildscriptsonly-is-not-scripts-only-with-addressables]].

## The Editor does not describe the build

- **Scenes:** a per-release build tool kept its own scene list per release type (by GUID);
  `EditorBuildSettings` was only the Play Mode list ([[check-what-actually-ships-before-scoping-per-entity-work]]).
- **Defines:** the Editor's Standalone defines were a snapshot of the tool's last build, for another
  release type. Resolve every `#if` against the target configuration
  ([[verify-the-define-fence-before-citing-a-hook]]).
- **Unity 6 Build Profiles** carry `m_ScriptingDefines` used only when that profile is active. A store
  wrapper asmdef with `"defineConstraints": ["<STORE_DEFINE>"]` defined only in a profile was never
  compiled by any headless gate.

```
grep -n -A6 "m_ScriptingDefines" "Assets/Settings/Build Profiles/"*.asset
grep -rn "defineConstraints" --include=*.asmdef Assets/
```

## Cloud-ready is one more configuration

The cloud build and the consumer build want opposite things (no splash, store client or cheats vs all
three). One studio already had about twenty per-platform `ScriptableObject` configurations owning
defines, `BuildOptions`, scenes, store selection and content flags; cloud-ready became one more asset,
created by a re-runnable editor method. Store-service field `None` cleared every store define; a cloud
define went in "additional directives"; the content mask was copied from release. A Build Profile
without the store define is the equivalent seam (`-activeBuildProfile "<cloud profile>.asset"`).

- **Assert at build time:** an `IPreprocessBuildWithReport` reads the baked values and throws
  `BuildFailedException`, but **only when the cloud define is being baked**, so it can never block the
  studio's builds (which would get it deleted).
- **Flip shared SDK settings for the build only:** set the cloud values, build, restore them in a
  `finally`, so a failed build leaves the local replay harness intact.

Related: [[store-platform-removal-is-layered-not-a-single-fix]].
