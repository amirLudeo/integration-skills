---
category: common-mistakes
tier: generalizable
sourceGame: multiple
phase: "1,2,3,4,5,6,7,8"
question: "Has a step failed with no clear error - no overlay, no room, no capture, no SDK log lines at all, Activate returning InvalidAuth, a hang - above all before the first capture has ever worked end to end? Then check the setup chain below, reading each value rather than inferring it, before opening any code."
sanitized: true
---

# Check the setup chain before debugging code

Seeing the overlay depends on settings spread across several systems, and one missed link fails
silently or with a symptom that points somewhere else. On a large integration much early-phase time
went to one missed setup step debugged as a bug in fresh code.

| Link | Symptom when it is wrong |
|---|---|
| API key, backend environment and game version in the SDK settings asset | activation or session errors, or data landing under the wrong game or version |
| Run-without-launcher, for any run the launcher did not start | no overlay; `userToken returned is null!`, then activate failing, while the log still says the overlay is enabled |
| Studio Lab environment matching `betaVersion` | `Activate: InvalidAuth` (see below) |
| The integration's own "start the SDK in play mode" toggle | nothing at all: the game plays normally, no SDK lines (see below) |
| Studio Lab configuration for the game and environment in use | a room that never opens although the code path runs (one case: an entitlement present in one backend environment, missing in the other) |
| The CLI's active login | uploads and build listings for another game ([[ludeo-cli-set-token-ignores-config]]) |
| Scripting defines left by a cloud-build configuration | the Editor boots as a cloud build and hangs on the loading screen |

## An off-by-default play-mode toggle leaves no fingerprint

Integrations often add an Editor switch for starting the SDK in play mode, off by default because the
native overlay hooks the OS cursor and the Editor does not reliably release it. With it off the game
plays normally and the log simply lacks SDK lines. One session lost six automated runs, each mistaken
for a different bug (a harness timeout, a "hung" menu, a slow shader compile). It usually lives in an
`EditorPref`, is reset by whoever tidied up, and does not survive sessions. Read and log it as step one
of every run, assert it rather than set-and-forget, and prefer the project's own launch helper over a
bare `EditorApplication.EnterPlaymode()`.

## `InvalidAuth` with "Environment not found" is the beta code

On a local run (`runWithoutLauncher = true`, `launcherUserId`, `betaVersion` set), the SDK's `Http:`
lines a few lines above the summary hold the answer:

```
Http:Log   POST …/api/v3/users/auth, data={"hashAuthSourceUserId":"<masked>","betaCode":"<betaVersion>",…}
Http:Error Reply Code=404, Body={"statusCode":404,"message":"Environment not found"}
Core:Error ludeo_Session_Activate failed with LudeoResult::InvalidAuth
```

A 401 that follows is a consequence. Read from these lines (inferred, not confirmed by Ludeo), no
environment for the `apiKey`'s game has a beta-branch name equal to `betaVersion` - match it exactly,
case included, or create the environment. Mask the user id and key when quoting these lines.

## The habit

1. Keep this list in the integration notes from phase 1 and **run it first** when a step fails without
   a clear error; say which links you checked, and how, before proposing a code cause.
2. Read each value through the Editor or the SDK API; several are binary-serialized or `EditorPrefs`.
3. Fold the checks into whatever starts a test run, refusing a run that cannot work.
4. Treat "no SDK lines at all" as setup first: if activation never appears, nothing downstream can.
