# A Thousand Battles Later

Unity 6000.5.2f1 2D action platformer. The enabled build scenes are `StartMenu`, `stage1_full`, `stage2_full`, and `Help` (in that order). Open the project with the version recorded in `ProjectSettings/ProjectVersion.txt`; editor-generated `Library`, `Temp`, and `Logs` are local data.

## Gameplay pipeline

1. `StartMenuController` selects a difficulty and opens the first stage, or resumes the second stage when its story progress marker is present. `GameProgress` coordinates clearing a run. `StoryProgress` and `Difficulty` use `PlayerPrefs`; `RunProgress`, `RunInventory`, and `RunEquipment` hold the current play session's abilities, items, and gear.
2. Each stage instantiates the Hero and UI. `Role` reads the Input System keyboard and drives movement, jump, dash, melee, and kunai states. `PlayerProgression` applies equipped and forged attack/defense values. `UIManager` coordinates the bag, forge, minimap, and pause panels.
3. Mobs use `MobStateMachine` for patrol, chase, and attacks. Each arena Boss uses `EnemyPlatformNavigator` for pursuit and `BossTeleport` for its authored reposition window. The navigator snaps shared landing nodes to each Boss collider, checks the full jump arc against solid level geometry when building A* edges, and checks each movement segment again while hopping. If a wall blocks the route, it cancels that hop and replans. `BossArenaController` changes cameras, music, minimap visibility, and story presentation when the Hero enters the arena.
4. `EnemyAttackController` chooses an eligible `EnemyAttackPattern` by range and weight, owns its coroutine and cooldown, and sends cast callbacks to `BossStateMachine`. The pattern owns telegraph, hitbox, projectile, damage, and impact feedback. Combat and hit timing do not depend on animation events.
5. `BossStateMachine` sends Idle/Run movement changes to the visual child's Unity Animator Controller. At cast start it selects Attack1/2/3; during windup it samples the clip through its authored release frame using the pattern's charge progress; the pattern's fire callback starts the follow-through. Damage outside a cast plays Hurt; death plays Death. The King and Wizard have separate Controllers and sprite clips under `Assets/Animations/Boss`. The Wizard's Attack3 state intentionally reuses its Attack1 frames. The visual `SpriteRenderer` flips independently of the Boss collider; the King retains its off-centre-pivot correction.
6. `EnemyHealth` handles Boss defeat: the first Boss preserves run inventory and moves to the next stage; final victory clears progress and returns to the menu. `StoryDialogueController` plays scene-authored dialogue, comics, and one-shot tutorials.

## Boss movement and collision pipeline

- Outside a hop, the Boss keeps a dynamic `Rigidbody2D` with gravity and vertical collision response, but freezes horizontal physics motion. Player contact and residual velocity cannot slide it along a platform. During a pursuit or authored retreat hop, the navigator releases horizontal motion, temporarily disables gravity, and advances the existing parabolic route through `Rigidbody2D.MovePosition`.
- The hop endpoint restores gravity, clears horizontal velocity, and freezes horizontal motion while the collider settles onto a platform. Navigation waits for ground contact or the authored landing timeout before choosing another jump. An attack interruption follows the same landing path; an explicit retreat hop continues during its owning attack. A blocked segment also cancels the hop and lets gravity settle the Boss before replanning.
- `EnemyAttackController` still owns attack selection and timing. `EnemyHealth` disables Boss physics on defeat. Movement changes do not alter damage, attack release timing, jump speed, or jump height. Verify collision routing and contact stability with `BossRelocationPlayModeTests`, then play both campaign Boss arenas to inspect platform landings and wall approaches.

## Boss animation asset and editor pipeline

- The first-stage Wizard is an instance of `Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab`. Its visual child owns `SpriteRenderer` and `Animator`; `BossStateMachine` on the root stores their references and release-frame settings. The second-stage King is authored directly in `stage2_full.unity` with the same arrangement.
- Each Controller contains Idle, Run, Attack1–3, Hurt, and Death. `Moving` controls the Idle/Run transitions. The cast states are selected and sampled by `BossStateMachine` because the skill coroutine owns the exact release instant. Sprite clips contain only the visual child's `SpriteRenderer.m_Sprite` track; they do not animate the Boss root or collider.
- `BossWizardBuilder` and `KingBossBuilder` wire the Animator Controller, SpriteRenderer, release frames, attack patterns, hit flash, and relocation when rebuilding their respective content. `StoryChapterBuilder` reads the Wizard portrait sprite from the prefab renderer.
- To verify in Unity, run `Tools/Boss/Validate Campaign Animators` (or `-executeMethod BossAnimatorValidation.Validate` in batch mode), then run `BossAnimatorPlayModeTests` and the Wizard cast test in `AttackDemoPlayModeTests`. The editor check also opens the older `Assets/Scenes/Legacy/stage1 boss.unity`; legacy scenes are development fixtures rather than build scenes.

## Controls

Move: A/D or arrows; jump: Space/W/Up; dash: Shift; melee: J; kunai: I; bag: B; forge: N; pause: Esc. Contextual prompts in the game describe chest and story interactions.

## Maintenance

See [TODO.md](TODO.md) for legacy design and tooling issues discovered during the Boss animation and physics work.
