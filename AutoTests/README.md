# Automatic play tests (editor only, not part of the game)

Kept outside `Assets/`, so Unity does not compile them and nothing runs by itself.
To use them, copy back:
- `__LevelTest.cs`, `__Probe.cs`, `__WalkDriver.cs`, `__SystemsTest.cs` -> `Assets/Scripts/__Tests/`
- `__ProbeDriver.cs` -> `Assets/Editor/`

Set the jobs in `__ProbeDriver.Jobs` (e.g. `LevelTestN`, `SystemsPhoneN`, `SystemsPCN`, `Probe3a`, `Probe3b`), give the
SessionState keys and the `AllDone` file a new number, open GameScene with the Simulator tab visible, focus Unity and
don't touch it until `Temp/__ProbeJobs<N>_done.txt` exists. Results: `Temp/<job>/levels.txt` or `systems.txt` and
screenshots. Remove the copies from `Assets/` afterwards (with an empty job list and the done file missing, the driver
would still switch the play view).
