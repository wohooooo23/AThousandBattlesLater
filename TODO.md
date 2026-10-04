# Technical debt

- [ ] Extend integrated wall candidate coverage for r8: 113×63, seed 10048, crimson rune, route multiplier 2.5, density 0 is feasible in r7 but currently rejected in r8. Overall retention is 1218/1219 (99.918%); keep the 40–60% integration and branch-count contracts when improving this case.

- [ ] Broaden the fixed-length main-route grammar without weakening motion/clearance contracts: r7 independent validation found 41/360 parameter combinations with no baseline route domain (before branches). Keep explicit failure and old-map preservation; never add a runtime post-generation repair/search.

- [x] Remove the obsolete compact-only raw platform-minimum assertion in r8. Runtime selects the fewest landings among complete wall/branch candidates; development validation now checks integrated support, wall ownership and every affected action.

- [ ] Audit the legacy `Assets/Prefab/Coin.prefab`: its ItemPickup points to a missing ItemData GUID. Random maps now use a dedicated corrected `CoinPickup.prefab`; check campaign usages before changing the shared legacy asset.

- [ ] Reconcile forge price comments with the actual `200 / 2` first-level calculation (100 coins); preserve current costs until a balance change is requested.
- [ ] Define cancellation/resume behaviour when closing the existing forge panel during its animation; stopping its coroutine on deactivation can leave the internal busy flag set.
- [ ] Extend the generated loot table from deterministic priority allocation to weighted recipes, preserving pickup claims and retry snapshots.

Items observed while migrating the campaign Bosses to Unity Animator and improving their collision behavior. These are follow-up work, not changes to the current combat balance.

- [ ] Remove the unused `BossSpriteAnimator` runtime class and references in recovery snapshots after the snapshots are archived or retired. The active Boss prefab and stage no longer use it.
- [ ] Make Boss editor builders update existing visual children without destroying and recreating them. Recreating children can invalidate scene and prefab overrides, so the rebuild tools currently need reference validation after each run.
- [ ] Move Boss release-frame metadata into a single validated animation asset or editor validation pass. It is currently stored alongside clip frame counts on `BossStateMachine` and must match the Controller's clips.
- [ ] Review legacy Boss scene tests that still exercise the old standalone arena; keep them as explicit compatibility tests or migrate their gameplay assertions to `stage1_full`.
- [x] Consolidate Hero melee, kunai, health, combo audio and one-way platform dropping in `Role`; drop input now uses the Input System and restores ignored collisions on disable.
- [ ] Review stage-two Hero tuning: the saved scene has 20000 maximum HP versus stage one's 200 HP. Keep the current values until the intended balance is confirmed.
- [x] Separate Hero base attack from effective attack: Role adds its Inspector base, equipped weapon attack and forge bonus without overwriting the base; startup/equipment/forge refreshes preserve separate multipliers, and forge attack previews use the same calculation.
- [ ] Review Hero attack scene overrides: stage one saves base attack 2000 and stage two saves 200. The additive formula now honors these authored values, which the old unarmed-10 write-back masked.
- [ ] Make Hero base defense configurable and separate it from effective defense. PlayerProgression still uses a hardcoded unarmored defense of 2 and replaces it with armor/forge defense.
- [ ] Clean up `Entity` collision probes: it keeps a large commented alternative implementation and unused probe fields alongside the active raycasts. Establish one null-safe ground/wall probing implementation and verify scaled actors and platform seams.
- [ ] Define durable save semantics for continuing stage two after restarting the application. The stage marker persists in `PlayerPrefs`, while inventory, equipment, unlocked abilities, and forge levels are session state.
- [ ] Replace deprecated `FindFirstObjectByType`, `FindObjectOfType`, and `FindObjectsByType` sort-mode calls in editor builders and navigation code with queries whose ordering semantics match the intended selection. Unity 6000.5 compilation reports these calls as obsolete.
- [ ] Replace the obsolete `TMP_Text.enableWordWrapping` use in `NarrativeAudioBuilder` with the current wrapping setting.
- [ ] Reconcile `KingBossBuilder.Validate` with the saved stage-two combat tuning. The validator requires radial orbit speed at least 360, while `stage2_full` saves 72; it also expects a retreat after every attack while the scene currently saves two attacks per retreat. Decide which balance values are authoritative before changing them.
- [ ] Reconcile `DemoSceneBuilder.ValidateFullMapStage` with the saved Hero spawn. Its "upper starting area" assertion fails on the existing `stage1_full` spawn, which the navigation migration does not change.
- [ ] Remove the legacy `EnemyNavigationNode` class and the old standalone Boss-room builder's node creation once the legacy scene tests have been migrated. Neither campaign Boss uses scene-authored navigation nodes now.
- [ ] Extend `BossLandingGraph` to track continuously moving platforms and edits within an existing Tilemap collider. The current geometry signature detects collider creation, removal, activation, and bounds changes; a Tilemap edit that leaves bounds unchanged needs `NotifyGeometryChanged` or a dedicated change signal.
- [ ] Make surface sampling density and the 256-spot budget configurable, and replace left-to-right truncation with coverage across the arena when the budget is exceeded. Current campaign arenas stay below 100 spots.
- [ ] Centralize Boss relocation and ground placement through the navigator. `BossTeleport` blink and `KingGroundCleavePattern` still move the root directly; future movement features should share one collision and landing policy.

- [x] Migrate stage-one/two Flying Eyes to Orc-style Enter/Update/Exit classes and a parameter/Entry/Exit Animator on the preserved Visual; attack release/recovery now follows animation events and Hurt interrupts pending shots.
- [x] Migrate Mushroom and Skeleton to EntityState classes and Visual parameter/transition Animators; retain authored combat timing, no-stun rule, prefab IDs and scene overrides, and add both to generated mixed camps.
- [ ] Migrate Goblin from MobStateMachine/MobSpriteAnimator to the same state/Animator pattern when its combat design is specified.
- [x] Adopt Mushroom/Skeleton warnings, strikes and clouds into the generated map, guard generated damage by region/visibility, and release transient sector meshes/materials on cleanup.
- [ ] Reconcile stage-two combat builder defaults with saved prefab tuning. The legacy builder still deliberately reapplies 0.8/0.42-second windups and shorter cooldowns, whereas current authored Mushroom/Skeleton prefabs use 1.6/0.8-second windups and 2/2.5-second cooldowns. Animator migration preserves saved values; do not run the rebalance builder merely to update animation assets.
- [ ] Migrate Boss animation switching to the same parameter/transition architecture. Bosses currently use Unity clips but still select/sample cast states in code; their skill-owned release timing needs an explicit migration contract.
- [ ] Reconcile broad campaign test and builder assumptions with current authored content: DemoSceneBuilder and FullMapStagePlayModeTests expect six first-stage eyes (the saved scene has seven), unified 20-coin rewards (the eye prefab currently saves 80), and older spawn/population layouts. Keep current authored counts, rewards and positions until the intended level tuning is decided.
- [x] Update the eye assertions that depended on the old 0.95-second windup and 1.35-second cooldown. Release now follows frame seven (0.5 seconds Normal/0.3 Hard); the saved prefab cooldown is 2 seconds before difficulty scaling. The ground-mob smoke test now respects their existing no-stun behavior and implemented Mushroom/Skeleton attacks.
- [ ] Remove unused legacy flying/Hurt branches from MobStateMachine once all remaining users and historical fixtures are migrated; FlyingEyeController no longer uses that component.
- [ ] Consolidate damage-notification routing in Enemy_Health. It currently probes separate Orc, legacy Mob and Flying Eye controllers; a common reaction interface would simplify later species migrations.

## Random-map research follow-up

- [x] Add an isolated 20x20 WFC room scene with seeded direct generation, platform compatibility constraints, protected jump corridors, separated rectangle walls and a non-combat Boss-door exit. Preserve the authored campaign scenes.
- [ ] Extract reusable map data, room placement, spawn placement, camera bounds and minimap setup from DemoSceneBuilder before integrating generated rooms with the campaign. Its room targets and ability-chest coordinates are currently authored for the fixed campaign maps; keep those campaign presets as a separate input.
- [ ] Make surface/spawn validation use real collider shapes, actor bounds and tile semantics. FindFullMapSurfaceSpawn currently treats every occupied cell in a collider-equipped Tilemap as a candidate floor and uses caller-supplied clearance dimensions; generated Spike/invisible-wall cells and one-way platforms need distinct rules.
- [x] Extend generation-time module compatibility and endpoint/clearance constraints to branching rooms, double-jump/dash gates and effective Hero profiles in the separate WfcDungeon scene. Per the direct-output requirement, do not add a post-generation reachability filter; verify new constraint contracts with offline physics tests. BossLandingGraph remains Boss-specific, not the generated-map pipeline.
- [ ] Review Role.ApplyRuneMovementStats: crimsonEquipped currently tests RunEquipment.Rune != null, so any equipped rune activates the Crimson movement/jump/dash multipliers. Generated-map reachability must not depend on an unintended equipment bonus; identify the intended rune before deriving movement profiles.
- [x] Make Role.ResetToIdlePose safe before Role.Start: Awake creates states but StateMachine.Change assumes an existing current state. ResetToIdlePose now initializes the first state when necessary.
- [x] Make Hero ground-state input transitions exclusive: Hero_idleState/Hero_moveState continue after the base Update changes to a jump/attack state, so same-frame input can overwrite that transition. Ground/air subclasses now stop after a base transition; attack-to-jump returns immediately as well. Dungeon tests exercise simultaneous movement/jump input.
- [x] Preserve the small WFC regression scene and add a separate multi-room library of independently tested platform/room modules, including entry/exit contracts and per-ability clearance. Add decorative wall sides/bottoms and responsive camera framing when supporting room sizes beyond the initial 20x20 scene.


## Multi-room generation follow-up

- [x] Add an isolated multi-room scene, effective Hero traversal snapshot, mandatory ability modules, tree branches, explicit wall edge palette, white smooth walls, room-bound Orc/Flying Eye populations, owned attack cleanup and campaign camera follow.
- [ ] Include kunai hover and its inventory/animation interruption rules in a future traversal contract. Explicitly excluded from this iteration at the user's request; no generation restriction is imposed on carried kunai.
- [ ] Expand the action-module library and visual room variety. The first multi-room implementation constructs the main chain before room WFC; required challenge shapes are parameterized templates, not unrestricted learned tile patterns.
- [ ] Consolidate opt-in generated ground probing with the campaign probes after campaign-specific collision regression. The generated version rejects rays originating inside one-way platforms, preventing false landing/jump resets during upward passage; existing campaign grounding remains unchanged.
- [ ] Replace per-frame optional enemy-bound component lookups with cached optional references if profiling shows a measurable cost at higher densities.
- [ ] Extend parameter/physics coverage whenever movement states change: current dash exclusivity uses gravity-preserving dash and conservative air/combat speed; new vertical boosts or gravity changes require updated domains before enabling them in generated challenges.

- [x] Avoid merging vertically adjacent platform tiles into a solid staircase side. Generated dungeons now retain separate thin one-way landing colliders; the 20x20 reference keeps its existing collider setup.


## Single-room follow-up

- [x] Replace automatic wall caps/wings with jointly admitted wide top-support and tall side-climb route modules. Render solid-top landings once, preserve fractional geometry, and reserve climb corridors from barriers and camps.
- [ ] Extend the current alternating-leg grammar with authored large hanging walls, underpasses and multi-bend obstacle groups matching the concept sketch more closely. Preserve fixed nominal length and pre-collapse motion constraints; ordinary walls still permit player shortcuts.

- [x] Review C1: retain Flying Eye detectionRange alongside generated camp/safe-zone/visibility restrictions, including cached-target decisions; 11/11 eye Play Mode tests passed on 2026-10-03.

- [x] Audit A1: retain the user-approved sparse/compact route design; remove the universal horizontal 75% promise and report actual final shelf-edge gaps separately from centre distance. The first jump from continuous ground has no artificial edge metric.
- [x] Audit A2: share Up/Flat/Down/Dash validation between the builder and data regression; use explicit current sizes and supported settlement rules. Save the target scene only after validation and preview construction, and fix startup seeds before tests capture the Hero.
- [x] Fix the wall-jump regression driver: root height did not guarantee foot clearance at the shaft lip. Check actual collider feet with a small collision margin before applying exit-walk inputs.

- [x] Replace the default room grid with one seeded 100–150 by 50–100 winding room, a configurable fixed route-length multiplier, sparse action-budget jumps, fractional ledges and ordinary walls only. Retain the multi-room solver as an explicit reference mode.
- [x] Add descending route segments, variable-width platforms and separated enemy settlements with explicit action and clearance contracts.
- [ ] Extend authored combat arenas and campaign-like boundary protrusions; do not add runtime post-generation reachability repair.
- [ ] Make builder settings/material/tile changes transactional as well as scene saving. Failed dungeon validation now preserves the target scene, but shared assets can still become dirty before the validation gate.
- [ ] Add more art variants for isolated single-cell exterior borders: the nine-part palette can express a rectangle, but a one-cell-thick shell shares opposing edge conditions. Dedicated thin-border art would avoid choosing one edge priority.
- [ ] Consolidate duplicated standalone test-scene setup and reflection-based Hero input helpers. Existing multi-room tests must explicitly request that retained mode now that WfcDungeon defaults to one room.
- [ ] Broaden movement-contract fixtures for non-default gravity, fixed timestep, jump/dash durations and body scale before treating arbitrary equipment profiles as supported. Report incompatible profiles rather than silently weakening jump utilization.
