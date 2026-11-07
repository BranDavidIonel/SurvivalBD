# Automatic play tests (editor only, not part of the game)

Kept outside `Assets/`, so Unity does not compile them and nothing runs by itself.
To use them, copy back:
- `__LevelTest.cs`, `__Probe.cs`, `__WalkDriver.cs`, `__SystemsTest.cs`, `__NewFeaturesTest.cs`, `__RiversTest.cs`
  -> `Assets/Scripts/__Tests/`
  (`__NewFeaturesTest.cs` and `__RiversTest.cs` are more parts of the `__SystemsTest` class: copy them with it or not
  at all; without one of them its jobs, `NewFeatures` or `Rivers`, only log a SKIP)
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
| `RiversPhoneN`, `RiversPCN` | `__SystemsTest` + `__RiversTest` | the island's rivers: the old one and the 5 new ones (checks R0-R10 below, about 3-4 min) |
| `LevelTestN` | `__LevelTest` | the 10 levels from a new game, `levels.txt` (about 6-8 min) |
| `Probe...`, `Walk`, `Profile`, `Colliders` | `__Probe` / the driver | older one-off looks (see the driver) |
| `VideoN_4K`, `VideoTryN_4K` | `__VideoDirector` | the presentation video (below) |

The run set now (`__ProbeDriver.Jobs`, number 15: SessionState keys `__ProbeDriver.job15` / `__ProbeDriver.jobphase15`,
done file `Temp/__ProbeJobs15_done.txt`): `RiversPhone15`, `RiversPC15`, `SystemsPhone15`, `SystemsPC15`,
`NewFeaturesPhone15`, `NewFeaturesPC15`, `LevelTest15`.

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

### The rivers (`Rivers` jobs)

The rivers are the scene's `River` objects: the old one (`River.Instance`, `lakeRadius` > 0, it ends in the crater
lake) and the 5 new ones under "Rivers" (`lakeRadius` 0, a `riverName`: East, North-east, South, North, West river),
all in `River.All`. "In the water" means the test's own distance to the water's edge of every river (each river's
`DistanceToWater`) below 0; the game's own `River.IsWater` / `NearestWater` / `NearestWaterAnywhere` are compared with it.
- **R0** the admin unlocked and Immortal for the run, a clear sky (if the weather was automatic: automatic again at the
  end), 10 o'clock; the player is put back where the run began at the end
- **R1** at least 6 rivers (the old one + 5 new); `River.All` lists every river of the scene and `River.Instance` is the
  old one; each new one has a name, at least 60 points and a water mesh (vertices, a renderer drawn with a material,
  its bounds over the spring, the middle and the mouth); length and width noted
- **R2** each new river springs on a mountain (its first point at least 25 m above the sea, -19.05), its water surface
  (`points[i].y`) never rises along it (0.05 m allowed), it ends at the sea (the last point within 3 m of sea level
  and `Ocean.DistanceFromShore` > -8); a picture of each spring (looking down the river) and each mouth
- **R3** the water lies in its bed, at 20 spots of each river (the old one too, not at its lake): the terrain under the
  middle line lower than the water, the ground 2 m outside both edges not lower than the water (0.15 m allowed; not
  looked at by the sea, the lake, another river or a tight bend); the worst spots are listed
- **R4** no `Forest` tree (`Trees`, `data.position`) within the half width + 1.5 m of any river's middle line (also
  counted with `River.NearestWater` and `River.IsWater(p, 1.5)`); no scene plant (`ScenePlant`, drawn by
  `Vegetation`) in a river's water: pickable ones and the others counted apart
- **R8** the map opened (`MapScreen.Toggle`, the Map button / N), a picture; its texture read (directly, or copied
  through the GPU) and 30 spots along each river must be blue (blue minus the mean of red and green > 0.04, or
  clearly bluer than the land 40 m beside it) at 60 % of them or more (not near the sea or in the lake)
- **R9** the level-2 map goal (`StoryManager.MapGoal` with the story's level set to 2 for that call only) seen from the
  story's beach, 20 m from the old river, 8 m from each new river and 70 m from the West river: it must lie on the
  nearest river of all (the test's own search over the whole middle lines), at its point nearest to the player, and
  agree with `River.NearestWater` (when near) and `River.NearestWaterAnywhere`
- **R5** drinking at 3 places of each new river (15 %, 50 %, 88 % along it): the player on land at the water's edge
  (1.2 m out), the water bar at 40 %, Immortal off, looking down at the water: `FreshWater.At` must say `River`, the
  crosshair "Drink water" / "Bea apă", and the primary press must raise the water bar; a picture at the middle
- **R6** crossing on foot at 2 flat places of each new river (one in 18-50 %, one in 50-86 % of it: land 8 m from the
  water on both sides at most 6 m above it, not steep, no tree, rock or other river on the way): the joystick
  forward, the view turned to the far side every frame, from 8 m on one side to 8 m on the other within 12 s; a stop
  without progress for 3.5 s in the water is reported "STUCK in the river's bed"; the time in the water, the lowest
  ground and swimming noted; a picture in the middle of the first crossing
- **R10** Brother Ioan's paths and steps (`IslandPaths.Data`) where they come within 6 m of a new river's water: the
  player walks along the path with the joystick from about 12 m before to 12 m after (out of the water), at most 8
  walks, the ones through the water first; a picture of the first one (in the data of 2026-11-07: the South river at
  (506, 253), the East river at (707, 326) on two paths, and a path 3.3 m beside the South river)
- **R7** rabbits keep out: by a flat place of each new river 4 grown test rabbits (`RabbitPopulation.Spawn`, no mating)
  1.8 m from the water, 2 of them between the water and the player (5.5 m from it, so they run from the player
  towards the river), the clock x20 for 4 s at each river (20 s): every living rabbit within 60 m looked at 4 times
  a second must stay out of the water; then no living rabbit of the island may stand in a river's water. The test
  rabbits are removed afterwards

## The presentation video

Copy `__VideoDirector.cs` too and use a job `VideoN_4K` (the Game view at 3840 x 2160; `VideoTryN_4K` films only some
shots: see `shots.RemoveAll`). It films the shots (about 2 min, sound included) into `Temp/<job>/SurvivalBD.mp4`
(H.264, 30 fps, AAC stereo) with 3 stills (3840 x 2160) and a frame sample every 5 s, then writes `finished.txt`.
(Its spear shot was written before the rabbits became shy.)

## Checking the scripts without Unity

`EDITOR=1 bash <scratchpad>/compile.sh AutoTests` compiles the game, the editor tools and all these scripts with
Unity's own compiler (no error lines before "exit done" = OK); without `EDITOR=1` it leaves the driver out.
