# PROJECT_STATUS.md - Handoff for the next Claude

Written 2026-10-08 from a live inspection through Unity MCP. Every item is tagged:
- [VERIFIED] read directly from the Unity project this session
- [OWNER BRIEF] stated by the project owner in chat, not checkable in the project
- [NOT VERIFIED] could not be checked; do not assume

If this file and the project disagree, trust the project and fix this file.

---

## 1. Game overview
- Unity 6000.4.1f1, URP, active scene `Assets/Scenes/MainGame.unity` [VERIFIED]
- Student project, deadline November 2026 [OWNER BRIEF]
- Humorous Vietnamese-style 3D game, first-person Player who finds a special chicken [OWNER BRIEF]
- NOT a gambling game: no betting. A win gives a medal that is later exchanged at a Reward NPC [OWNER BRIEF]
- Planned loop (only the first half exists): discover chicken -> first battle -> LOSE -> quests/scrap -> money -> training equipment -> train -> rematch -> WIN -> medal -> Reward NPC -> next challenge [OWNER BRIEF]

## 2. Team responsibilities
- [NOT VERIFIED] Names and the split between the two developers are not recorded anywhere in the project.
- [OWNER BRIEF] Two developers work in parallel; the owner has limited AI usage, so keep tool use targeted and avoid re-auditing.
- Claude's role so far: inspect and fix through Unity MCP. Claude does not have Git access (see section 14).

## 3. Current gameplay flow (what works end to end)
1. Scene starts. ChienKe_Pet lies on the ground playing the Dead animation. PetCombatAI is disabled, so no follow and no combat.
2. Player walks into the pet's trigger (SphereCollider, isTrigger) and presses E (legacy `Input.GetKeyDown`).
3. WakeUp plays (the Dead clip reversed). When the Animator reaches ChickenIdle, the pet becomes MainChicken and PetCombatAI is enabled, so it follows the Player.
4. Player walks into the battle zone (SphereCollider trigger on GuguGaga with BattleTrigger). The trigger only fires if the chicken is already MainChicken.
5. BattleManager.StartBattle(): first-battle damage override applied (pet 1, enemy 15), listeners added to both HealthSystem.OnDeath.
6. Existing combat AI fights by proximity. Pet dies via HealthSystem, BattleManager sets PlayerLost, restores damage, sets `BattleManager.firstBattleCompleted = true`, state becomes Completed. The pet is not destroyed.
7. Nothing happens after that yet (no quest, no dialogue). That is the Week 2 hook.
All of the above was exercised in Play Mode earlier this session by driving the same code paths, but NOT with real keyboard input (see section 15).

## 4. Completed systems [VERIFIED present and compiling; console had 0 project errors at audit time]
- Player movement and first-person camera
- Chicken combat: follow, aggro, chase, attack, cooldown, damage, death
- HealthSystem with idempotent death and OnDeath event
- Health bars (two HealthBarUI objects in the scene)
- Main chicken wake-up and acquire flow
- BattleManager, BattleTrigger, first-battle loss and balance override/restore
- Idle push-up behavior (code and Animator wiring present)

## 5. Partially completed
- Push-up idle routine: code in PetCombatAI plus Animator transitions exist, never visually confirmed in Play Mode.
- Wake-up animation: logic verified, visual quality unverified (reversed Dead clip, state speed -2).
- Interaction: only a bare E-key proximity check on the chicken. There is no on-screen [E] prompt. The older InteractableObject/SelectionManager system is for item pickup and is NOT used for the chicken.
- Inventory: exists (InventorySystem, ItemSlot, DragDrop), not audited in depth.

## 6. Important scripts (`Assets/Script/`, 16 files) [VERIFIED list]
- PetCombatAI: pet brain. States Follow and Combat. Follow also runs the 10 s idle push-up routine. Uses NavMeshAgent.isStopped, an Animator state tag `Attack` as attack lock, OverlapSphere from `attackPoint` via the animation event `OnAttackHit`. Drops its target as soon as the target HealthSystem.IsDead.
- RoosterCombatAI: enemy brain. Idle (returns to spawn) and Combat. Same fight logic, targets tag `Pet`.
- HealthSystem: health authority. TakeDamage, Heal, IsDead, OnHealthChanged, OnDeath, interface IStopOnDeath (defined in the same file). On death it sets Animator `isDead`, disables NavMeshAgent and colliders, calls StopCombat on IStopOnDeath components.
- HealthBarUI: world-space health bar bound to a HealthSystem.
- MainChickenAcquire: Down -> WakingUp -> MainChicken state. Disables PetCombatAI and sets isDead=true on Awake. Exposes `Instance` and `IsAcquired`.
- BattleManager: battle state enum (None, Preparing, InBattle, PlayerWon, PlayerLost, Completed), first-battle damage override and restore, static `firstBattleCompleted`.
- BattleTrigger: starts the battle when the Player enters and the chicken is acquired.
- PlayerMovement: CharacterController movement using legacy Input.
- MouseMovement, AI_Movement: not inspected this session.
- FollowPlayer: no longer attached to ChienKe_Pet; follow logic now lives in PetCombatAI. Kept, not deleted.
- InteractableObject, SelectionManager, InventorySystem, ItemSlot, DragDrop: item pickup and inventory prototype, untouched.
- `Assets/Editor/AnimationEventCleaner.cs`: editor menu that strips animation events with an empty function name.

## 7. Current scene structure (MainGame, 15 root objects) [VERIFIED]
- Directional Light, Global Volume, Terrain (layer 6, TerrainCollider, NavMeshSurface)
- Player: tag `Player`, CharacterController + PlayerMovement; children GroundCheck, Main Camera, Cylinder, Follow
- ChienKe_Pet: tag `Pet`; Animator, NavMeshAgent, BoxCollider, SphereCollider(trigger), PetCombatAI, HealthSystem, MainChickenAcquire; children Armature, obj1, AttackPoint
- GuguGaga: tag `Rooster`; Animator, NavMeshAgent, BoxCollider, SphereCollider(trigger), RoosterCombatAI, HealthSystem, BattleTrigger; children Armature, node_0, AttackPoint. THIS is the live enemy.
- Canvas, EventSystem (InputSystemUIInputModule), SelectionManager, InventorySystem
- HealthBarUI and HealthBarUI (1)
- BattleManager (references to both HealthSystem and both combat AIs are assigned)
- INACTIVE leftovers: `Rooster` (tag Untagged, has combat scripts, no AttackPoint child) and `ChienKe_blend` (tag Rooster). Not used. Do not edit them by mistake.
- The scene was saved at audit time (isDirty = false).

## 8. Current asset structure [VERIFIED]
Assets/: _Recovery (8 recovery scenes), Animations (Chicken/, Enemy/, clips, controllers), Art (Character, Chicken, Enviroment, Models, Props, Textures, UI, ui_art, VFX), Audio, Editor, Materials, Prefabs (Item/, Model/), Scenes (MainGame/, MainGame.unity), Script, Settings, TextMesh Pro, TutorialInfo, UI, VFX, Documentation (created with this file).
- Prefabs: Prefabs/Model/ChienKe_Pet.prefab, Prefabs/Model/GuguGaga.prefab, Prefabs/Player.prefab. [NOT VERIFIED] whether prefab assets contain the scene-level changes made this session (tags, triggers, MainChickenAcquire, BattleTrigger); they were edited on scene instances.
- Animator controllers: Chicken.controller (used by both chickens). ChickenPushUp.controller has no parameters and no users in the scene.
- Clips: ChickenIdle, Run, Dead, IdleToPushUp, Push Up, PushUpToIdle, Kick 1, Kick2-5, Hurricane Kick, Ninja Idle.
- Chicken.controller parameters: isRunning (bool), isDead (bool), Attack (trigger), AttackIndex (int), IsPushing (bool), WakeUp (trigger).
- Chicken.controller: Kick1-5 and Hurricane Kick states have tag `Attack`. Push Up -> PushUpToIdle uses `IsPushing IfNot`, no exit time. WakeUp state reuses the Dead clip at speed -2. Dead is a non-looping terminal state apart from the WakeUp transition.

## 9. Combat status
- Working and treated as frozen (owner said combat was tested and stable).
- Live values: ChienKe_Pet HP 100, damage 10, attackRange 1, cooldown 1.5, detect 6, leash 12, agent speed 8. GuguGaga HP 150, damage 18, attackRange 1, cooldown 1.5, detect 6, leash 12, agent speed 3.5, stoppingDistance 0.
- First battle only: BattleManager sets pet damage 1 and enemy damage 15, then restores 10 and 18.
- Known open problem: see section 15, item 1 (pet stoppingDistance).

## 10. Chicken wake-up / follow status
- Implemented in MainChickenAcquire.cs (Down / WakingUp / MainChicken). E only works while Down; repeat presses are ignored.
- Follow starts only after the Animator reports ChickenIdle (polled, not a timer).
- Verified in Play Mode by driving the same Animator calls: Dead -> WakeUp -> ChickenIdle, then PetCombatAI enabled.
- NOT verified: real E keypress, how the reversed animation looks, follow distance and jitter feel.
- Because PetCombatAI is saved as enabled in the scene, removing MainChickenAcquire would make the pet follow from the start.

## 11. Quest status
Not started. No quest script, data or UI exists in Assets/Script. [VERIFIED by script list]

## 12. Scrap collection status
Not started. [VERIFIED by script list]

## 13. Money / currency status
Not started. No money, shop or economy script exists. [VERIFIED by script list]

## 14. Git branch / status
- [NOT VERIFIED] Branch, status and diff cannot be read through Unity MCP.
- The Editor title bar showed a Unity VCS menu, which suggests Unity Version Control rather than git. Not confirmed.
- The owner must check pending changes locally before committing. Nothing has been committed by Claude.
- Files created or changed this session: HealthSystem.cs, PetCombatAI.cs, RoosterCombatAI.cs, HealthBarUI.cs, MainChickenAcquire.cs, BattleManager.cs, BattleTrigger.cs (all in Assets/Script), Chicken.controller, Assets/Documentation/PROJECT_STATUS.md, plus scene changes (Player tag, trigger colliders, BattleManager object, stats). Assets/Editor/AnimationEventCleaner.cs exists but was added outside this verified session history.
- Note for editing scripts via MCP: manage_script supports only create, read and delete. To overwrite a script you must delete then create, which changes the file GUID and leaves a missing-script slot on scene objects. Clean those with GameObjectUtility.RemoveMonoBehavioursWithMissingScript and re-check the console.

## 15. Known bugs / issues [VERIFIED unless stated]
1. ChienKe_Pet NavMeshAgent.stoppingDistance is currently 10 while attackRange is 1. An earlier session set it to 0.5 to fix the pet stalling short of the target; it has been changed back to 10 by someone else since. This will likely make the pet stop far from the enemy again. Needs the owner's decision before changing, because it may have been changed on purpose.
2. Manual keyboard test (real E, real WASD) has never been done. Everything was tested by driving code paths.
3. In the one recorded first-battle run the pet landed 0 hits (enemy stayed 150/150). Cause not investigated.
4. Wake-up and push-up animations were never judged visually.
5. Input is mixed: activeInputHandler = Both. PlayerMovement and the E key use the legacy Input class, the EventSystem uses the new Input System module. `InputSystem_Actions.inputactions` is unused by gameplay scripts. Console shows a deprecation warning for Input Manager.
6. PlayerMovement has `velocity.y =- 2f;` (assignment, not subtraction). It happens to keep the player grounded; leave it unless it causes a problem.
7. Both chickens have Animator applyRootMotion = true while NavMeshAgent also moves them. A past report said hands and feet sink into the ground during Push Up. A fix (bake Y into pose in clip import settings, or turn off Apply Root Motion) was suggested but NOT applied or tested.
8. Pet and enemy NavMeshAgent radii are small (0.13 and 0.2).
9. Inactive duplicate objects Rooster and ChienKe_Blend, plus Assets/_Recovery scenes, are clutter.
10. HealthBarUI targets and the stored radii of the two trigger colliders were not re-read in this audit.
11. Console at audit time: no project errors. Only editor noise (Input Manager deprecation, Account API timeout, Windows signature message, MCP connection state change). The old `AnimationEvent has no function name specified` spam was not seen in this audit, but it was not re-tested in Play Mode either.
12. MCP calls sometimes fail with Unity is reloading or No Unity Editor instances found. Retry after a few seconds.

## 16. Things that MUST NOT be changed (owner directives)
- Do not replace or rewrite the combat system (PetCombatAI, RoosterCombatAI, HealthSystem) unless there is a direct blocking bug. Make the smallest possible fix and report it.
- Do not change global damage values. First-battle balance stays inside BattleManager (override then restore).
- Do not force the first loss with fake HP, instant death, Destroy or disabled attacks. The loss must come from real damage.
- Do not destroy the MainChicken. Do not show Game Over or return to the main menu after the first loss.
- No betting or gambling mechanics.
- Do not restructure folders, change Unity version or render pipeline, install packages, or download assets.
- Do not delete files just because they look unused (FollowPlayer.cs, ChickenPushUp.controller, Rooster, ChienKe_blend, _Recovery).
- Keep Animator parameter names (isDead, isRunning, Attack, AttackIndex, IsPushing, WakeUp) and the `Attack` state tag.
- Do not touch teammate-owned systems (inventory, interaction) without checking first.

## 17. Exact next task
[OWNER BRIEF] Week 2 starts with the Quest system hooked to `BattleManager.firstBattleCompleted` (and BattleManager.CurrentBattleState). Before any new feature, do a short manual playthrough with real keyboard and mouse and settle bug 1 (pet stoppingDistance). Do not start Quest, Scrap, Money, Shop or Training work until the owner confirms the playthrough result.

## 18. Recommended next steps
1. Owner plays the flow once manually: approach pet, press E, watch wake-up, follow, enter the battle zone, watch the loss. Report anything that looks wrong.
2. Decide the pet stoppingDistance value (bug 1) and check that the pet closes to attack range.
3. Run Tools > Cockfighting > Strip Empty Animation Events on the idle, run, kick and dead clips if the empty-event error returns.
4. Fix the push-up sinking (bug 7) if it is visible.
5. Remove or clearly rename the inactive Rooster and ChienKe_blend objects only with the owner's approval.
6. Then build the minimal Quest hook off `firstBattleCompleted`, and nothing more in that task.
7. Check Git or Unity VCS status locally and commit in small pieces.

## How a new Claude should start
1. Check Unity MCP is connected (manage_editor telemetry_status, read scene hierarchy, read console).
2. Read this file, then only the scripts for the task at hand. Do not repeat the full audit.
3. Follow inspect, small change, test, and report only what was really tested in Unity.
