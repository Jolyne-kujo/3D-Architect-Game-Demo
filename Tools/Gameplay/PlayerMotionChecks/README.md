# Player motion verification

Run from the project root:

```powershell
dotnet run --project Tools/Gameplay/PlayerMotionChecks/PlayerMotionChecks.csproj
```

The test project compiles the actual shared walker, player camera, avatar, and editor authoring source against this workspace's Unity 6000.5.9f1 managed assemblies. Its numerical checks invoke the actual pure swim and camera solvers; Unity engine object behavior must still be checked in the editor.

## Red / green evidence

Before implementation, the runner completed and reported `0 passed, 2 failed`: the required bounded swim and camera obstruction solvers were absent. The behavior cases were already in the test source at this point.

After player implementation: **21 passed, 0 failed**. Coverage includes water entry/exit, airborne above-water state, constant surface floating and holding ascent at 30/60/144 Hz, deliberate diving and release-to-surface, no downward teleport, capped upward inertia, zero timestep, immediate camera retraction, gradual camera recovery, very close obstructions, and unobstructed follow distance.

The subsequently requested lift tests first reported **21 passed, 1 failed** because its bounded solver was absent. After implementation: **34 passed, 0 failed**. New coverage verifies lift speed, both-on and neither-on stops, zero timestep, top/bottom landing limits, no teleport from an invalid starting position, negative speed, and 30/60/144 Hz bounds. The runner now also compiles the real light receiver dependencies and lift component.

The camera spherecasts and authored avatar can also be checked in the Unity editor by calling `CoastalTemple.Editor.PlayerAvatarAuthoring.RunComponentChecks()`. This creates and immediately disposes of a temporary isolated hierarchy; it does not save or modify existing scene objects.

## Scene setup

- Preserve the existing `CourtyardWalker.eye`; its yaw and pitch still drive movement and the standalone first-person courtyard.
- Create the explorer with `PlayerAvatarAuthoring.BuildAvatar(player.transform, whiteMaterial, yellowMaterial, darkMaterial)`. Save the resulting hierarchy as a prefab in the scene authoring step.
- Add `CoastalPlayerCamera` to the player. Assign `walker`, `view`, and the returned `avatar`, with `thirdPerson = true`.
- Overview code calls `cameraRig.SetOverview(value)`, then moves the view camera to its authored marker only while overview is enabled. Do not also attach the camera to the old eye when this rig exists.
- Call `cameraRig.SnapToTarget()` after resetting or teleporting the character.
- `SetThirdPerson(false)` is the explicit first-person option. It hides the avatar in first-person play and restores visibility for overview.
- `SimulateMovement(input, running, jumpPressed, swimInput, seconds)` supports real scene integration checks. `swimInput` is −1 for dive, 0 for natural buoyancy, +1 for ascent. Surface feet height is bounded at `surface − 1.4 m`.

Visual geometry is authored in the editor. Runtime scripts only animate joints and renderers.

## Light-driven lift

Assign `CoastalTemple.Mechanisms.LightDrivenLift.platform` and `platformBody` to an authored platform with its own Rigidbody and colliders. `Initialize()` configures it as kinematic, gravity-free, interpolated, with speculative collision detection. With `positionsAreLocal=true`, `bottom` and `top` are coordinates in the platform parent's space; set it false for world coordinates. `speed` defaults to 1.5 m/s.

`receiverRaise` moves up; optional `receiverLower` moves down. Neither active, both active, disabled receivers, zero duration, or nonpositive speed all stop movement. Use nonlatching receivers with zero return grace for immediate light-loss stopping. `SetManualDirection(-1/0/+1)`, `ReturnToBottom()`, and `StopManual()` support authored interaction buttons. A manual direction combined with an opposing light also stops, and a completed manual trip clears itself. `NormalizedHeight` exposes 0–1 travel progress. Optional `counterweight` and `counterweightTravel` animate an authored local transform inversely.

Unity editor callable `CoastalTemple.Mechanisms.LightDrivenLiftChecks.Run()` verifies 11 real geometry/state/component behaviors, including a lower beam that can reach the receiver at the bottom and misses after the platform moves to upper height. It only updates its own receiver state and cleans temporary objects. Play-mode rider carry and the authored route remain integration checks for the scene owner.
