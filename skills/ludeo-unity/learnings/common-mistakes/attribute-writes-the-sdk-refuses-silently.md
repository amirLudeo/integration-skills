---
category: common-mistakes
tier: generalizable
sourceGame: SurvivalSample
phase: "5,6"
question: "Are attribute keys typed separately from the values they carry (a typed key wrapper, a keys constants class), or do any string attributes join values with punctuation (lists, coordinates, decimals as text)? Both writes can be refused with no error at the call site; prove the SDK's Data log category is emitting, then read its errors from a real capture run."
sanitized: true
---

# Attribute writes the SDK refuses silently

Some attribute writes never reach the clip, and neither `WriteData` nor the compiler says so. The only
witness is the SDK's `Data` log category, which is often switched off.

## A widened `int` against a float key

```csharp
Key<float> GridX = new Key<float>("player.gridX");   // keys class
GridX.Write(obj, player.GridPosition.x);             // Vector2Int.x is an int: compiles clean
```

C# widens `int` to `float` silently (likewise `long`→`float`, `int`→`double`, enum→integral). The
platform holds the name as `Int32` from an earlier correct write, and the SDK refuses every write:

```
Data:Error Object 3, attribute 'player.gridX': Expected type Int32 but client specified Float
```

A schema sweep registers the declared type and agrees with the writer; desk review called one "harmless
at these magnitudes" and missed two siblings. Three names hit 1,801 times each in a 55-second run, one
per tick, easy to scroll past. Fix the **key** to the game property's type, never cast the value, and
check sibling keys in the same commit.

```bash
grep -oE "attribute '[^']+': Expected type [A-Za-z0-9]+ but client specified [A-Za-z0-9]+" <log> | sort | uniq -c | sort -rn
```

## Punctuated text values

String values containing commas, semicolons or periods never reached the clip - observed, not
documented by the SDK. A joined `"x,y,z;x,y,z"` was **absent** from the clip while an integer written on
the next line arrived. A long value of letters, digits and underscores round-tripped at 61 characters,
so it is the separators, not the length; the exact offending character was not isolated. Use typed
attributes (a `Vector3` per element plus a count); for a genuine list of names join with one named
separator constant (`_` worked), assert it at write time, and use string attributes sparingly.

## First prove the `Data` category is emitting

Integrations set levels for some categories and never name `Data`; in one, it emitted **zero** lines
all session, so every grep above came back clean on a broken integration. Count what arrived:

```bash
grep -oE "^[0-9]{2}:[0-9]{2}:[0-9]{2}:[0-9]{3}:[A-Za-z]+:[A-Za-z]+" <log> | sed 's/^[0-9:]*//' | sort | uniq -c | sort -rn
```

A category with three lines still shows; **absence means disabled**. Set `Data` explicitly at `Warning`
(`Log` is per attribute per tick). `SetLoggingLevel` also writes one shared managed-logging gate, so the
last call wins it: silence noisy categories first, set diagnostic ones last.

When a written value never arrives, write a trivially safe value beside it and compare, changing one
thing per probe.
