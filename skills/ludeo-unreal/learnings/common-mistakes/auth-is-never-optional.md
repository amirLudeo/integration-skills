---
category: common-mistakes
tier: universal
sourceGame: FPSGameStarterKit
phase: 3
question: null
sanitized: true
---

# Auth is never optional — the question is implicit vs explicit, not whether auth exists

## The Mistake

Agent wrote "FTPS_Online is not a Steam game — leave AuthenticationDetails default (None)" and skipped the entire auth resolution block from Section 5.3. Activation failed with `InvalidParameters (null apiKey)` and the agent rationalized it as "expected — no API key configured yet" instead of diagnosing the missing auth.

## Why This Is Wrong

Every Ludeo session requires authentication. Two paths exist:

- **Implicit:** Steam is initialized. SDK reads the Steam user automatically.
- **Explicit:** SteamAuthID provided via CLI/env/config. Required when Steam is NOT initialized.

"This game doesn't use Steam" is not a reason to skip auth. While debugging, it's a reason to use explicit auth — the Section 5.3 resolution chain (CLI → env → config) exists for that. Explicit auth is removed before shipping (phase 07 gate); shipped builds authenticate implicitly through Steam, and local creation for a non-Steam title isn't supported yet, so raise that with the Ludeo integrations team rather than shipping a Steam ID.

## The Second Mistake

When activation failed, the agent said "expected" and marked Stage 2 complete. An `InvalidParameters` error is NEVER expected. It means a required field (ApiKey, GameVersion, or AuthenticationDetails) is null. The correct response is to diagnose which field is missing, not to wave it off.

## Prevention

- Section 5.3 auth block is NOT optional. Copy it verbatim for every integration.
- If activation returns any error, investigate — don't rationalize.
- Read `missing-explicit-auth.md` and `steam-user-must-be-in-environment.md` during Stage 2.
- Section 7.10 runtime verification checklist explicitly checks for activation success.
