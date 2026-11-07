# Automatic play tests (editor only, not part of the game)

Kept outside `Assets/`, so Unity does not compile them and nothing runs by itself.
To use them, copy back:
- `__LevelTest.cs`, `__Probe.cs`, `__WalkDriver.cs`, `__SystemsTest.cs`, `__NewFeaturesTest.cs` -> `Assets/Scripts/__Tests/`
  (`__NewFeaturesTest.cs` is the second half of the `__SystemsTest` class: copy both or neither; without it the
  `NewFeatures` jobs only log a SKIP)
- `__ProbeDriver.cs` -> `Assets/Editor/`

Set the jobs in `__ProbeDriver.Jobs`, give the SessionState keys (`__ProbeDriver.jobN`, `__ProbeDriver.jobphaseN`) and
the `AllDone` file (`Temp/__ProbeJobsN_done.txt`) a new number, open GameScene with the Simulator tab visible, focus
Unity and don't touch it until `Temp/__ProbeJobs<N>_done.txt` exists. Results: `Temp/<job>/levels.txt` or `systems.txt`
and screenshots. Remove the copies from `Assets/` afterwards (with an empty job list and the done file missing, the
driver would still switch the play view).

## Jobs

A job name ending in `PC` (a run number after it is fine: `SystemsPC13`) plays in the Game view at 1920 x 1080, one
ending in `4K` at 3840 x 2160, all others in the phone Simulator.

| Job | Test | What it plays |
| --- | --- | --- |
| `SystemsPhoneN`, `SystemsPCN` | `__SystemsTest` | every system: admin safety, windows, campfire, axes and planks, torch at night, snare, storage box, fenced yard + foundation + wall, seeds and forests, water and food, hunting, spear, save and load (about 4-5 min) |
| `NewFeaturesPhoneN`, `NewFeaturesPCN` | `__SystemsTest` + `__NewFeaturesTest` | the changes of 2026-11-07 (checks N0-N6 below, about 3-4 min) |
| `LevelTestN` | `__LevelTest` | the 10 levels from a new game, `levels.txt` (about 6-8 min) |
| `Probe...`, `Walk`, `Profile`, `Colliders` | `__Probe` / the driver | older one-off looks (see the driver) |
| `VideoN_4K`, `VideoTryN_4K` | `__VideoDirector` | the presentation video (below) |

The run set now (`__ProbeDriver.Jobs`, number 13): `SystemsPhone13`, `SystemsPC13`, `NewFeaturesPhone13`,
`NewFeaturesPC13`, `LevelTest13`.

All the tests play the way a player does: the crafting cards' Craft buttons, items used from the inventory (the
phone's double tap), the crosshair on things and the primary press (`GameInput.PressTouch`, with
`GameInput.TestDriven` on PC), the joystick through `__WalkDriver`. Materials are given instead of walking far. Each
check writes a `PASS` / `FAIL` / `SKIP` line with what was seen; the end of `systems.txt` has the summary and the
console errors.

### The shy rabbits (since 2026-11-07)

Rabbits run from a player closer than `GameSettings.RabbitShyRadius` (Easy 4 m, Normal 7 m, Hard 10 m; the difficulty
is PlayerPrefs `SurvivalBD.Difficulty`, Normal by default) and are faster on Normal / Hard. The tests do what a player
does, nothing turns the shyness off:
- hunting (systems 9, new N6.5): the player walks up from farther away and runs after the rabbit (12 m/s, a rabbit at
  most 8.8), swings when it is in reach with the crosshair on it (`Chase` in `__SystemsTest`)
- the spear (systems 10): thrown from at least 12 m (the shy radius + 2.5 m)
- snares (systems 4, level 9): the player waits `2 x radius + 4..6` m away, on the far side from the rabbits, then
  takes the caught rabbit alive with empty hands (a caught rabbit does not run). Level 9 sets two snares and waits
  until they caught a male and a female (up to 50 s with the clock x20); what they did not catch is taken from the
  wild by code, as before (logged "given from the wild")
- systems 12 also checks that a load gives back exactly what was saved (12.6)

### The new features (`NewFeatures` jobs)

- **N0** the admin unlocked and Immortal for the run (locked again at the end); the season, snow and weather noted
- **N5 branches**: `FallenBranches.Instance` exists; branches shown near a forest (`ShownCount > 0`); one taken with the
  crosshair + press gives a stick, `TakenCount` +1 and it is not shown again; a 7-11 m leafy tree felled with the
  stone axe leaves at least 3 "Branch" pickables, one of them taken
- **N1 winter**: winter + full snow (`TimeManager.SetSeason`, `WeatherSystem.SetTestWeather(Snow)`, `SetSnowCover(1)`);
  grass picked on a meadow (looking down + press) gives hay; a pickable plant is drawn and taken under the snow; a
  campfire crafted, placed and lit with that hay (a log in its fuel slot, "Light Fire"); the weather put back
- **N6 shy rabbits**: for Easy, Normal and Hard a grown free rabbit let onto open grass (`RabbitPopulation.Spawn`), the
  player walks slowly towards it: it must run (`RabbitLife.ShyRuns`) when the player comes within the radius (not
  more than 0.6 m earlier), at `RabbitShySpeed` (+-20 %) for about `RabbitShyRun` metres; 6 rabbits hopping around on
  Easy and on Normal: faster on Normal (their speeds in the Easy / Normal ranges); on Hard a rabbit killed with the
  stone axe by chasing it; a rabbit in a closed fenced yard (built the player's way) stays calm with the player next
  to it. The difficulty is put back afterwards (also after a timeout and at the end)
- **N2 load without extra axes**: saved to slot 47 with 1 stone + 1 hay and an empty quick bar, 2 iron ore added after
  saving, loaded the Load Game way: exactly 1 stone + 1 hay, no "Axe" in the inventory, quick bar or hand, 32 slots
- **N4 loading screen** (during that load): `LoadingScreen.Instance` shows, has no Text named "Prayer" /
  "Prayer title" / "Band", has the "Loading..." text, and is gone within 5 s after the save was put back
- **N3 backpack**: the "Backpack" card in Crafting > Survival, greyed out without materials, ready with 3 "Rabbit Hide"
  + 2 "stick"; crafted: 32 -> 48 slots, `HasBackpack`, the materials used, the card says "You have it" / "Îl ai
  deja" and can't craft again; the inventory window has 12 columns x 4 rows with every slot inside the window and on
  the screen (RectTransform corners), next to the crafting window without overlapping (screenshots); saved with 37
  things (5 in slots 32-47) and loaded: the backpack, 48 slots and all 37 things are back, the window laid out again

The test save (slot 47: `Assets/SaveGame47.json` / `.jpg`, PlayerPrefs `Slot47Description`) is deleted afterwards.

## The presentation video

Copy `__VideoDirector.cs` too and use a job `VideoN_4K` (the Game view at 3840 x 2160; `VideoTryN_4K` films only some
shots: see `shots.RemoveAll`). It films the shots (about 2 min, sound included) into `Temp/<job>/SurvivalBD.mp4`
(H.264, 30 fps, AAC stereo) with 3 stills (3840 x 2160) and a frame sample every 5 s, then writes `finished.txt`.
(Its spear shot was written before the rabbits became shy.)

## Checking the scripts without Unity

`EDITOR=1 bash <scratchpad>/compile.sh AutoTests` compiles the game, the editor tools and all these scripts with
Unity's own compiler (no error lines before "exit done" = OK); without `EDITOR=1` it leaves the driver out.
