# The Trail: working notes for coding agents

Unity **6000.0.85f1**, built-in render pipeline, gamma colour space. The game is a retro
green-phosphor wagon-trail game. Read `README.md` first.

## Where things are

- `Assets/Scripts/Core` has **no Unity dependency** (`noEngineReferences`). It holds the whole game:
  rules (`Sim.cs`), route and landmark text (`Trail.cs`), save state (`GameState.cs`, plain fields so
  JsonUtility and System.Text.Json both work), the 384x240 palette framebuffer (`Canvas.cs`, shades
  0-5), pixel art drawn in code (`Art.cs`), the square-wave beeper (`Synth.cs`), the scene stack and
  dialogs (`App.cs`, `Ui.cs`), and every screen in `Screens/`.
- `Assets/Scripts/Game/Runner.cs` is the only Unity code at runtime: framebuffer to texture, the CRT
  shader (`Assets/Resources/Crt.shader`), input, audio (`OnAudioFilterRead` on the listener).
- `Canvas` clashes with `UnityEngine.Canvas`: Unity-side files use `using Canvas = TheTrail.Core.Canvas;`.
- No physics module: don't use `GameObject.CreatePrimitive`.

## Verify every change

```bash
dotnet test Tools/CoreTests                              # .NET 6+; same tests as Unity's EditMode run
dotnet run --project Tools/Preview -- /tmp/screens       # PNG of every screen; look at the ones you touched
dotnet run --project Tools/TrailSim -c Release -- 2000   # rerun after changing any number in Sim.cs
```

The tests include a monkey test (thousands of random taps and keys on several seeds); keep it
passing. `Tools/Preview/Program.cs` plays through the game with keys and `Choose("label")`; extend it
when you add a screen.

In the real Editor (the Pipeline package is a local dev tool, **not** in the committed manifest):

```bash
unity pipeline install --project-path .     # once per clone
unity open . --until-ready
Tools/unity-recompile.sh
unity command --project-path . test run editor --timeout 600 --result-only
Tools/unity-capture.sh out.png 4 'var app = TheTrail.Game.Runner.Instance.App; /* set up a screen */ return 0;'
```

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`; don't commit `Packages/packages-lock.json`.
- 0 compile errors, all tests passing, Play mode with 0 Console errors.
- Commit `.meta` files with their assets.

## Content rules

- Everything must stay original: no names, text, art, music or scoring tables from any commercial
  game. Real history (places, dates, peoples) is fine. Speakers in `Trail.cs` are fictional or
  described by role, not real people.
- Write about Native peoples accurately and respectfully: traders, guides, fishermen, neighbours.
- Text is 8x8 pixels: 48 columns across the screen. Landmark blurbs wrap at 42 columns and must fit
  in 4 lines (a test checks this).

## iPhone builds

App Store Connect app "The OG Trail", bundle id `com.rushingtech.theogtrail`. `Tools/ios-export.sh`
exports the Xcode project from the running Editor and uploads it as the `ios-latest` release asset;
then Codemagic's `thetrail-testflight` workflow (train app, branch `codemagic/thetrail` of
mrnickrushing/train, which has the App Store Connect key) signs it and uploads to TestFlight. See
README. Only send a TestFlight build when the user asks.
