# Argonauts '49

An original Gold Rush survival game for iPhone and iPad (landscape). Spring 1849: you raise a
company of argonauts in St. Joseph, Missouri, outfit a wagon, take it about 2,000 miles over the
California Trail and work a claim in the diggings until the winter rains. Your result is the
company's take: the gold dust, the cash left, and everyone you brought through.

The rules, numbers, writing, pictures and code are all original (see `docs/DESIGN.md`). The places,
dates and peoples along the road are real history; the company members and everyone you meet are
fictional. The tune is Stephen Foster's *Oh! Susanna* (1848, public domain). See `CREDITS.md`.

The earlier game in this repo, "The OG Trail", followed The Oregon Trail (1985) too closely to
publish. It is kept unchanged on the branch `og-original` and is never submitted.

## What's in the game

| | |
|---|---|
| **The company** | Pick 4 partners from a hiring board of 8 strangers: a trade (blacksmith, physician, hunter, guide, cook, teamster, miner, greenhorn), a temper and money for the pot |
| **Outfitting** | St. Joseph's store with weights and a load limit set by the team: oxen or mules, food, water kegs, repair kits, medicine, powder, pans, picks and rockers |
| **The road** | A day at a time, with orders in plain numbers (hours on the road, pounds of food a day); grass thinned by the 1849 traffic, dry stretches drunk from the kegs, illness, morale and 26 decision events |
| **Two forks** | At South Pass the Greenwood Cutoff or Great Salt Lake City; at Lassen Meadows the Carson route to Hangtown or Lassen's cutoff to the northern diggings |
| **Crossings** | Scout three fords (depth, current, bottom) or wait in line for a ferry, then hold to keep the wagon on the shallow bar |
| **Ropes** | Let the wagon down Windlass Hill and haul it up the Sierra on chains: keep the strain in the green |
| **Stalking** | Creep while the animal grazes, keep still when it looks up, fire when close |
| **The diggings** | Claims get poorer the later you arrive; work week by week, panning in a timed swirl game; river bars flood when the rains come; the season ends December 20 |
| **Result** | Gold at $16 an ounce, plus cash, plus $500 for each member brought through alive; the Assay Ledger keeps the best ten |

## How it's built

- `Assets/Scripts/Core`: the whole game as plain C# with no Unity dependency: rules (`Sim.cs`,
  `Sim.Places.cs`), the route (`Route.cs`), goods and the hiring board (`Content.cs`), events
  (`Events.cs`), the save (`GameState.cs`), a 480x270 framebuffer over a 54-colour palette
  (`Canvas.cs`, `Palette.cs`), pixel art drawn in code (`Art*.cs`), a plucked-string synth
  (`Synth.cs`), the scene stack and widgets (`App.cs`, `Ui.cs`), every screen (`Screens/`) and an
  auto-player (`Bot.cs`) for the tests and the balance tool.
- `Assets/Scripts/Game/Runner.cs`: the Unity shell. It shows the framebuffer through
  `Assets/Resources/Pixels.shader` (sharp pixels at any size), shows the Rushing Technologies logo
  at launch (`Resources/UI/Brand/rushingtech.png`, `Logo.shader`), turns touch, mouse and keys into
  game input, and plays the synth. The scene is empty; the runner builds everything.
- `Assets/Editor/ProjectSetup.cs`: player settings (landscape, iOS 15+, bundle id
  `com.rushingtech.theogtrail`, kept from the earlier app record), the app icon (drawn by
  `Art.Icon`), the iOS build.

## Developing

Unity 6 (6000.0.85f1). Open the folder and press Play (the setup step runs once on first open).

```bash
dotnet test Tools/CoreTests                                 # 18 tests, no Unity needed
dotnet run --project Tools/Preview -- /tmp/screens          # every screen as PNG, no Unity needed
dotnet run --project Tools/TrailSim -c Release -- 2000      # balance: the bot's journeys by start and team
```

With the local Pipeline package (see `CLAUDE.md`) the running Editor can be driven from the
terminal: `Tools/unity-recompile.sh`, `Tools/unity-test.sh`, `Tools/unity-capture.sh out.png`.

## Building for iPhone (TestFlight)

App Store Connect app id 6830442340 (still named "The OG Trail" there until the listing is redone),
bundle id `com.rushingtech.theogtrail`.

1. `Tools/ios-export.sh` exports the Xcode project from the running Editor and uploads it as the
   public `ios-latest` release asset of this repo.
2. In Codemagic, start the **`thetrail-testflight`** workflow of the **train** app on the
   `codemagic/thetrail` branch of `mrnickrushing/train`. It runs there because that app's
   `ios_credentials` group holds the team's App Store Connect API key. It downloads this repo's
   export, archives, signs and uploads to TestFlight.

`codemagic.yaml` here is the same workflow, ready for the `thetrail` Codemagic app
(id 6aea9cab1c40377fbbf334bb) once an `ios_credentials` group with the API key is added to it.
