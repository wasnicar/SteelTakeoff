# Steel Coating Takeoff → Sage Estimating

A native Windows desktop tool for a painting contractor's estimator. It converts a
linear-foot steel takeoff (as taken off in eTakeoff) into **coating surface area
(square feet)** using AISC contour SF/LF factors, flags each member **Intumescent
Yes/No**, and pushes the area into the matching **Sage Estimating assembly** via the
Sage Estimating SDK.

```
 eTakeoff  ──LF──►  Steel Coating Takeoff  ──area (SF)──►  Sage Estimating
                    (this tool)                            ├─ Intumescent assembly  (YES)
   pick shape + size, enter LF, Intumescent Y/N            └─ Standard steel assembly (NO)
```

The intended workflow: take off quantities in eTakeoff → open this tool → enter total
LF, pick the steel member and size, set Intumescent YES/NO → the tool calculates the
coating area → **Send to Sage** routes that area to the correct assembly, whose own
formulas explode it into the estimate's items.

---

## What's in the box

```
SteelCoatingTakeoff.sln
├─ src/
│  ├─ SteelCoatingTakeoff.Core/        netstandard2.0 — no external dependencies
│  │   ├─ Resources/shapes.dat         1,416 AISC shapes with published SF/LF (embedded)
│  │   ├─ Model/…                      ShapeFamily, SteelShape, TakeoffLine, CoatingType
│  │   ├─ ShapeDatabase.cs             loads + labels the shape data
│  │   ├─ TakeoffCalculator.cs         SF/LF, area = LF × SF/LF, plate width formula
│  │   └─ Sage/…                       ISageConnector seam, request/result models,
│  │                                   TakeoffRequestBuilder (routing), MockSageConnector
│  └─ SteelCoatingTakeoff.App/         WPF, .NET Framework 4.8 (the SDK's runtime)
│      ├─ MainWindow.xaml              the UI (takeoff grid, totals, Sage panel, log)
│      ├─ ViewModels/…                 MainViewModel, TakeoffRowViewModel
│      └─ Sage/SageEstimatingConnector.cs   ◄── the ONLY file that touches the SDK
└─ tests/
   └─ SteelCoatingTakeoff.CoreTests/   22 assertions over data, math, routing
```

**Design rule:** every bit of domain logic lives in `Core` and is unit-tested and
portable. All version-specific Sage SDK code is isolated in **one file**
(`SageEstimatingConnector.cs`). You can build and run the whole app *before* wiring
the SDK — it uses a built-in simulator (`MockSageConnector`) so you can exercise the
UI, the SF/LF math, and the assembly routing with nothing installed.

---

## The numbers

Coating area uses the **CONTOUR (full wrap) perimeter** — the standard convention for
paint and applied intumescent coatings (boxed/rectangular perimeter is *not* used):

```
coating area (SF) = linear feet × SF/LF
```

SF/LF factors are the AISC **Design Guide 19** published shape perimeter ÷ 12, from
the source workbook you provided (AISC Shapes Database v15.0):

| Family | SF/LF basis |
|---|---|
| W / M / S, Channel (C/MC), Tee (WT), Angle (L) | AISC published contour perimeter ÷ 12 |
| HSS rectangular / square | 2 × (H + B) ÷ 12 |
| HSS round, Pipe (Std/XS/XXS) | π × OD ÷ 12 |
| MT / ST tees | calculated (AISC does not publish), within ~1% of WT |
| Plate | 2 × (width + thickness) ÷ 12 — width entered per line |

---

## Build & run (simulator mode — no Sage needed)

1. Open `SteelCoatingTakeoff.sln` in Visual Studio 2022 (.NET desktop workload).
2. Set **SteelCoatingTakeoff.App** as the startup project and press **F5**.
3. The app opens with **Dry run** checked. Add members, enter LF, tick Intumescent as
   needed, and click **Send ALL to Sage** — the Activity pane shows exactly what
   *would* be taken off (assembly + variables), writing nothing.

`appsettings.json` (next to the .exe) stores your server, database, assembly names,
and the area variable — edit in the app and click **Save settings**.

---

## Wiring the SDK (go live against Sage)

**This is wired and verified** against Sage Estimating SDK **25.2**
(`Sage.Estimating.Sdk` 25.2.0.0). The build turns the live connector on by itself:
if the SDK is present at `SageSdkDir` the `SAGE_SDK` constant is defined and the real
connector compiles; if it is absent the project still builds and the app runs on the
built-in simulator. There is nothing to uncomment.

To point the build at a different SDK location:

```
msbuild /p:SageSdkDir="D:\path\to\Sage.Estimating.Sdk.<version>\Binaries\"
```

At runtime the SDK folder is found via the `SAGE_SDK_DIR` environment variable, else the
path baked in at build time. The SDK assembly drags in ~48 sibling DLLs that must load
from that folder, so `App.xaml.cs` installs an `AssemblyResolve` hook rather than copying
them next to the exe.

**x64 is required.** The SDK ships x64-only; an AnyCPU/32-bit build dies at load with a
`BadImageFormatException`. The project pins `<PlatformTarget>x64</PlatformTarget>`.

**The SDK version must match the estimates database.** The 25.2 SDK only opens a
`25.01.00.*` database and refuses a 26.x one outright. If **Test connection** reports a
version error, you are pointed at the wrong SQL instance — this machine has both
`SAGE_EST25` and `SAGE_EST26`, holding databases of the matching vintage.

### How the area reaches the assembly — the important part

An SF-unit assembly whose **Calculation is empty** takes the coating area as its
**takeoff quantity**, *not* as a variable named `SF`. Verified against a live estimate:
`3000.310.01` at quantity 988.01 wrote all five of its `UseFactor × 1` items at
988.01 sf, each keeping its own phase.

Leave **Area variable** blank for that (the default). Set it only for an assembly that
computes its own quantity from a formula/table and takes the area through a named
variable — e.g. `1100.150.051` (`T(Area SF Calc Options)`) needs
`Area variable = Area SF` plus `Area SF Calculation Type=4` in **ExtraVariables**, and
still needs its product/coat variables before it yields anything but zeros.

If an assembly produces only zero quantities, the connector **skips it and says so**
rather than writing an empty assembly into the estimate.

### Labor: wage + productivity → labor price

Two typed inputs on the takeoff screen (right panel) price labor for **every** line:

```
Labor Rate (LR, $/SF) = Wage Rate ($/hr) ÷ Productivity (SF/hr)

standard line     : labor price/SF = LR
intumescent line  : labor price/SF = (WFT ÷ divisor) × LR
```

**Coats** multiply the coating **area** (total SF sent to Sage), not the labor rate:
`area = geometric area × coats`. Total labor still follows the area, but the per-SF rate
is coats-independent.

**Each steel member is its own assembly takeoff**, and the tool stamps the steel
type/size into that estimate assembly's **Description** (e.g. `W12 × 26 — Intumescent
Coating`), so every member is identifiable in Sage. Verified in a live estimate.

The connector writes that price into the item's **labor UnitPrice**, so Sage shows labor
**Amount = takeoff quantity × UnitPrice**. Coating **area is the same** for both types —
thickness only changes the intumescent multiplier.

Verified against a live estimate: W12×26 @ 240 LF, 20 mils, 1 coat, $50/hr ÷ 100 SF/hr →
LR $0.50/SF, intumescent factor 4, **$2.00/SF**, `Generic Insulation Coat 1` labor
**$1,976.02**; the other four items carried no intumescent labor.

- **Wage rate** / **Productivity** — right panel; global, typed. Changing either re-prices
  every line live.
- **WFT divisor** (the 5), **Default WFT**, **Default coats** — right panel.
- Per-line grid columns (editable only when **Intumescent** is ticked): **WFT (mils)**,
  **Coats**.
- Which generated item carries the labor: `IntumescentLaborItemMatch` (default "Insulation")
  and `StandardLaborItemMatch` (default blank = every item), in **Connection settings**.

If Wage Rate or Productivity is unset, no labor is written and the **Show calculation**
breakdown says why.

### Two windows

- **Takeoff screen** (main): the member grid (Shape, Size, Plate W, Length, **Coats**,
  Intumescent, WFT, SF/LF, Area, Labor $), totals, the **labor inputs** (Wage rate,
  Productivity) and defaults, and the Send buttons.
- **Connection settings** (opened from the right panel): SQL Server, databases, estimate
  name, assemblies, labor-item matches, area/LF variables, Dry run, and Test connection.
  It shares the takeoff screen's view-model, so Save and Test connection are the same
  actions.

**Coats** is a per-line grid input for every line (intumescent or not); it multiplies the
coating area (total SF), never the labor rate.

### Choosing assemblies

In Connection settings the two assembly fields are **dropdowns** populated by **Load from
Sage** (reads the standard database, groups excluded). Each also has a **Browse…** button
that opens a picker showing the assemblies in their **grouped hierarchy** exactly as Sage
displays them (group headers → their members), with a filter box. Reads are safe, so
loading works regardless of Dry run; without the SDK it falls back to a small sample list.

### Show calculation

The **Show calculation** checkbox under the grid opens a derivation panel beneath the
selected line: where the SF/LF factor came from, the LF × SF/LF multiply, the rounding,
the assembly it routes to, how the area reaches it, and — for intumescent — the labor
productivity, $/SF and total. `TakeoffExplainer` lives in Core and calls
`TakeoffCalculator` for every number it prints, so the explanation cannot drift from the
arithmetic it describes.

### What the connector receives

`TakeoffRequestBuilder` hands the connector fully-resolved requests so it never has to
know about shapes or factors:

| Field | Meaning |
|---|---|
| `AssemblyId` | already routed — Intumescent assembly if YES, standard steel assembly if NO |
| `AreaSquareFeet` | the coating area to take off (LF × SF/LF), rounded to 2 dp |
| `Variables` | extra takeoff variables by name; any the assembly doesn't expose are reported in the Activity log, never dropped silently |
| `LinearFeet`, `AiscKey`, `Description` | audit / logging |

### Settings you must match to your Sage database

- **SQL Server** — instance holding the estimates DB, e.g. `WILLS-LAPTOP\SAGE_EST25`.
- **Estimating database** — where the estimate lives (e.g. `Estimates`).
- **Standard database** — where the **assemblies** live (e.g.
  `DS0056_v23_Industrial_Coatings_04`). Both are required: takeoff reads the assembly
  from the standard DB and writes items into the estimate DB.
- **Estimate name** — the exact estimate to take off into.
- **Intumescent assembly** / **Standard steel assembly** — assembly names as they appear
  in the standard database (e.g. `3000.310.01`).
- **Area variable** — blank sends area as the takeoff quantity (see above).
- **Target phase** — *not applied.* Every item carries its own phase from the standard
  database (Surface Prep → `1500.101`, finishes → `1000.390`); reassigning it would
  decouple the item from its standard-database source. The connector logs a notice if
  this is set.

---

## Adjusting the shape data

`Core/Resources/shapes.dat` is a plain pipe-delimited resource:

```
@W
W12X26|12 x 26||4.1167        (key | size | type | SF/LF)
@PLATE
3/16|0.1875                   (thickness | thickness_inches)
```

Add or edit rows and rebuild — no code change needed. Values came directly from the
`Steel_Sizes__Wenrich_with_SA.xlsx` reference so they tie out to your Sage SF/LF
estimating factors.

---

## Tests

`tests/SteelCoatingTakeoff.CoreTests` is a dependency-free console harness
(`dotnet run`) covering the shape database load, SF/LF spot-checks against the
workbook, the plate width formula, area math, assembly routing, and an end-to-end
mock takeoff. 22/22 passing.
```
================  22 passed, 0 failed  ================
```
