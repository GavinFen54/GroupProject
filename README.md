# Archer VR

A stationary VR archer defends a tower from enemy waves. As enemies damage the tower it collapses lower, bringing the player closer to the battlefield. Design board: the team's FigJam "Archer Vr".

## Opening the project
- **Unity 6.3 LTS (6000.3.7f1)**. Open the folder in Unity Hub with *Add → Add project from disk*.
- The first open takes a few minutes while Unity rebuilds the `Library` folder (it is not in Git on purpose).
- Assets from the Asset Store (Medieval Stone Keep, Medieval Castle – Modular, Fantasy Skybox FREE) are included. Keep this repository **private**: Asset Store licences don't allow public redistribution.

## Playing it
Open `Assets/ArcherVR/Scenes/S0_Prologue` and press Play (or start at `S1_Exposition` to skip the story).

| Scene | Story beat | What happens |
|---|---|---|
| S0_Prologue | Exposition (story) | A quiet camp at dawn. Story text prepares you for the fight, then moves on to S1 by itself (or press Skip). |
| S1_Exposition | Exposition | Start on the ground; the army is visible beyond the tower. Walk onto the glowing circle to climb the tower. |
| S2_Battle | Rising Action, Climax, Falling Action | On the tower top. Grab the bow, hit the warm-up target, then the waves start. 4 waves, a boss wave, then stragglers. Each damage threshold sinks the tower (and you) lower. Losing shows Try Again / Start Over / Quit. |
| S4_Resolution | Resolution | Calm sunrise, victory panel, Play Again / Quit. |

**Headset (Quest):** left stick to walk, grip to hold the bow, trigger to fire.
**No headset (Mac/PC Editor):** the XR Interaction Simulator is on automatically. Press **Y** for the key guide.
WASD walk · hold right-mouse to look · `]` / `[` select right / left hand · **G** grip · **T** trigger.

## Where things live
- `Assets/ArcherVR/Scripts/Runtime` — gameplay: `EnemyController`, `WaveManager`, `TowerHealth`, `Bow`, `Arrow`, `GameFlow`, `BattleHUD`, `PrologueSequence`, `PracticeTarget`, `PlayerSafetyNet`, `SceneLoadZone`, `RuntimeNavMesh`.
- `Assets/ArcherVR/Scripts/Editor/ArcherVRBuilder.cs` — the **Archer VR** menu in Unity.
- `Assets/ArcherVR/Prefabs` — enemies, bow, arrow (generated).
- `Assets/Group Project.unity` — the team's layout scene. The story scenes are copied from it.

## Archer VR menu
- **Build Everything** — regenerates prefabs and all four story scenes (S0, S1, S2, S4) from `Group Project.unity`. ⚠️ This overwrites manual edits to those scenes.
- **Add Prologue + Target Practice (keeps your edits)** — builds S0_Prologue and adds the warm-up target to S2 without rebuilding the other scenes.
- **Fix Tower Collapse In Existing Scenes** — makes the tower movable in the existing scenes without rebuilding them.
- **Convert Built-in Materials to URP** — fixes pink materials on newly imported Asset Store packs.

Tune waves, enemy count, speed and size on the `WaveManager` object in S2 and on the `Enemy_*` prefabs (each enemy rolls a random speed via `speedVariation`).

## Working together in Git
- **Pull before you start, commit + push when you finish.**
- Two people editing the **same scene** at the same time will conflict. Agree who owns a scene, or build your piece as a prefab and drop it in.
- Never commit `Library/`, `Temp/` or `Logs/` (the `.gitignore` already handles this).

## Still to do (from the story map)
- Real enemy models: Low Poly Medieval Skeleton (basic), Orc Destroyer 2 / Giant / Drakonit (boss), Crusader (advanced). Then run **Archer VR › Build Everything**.
- Real bow draw (nock + pull) instead of point-and-trigger; upgrades between waves; audio (horns, music, hit sounds); villagers in S4; ghost-hand tutorial hint.
