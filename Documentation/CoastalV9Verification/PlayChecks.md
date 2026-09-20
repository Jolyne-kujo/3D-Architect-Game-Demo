# V9 continuous route verification

Result: **PASS**

Progress: Recovery stage 3 remains stable at its safe point

Route passed: True; recovery passed: True.

Main-route teleports: 0; post-route repositions: 4; post-route automatic recoveries: 3; post-route teleport total: 7.

Normal interactions: 19; unique rendered frames with motor intent: 4871.

Main route uses CourtyardWalker.SimulateMovement/ApplyLook with no jump intent, teleport, motor setter, direct optical selection or forced bridge state. Every E action goes through PlayerInteractor.Interact with the ordinary availability, distance and obstruction policy, and the target must be the nearest reachable prompt. Real rendered/physics frames drive all mechanisms. Physical keyboard events are not injected. Game fall/sea-boundary recoveries and unexplained position discontinuities fail the main route and are counted. BeginRecoveryChecks is separate and requires a previously accepted zero-teleport route; every deliberate post-route reposition and automatic recovery is reported separately. Exit Play restores authored state.

- PASS — Initial authored states settle: Observed 0.123 seconds of ordinary frames.
- PASS — Start at authored shore spawn: No test reposition; all three puzzles are initially unsolved. Keyboard movement is suspended while real controller intent is supplied.
- PASS — 01 missing light bridge is a real physical gap: A real downward ray at the missing route found no nearby solid support.
- PASS — 01 reach shore ramp: Reached using ordinary CharacterController intent; vertical error=-0.206m.
- PASS — 01 climb to entry mirror: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 01 E borrows light from the entry mirror: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.482m; radius=2.900m; no policy bypass.
- PASS — 01 A gains support while B disappears: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 01 optical tradeoff is physical: A light-bridge colliders are enabled; returning B collider is disabled; permanent cracked window remains open.
- PASS — 01 cross A onto the safe middle island: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 01 E returns the borrowed light from the island: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.686m; radius=2.900m; no policy bypass.
- PASS — 01 A disappears and B returns: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 01 cross the returned stone bridge: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 01 E collects the reusable lantern: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.039m; radius=2.800m; no policy bypass.
- PASS — 01 lantern acquired: Possession and reusable illumination came from the normal nearby pickup.
- PASS — 01 leave the lantern landing: Reached using ordinary CharacterController intent; vertical error=0.175m.
- PASS — Shore mountain path: Reached using ordinary CharacterController intent; vertical error=0.243m.
- PASS — Path toward the stone workshop: Reached using ordinary CharacterController intent; vertical error=0.283m.
- PASS — 02 reach the workshop approach: Reached using ordinary CharacterController intent; vertical error=-0.768m.
- PASS — 02 approach the riding wall from the entry: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 E starts raising the stone wall from the entry: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.606m; radius=2.900m; no policy bypass.
- PASS — 02 board the real rising wall: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 CharacterController stands on the moving wall Rigidbody: Real downward support ray found the expected Rigidbody and the controller is grounded.
- PASS — 02 ride to the middle gallery height: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E stops the wall at the middle gallery: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.098m; radius=2.900m; no policy bypass.
- PASS — 02 light loss stops the wall: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 middle stop holds its physical height settles: Observed 0.128 seconds of ordinary frames.
- PASS — 02 middle stop holds its physical height: Observed 0.371 seconds of ordinary frames.
- PASS — 02 step sideways onto the fixed gallery: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 go around the winch pedestal: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 reach the bridge winch: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 E starts the bridge leftward: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.309m; radius=2.900m; no policy bypass.
- PASS — 02 bridge passes the center alignment: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E stops the bridge beyond center: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.309m; radius=2.900m; no policy bypass.
- PASS — 02 bridge holds the first intermediate stop settles: Observed 0.123 seconds of ordinary frames.
- PASS — 02 bridge holds the first intermediate stop: Observed 0.270 seconds of ordinary frames.
- PASS — 02 E reverses the bridge rightward: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.309m; radius=2.900m; no policy bypass.
- PASS — 02 bridge returns to its centerline: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E completes the four-step winch cycle at center: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.309m; radius=2.900m; no policy bypass.
- PASS — 02 aligned cross-bridge remains stopped settles: Observed 0.130 seconds of ordinary frames.
- PASS — 02 aligned cross-bridge remains stopped: Observed 0.370 seconds of ordinary frames.
- PASS — 02 winch completes forward-stop-reverse-stop: All four commands used normal nearby E; actual Rigidbody x=-0.018m, no motor setters were called.
- PASS — 02 leave the winch without crossing its pedestal: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 return along the fixed gallery: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 reboard the stopped wall: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 rider reboards from the middle gallery: Real downward support ray found the expected Rigidbody and the controller is grounded.
- PASS — 02 E advances middle stop to lower: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.196m; radius=2.900m; no policy bypass.
- PASS — 02 lower aim produces an actual lowering request: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E advances lower to resting light: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.202m; radius=2.900m; no policy bypass.
- PASS — 02 resting light stops the downward request: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E starts the second ascent: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.196m; radius=2.900m; no policy bypass.
- PASS — 02 rider reaches the upper wall stop: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 E parks the wall at the upper gallery: PlayerInteractor.Interact accepted the nearest reachable object; distance=2.195m; radius=2.900m; no policy bypass.
- PASS — 02 upper wall stops physically: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 02 upper stop holds the rider settles: Observed 0.130 seconds of ordinary frames.
- PASS — 02 upper stop holds the rider: Observed 0.302 seconds of ordinary frames.
- PASS — 02 rider remains on the wall at the upper stop: Real downward support ray found the expected Rigidbody and the controller is grounded.
- PASS — 02 approach the aligned bridge from the wall: Reached using ordinary CharacterController intent; vertical error=0.001m.
- PASS — 02 board the aligned sliding bridge: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 real sliding bridge supports the crossing: Real downward support ray found the expected Rigidbody and the controller is grounded.
- PASS — 02 cross the repaired upper corridor: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 pass the workshop completion landing: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 02 leave by the authored joining ramp: Reached using ordinary CharacterController intent; vertical error=-0.186m.
- PASS — Mountain connection toward folded light: Reached using ordinary CharacterController intent; vertical error=-0.338m.
- PASS — 03 reach the folded-light approach: Reached using ordinary CharacterController intent; vertical error=-0.252m.
- PASS — 03 climb to the entrance portal control: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 unaligned stone bridge leaves a real gap: A real downward ray at the missing route found no nearby solid support.
- PASS — 03 E directs portal light to the stone-drive receiver: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.457m; radius=2.900m; no policy bypass.
- PASS — 03 redirected light moves A into the centerline: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 03 E passes through the long-corridor mode: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.457m; radius=2.900m; no policy bypass.
- PASS — 03 visible control-cycle transition: Observed 0.085 seconds of ordinary frames.
- PASS — 03 E returns the portal to rest before crossing: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.457m; radius=2.900m; no policy bypass.
- PASS — 03 A returns as a stationary physical bridge: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 03 cross centered A onto the safe middle pier: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 E passes through stone-drive mode from the middle pier: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.493m; radius=2.900m; no policy bypass.
- PASS — 03 middle control-cycle transition: Observed 0.092 seconds of ordinary frames.
- PASS — 03 E aims the low beam toward the far corridor: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.493m; radius=2.900m; no policy bypass.
- PASS — 03 low portal aim settles: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 03 low beam dissolves A while B remains dark: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 03 observe the low beam obstruction: Observed 0.367 seconds of ordinary frames.
- PASS — 03 failed low aim is observable: Actual final beam endpoint=(0.00, -0.10, 0.00); B has no collider support; A disappeared. The player stands on the permanent middle pier.
- PASS — 03 move across the safe pier to its height winch: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 E raises the portal carriage: PlayerInteractor.Interact accepted the nearest reachable object; distance=1.596m; radius=2.900m; no policy bypass.
- PASS — 03 carriage reaches 5.6m and far B becomes solid: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — 03 elevated portal light repairs the far route: The original beam cleared the physical middle pier, illuminated the far receiver and enabled the authored rising bridge.
- PASS — 03 approach the rising light bridge: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 climb the real illuminated corridor: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 step around the receiver stand: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — 03 reach the sea-view reward landing: Reached using ordinary CharacterController intent; vertical error=-0.025m.
- PASS — Full V9 shore-to-sea-view route passed: Three real broken routes crossed by CharacterController; normal nearest E interactions; wall riding and intermediate stops; full winch reversal cycle; observable failed low light followed by a real elevated portal solution; no jump intent or reposition.
- PASS — Recovery Home retains completed world state: Explicit post-route test reposition; excluded from the accepted main route. World mechanism state was not altered.
- PASS — Recovery settles after actual Home reset: Observed 0.202 seconds of ordinary frames.
- PASS — Recovery deliberately enters stage 1 fall region: Explicit post-route test reposition; excluded from the accepted main route. World mechanism state was not altered.
- PASS — Recovery stage 1 uses its authored automatic return: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — Recovery stage 1 remains stable at its safe point: Observed 0.369 seconds of ordinary frames.
- PASS — Recovery stage 1 preserves the solved world: Post-route placement into the fall volume was explicit; the ordinary LateUpdate performed one automatic return. No mechanism was reset.
- PASS — Recovery deliberately enters stage 2 fall region: Explicit post-route test reposition; excluded from the accepted main route. World mechanism state was not altered.
- PASS — Recovery stage 2 uses its authored automatic return: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — Recovery stage 2 remains stable at its safe point: Observed 0.363 seconds of ordinary frames.
- PASS — Recovery stage 2 preserves the solved world: Post-route placement into the fall volume was explicit; the ordinary LateUpdate performed one automatic return. No mechanism was reset.
- PASS — Recovery deliberately enters stage 3 fall region: Explicit post-route test reposition; excluded from the accepted main route. World mechanism state was not altered.
- PASS — Recovery stage 3 uses its authored automatic return: Satisfied through normal render/physics updates with neutral controller intent.
- PASS — Recovery stage 3 remains stable at its safe point: Observed 0.360 seconds of ordinary frames.
- PASS — Recovery stage 3 preserves the solved world: Post-route placement into the fall volume was explicit; the ordinary LateUpdate performed one automatic return. No mechanism was reset.
