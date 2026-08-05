# Agent context — jgz/SE-NavOS-2

Written 2026-08-04. Read this before touching anything here.

Jon's main notes repo is `jgz/space-engineers-notes` (private). Its `handbook/sdx2/flip-and-burn.md`
holds the mod-side research this fork is built on. **Its prime directive applies here too: never
guess. Every claim is either sourced (file + line, command output, URL), labelled as inference, or
stated as "I couldn't verify this."**

## Why this fork exists

NavOS 2 (StarCpt) is the flight-control script on Sigma Draconis Expanse 2. That server runs the
**Flip and Burn** mod (workshop `2832248541`), which moves grids far above the world speed limit.

It cannot do that with physics. Server-side it clamps `Grid.Physics.LinearVelocity` to
`LargeShipMaxSpeed` and repositions the grid with `Grid.Teleport()` every tick
(`FlipAndBurn/SpeedController.cs`, `UpdatePosition`). So `GetShipVelocities()` returns **1,000 m/s
while the ship does tens of km/s**, and everything NavOS derives from velocity is wrong by that
factor.

The visible failure: `RetroCruiseControl`'s decel trigger is **quadratic in speed** —
`stopDist = v·flipTime + v·0.5·(v/accel)`. At a true 20 km/s read as 1,000 m/s the stopping distance
is ~400× short, so Cruise sails past the target and never enters `Decelerate`.

**Position stays truthful** — the teleport destination is a real world matrix. Differencing it
recovers true velocity with no mod cooperation. That is the whole idea.

## What's in the fix

Two commits, 12 files, +128/−7:

| Piece | Where |
|---|---|
| Per-tick position differencing | `TrueVelocity.cs` (new) |
| Selection between physics/tracked | `Utils.GetTrueVelocity()` |
| `MaxSpeedOverride` config option | `Config.cs` (declaration **+ TryParse + ToString**) |
| Sampler call, once per tick | `Program.Main` |
| 7 velocity read sites | `Navigation/*.cs` |

Design points worth not re-litigating:

- **Samples `Me.CubeGrid.WorldMatrix.Translation`, not `WorldAABB.Center`.** The AABB centre shifts
  as the grid rotates, injecting fake velocity during exactly the manoeuvre that matters — the flip.
- **Discards samples implying >2,000 m/s².** Filters grid jumps (Nexus instance transfers) without
  detecting them explicitly; the following tick is continuous and is accepted.
- **Prefers tracked only above `physics × 1.05 + 5`.** Below the clamp the two agree and physics
  wins, so unmodded worlds never take the new path. This is what makes it safe to upstream.
- **`MaxSpeedOverride` defaults to 0** = use the world limit = stock behaviour.

## Branches

| Branch | Purpose |
|---|---|
| `master` | The fix + `README.md` for third parties. Same code as the PR. |
| `flip-and-burn-velocity-fix` | Head of upstream PR #14. **Do not add anything to this** — it shows in the maintainer's diff. |
| `flip-and-burn-true-velocity` | Jon's local flying build. Adds a `-- Velocity Source --` readout to the PB detail pane and trims `Instructions.readme` for character headroom. |
| `cruise-thrust-flag` | Parked experiment: per-command `thrust=<0..1>` flag for Cruise. Not in the PR. |

Upstream is `StarCpt/SE-NavOS-2`, default branch `master`, at `e805718` when this fork was taken.
NavOS version is **2.16**.

## Building — the parts that cost time to discover

- **.NET SDK 9.0+, and it must be the SDK, not the Runtime.** Having runtimes installed is not
  enough; MDK reports "Required .NET SDK is missing". Check with `dotnet --list-sdks` — empty output
  means none installed.
- **Windows only.** MDK2's packager is Windows executables (`checkdotnet.exe`, `mdk.exe`). The C#
  compiles fine under WSL and then dies at the packaging step. Don't burn time trying.
- No Visual Studio needed. `dotnet build "NavOS 2.16.csproj" -c Release` does everything; MDK pulls
  SE's reference assemblies from NuGet (`Mal.Mdk2.References`).
- Output lands at `%AppData%\SpaceEngineers\IngameScripts\local\NavOS 2.16\Script.cs`.

**Character budget.** The PB limit is 100,000. This script is ~99.7k with the full readme and no
diagnostic. MDK prepends `Instructions.readme` as a comment header, costing ~4.8k. Levers, cheapest
first: trim that readme (what `flip-and-burn-true-velocity` does), then `minify=lite` → `full` in
`NavOS 2.16.mdk.ini`. `full` renames identifiers, which breaks the verification trick below.

## Testing — two traps

**1. Single-player will lie to you.** Flip and Burn writes the *full* virtual velocity into physics
on the **client** ("trick speedometer", `SpeedController.cs`, `UpdateAfterSimulation`) while the
**server** clamps. PBs run server-side in multiplayer. A creative world looks correct with or
without this fix. **Test on the live dedicated server or the test is meaningless.**

**2. Verifying which build is in a PB.** The header still says "NavOS v2.16 brought to you by
StarCpt" either way. Pull `<Program>` out of a blueprint and count markers:

| | stock | this fork |
|---|---|---|
| `GetShipVelocities` | 7 | 1 |
| `TrueVelocity` | 0 | 11 |

Also: on SDX2's `LargeProgrammableBlockReskin`, **config is stored in a mod-storage dictionary, not
`<CustomData>`**. The blueprint's `<CustomData>` element reads empty even when config is set. This
was misread once as "he never recompiled".

## What was actually tested

Confirmed in flight, live server, large-grid Rockhopper: speed above the world limit; PB reporting
true speed and true distance-to-target; retroburn triggering at the correct range; arrival accuracy
matching pre-SDX-2 NavOS.

**Not tested:** small grids, gravity wells, `Journey` multi-point runs, `SpeedMatch` against a
WeaponCore target, Nexus instance transfer mid-cruise (the 2,000 m/s² gate is meant to cover it but
was never exercised deliberately).

## Mistakes made here — don't repeat them

- **`MaxSpeedOverride` was declared but never wired.** `Config.TryParse` and `Config.ToString` are
  hand-written key-by-key. Adding a property to the class does nothing on its own — it must be added
  to **both** methods. Shipped broken once and cost a whole build/fly cycle.
- **First version sampled `WorldAABB.Center`.** See above. Caught by reasoning, not by testing.
- Two builds went out with bugs that reading the diff carefully would have caught. Instrument and
  measure (that's what the `-- Velocity Source --` block is for) rather than guess-and-rebuild.

## Open threads

- **Upstream PR: `StarCpt/SE-NavOS-2#14`.** If merged, this fork should be retired and the README's
  status note points people upstream.
- Unrelated, same maintainer: `StarCpt/PluginHub#153` bumps the GPS Folders pin to v1.6.4. Worth
  knowing if he responds to one and not the other.
- `cruise-thrust-flag` awaits Jon's verdict on whether the flag syntax feels right before extending
  it to `Journey` and `Autopilot`.
- Jon has said he may extend NavOS for his own use and considers `Autopilot` dead weight (it's a
  6-axis precision-parking mode, never rotates the ship, 5 cm / 1 cm/s termination — useless for
  transit on Expanse hulls where nearly all thrust is aft). **Any such stripping belongs on a
  separate branch, never on `master` or the PR branch.**
