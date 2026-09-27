# Technical debt

Items observed while migrating the campaign Bosses to Unity Animator and improving their collision behavior. These are follow-up work, not changes to the current combat balance.

- [ ] Remove the unused `BossSpriteAnimator` runtime class and references in recovery snapshots after the snapshots are archived or retired. The active Boss prefab and stage no longer use it.
- [ ] Make Boss editor builders update existing visual children without destroying and recreating them. Recreating children can invalidate scene and prefab overrides, so the rebuild tools currently need reference validation after each run.
- [ ] Move Boss release-frame metadata into a single validated animation asset or editor validation pass. It is currently stored alongside clip frame counts on `BossStateMachine` and must match the Controller's clips.
- [ ] Review legacy Boss scene tests that still exercise the old standalone arena; keep them as explicit compatibility tests or migrate their gameplay assertions to `stage1_full`.
- [ ] Standardize keyboard handling. `PlayerDropThrough` still uses the legacy `Input.GetKeyDown`, while `Role` and UI use the Input System.
- [ ] Define durable save semantics for continuing stage two after restarting the application. The stage marker persists in `PlayerPrefs`, while inventory, equipment, unlocked abilities, and forge levels are session state.
- [ ] Replace deprecated `FindFirstObjectByType`, `FindObjectOfType`, and `FindObjectsByType` sort-mode calls in editor builders and navigation code with queries whose ordering semantics match the intended selection. Unity 6000.5 compilation reports these calls as obsolete.
- [ ] Replace the obsolete `TMP_Text.enableWordWrapping` use in `NarrativeAudioBuilder` with the current wrapping setting.
- [ ] Reconcile `KingBossBuilder.Validate` with the saved stage-two combat tuning. The validator requires radial orbit speed at least 360, while `stage2_full` saves 72; it also expects a retreat after every attack while the scene currently saves two attacks per retreat. Decide which balance values are authoritative before changing them.
- [ ] Scope `EnemyPlatformNavigator` navigation nodes to each Boss arena. It currently discovers every `EnemyNavigationNode` in the loaded scene, so a future stage with nearby separate arenas could accidentally link them.
- [ ] Centralize Boss relocation and ground placement through the navigator. `BossTeleport` blink and `KingGroundCleavePattern` still move the root directly; future movement features should share one collision and landing policy.
