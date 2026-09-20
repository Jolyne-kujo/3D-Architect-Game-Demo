# Shared motor lever and arrival checks

Run `dotnet run --project Tools/Gameplay/LeverChecks/LeverChecks.csproj`.

This compiles the real `MotorLever`, `MotorLeverState`, `LessonCompletionCondition`, and `PlayerReachedCondition` sources against a minimal managed Unity surface. Only native Unity infrastructure and the motor's external command contract are replaced; the lever cycle, state selection, prompts, and arrival condition are production code. It verifies alternating two handles, immediate observation of external motor commands, external stops, disabled action permission, separate motors, and local arrival bounds. It does not simulate Rigidbody motion or claim that keyboard events were injected.

Before the fix: 2 passed, 12 failed, including the shared-handle regression and absent arrival condition. After the fix: 20 passed, 0 failed.

Real Unity verification is included in `CoastalTemple.Mechanisms.MechanismComponentChecks.Run()` and `CoastalTemple.Tutorial.Editor.TutorialInteractionChecks.Run()`. These additionally check actual handle rotations, the real motor component contract, transformed destination regions, and one-time tutorial rewards on physical arrival. After import, run the first **in Play mode** because it creates its own disposable local physics scene; the second can run in **Edit mode**. Save each report independently.
