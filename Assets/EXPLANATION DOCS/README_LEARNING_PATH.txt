EXPLANATION DOCS - JUNIOR UNITY DEVELOPER LEARNING PATH
-------------------------------------------------------

Purpose
-------
This folder is a teaching companion for the current game code. The documents do
more than list changed lines: they explain the original problem, responsibility of
each class, execution order, safety boundaries, and reason behind the design.

The .cs files are always the final source of truth. If a document and current code
ever disagree, verify the code, reproduce the behavior, and then update the
document. Documentation is a map; executable code is the territory.

Script folder organization
--------------------------
Only current, required code belongs under Assets/Scripts/Main Scripts:

- Core Gameplay: board, boxes, nodes, spawning, soda transfer, and lift trucks.
- Managers: persistent data, game state, loading, sound, shop, trucks, and UI.
- Tutorial: tutorial lifecycle, state machine, hand, and tooltip presentation.
- UI: panels, settings, boosters, currency animation, and UI feedback helpers.
- Visual: shared visual settings and their runtime application.
- Compatibility: older code that current production callers still require.

Unused, repeated, prototype, and replaced code is isolated under
Assets/Scripts/Old Version Scripts. Do not use it as the current architecture or
include it in the normal learning path. DOTween remains in Assets/Plugins because
it is a third-party package, not project-authored gameplay code.

Recommended reading order
-------------------------
Read in this order because each lesson introduces concepts used by later lessons:

1. GameDataManager_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Managers/GameDataManager.cs
   Learn PlayerPrefs, destructive reset safety, per-level runtime counters, and
   repairing saved level state from the active scene.

2. LEVEL_END_FLOW_EXPLANATION.txt
   Sources: GameManager.cs, GameDataManager.cs, Board.cs, UIManager.cs,
   WinPanel.cs, RevivePanel.cs, LoseGame.cs, and LiftTruck.cs.
   For the safe truck queue and its complete unload/return lifecycle, read
   LIFT_TRUCK_QUEUE_EXPLANATION.txt.
   Learn exactly-once Win/Lose gates, correct post-match loss timing, pausing
   gameplay while unscaled UI continues, and safe scene-name progression.

3. TutorialCompletionStore_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Tutorial/TutorialCompletionStore.cs
   Learn interfaces, persistence abstraction, stable IDs, and scoped resets.

4. LoadManager_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Managers/LoadManager.cs
   Learn startup routing and how completion chooses TUTORIAL or Level1.

5. TutorialControllerBase_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Tutorial/TutorialControllerBase.cs
   Learn inheritance, lifecycle states, registration metadata, and contracts.

6. TutorialManager_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Tutorial/TutorialManager.cs
   Learn singleton lifecycle, one-active-tutorial coordination, ownership, and
   scene-change cleanup.

7. SpawnContoller_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Core Gameplay/SpawnContoller.cs
   Learn deterministic spawning, coroutines, stable results, and live rail state.

8. Box_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Core Gameplay/Box.cs
   Learn mouse input, drag lifecycle, events, owner-scoped authorization, and why
   blocked Stage 2 Boxes cannot move even one frame.

9. Board_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Core Gameplay/Board.cs
   Learn authoritative placement, event payloads, correlation tokens, resolution,
   optional placement constraints, and exactly-once progress.

10. DYNAMIC_BOARD_LAYOUT_EXPLANATION.txt
   Sources: Board.cs, BoardEditor.cs, GameManager.cs, MoveCylinder.cs, and the
   production level scenes.
   Learn per-scene dimensions, visual custom-shape masks, playable-cell rules,
   shape-aware loss detection, and removal of fixed-grid tool assumptions.

11. INITIAL_BOX_LEVEL_SETUP_EXPLANATION.txt
    Sources: Board.cs, Box.cs, BoardEditor.cs, SpawnContoller.cs, and Soda.cs.
    Learn sparse per-level starting-state data, prefab-derived capacity, ordered
    Soda colors, editor tooling, and safe runtime registration without fake moves.

12. Node_EXPLANATION.txt
   Source: Assets/Scripts/Main Scripts/Core Gameplay/Node.cs
   Learn how valid-cell logic and the visible highlight child are kept separate.

13. ADJACENT_CELL_HIGHLIGHT_EXPLANATION.txt
    Sources: Board.cs, Box.cs, Node.cs, SortingTutorialController.cs, Node.prefab,
    and Highlight.prefab.
    Learn the complete adjacency rule, historical evidence, geometry diagnosis,
    implementation contract, and normal-level compatibility.

14. HandAnimation_EXPLANATION.txt
    Source: Assets/Scripts/Main Scripts/Tutorial/HandAnimation.cs
    Learn DOTween sequences, world/screen/UI coordinate conversion, RectTransform
    pivots, sprite fingertip compensation, and serialized-value pitfalls.

15. ToolTipTutorial_EXPLANATION.txt
    Source: Assets/Scripts/Main Scripts/Tutorial/ToolTipTutorial.cs
    Learn presentation-only UI, button listener cleanup, and why animation timing
    must not control gameplay progress.

16. SortingTutorialController_EXPLANATION.txt
    Source: Assets/Scripts/Main Scripts/Tutorial/SortingTutorialController.cs
    Learn the full state machine that connects spawning, input, placement,
    resolution, highlighting, left-to-right Stage 2 order, and completion.

17. TUTORIAL_SCENE_SETUP_EXPLANATION.txt
    Source: Assets/Scenes/TUTORIAL.unity and all serialized references above.
    Learn how code architecture is assembled in the Unity Inspector and how to run
    the complete manual validation sequence.

18. ANDROID_BUILD_AND_RESPONSIVE_UI_EXPLANATION.txt
    Sources: AndroidBuildAutomation.cs, the production Canvas Scalers,
    ProjectSettings.asset, and mainTemplate.gradle.
    Learn reference resolution versus device resolution, balanced Canvas scaling,
    portrait settings, ARM64/IL2CPP, debug signing, and Android dependency alignment.

Runtime architecture story
--------------------------
LoadManager selects TUTORIAL when sorting.initial is incomplete. TutorialManager
evaluates registered TutorialControllerBase components and starts
SortingTutorialController. The controller asks SpawnContoller for deterministic
Boxes, tells HandAnimation and ToolTipTutorial what to present, and installs
temporary input/placement rules. Box reports player input. Board authoritatively
accepts a placement and later reports full resolution. Node presents allowed cells.
After every required Match, TutorialManager records completion through
TutorialCompletionStore, and later launches route to Level1.

How to study each lesson
------------------------
1. Read the explanation document once without opening code.
2. Open the matching source and find every named field/method.
3. Trace one runtime path with a debugger or temporary breakpoints.
4. Write the state changes in your own words.
5. Predict one failure before reading the document's debugging section.
6. Make a tiny safe exercise on a separate branch, then revert or review it.
7. Answer the lesson quiz from the AI mentor prompt without looking at notes.

Recent changes that deserve special attention
----------------------------------------------
Hand target alignment:
    The tween reached the correct RectTransform pivot, but the visible fingertip
    was above it. HandAnimation now adds a runtime (0, -70) sprite-pivot correction.
    This teaches coordinate spaces and Unity serialization behavior.

Stage 2 ordered rail input:
    Stage 2 sorts rail Boxes left-to-right and authorizes only currentSourceBox.
    Box checks the rule before pointer capture, so an incorrect Box never moves.
    This teaches the difference between authorization (before an action) and an
    event notification (after an action starts).

Using the AI teaching prompt
----------------------------
Copy the contents of AI_JUNIOR_DEVELOPER_MENTOR_PROMPT.txt into an AI that can read
this project or attach the listed documents and source files. The prompt asks for
one lesson at a time, diagrams, exercises, quizzes, and a separate voice/audio
lesson for every part.
