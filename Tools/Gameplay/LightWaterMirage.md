# Optional water mirage sandbox

`CoastalTemple.Mechanisms.WaterMirage` is a small gameplay prototype. It computes an incoming ray's intersection with the simulated water surface, applies Snell refraction using the sampled surface normal, and intersects the transmitted ray with an authored wall plane. A supplied projection marker displays that actual hit. Authored steps become visible and solid only when the hit falls within `targetRadius` of the authored target. An optional `LightReceiver` additionally gates power; receiver activation by itself never makes the steps solid.

This is one air-to-water interface, not a complete camera/image formation model. The sample anchor must be submerged and the wall hit must remain inside the sampled water depth. It does not model an exit interface, caustics, diffraction, a full statue image, or volumetric reconstruction. Making the authored projected steps solid is deliberately a fantasy puzzle rule. The authored marker/steps provide the image shape; the runtime creates no scene geometry.

## Minimal reproducible layout

Use translation-only water with a 6 m by 8 m domain, local surface 0 m and bottom -3 m. All positions below are relative to the water object's position:

* source: `(0, 2, -3)`, with 50-degree downward pitch and 0-degree yaw;
* sampleObject: `(0, -0.5, -1.5)`; its collider is excluded from the single representative light ray;
* projectionPlane: `(0, 0, 0)`, with its forward normal along +Z or -Z;
* target: approximately `(0, -2.4015, 0)`, radius 0.25 m;
* projectionMarker: existing renderer at the wall, assigned to `markerRenderer`;
* solidSteps / stepRenderers / stepColliders: authored phantom steps to toggle. Their transforms are never repositioned.

Keep the traced water path free of other physical colliders and place the wall collider at or just behind the plane; raycasts stop shortly before the intended wall hit. The two default aim states are 50 and 65 degrees down. The first reaches the target in this layout; the second sends the hit below the bed and therefore fails. `SetAimState(int)` selects exact states; `CycleAim()` rotates between them; `SetSourceTarget(Transform)` points the source toward an authored guide position. `EvaluateNow()` updates immediately, while normal updates run at 20 Hz.

The seven pure math tests are in `Tools/Gameplay/LightMirageTests/Run.ps1`; `-MissingBaseline` reproduces missing-feature failures. Current tests pass for normal incidence, Snell's angle ratio, total internal reflection, air/water source classification, wall intersection and range, and water depth bounds. Runtime plus tests compile against Unity 6000.5.9f1 with zero errors/warnings. The integrated Unity Play checks now also verify actual CharacterController support on the middle step, removal of support after misalignment, falling into water, flotation, and restoration. See [V8 Play checks](../../Documentation/CoastalV8Verification/PlayChecks.md). These are automated real-frame checks, not manual keyboard playtesting.

## Serialized optional basin helper

`CoastalTemple.Editor.MirageSandboxAuthoring.Build(parent, white, yellow, cyan, waterMaterial, origin)` returns a complete optional basin GameObject. Parent world scale must be one and world rotation zero. Origin is the water-surface center. The floor is 3 m lower, the rim/deck 0.5 m higher, and the permanent 12-step west approach starts near floor elevation. Its total local footprint is approximately x = -10.25..3.25 m and z = -4.35..4.35 m. Choose ground near the floor elevation so terrain does not fill the inside of the basin.

The helper shifts the optical example +3.5 m along z: source `(0,2,0.5)`, underwater sample `(0,-0.5,2)`, projection plane z = 3.5, target `(0,-2.401375,3.5)`. This underwater Snell footprint activates an expanded, authored fantasy staircase near the same wall; the above-water steps are not physical optical image positions. Their centers are x = -2.3, -1.15 and 0 m, their tops are 0.05, 0.35 and 0.65 m above the water surface, and their 1.18 m widths overlap slightly. This layout is intended to let a player leave the west rim and climb above the water instead of floating above submerged treads. The projection wall extends from y = -3 to +1 m, and its painted guides follow these step heights. Actual standing on the middle step was verified in the integrated Play report; a complete manual climb of all three steps is not claimed. A separate permanent inner stair gives basin access and escape. Step transforms never change at runtime, and the helper supplies bed blocks for the permanent inner stairs.

`MirageAimConsole` derives from the existing tutorial interaction type, so E cycles the 50/65-degree states without coupling the bonus to story progress. `MirageBeamView` updates two serialized line renderers using the actual source, entry and projection positions and hides the edit-mode flat-water preview on Awake. All decorative and step geometry is authored by the editor helper. Existing WaterVolume owns its usual runtime water mesh. The helper does not register its water with the player swimmer list automatically; choose that integration deliberately because swimming changes how the underwater projected steps are traversed.
