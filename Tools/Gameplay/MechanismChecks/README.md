# Reusable mechanism checks

Run numerical and drive-policy behavior checks without Unity:

```powershell
dotnet run --project Tools/Gameplay/MechanismChecks/MechanismChecks.csproj
dotnet run --project Tools/Gameplay/PlayerMotionChecks/PlayerMotionChecks.csproj
```

The new checks were first run against missing implementations: 0 passed, 3 failed. After implementation: 35 passed, 0 failed. Existing player/lift numerical checks: 34 passed, 0 failed.

After script import, run the isolated native-physics helper **in Play mode**; Unity does not allow this local physics scene creation in Edit mode:

```csharp
CoastalTemple.Mechanisms.MechanismComponentChecks.Run();
```

The smaller receiver/lift inspection helper can run in **Edit mode**:

```csharp
CoastalTemple.Mechanisms.LightDrivenLiftChecks.Run();
```

The native-physics helper creates a disposable local physics scene in Play mode. It advances only that scene and verifies actual Rigidbody travel, a horizontal intermediate stop, the counterweight, endpoint clamp, delayed illumination, immediate light loss despite receiver grace/latching, receiver disable, both Renderer and Collider state, manual/light conflict, handle commands, and inherited serialized lift fields. Its finally block removes temporary objects immediately and requests `UnloadSceneAsync` for the empty scene. It does not tick the global light registry. Disabled-component checks call the public evaluation methods explicitly; riding the authored route still requires separate Play-mode integration checks.

## Scene authoring contract

- `LinearPlatformMotor` owns an existing `platform` / `platformBody` Rigidbody, local or world `bottom` / `top`, `speed`, and optional `counterweight` / `counterweightTravel`. It creates no geometry or body. Use `SetDirection(-1|0|1)` or `SetDrive`; old `SetManualDirection`, `StopManual`, `ReturnToBottom`, `Initialize`, `Simulate`, `NormalizedHeight`, and `NextPosition` remain supported. `NormalizedTravel` is a clearer name for horizontal movement. One inherited `FixedUpdate` performs all movement; drivers must never call `Simulate` every frame themselves.
- `LightDrivenLift` inherits that motor and adds only `receiverRaise` / `receiverLower` direction sampling. Simultaneous opposing requests stop. A receiver must be enabled, active, and currently illuminated, so its activation delay is honored but stale grace/latch state never powers movement after light loss. A deliberate manual request is independent of light and may continue when the beam is absent.
- `SolidPath` holds explicit `Renderer[] renderers` and `Collider[] colliders`. `initiallySolid` defaults false. Call `SetSolid(bool)` to switch both sets; unchanged state returns without further writes. It creates no meshes or material instances and never alters other mechanisms. Keep the receiver and its optical collider outside these arrays.
- `LightPathDriver` holds `receiver` and `path`. `EvaluateNow()` samples the receiver; ordinary runtime evaluation occurs after light tracing in `LateUpdate`. No hold timer is added: moving the beam away removes the assigned path immediately. The receiver owns the activation delay.
- `MotorLever` holds `motor`, optional visual `handle`, `rotationAxis` / `handleAngle`, and `forwardLabel` / `backwardLabel`. Every `Use(PlayerInteractor)` cycles forward, stop, backward, stop. `SetDirection(int)` is also public. Inherited `prompt`, `useRadius`, and `interactionPoint` define interaction. It only updates the motor command, never advances physics.

## Existing lift migration

Keep the original `LightDrivenLift.cs.meta` GUID and scene component. Its old public serialized motion fields moved to the base class with identical names and types; Unity serializes inherited fields on the same component. No new motor component should be added beside an existing light lift. The helper checks all ten original field paths with `SerializedObject`.

The authoring owner should reopen the main scene and `FoldedLight.prefab`, verify their inherited references/endpoints and run both component helpers. This implementation does not rewrite scenes or prefabs and does not claim that reopen/migration verification was performed by the mechanism agent.
