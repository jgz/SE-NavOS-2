# NavOS 2 — Flip and Burn velocity fix

A fork of [StarCpt/SE-NavOS-2](https://github.com/StarCpt/SE-NavOS-2) that makes NavOS work on
servers running **Flip and Burn** — Sigma Draconis Expanse 2, in particular.

All credit for NavOS goes to **StarCpt**. This fork is two small changes on top of v2.16.

> **Status:** submitted upstream as
> [StarCpt/SE-NavOS-2#14](https://github.com/StarCpt/SE-NavOS-2/pull/14).
> If that merges, use upstream instead and ignore this fork.

## What it fixes

On servers where a mod pushes grids past the world speed limit, that mod can't do it with physics —
Flip and Burn clamps `Grid.Physics.LinearVelocity` to the world limit and repositions the grid with
`Grid.Teleport()` each tick. `GetShipVelocities()` therefore reports **1,000 m/s while you're doing
tens of km/s**.

Everything NavOS computes from velocity is then wrong by that factor. The visible symptom is
**Cruise flying straight past its target without ever starting the retroburn** — the decel trigger
is quadratic in speed, so a 1,000 m/s reading makes the stopping distance roughly 400× too short.

This fork recovers the true velocity by differencing grid position per tick (position stays truthful
because the teleport destination is a real world matrix), and adds a config option so commanded
speed can exceed the world limit.

**Below the clamp nothing changes.** On unmodded worlds the new code path is never taken.

## Building

You do **not** need Visual Studio. MDK2 pulls Space Engineers' reference assemblies from NuGet, so
the whole toolchain is the .NET SDK.

**Requirements**

- **.NET SDK 9.0 or newer** — the **SDK**, not the Runtime. Having runtimes installed is not enough;
  MDK will tell you "Required .NET SDK is missing" even though `dotnet` works.
  Check with `dotnet --list-sdks` — if it prints nothing, you don't have one.
- **Windows.** MDK2's packager (`checkdotnet.exe`, `mdk.exe`) is Windows-only. The C# compiles fine
  under WSL/Linux but packaging will not run there.
- Space Engineers installed (MDK reads the game's assemblies).

**Build**

```bash
git clone https://github.com/jgz/SE-NavOS-2
cd SE-NavOS-2
dotnet build "NavOS 2.16.csproj" -c Release
```

That's it. MDK writes the packaged single-file script to:

```
%AppData%\SpaceEngineers\IngameScripts\local\NavOS 2.16\Script.cs
```

## Installing

1. In game, open the Programmable Block → **Edit**
2. **Browse Scripts** → **NavOS 2.16** → load it
3. **Recompile**

The script name is unchanged from stock NavOS, so if you already run NavOS it will overwrite it in
the browser list. To check which build is actually in a PB, look at its Custom Data — this fork
writes a `MaxSpeedOverride` entry that stock NavOS does not have.

## Configuration

Config lives in the Programmable Block's **Custom Data**. See `Instructions.readme` for every
option. **You must recompile the PB after changing it** — NavOS only reads Custom Data at compile
time.

This fork adds one option:

```
// Max commanded speed in m/s. 0 = use the world speed limit.
MaxSpeedOverride=0
```

Leave it at `0` and behaviour is identical to stock. Set it above the world limit (e.g. `20000`) on
a Flip and Burn server so Cruise, Autopilot and Journey can command a speed you can actually reach.

## Two things that will waste your time if you don't know them

**Test on a dedicated server, not in single-player.** Flip and Burn writes the *full* virtual
velocity into physics on the **client** to keep the speedometer honest, while the **server** clamps.
Programmable Blocks run server-side in multiplayer. Single-player and creative worlds will therefore
look correct with or without this fix.

**Watch the character count.** The Programmable Block limit is 100,000 characters and this script
sits close to it. MDK prepends `Instructions.readme` into the output as a comment header, which
costs about 4.8k on its own. If you add anything and hit the limit, either trim that readme or
switch `minify=lite` to `minify=full` in `NavOS 2.16.mdk.ini`.

## Branches

| Branch | What it is |
|---|---|
| `master` | The fix. What you want. Same content as the PR. |
| `flip-and-burn-velocity-fix` | The branch the upstream PR is opened from. Identical to `master`. |
| `flip-and-burn-true-velocity` | Working branch — adds a `-- Velocity Source --` readout to the PB detail pane and trims the readme for headroom. Useful for debugging, noisier. |
| `cruise-thrust-flag` | Experimental: a per-command `thrust=<0..1>` flag for Cruise. Not part of the PR. |

## Licence

Unchanged from upstream — see `LICENSE.txt`.
