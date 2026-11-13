# Argonauts '49: working notes for coding agents

Unity **6000.0.85f1**, built-in render pipeline, gamma colour space. An original 1849 Gold Rush wagon
and diggings game in full-colour pixel art. Read `README.md` and `docs/DESIGN.md` first. The old
"The OG Trail" lives only on the branch `og-original`: never merge it back or submit it.

## Where things are

- `Assets/Scripts/Core` has **no Unity dependency** (`noEngineReferences`). It holds the whole game:
  rules (`Sim.cs` on the road, `Sim.Places.cs` for stops, crossings, hauls and the diggings), the
  route (`Route.cs`: stops, legs, forks, blurbs), goods, prices and the hiring board (`Content.cs`),
  decision events (`Events.cs`), the save (`GameState.cs`, plain fields so JsonUtility and
  System.Text.Json both work), the 480x270 framebuffer (`Canvas.cs`) over the palette
  (`Palette.cs`, `Pal.Rgb`), pixel art drawn in code (`Art.cs`, `Art.Figures.cs`, `Art.Scenes.cs`;
  `Art.Icon` is the app icon), the plucked-string synth (`Synth.cs`), the scene stack and widgets
  (`App.cs`, `Ui.cs`), every screen in `Screens/`, and `Bot.cs`, which plays whole journeys.
- Minigames: `Screens/Crossing.cs` (fords, `FordScene`), `Hunt.cs` (`StalkScene`), `Haul.cs`
  (`HaulScene`), `Diggings.cs` (claims, the weekly menu, `PanScene`, the payout and the ledger).
  Each one ends by calling one `Sim` method (`Cross`, `HuntResult`, `Hauled`, `WorkWeek`), which the
  bot also calls.
- `Assets/Scripts/Game/Runner.cs` is the only Unity code at runtime: framebuffer to texture through
  `Assets/Resources/Pixels.shader`, the launch logo (`Resources/UI/Brand/rushingtech.png`,
  `Logo.shader`; every Rushing Technologies game shows it), input, audio (`OnAudioFilterRead`).
- `Canvas` clashes with `UnityEngine.Canvas`: Unity-side files use `using Canvas = TheTrail.Core.Canvas;`.
- No physics module: don't use `GameObject.CreatePrimitive`.

## Verify every change

```bash
dotnet test Tools/CoreTests                              # .NET 6+ (DOTNET_ROLL_FORWARD=Major on newer SDKs)
dotnet run --project Tools/Preview -- /tmp/screens       # PNG of every screen; look at the ones you touched
dotnet run --project Tools/TrailSim -c Release -- 2000   # rerun after changing any number in Sim*.cs
```

The tests include bot journeys (most companies arrive, a late start is harder) and a monkey test
(random taps and keys from several start states); keep them passing. `Tools/Preview/Program.cs`
walks through every screen; extend it when you add one. Balance as of this version: a May 1 ox
company played fairly arrives about 80% of the time in about 150 days; mules about 93%; June 1
about 42%.

In the real Editor (the Pipeline package is a local dev tool, **not** in the committed manifest):

```bash
unity pipeline install --project-path .     # once per clone
unity open . --until-ready
Tools/unity-recompile.sh
Tools/unity-test.sh
Tools/unity-capture.sh out.png 4 'var app = TheTrail.Game.Runner.Instance.App; /* set up a screen */ return 0;'
```

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`; don't commit `Packages/packages-lock.json`.
- 0 compile errors, all tests passing, Play mode with 0 Console errors.
- Commit `.meta` files with their assets.

## Content rules

- Everything must stay original: no names, terms, screens, scoring or minigames from any commercial
  game (in particular nothing from The Oregon Trail). Real history (places, dates, peoples, period
  songs) is fine. People you meet are fictional or described by role, not real people.
- Write about Native peoples accurately and respectfully: traders, guides, fishermen, neighbours.
- Lettering is the font8x8 glyphs; check every new text in `Tools/Preview` at 480x270 (nothing may
  run off a panel or into a picture).

## iPhone builds

App Store Connect app id 6830442340 (still named "The OG Trail" until the listing is redone), bundle
id `com.rushingtech.theogtrail`. `Tools/ios-export.sh` exports the Xcode project from the running
Editor and uploads it as the `ios-latest` release asset; then Codemagic's `thetrail-testflight`
workflow (train app, branch `codemagic/thetrail` of mrnickrushing/train, which has the App Store
Connect key) signs it and uploads to TestFlight. Only send a TestFlight build when the user asks,
and don't submit Argonauts '49 for review until the user has approved a TestFlight build.
