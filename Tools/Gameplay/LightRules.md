# Coastal Temple light puzzle runtime

Namespace: `CoastalTemple.LightPuzzles`. Components are independent of the tutorial and water assemblies.

## Scene setup

* `LaserEmitter`: place its transform (or `Origin`) at the lens, with local +Z along the emitted beam. Set `IgnoreRoot` to the complete lantern/console model so its own housing cannot block it. Set `Powered` as the lantern is carried, switched, or docked. `BeamMaterial` can be assigned an existing URP unlit/emissive material. Tracing uses real physics occluders and bounded reusable line segments; crossing a portal never draws a line across the gap.
* `RedStoneCurtain`: assign a `BoxCollider` to `PhysicalCollider`. The optical box remains detectable after the physical collider is disabled. `Mode = Permanent` needs a continuous hit for `ContinuousHitSeconds` (default 0.35 s), then stays open until `ResetState()`. `WhileIlluminated` opens immediately and returns after `ReturnGraceSeconds` (default 0.15 s). An open temporary curtain transmits the beam onward while continuing to receive illumination itself. Assign only the disappearing geometry to `Visuals`.
* `OccupantMask` on curtains should contain only player/occupant layers. The default zero mask identifies CharacterControllers, Rigidbodies, or colliders with the Player tag. When light is lost while an occupant is inside the curtain box, `RestorePending` is true and the curtain remains invisible and non-solid until the box is clear. Explicit reset also obeys this occupancy protection.
* `LightPortal`: local +Z points toward the arriving ray. Set `Paired`, local `Aperture` width/height, and `AllowEntry`. A Y-axis 180-degree transform maps the hit position and ray direction into its partner. For two-way portals, pair both sides and allow entry on both. For one-way entry, leave the exit's `AllowEntry` false. Openings need no solid collider; frame colliders may surround the aperture. A solid collider directly on the portal GameObject is treated as its entry surface within the aperture.
* `LightReceiver`: assign `OpticalCollider` or use `OpticalSize`/`OpticalCenter`. Set `RequiredHitSeconds`, `ReturnGraceSeconds`, and optional `Latching`. `IsActive` drives mechanisms. The default receiver absorbs light; set `TransparentToBeam` for onward transmission. All targets expose `IsIlluminated`, `Progress`, `Activated`, and `Deactivated`.

## Update and test API

`LightPuzzleWorld` registers active components and runs at most once per game frame in LateUpdate. It clears illumination, traces every powered emitter, and only then resolves each target. Disabling one emitter therefore cannot erase another emitter's hit. Existing component registrations and bounded buffers are reused; normal tracing does not search the scene or allocate arrays each frame.

`LaserEmitter.TraceNow()` recomputes all registered emitters without advancing charge or grace time. `LightPuzzleWorld.Step(deltaTime)` is the deterministic integration-test entry point. `ResetAll()` resets every target; a lantern's placement/power is controlled separately by the tutorial. Beam inspection uses `SegmentCount`, `GetSegment(index)`, `PortalHopCount`, and `TraceLimited`. Default range is 45 m, default portal budget six; hop budget clamps to 16 and the trace is bounded to 32 segments. Portal offsets count against remaining range; the physical gap between the pair does not.

Run pure activation tests with `Tools/Gameplay/LightStateTests/Run.ps1` (requires .NET 9). They cover continuous charge, permanent persistence, immediate temporary opening, grace, reacquisition, explicit reset, and zero-time traces. Their missing-feature baseline failed all seven assertions before implementation; all seven pass against the runtime core.

Run the 15 Unity integration tests from `Coastal Temple > Tests > Light Rules`, or call `CoastalTemple.LightPuzzles.Editor.LightRuleTests.Run()`. It returns JSON and writes `Documentation/CoastalTemple/LightRuleTests.json`. Fixtures are placed far from the scene and destroyed after each check. It covers physics obstruction, multiple emitters, loss, permanent reset, invisible curtain sustain/transmission, occupied return, rotated portal mapping with separated segments, aperture/backside rejection, total range, hop limits, receiver latching, callback-driven disabling, overlapping transparent sensors, and different physical/optical curtain geometry. Running it in Edit Mode avoids advancing any active tutorial state.

## Validation scope

The agent that authored these components verified seven pure state tests and compiled runtime/editor test sources against installed Unity 6000.5.9f1 assemblies with zero errors or warnings. Unity physics execution and scene integration are performed by the coordinating task; check the JSON report for the current integration result.
