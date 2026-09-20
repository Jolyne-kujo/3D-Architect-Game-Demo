# V8 Play verification

Result: **PASS**

Progress: Actual overview mode retains built-in rider physics

Route passed: True; extra checks passed: True; optional recovery checks passed: True.

Route teleports: 0; post-route teleports: 5.

Route uses real CharacterController movement and normal-frame physics, with no teleport or synthetic jump. E is exercised through TutorialJourney.Interact, including its reach/occlusion/player-active policy; physical keyboard events are not injected. Supplementary pool/recovery/mirage repositioning is permitted only after the route passes and is counted separately. Mirage assertions cover real physics raycasts, actual CharacterController standing on an above-water step, and support removal followed by swimming recovery; each is claimed only by its passing result record. Optional respawn/reentry and upper-lift recall regressions require a separate BeginRecoveryChecks call after route acceptance. Exit Play to restore the authored scene.

- PASS — Start at authored spawn: Input disabled only for deterministic motor intent; no route teleport. timeScale=1
- PASS — 01 approach first light: Reached through CharacterController movement; vertical error=-0.055m.
- PASS — 01 reach sun-lens console: Reached through CharacterController movement; vertical error=-0.018m.
- PASS — 01 E rotates first sun lens: TutorialJourney.Interact accepted; eye-to-control distance=1.119m, allowed radius=2.800m.
- PASS — 01 permanent curtain opens: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — 01 curtain is physically passable: IsOpen=true; physical collider disabled=true.
- PASS — 01 cross opened curtain: Reached through CharacterController movement; vertical error=-0.119m.
- PASS — 01 E collects reusable lantern within reach: TutorialJourney.Interact accepted; eye-to-control distance=2.226m, allowed radius=2.800m.
- PASS — 01 lantern acquired: HasLantern=true; LanternOn=true; pickup used through normal E policy.
- PASS — shore route waypoint 1: Reached through CharacterController movement; vertical error=0.150m.
- PASS — mountain route waypoint 2: Reached through CharacterController movement; vertical error=0.252m.
- PASS — mountain route waypoint 3: Reached through CharacterController movement; vertical error=0.287m.
- PASS — 02 approach borrowed-light passage: Reached through CharacterController movement; vertical error=0.100m.
- PASS — 02 temporary entrance is lit and open: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — 02 enter and reach redistribution console: Reached through CharacterController movement; vertical error=0.042m.
- PASS — 02 E redirects the single beam: TutorialJourney.Interact accepted; eye-to-control distance=1.132m, allowed radius=2.800m.
- PASS — 02 enduring exit opens and temporary entrance returns: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — 02 physical curtain rules differ after redirect: Permanent exit open/collider off; temporary entrance closed/collider on.
- PASS — 02 cross enduring exit: Reached through CharacterController movement; vertical error=0.106m.
- PASS — route toward folded light: Reached through CharacterController movement; vertical error=0.200m.
- PASS — 03 reach lift approach ramp: Reached through CharacterController movement; vertical error=0.056m.
- PASS — 03 ascend authored ramp: Reached through CharacterController movement; vertical error=0.156m.
- PASS — 03 board real lift platform: Reached through CharacterController movement; vertical error=-0.025m.
- PASS — 03 settle on platform: Simulated motor intent across 8 real frames; swim input=0.
- PASS — 03 E changes rail from stop to raise: TutorialJourney.Interact accepted; eye-to-control distance=1.135m, allowed radius=2.800m.
- PASS — 03 neutral-input physical lift ride: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — 03 settle at upper landing: Simulated motor intent across 10 real frames; swim input=0.
- PASS — 03 rider carried by actual Rigidbody platform: Neutral input throughout ride; no jumps/teleports. Rider rise=3.100m; platform rise=3.100m; normalized height=1.000
- PASS — 03 cross upper landing: Reached through CharacterController movement; vertical error=-0.010m.
- PASS — 03 follow descent deck: Reached through CharacterController movement; vertical error=-0.122m.
- PASS — exit folded-light structure: Reached through CharacterController movement; vertical error=-0.122m.
- PASS — 04 reach halfway sea reveal: Reached through CharacterController movement; vertical error=0.062m.
- PASS — Full spawn-to-halfway route passed: Actual controller movement, four E interactions across three stages, two curtain rules and a physical lift ride; route teleports=0.
- PASS — Supplementary pool reposition: Pool check reposition occurs after accepted route; route teleport count remains zero.
- PASS — Pool initial 30 floating frames: Simulated motor intent across 30 real frames; swim input=0.
- PASS — Pool continued 120 floating frames: Simulated motor intent across 120 real frames; swim input=0.
- PASS — Pool stable physical surface draft: After 150 real frames, draft=1.416m; Swimming=true.
- PASS — Pool Ctrl-style deliberate dive: Simulated motor intent across 90 real frames; swim input=-1.
- PASS — Pool dive changes physical depth: Feet descended 2.388m.
- PASS — Pool Space-style ascent with repeated presses: Simulated motor intent across 240 real frames; swim input=1.
- PASS — Pool surface ascent remains bounded: 120 additional held-ascent frames, repeated jump intents; largest float-target overshoot=0.000m.
- PASS — Mirage solid step 1 physically raycastable: Hit actual authored collider at (-90.30, 3.05, 197.08).
- PASS — Mirage solid step 2 physically raycastable: Hit actual authored collider at (-89.15, 3.35, 197.08).
- PASS — Mirage solid step 3 physically raycastable: Hit actual authored collider at (-88.00, 3.65, 197.08).
- PASS — Mirage place character above middle solid step: Explicit supplementary reposition after the completed route; subsequent interactions and crossing use normal policies/physics.
- PASS — Mirage neutral physical landing on raised step: Simulated motor intent across 45 real frames; swim input=0.
- PASS — Mirage solid step physically supports the actual player: 45 neutral real frames after landing; grounded=true, Swimming=false, feet=3.375m, step top=3.350m; supporting collider=40_Tutorial/05_Optional_WaterMirage/ProjectedSolidStep_2
- PASS — Mirage misalignment removes physical step colliders: 65 degrees: IsSolid=false; all three colliders disabled and absent from downward rays. Status=投影点超出单层水体光路
- PASS — Mirage unsupported player falls for 45 real frames: Simulated motor intent across 45 real frames; swim input=0.
- PASS — Mirage removed support transitions to actual swimming: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Mirage swimmer recovers surface buoyancy: Simulated motor intent across 90 real frames; swim input=0.
- PASS — Mirage support loss causes falling and swimming recovery: No movement input or rescue teleport after support removal; grounded=false, Swimming=true, recovered draft=1.419m in the authored optional basin.
- PASS — Mirage correct aim restores solid steps after the fall: 50-degree aim restored IsSolid=true after the physical support-loss/swim cycle.
- PASS — Supplementary checks passed: Original-pool float/dive/ascent and optional mirage collision states passed within the declared scope.
- PASS — Recovery settle after actual Home reset: Simulated motor intent across 8 real frames; swim input=0.
- PASS — Home/respawn preserves acquired and enduring state: Called the real walker.ResetPosition used by Home; counted separately as a post-route reposition. Lantern and permanent openings remain.
- PASS — Recovery position outside closed temporary entrance: Explicit supplementary reposition after the completed route; subsequent interactions and crossing use normal policies/physics.
- PASS — Recovery settle at exterior entrance handle: Simulated motor intent across 8 real frames; swim input=0.
- PASS — Recovery E restores entrance illumination from outside: TutorialJourney.Interact accepted; eye-to-control distance=1.139m, allowed radius=2.800m.
- PASS — Recovery temporary entrance reopens: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Recovery physically reenter restored entrance: Reached through CharacterController movement; vertical error=0.074m.
- PASS — Previously solved passage remains recoverable after respawn: Normal exterior E interaction reopened the temporary barrier, and the real controller crossed it after Home. No collision or interaction bypass.
- PASS — Recovery reach interior console again: Reached through CharacterController movement; vertical error=0.033m.
- PASS — Recovery restore original interior beam allocation: TutorialJourney.Interact accepted; eye-to-control distance=1.172m, allowed radius=2.800m.
- PASS — Recovery temporary entrance closes while enduring exit stays open: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Recovery position at upper landing recall handle: Explicit supplementary reposition after the completed route; subsequent interactions and crossing use normal policies/physics.
- PASS — Recovery settle on fixed upper landing: Simulated motor intent across 8 real frames; swim input=0.
- PASS — Recovery E lowers lift from upper landing: TutorialJourney.Interact accepted; eye-to-control distance=1.268m, allowed radius=2.800m.
- PASS — Recovery platform leaves upper landing and reaches bottom: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Upper landing handle remains reachable with lift absent: Player stayed on static landing; platform reached bottom through the normal light-driven motor.
- PASS — Recovery E selects stop from upper landing: TutorialJourney.Interact accepted; eye-to-control distance=1.268m, allowed radius=2.800m.
- PASS — Recovery moving portal reaches stop dock: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Recovery E recalls lift upward from upper landing: TutorialJourney.Interact accepted; eye-to-control distance=1.268m, allowed radius=2.800m.
- PASS — Recovery absent platform returns to upper landing: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Recovery physically reboard recalled lift: Reached through CharacterController movement; vertical error=-0.025m.
- PASS — Recovery settle on recalled platform: Simulated motor intent across 8 real frames; swim input=0.
- PASS — Upper lift recall prevents a stranded return route: Upper E handle lowered an occupied-world platform, selected stop, recalled it upward, and the real controller reboarded its physical collider. Main route remained teleport-free; supplemental repositions=5
- PASS — Overview regression returns occupied platform to bottom: Satisfied during real rendered/physics frames with neutral controller input.
- PASS — Overview regression settles rider at bottom: Simulated motor intent across 8 real frames; swim input=0.
- PASS — Actual overview mode carries player on the moving lift: Called CoastalWalkthrough.SetOverview(true), with input inactive. Observed 252 real frames for 4.252s without any harness motor call or synthetic keystroke; built-in Update carried rider 3.100m while Rigidbody rose 3.100m.
