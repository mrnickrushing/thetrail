# The Trail

A wagon-trail survival game for iPhone and iPad in the style of the 1980s school computer-lab
classics: a green phosphor monitor, chunky 8x8 lettering, square-wave beeps, and a party of five
trying to get from Independence, Missouri to Oregon City in 1848.

Everything here is original: the rules, the numbers, the writing, the pictures and the code. The
route and landmarks are real history (Chimney Rock, Fort Laramie, South Pass, the Barlow Road),
and the title tune is Stephen Foster's *Oh! Susanna* (1848, public domain). The font is the
public-domain `font8x8` set.

## What's in the game

| | |
|---|---|
| **Outfitting** | Choose banker, carpenter or farmer (more money, or a higher score multiplier), name your party, pick a month, and shop at Pruett's Outfitting: oxen by the yoke, food, clothing, bullets and spare wheels, axles and tongues |
| **The trail** | 20 real stops over about 1,900 miles, with two forks (the Sublette Cutoff or Fort Bridger; rafting the Columbia or the Barlow Road toll road) |
| **Each day** | Weather by month and altitude, food by ration size, wear from the pace, illness (cholera, typhoid, dysentery, measles, fever, exhaustion), injuries, and about 20 kinds of trouble and luck |
| **Stops** | Size up the situation: check supplies, the map, pace and rations, rest, trade with other travelers, talk to people (three lines at every landmark), buy supplies at forts (prices climb westward), hunt |
| **Rivers** | Width and depth that rise with spring runoff and rain; ford, caulk and float, take the ferry, or wait |
| **Hunting** | Tap to shoot buffalo, elk, deer, antelope, bear, rabbits and squirrels; trees and rocks stop bullets; you carry back 100 lb per able hunter (two at most) |
| **The Columbia** | A rafting run between rocks; touch and hold to steer |
| **Endings** | Points for survivors, health and what's left in the wagon, multiplied by your way of life, and a Top Ten list. If the whole party dies, you write the leader's epitaph, and later journeys pass that grave at the same mile |
| **Options** | Green, amber or white phosphor; CRT glow on or off; fast travel; sound |

## How it's built

- `Assets/Scripts/Core`: the whole game as plain C# with no Unity dependency. The rules
  (`Sim.cs`, `Trail.cs`, `GameState.cs`), a 384x240 framebuffer of six phosphor shades
  (`Canvas.cs`), all the pixel art drawn in code (`Art.cs`), the beeper (`Synth.cs`), and every
  screen (`Screens/`).
- `Assets/Scripts/Game/Runner.cs`: the Unity shell. It shows the framebuffer through
  `Assets/Resources/Crt.shader` (sharp pixels at any size, scanlines, glow, curvature), turns touch,
  mouse and keys into game input, and plays the beeper. The scene is empty; `Boot` builds everything.
- `Assets/Editor/ProjectSetup.cs`: player settings (landscape, iOS 15+, bundle id
  `com.rushingtech.thetrail`), the app icon (drawn from the game's own pixels), the iOS build.

## Developing

Unity 6 (6000.0.85f1). Open the folder, press Play (the setup step runs once on first open).

```bash
dotnet test Tools/CoreTests                                 # 16 tests, no Unity needed
dotnet run --project Tools/Preview -- /tmp/screens          # every screen as PNG, no Unity needed
dotnet run --project Tools/TrailSim -c Release -- 2000      # balance bot: survival by strategy
```

With the local Pipeline package (see `CLAUDE.md`) the running Editor can be driven from the
terminal: `Tools/unity-recompile.sh`, `Tools/unity-capture.sh out.png`.

## Building for iPhone

1. Create the app in App Store Connect with bundle id `com.rushingtech.thetrail`.
2. Add this repository as an app in Codemagic with the `ios_credentials` variable group (an App
   Store Connect API key with the Admin role; plus `GITHUB_TOKEN` while the repo is private).
3. `Tools/ios-export.sh` exports the Xcode project from the running Editor and uploads it as the
   `ios-latest` release asset; then start the `ios-testflight` workflow.
