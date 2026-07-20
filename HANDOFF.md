# Handoff to Claude Code — wiring the Sage Estimating SDK hooks

This brief hands the project from the planning/scaffolding phase to Claude Code
running **on the estimator's Windows machine** (which has Visual Studio, the Sage
Estimating SDK assemblies, and Sage Estimating installed). The scaffold is complete
and the domain logic is tested; the remaining work is to connect one file to the
real SDK.

---

## Current state (what's done)

- Full VS solution, three projects: `Core` (netstandard2.0, tested), `App` (WPF /
  .NET Framework 4.8), `CoreTests` (net48 console, 22/22 passing).
- Coating-area math, the 1,416-shape AISC database, and Intumescent-vs-standard
  **routing** are complete, isolated in `Core`, and unit-tested.
- The app **builds and runs in Dry-run mode today** using `MockSageConnector` — no
  SDK needed to exercise the UI, the SF/LF math, and the routing.
- All SDK-specific code is confined to **one file**:
  `src/SteelCoatingTakeoff.App/Sage/SageEstimatingConnector.cs`, behind the
  `SAGE_SDK` compile constant.

## The one job

Implement the live Sage takeoff in `SageEstimatingConnector.cs` so that clicking
**Send to Sage** performs an assembly takeoff into the open/target estimate, routing
the coating **area (SF)** to the Intumescent assembly (Intumescent = YES) or the
standard steel assembly (NO). Do **not** change the `Core` project or the
`ISageConnector` contract — the whole app talks only to that interface.

---

## Step 0 — build green in Dry-run FIRST (before any SDK work)

The WPF app was authored but not compiled on the original (Linux) box. Before wiring
the SDK, get a clean build and run:

1. Open `SteelCoatingTakeoff.sln`, set **SteelCoatingTakeoff.App** as startup, F5.
2. It should launch with **Dry run** checked. Add a couple of members, enter LF, tick
   Intumescent on one, click **Send ALL to Sage**, and confirm the Activity pane logs
   the simulated takeoffs (assembly + `SF=` variable).
3. Fix any XAML/binding or compile nits here. Run `CoreTests` — expect
   `22 passed, 0 failed`.

Only proceed once Dry-run is green.

## Step 1 — reference the SDK and flip the switch

1. In `src/SteelCoatingTakeoff.App/SteelCoatingTakeoff.App.csproj`, uncomment the
   `<Reference>` block and set each `HintPath` to the actual SDK DLLs in the Sage
   Estimating program folder. **Discover the real assembly names/paths** — don't
   assume; e.g. `dir "C:\Program Files (x86)\Sage\Estimating*" /s /b | findstr /i "SDK.*dll"`.
2. Add `SAGE_SDK` to the App's conditional compilation symbols (uncomment the
   `<DefineConstants>` line, or Project Properties » Build).

## Step 2 — learn the SDK object model, then fill the three regions

The Sage Estimating SDK is partner-gated and version-specific, so **inspect the actual
API** rather than guessing:

- Read the SDK docs/samples shipped with the install (often a CHM/PDF or a `Samples`
  folder next to the DLLs).
- Introspect the referenced assemblies in VS (Object Browser / Go to Definition) to
  find the concrete types for: opening a database/estimate, finding an assembly,
  creating an assembly takeoff, setting takeoff variables, and saving.

Then implement the three TODO regions in `SageEstimatingConnector.cs`:

| Region | Responsibility |
|---|---|
| `Connect` | Open the estimating DB (`Settings.SqlServer` / `Settings.Database`) and the estimate (`Settings.EstimateName`, or attach to the open one if blank). |
| `TakeoffBatch` | For each request: resolve `req.AssemblyId`, create an assembly takeoff, set each `req.Variables` entry (the coating area is already in there under `Settings.AreaVariableName`), optionally set `Settings.TargetPhase`, calculate/add, tally `AssembliesTakenOff`/`ItemsCreated`. |
| `Commit` / `Dispose` | Save the estimate; release SDK objects. |

Each region already contains a commented example of the expected call shape — replace
the placeholder type/method names with the ones you found.

## Step 3 — verify against a TEST estimate (not production)

1. Make a **copy** of a real estimate to take off into. Set the Sage panel fields:
   SQL Server, database, estimate name, the two assembly IDs/names, and the **Area
   variable** name (must match the variable on both assemblies). Save settings.
2. **Test connection** (Dry run off) → expect success.
3. Take off one Intumescent line and one standard line, **Send selected** each, and in
   Sage confirm: items landed under the correct assembly, the quantity equals the
   area shown in the app (LF × SF/LF, 2 dp), and the phase is right.
4. Check re-sending behavior (does it add again vs. update?) and decide whether the UI
   should warn on duplicate sends — flag to the estimator before batch use.

## Config values to have on hand

- SQL Server instance + estimating database name
- A test estimate name
- Exact **assembly ID or name** for the Intumescent assembly and the standard steel
  assembly
- The assembly **takeoff variable** that receives square feet (default assumed: `SF`)

## Guardrails

- Work against a copy/test estimate until verified — takeoff writes to the estimate.
- Keep all SDK types inside `SageEstimatingConnector.cs`. If you find you need a new
  piece of data from the UI, add it to `SageSettings` / `SageTakeoffRequest` in `Core`
  rather than leaking SDK types upward.
- Prefer a single transaction/commit per batch; surface any SDK exception text into
  `SageTakeoffResult.Message` so it shows in the Activity pane.

---

### Kickoff prompt to paste into Claude Code

> This repo is a WPF (.NET Framework 4.8) tool that turns a linear-foot steel takeoff
> into coating area (SF) and takes it off into Sage Estimating assemblies. Read
> `HANDOFF.md` and `README.md`. Do Step 0 first: get a clean Dry-run build of
> `SteelCoatingTakeoff.App` and confirm `CoreTests` passes. Then wire the real Sage
> Estimating SDK by implementing the three TODO regions in
> `src/SteelCoatingTakeoff.App/Sage/SageEstimatingConnector.cs` — inspect the installed
> SDK assemblies/docs for the exact API instead of guessing, add the SDK references and
> the `SAGE_SDK` constant, and don't change the `Core` project or the `ISageConnector`
> contract. Stop after each step and show me the diff before moving on.
