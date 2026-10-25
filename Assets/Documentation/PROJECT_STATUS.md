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
(Updated Week 3. The original 'not started' text was stale.) A quest system exists in `Assets/Script/Quest/` (QuestManager, QuestDefinition, QuestNPC, QuestHUD, Que## 13. Money / currency status
(Updated Week 3.) `Assets/Script/Economy/CurrencyWallet.cs` (earn only, no spending) and MoneyHUD exist. Quest rewards go through `CurrencyWallet.Add`. Spending now exists: `CurrencyWallet.TrySpend` / `CanAfford` and the tool shop, see section 25.

d Week 3.) Scrap system exists in `Assets/Script/Scrap/` (ScrapPickup, ScrapCollector, ScrapTypeDefinition); 12 pickups are in the scene. Collecting 10 and turning in was exercised in Play Mode, see section 19.

## 13. Money / currency status
Not started. No money, shop or economy script exists. [VERIFIED by script list]

## 14. Git branch / status
- [VERIFIED 2026-10-10 by reading .git] The project uses Git (no .plastic folder). Branch `feature/Character-Pet-Enemy`. Last commit: `fix character combat` at 2026-10-09 10:56 (+07). `git status` / `git diff` cannot be run from Unity MCP, so exact pending changes are NOT verified; see section 20 for what the index and file timestamps show.
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
(SUPERSEDED: Week 2 and Week 3 Task 1 work now exists, see sections 19 and 20. The text below is the original 2026-10-08 plan.)
[OWNER BRIEF] Week 2 starts with the Quest system hooked to `BattleManager.firstBattleCompleted` (and BattleManager.CurrentBattleState). Before any new feature, do a short manual playthrough with real keyboard and mouse and settle bug 1 (pet stoppingDistance). Do not start Quest, Scrap, Money, Shop or Training work until the owner confirms the playthrough result.

## 18. Recommended next steps
1. Owner plays the flow once manually: approach pet, press E, watch wake-up, follow, enter the battle zone, watch the loss. Report anything that looks wrong.
2. Decide the pet stoppingDistance value (bug 1) and check that the pet closes to attack range.
3. Run Tools > Cockfighting > Strip Empty Animation Events on the idle, run, kick and dead clips if the empty-event error returns.
4. Fix the push-up sinking (bug 7) if it is visible.
5. Remove or clearly rename the inactive Rooster and ChienKe_blend objects only with the owner's approval.
6. Then build the minimal Quest hook off `firstBattleCompleted`, and nothing more in that task.
7. Check Git or Unity VCS status locally and commit in small pieces.

## 19. Week 3 Task 1 - Meme money quests (done through Unity MCP, NOT committed)
Tags: [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths, with NO real keyboard or mouse. [NOT VERIFIED] = not tested.

Quests (assets in `Assets/Data/Quests/`, rewards and amounts are plain fields on the asset):
- `scrap_collector` - Ve Chai Dai Chien: 10 scrap, 100 coins. This is the EXISTING first quest asset `Quest_EarnMoneyForTraining.asset` reconfigured (questId changed from `earn_money_for_training`, new title). No other script referenced the old id.
- `grandparents_helper` - Osin Cua Ba: 3 chores, 150 coins. Giver: NPC_Grandmother.
- `village_delivery` - Shipper Lang Que: 3 deliveries, 200 coins. Giver: QuestNPC_Villager (second quest after scrap_collector). (SUPERSEDED by section 24: now 1 delivery, giver NPC_Grandmother.)

Framework changes:
- QuestDefinition: new `repeatable` (default false), `turnInObjective`, optional per-quest NPC dialogue arrays.
- QuestManager: `Cycle` and accept order per quest, keys `ChoreObjective` and `DeliveryObjective`, repeatable quests return to NotStarted only after payout, HUD falls back to the most recently accepted running quest.
- QuestNPC: offers only its `quest` plus `additionalQuests`; picks ReadyToTurnIn, then Active, then NotStarted, then Completed; optional `idleLines` small talk.
- New `QuestObjectiveTarget`: one-shot interaction point per quest cycle. Only reacts while its quest is Active. Never pays money.
- VillagerFlavorNPC: small hook so it yields the E key while a QuestObjectiveTarget on the same NPC is waiting. (Teammate-owned script, please review.)
- NPC_Grandmother: VillagerFlavorNPC replaced by QuestNPC; her two old lines are kept as idleLines (shown before the first battle loss).
- Scene: new root `QuestObjectives` with placeholder cylinders Chore_QuetSan, Chore_GanhNuoc, Chore_PhoiDo (no colliders) in the yard east/south of the grandparents. Delivery targets sit on NPC_Grandfather, NPC_ChildhoodFriend, NPC_MarketVillager. Scene saved.

Verified [VERIFIED-PLAY]: nothing counts before accept; targets are inert before accept; each target counts once; progress clamps; reward paid exactly once per quest; repeat TurnIn and re-accept refused for one-time quests; wrong-quest targets stay inert; HUD title, objective and progress follow the tracked quest and hide after completion; HUD falls back from a newer to an older running quest; repeatable path (tested with a throwaway in-memory quest, nothing saved) pays each cycle once; opening dialogue on every QuestNPC changed the balance by 0; each NPC reports only its configured quests. Compile clean, Console had no project errors.

[NOT VERIFIED]: real keyboard E play-through; the on-screen prompts and dialogue for the new quests; VillagerFlavorNPC yielding the E key in range (no frame-by-frame test); chore placeholder positions and look in the scene view; whether the first battle loss unlocks the grandmother offer in a real run; combat and chicken follow were not touched and not re-tested.

Known limits: delivery has no inventory item or recipient AI, it is a placeholder interaction. One NPC talks about one quest at a time. If every quest of an NPC is completed it shows the first quest's completed lines.

## 20. Week 3 audit - scrap, quest progress, NPC turn-in, payout, Money HUD (2026-10-10)
Audit only, nothing rebuilt. [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (Interact, TryCollect, dialogue action button); NO real keyboard or mouse was used. [READ] = read from scripts or scene. [NOT VERIFIED] = not tested.

Implemented and working [VERIFIED-PLAY]:
- Scrap: ScrapCollector + ScrapPickup + ScrapTypeDefinition. 12 pickups under `ScrapItems` (4 Chai nhua 5, 3 Lon nhom 10, 3 Bia cac-tong 8, 2 Sat vun 15 = 104 total item money). Cannot collect before the quest is accepted; each item counts and pays once; the 11th item still pays its item money but progress stays clamped at 10/10.
- Quest progress: Accept -> 1..10/10 -> ReadyToTurnIn. Banners: accepted, objective done. Quest HUD shows title, objective, `Ve chai: n / 10`.
- NPC turn-in: villager does nothing before `BattleManager.firstBattleCompleted`; afterwards opening the dialogue pays 0; the action button pays +100 exactly once; talking again pays 0 and (since scrap is done) offers village_delivery.
- Payout: quest reward goes through CurrencyWallet.Add only from TurnIn. Per-item scrap money is paid separately at pickup (so a full run earns about 100 reward + the value of the items picked).
- Money HUD: `Tiền: N`, top-right, 320x72 at 1920x1080, yellow, Screen Space Overlay. Text matched the wallet after each of 10 pickups and after the turn-in (0 mismatches).
- Scene wiring [READ]: ScrapCollector, MoneyHUD, QuestHUD, QuestFeedback, QuestManager have no null references; one QuestManager, one CurrencyWallet, one MoneyHUD; 0 missing scripts in MainGame.

Incomplete / not tested:
- [NOT VERIFIED] Any real keyboard play-through (E to pick up, E to talk). The `Nhan E` prompts, dialogue panel and Money HUD have never been looked at on screen by Claude; HUD position is computed from RectTransform only.
- [NOT VERIFIED] The real unlock path: the first-battle loss was simulated by setting the static flag, not by losing the fight.
- [NOT VERIFIED] Pickup reachability and look. Only checked: terrain height 0 and no solid collider within 0.3 m. Pickups have no colliders by design (distance check). Scrap_Can_06 (4,8) and Scrap_Cardboard_07 (8,20) lie inside the pet's trigger sphere.
- Not built (by design for now): spending/shop, save/load (everything resets on Play), money loss, training.
- Known behaviour: after scrap_collector the villager immediately offers village_delivery. No limit on simultaneous active quests; HUD shows the most recently accepted.

Console: no project errors or warnings at audit time. Only editor/MCP notices (Account API timeout, MCP port 6400/6401 fallback, duplicate-command suppression).

Git [VERIFIED by reading .git, not by running git]:
- Branch `feature/Character-Pet-Enemy`; last two commits are `fix: combat of pet and enemy` and `fix character combat`.
- NOT in the git index (so untracked, never committed): PostLossSequence.cs, everything under Script/Quest, Script/Economy, Script/Scrap, VillagerFlavorNPC.cs, and Data/Quests assets. Treat the whole Week 2 and Week 3 feature set as uncommitted.
- In the index but changed since the last commit (file times are later than the commit): MainGame.unity, PROJECT_STATUS.md, and the combat scripts BattleManager, HealthSystem, PetCombatAI, RoosterCombatAI (changed 2026-10-09 12:11 +07, inferred from timestamps; diff not read).
- Nothing was committed or pushed by Claude.

## 21. Week 3 - scrap asset replacement (2026-10-11)
Only visuals were swapped. No C# script was changed. Tags: [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (no real keyboard); [SEEN] = looked at in a camera capture; [NOT VERIFIED] = not tested.

Replaced (the existing prefabs in `Assets/Prefabs/Scrap/` were edited in place, so GUIDs, `ScrapPickup.type` links, scene instances and money values are unchanged):
- `Scrap_PlasticBottle` -> `Art/UI/bottle/source/PlasticBottle1_LP.fbx` (scaled x6.75, 0.32 m tall), material `Materials/ScrapReal_PlasticBottle.mat`.
- `Scrap_Cardboard` -> `Art/UI/box/source/CardboardBox_LP.fbx` (0.45 x 0.23 x 0.37 m), material `Materials/ScrapReal_Cardboard.mat`.
- `Scrap_ScrapMetal` -> `Art/UI/water can/source/Watering_can_lp.fbx` (an old iron watering can, 0.38 m tall), material `Materials/ScrapReal_WateringCan.mat`.
- NOT replaced: `Scrap_AluminumCan`. No can model exists anywhere in the project, so it is still the primitive cylinder can (3 scene items). If a can model is added, or the watering can is preferred for this type, it is a one-prefab swap.

Asset handling:
- Only one file was added: `Art/UI/box/source/CardboardBox_LP.fbx`, extracted from the project's own `CardboardBox.zip` (its textures were already loose in `Art/UI/box/textures`). Zips and every original asset were left in place. The old `Scrap_*` primitive materials are kept (now unused by the three swapped prefabs).
- Three normal textures were switched to import type Normal Map (bottle, box, watering can). They are not used by the materials yet, see below.
- `plastic_water_bottle.glb` cannot be used: no glTF importer is installed (it imports as a generic asset).

Material limitation [VERIFIED]: the three new materials use only the base-colour texture. Enabling `_NORMALMAP` / `_OCCLUSIONMAP` made the editor's shader compiler report `Error out of memory while parsing (D3D)` for URP Lit and the items rendered magenta. Without those keywords they render like the rest of the project. So normal, AO and metallic/specular maps are NOT linked. If this machine's shader compiler problem is fixed, they can be added.

Placement: the existing 12 pickups under `ScrapItems` were kept (12 >= the 10 the quest needs). Types: 4 bottle (5 each), 3 can (10), 3 cardboard (8), 2 scrap metal/watering can (15) = 104 item money. Positions were not moved (map layout untouched); only each pickup's facing was varied. Checked: all on flat TL_VillageDirt or TL_VillageGrass, 0 deg slope, no solid collider within 0.5 m, every one has a complete NavMesh path from the player start (6 to 42 m). The NavMesh is only a proxy for the CharacterController.

Verified [VERIFIED-PLAY]: nothing can be collected before accept; ScrapCollector's own range check picks the right pickup 1 m away; each item pays exactly its type value once, a second TryCollect is refused and pays 0, the item is hidden; Money HUD matched the wallet after every pickup; progress clamps at 10/10; TurnIn paid +100 once and a second call paid 0. Compile clean, Console no project errors. [SEEN] bottle, cardboard and watering can render textured, grounded and upright.

Found, not touched: `ItemPickups/Pickup_VeChai` (11.5, 0, 9.5) is an inventory `ItemPickup` (`Item_VeChai`) whose child reuses the scrap bottle prefab and carries a `ScrapPickup`. It was not in the earlier audit (12 pickups) and is not mine. `ScrapCollector` only reads children of `ScrapItems`, so it is not collected by E-scrap and not counted for the quest. Its owner should confirm this is intended.

[NOT VERIFIED]: real E-key collection in first person; how the items look from the player camera at walking distance; walking to every item with the CharacterController; the cylinder can next to the real models. A magenta character leg was seen near NPC_ChildhoodFriend (14, 35) in a capture. It is not part of this task and its cause was not investigated (it may be the same shader-variant failure).
Git: nothing committed or pushed. New/changed files: 3 prefabs, 3 materials, CardboardBox_LP.fbx(+.meta), 3 texture .meta files, MainGame.unity, this file.

## How a new Claude should start
1. Check Unity MCP is connected (manage_editor telemetry_status, read scene hierarchy, read console).
2. Read this file, then only the scripts for the task at hand. Do not repeat the full audit.
3. Follow inspect, small change, test, and report only what was really tested in Unity.

## 21. Week 3 Task 2 - Grandparents chore quest "Cháu ngoan bất đắc dĩ" (2026-10-10, NOT committed)
[VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (QuestNPC.Interact, dialogue buttons, QuestObjectiveTarget.Interact, and the private Update() of the plots called by reflection). NO real keyboard or mouse. Unity was not advancing frames while driven by MCP (Time.frameCount stayed 1).

What changed (reused Task 1 framework, no new manager, no duplicate quest):
- Quest asset `Assets/Data/Quests/Quest_GrandparentsHelper.asset` (questId `grandparents_helper` unchanged) now reads: title `Cháu ngoan bất đắc dĩ`, objective `Tưới 3 luống rau cho ông bà`, progressLabel `Đã tưới` (HUD shows `Đã tưới: n / 3`), requiredAmount 3, rewardMoney 150 (configurable on the asset), not repeatable. offerLines[0] is `Muốn có tiền huấn luyện gà thì phụ ông bà chút đi cháu!`. The old chore text (quet san / ganh nuoc / phoi do, title `Osin Cua Ba`) was replaced. Giver is still NPC_Grandmother (QuestNPC, requireFirstBattleLoss = true).
- Scene `MainGame`: the three chore placeholders under `QuestObjectives` were renamed `VegetablePlot_1..3`, flattened into brown beds with a small green `Rau` child, moved to about (3,25), (7,22.5), (10.5,24) (checked free of colliders and renderers) and given interactionDistance 1.8. Moved on purpose: at the old spots the plot range overlapped the grandparents' talk range and neighbouring plots, so one E press could do two things. Prompt `Nhấn E để tưới rau`, banner `Đã tưới`.
- `QuestObjectiveTarget.cs`: added read-only `IsUsed` (used in the quest's current cycle). Nothing else changed.
- New `Assets/Script/Quest/QuestTargetMaterialSwap.cs`: swaps Plot_Dry -> Plot_Watered by reading `IsUsed`. Never counts progress or pays.
- New materials `Assets/Materials/Plot_Dry.mat`, `Plot_Watered.mat`, `Plot_Greens.mat`.

Verified [VERIFIED-PLAY]: plots refuse to count before accept; dialogue shows the required line, then the accept button; HUD shows `Đã tưới: 0 / 3` after accept; each plot counts once (repeat presses return false); HUD 1/3, 2/3, 3/3; state ReadyToTurnIn at 3/3 with HUD text `Quay lại gặp bà để nhận tiền công`; plots change material when used; extra presses after 3/3 do nothing; balance stays 0 until the claim button, then 150 (Money HUD `Tiền: 150`); second claim, direct TurnIn, re-accept and talking again all pay 0; HUD hides after completion; plots stay inert after completion. Prompt `Nhấn E để tưới rau` shows with the plot as owner when the player is in range and hides when far. Compile clean; Console had no project errors (only MCP port and Account API notices).

[NOT VERIFIED]: real keyboard E play-through; real first-battle loss unlocking the offer (BattleManager.firstBattleCompleted was set by hand); how the plots look in the Game view (only a Scene view screenshot was taken); the 'Đã tưới' banner on the 3rd plot is overwritten by the objective-complete banner (the bed still changes); whether the grandmother/grandfather prompts and the plot prompt compete in a real frame-by-frame walk.

Known issues / notes:
- Two scratch screenshots (`Assets/Screenshots/screenshot-20261010-161536.png` and `-161552.png`) were left by this task; delete them. The tool blocked deleting them.
- The scene showed isDirty = true after the Play session although it was saved before; it was not re-saved. Check the diff before committing.
- Git: `git status` could not be run from Unity MCP. Files touched by this task: QuestObjectiveTarget.cs, QuestTargetMaterialSwap.cs (new), Quest_GrandparentsHelper.asset, Plot_*.mat (new), MainGame.unity, this file. Nothing committed or pushed.
- Sections 11 and 12 of this file are garbled (text from section 11 runs into section 13 and the scrap heading is cut off). I did not reconstruct them; section 19 and 20 hold the accurate Quest, Scrap and Money details.

## 22. Inventory + Hotbar (2026-10-11, NOT committed)
[VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (Select/Step, ItemPickup.Update via reflection, TryPickUp, inventory API). NO real keyboard, mouse wheel or on-screen look. Unity was not advancing frames while driven by MCP.

Reused (no duplicate systems): `InventorySystem` (teammate-owned, old grid code left untouched; an item data layer was appended to it earlier today: `Add / Remove / Has / GetCount / CanAdd / GetSlot / Changed`, 5 slots), `InteractionPromptUI` (shared E prompt), `MessageBannerUI`, `DialogueUI`, and the `QuestUI_Canvas`. PlayerMovement was not touched. The old `InteractableObject` / `SelectionManager` mouse pickup was not touched either.

Files:
- `Assets/Script/Inventory/ItemDefinition.cs` - ScriptableObject (id, displayName, icon, placeholderColor, maxStack) + `ItemStack`. (existing from the earlier partial work)
- `Assets/Script/Inventory/HotbarUI.cs` - 5-slot HUD: keys 1-5, mouse wheel (wraps), selected slot highlighted (colour + scale), icon/name/count per slot, `SelectedItem` for other systems, hides during dialogue. Display only. (existing)
- `Assets/Script/Inventory/ItemPickup.cs` - NEW. Put on any object + assign an ItemDefinition. Nearest pickup in range gets the shared prompt `Nhấn E để nhặt <tên>`; one E press picks one item; once-only `IsCollected` flag set before adding; object deactivated after; if the inventory is full it stays in the world and the banner says `Túi đầy, ...`. Ignored while dialogue or the inventory screen is open.
- `Assets/Data/Items/Item_VeChai.asset`, `Item_BinhTuoiCay.asset`, `Item_ThungHang.asset` - NEW (maxStack 20 / 20 / 5, coloured placeholder icons, no sprites).
- `Assets/Materials/Item_WateringCan.mat`, `Item_Crate.mat` - NEW placeholder materials.
- Scene `MainGame`: `QuestUI_Canvas/Hotbar` (bottom centre, 5 slots) and `ItemPickups` root with `Pickup_VeChai` (existing `Scrap_PlasticBottle` prefab, its ScrapPickup component disabled so it is not a quest item), `Pickup_BinhTuoiCay` and `Pickup_ThungHang` (primitive placeholders, no colliders) at about (11.5,9.5), (13,6.5), (9.5,6) near the player start.

How to use: add a new item with Assets > Create > Chicken Training > Inventory Item; put it in the world by adding `ItemPickup` to an object; other code reads `InventorySystem.Instance.Has/GetCount/Remove` and `HotbarUI.SelectedItem`.

Verified [VERIFIED-PLAY]: compiles, no project errors in the Console; selection by index and by step incl. wrap; the selected slot colour/scale differ from the others; prompt shows only near an item and hides when far or after pickup; pickup adds the item and the slot shows icon colour, name and `x1`; a second pickup of the same object is refused; with two items in range only the nearest is picked; Add/Remove/Has/GetCount including all-or-nothing behaviour and invalid input; full inventory refuses the pickup and the item stays active, then can be picked up once after freeing a slot.

[NOT VERIFIED]: real keys 1-5, mouse wheel and E; how the hotbar and the placeholder items look in the Game view (never captured); the Vietnamese item names in the TMP font (`Bình tưới cây`, `Thùng hàng`) rendering with all diacritics; opening the old grid inventory (I) together with the hotbar.

Not done / next steps: real icons (assign a Sprite on each ItemDefinition); the old grid `InventorySystem.AddToInventory` (Resources prefabs) and the new hotbar item slots are two separate stores and are not synced; using the selected item (e.g. watering the plots only with the watering can selected) is not wired; the scrap quest still counts its own pickups and does not add bottles to the inventory; there is no save/load of inventory; keep ItemPickup ~4 m away from NPCs and quest targets so one E press cannot also trigger those.

Console: the editor also logs `Shader error ... out of memory while parsing` for URP/Lit (machine memory, not project code) and the Input Manager deprecation notice. Scene showed isDirty after the Play session although it was saved after setup; not re-saved. Git status could not be run from Unity MCP; nothing committed or pushed.


## 23. Inventory + Hotbar re-check (2026-10-11, audit only, nothing rebuilt)
Scope: section 22 only (ItemDefinition, HotbarUI, ItemPickup, InventorySystem item layer). No script was
rewritten (InventorySystem, HotbarUI, ItemDefinition untouched, per instruction). No commit/push.
Tags: [READ] = read from code/scene/assets this session. [CHECKED-TOOLING] = actively tested whether a
capability exists. [CARRIED OVER] = from section 22's own Play-Mode code-path run (2026-10-11, same day,
scripts unchanged since). [NOT VERIFIED] = not tested by anyone this session.

1. Real keyboard/mouse input (keys 1-5, wheel, E) [CHECKED-TOOLING]:
   Searched the full Unity MCP toolset for an input-simulation tool (keyboard/mouse/EventSystem synthetic
   events). None exists - only run_tests, manage_physics, manage_editor (play/pause/stop), manage_ui,
   manage_animation, manage_graphics, refresh_unity, model import. Same conclusion as section 22: Unity MCP
   cannot press real keys or move a real mouse wheel. **The owner must do this check manually**: enter Play
   Mode, approach an ItemPickup, confirm the `Nhấn E để nhặt ...` prompt, press E, watch the hotbar slot
   fill, press 1-5 and scroll the wheel to confirm the highlighted slot changes.
   Did not re-run this session: a fresh Play Mode code-path pass (no scripts changed since section 22's
   2026-10-11 run, so its [VERIFIED-PLAY] results are carried over, not re-tested today).

2. Hotbar display, Vietnamese font, 'I' key conflict:
   - Hotbar wiring [READ]: `QuestUI_Canvas/Hotbar`, 5 slots, all background/icon/name/count/key references
     assigned, `inventory` -> InventorySystem, `dialogue` -> DialogueUI, `group` -> its own CanvasGroup.
     No null references found.
   - Vietnamese font [READ - confirmed root cause, NOT a Hotbar-only bug]: every TMP text in the project
     (Hotbar, DialogueUI, MoneyHUD, QuestHUD) uses the single font asset in the project,
     `LiberationSans SDF` (file untouched since 2023-07-22, i.e. the Unity/TMP default, never customized).
     Checked its glyph table in code (`TMP_FontAsset.HasCharacter`): it has `â ê ô` (circumflex) and their
     uppercase, but is MISSING `ă đ ơ ư` and `Ă Đ Ơ Ư` (breve, horn, d-stroke) even with fallback search,
     and there is no second/fallback font asset in the project to cover them. Item names
     `Bình tưới cây` (ư) and others using these letters will render with missing/boxed glyphs. This is a
     pre-existing, project-wide gap (predates the Hotbar), not something the Hotbar introduced.
     NOT fixed: regenerating the atlas needs a source font that actually contains those glyphs (Liberation
     Sans itself may not), i.e. importing a new Vietnamese-capable font file - which the 'no new asset
     downloads' project rule blocks, and is bigger than the 'small fix' scope of this task. Flagging for
     the owner instead of doing it.
     [NOT VERIFIED]: nobody has looked at the actual rendered glyphs in the Game view this session; the
     glyph-table check is a reliable proxy but not a screenshot.
   - 'I' key conflict [READ - no code-level conflict found]: grepped `Assets/Script/**/*.cs` for
     `KeyCode.I` and `Alpha1` - `KeyCode.I` appears only in `InventorySystem.cs`, `Alpha1` only in
     `HotbarUI.cs`. No other script reads either key. `HotbarUI.Update()` already returns early while
     `inventory.isOpen` is true, so keys 1-5/wheel are ignored whenever the old grid screen is open.
     Canvas layout [READ]: the old `InventoryScreen` panel lives on a separate `Canvas` (sortingOrder 0),
     centered, 971x600 (covers roughly screen Y 240-840 at 1920x1080); the Hotbar lives on
     `QuestUI_Canvas` (sortingOrder 20), bottom-center, 590x110 (covers roughly screen Y 24-134). The two
     do not overlap on screen. The only cross-effect is that opening the old screen sets
     `Cursor.lockState = None` (pre-existing `InventorySystem` behaviour, not touched) which would affect
     camera look while it's open - this is teammate-owned behaviour, not a Hotbar/key conflict, and was
     left alone per instruction 4.
     Conclusion: no conflict was found that needs fixing. [NOT VERIFIED] visually (no screenshot taken).

3. Input System / Input Manager [READ - no root cause found, nothing changed]:
   `ProjectSettings/ProjectSettings.asset` has `activeInputHandler: 2` (Both), matching the existing known
   issue in section 15.5. `Assets/InputSystem_Actions.inputactions` exists but no action in it is read by
   HotbarUI, ItemPickup or InventorySystem (all three use legacy `Input.GetKeyDown`/`Input.mouseScrollDelta`
   exclusively). No InputAction binds 1-5, E, I or the scroll wheel, so there is no binding-level conflict
   with the EventSystem's `InputSystemUIInputModule`. Per instruction 3, since no root cause requiring a
   fix was identified, nothing was changed in Input settings.

4. Console / Git [READ]: 0 project errors/warnings in the Console this session (only MCP/Account API
   notices, same as before). Git: still on `feature/Character-Pet-Enemy`; `git status`/`git diff` still
   cannot be run from Unity MCP (process execution is blocked in this sandbox - confirmed again this
   session). File mtimes show no change to `ItemDefinition.cs`, `HotbarUI.cs`, `ItemPickup.cs` or
   `InventorySystem.cs` since section 22 was written (all 2026-10-11, before this audit). Only this file
   (`PROJECT_STATUS.md`) was changed by this task. Nothing committed or pushed.

Net result of this task: no code, scene, or settings were changed. This was a read-only re-check of
section 22 plus the font/key-conflict questions. The two real findings are (a) real-input testing is
confirmed impossible through Unity MCP - manual test still required and not done, and (b) a project-wide
missing-glyph font gap (ă đ ơ ư) that affects the Hotbar's Vietnamese item names and predates it - flagged,
not fixed.


## 24. Week 3 Task 3 - Delivery quest "Ship hàng làng quê" (2026-10-11, NOT committed)
Tags: [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (QuestNPC.Interact, dialogue buttons, QuestCarryItem.TryPickUp, QuestObjectiveTarget.Interact) with frames advanced by EditorApplication.Step(). NO real keyboard or mouse. [SEEN] = looked at in a Game view capture. [NOT VERIFIED] = not tested.

What changed (reused QuestManager, CurrencyWallet, QuestNPC, QuestCarryItem, QuestObjectiveTarget, the shared prompt/banner/dialogue; no new manager, no inventory item):
- Quest asset `Assets/Data/Quests/Quest_VillageDelivery.asset` (questId `village_delivery` unchanged, the old 3-parcel quest was REPURPOSED): title `Ship hàng làng quê`, requiredAmount 1, rewardMoney 200 (unchanged, configurable on the asset), not repeatable, progressLabel `Gói hàng`, turnInObjective `Quay lại gặp bà để nhận tiền công`. offerLines[0] is `Ông có gói hàng cần giao. Mang nó đến người nhận rồi quay lại lấy tiền công nhé!`.
- Giver: `NPC_Grandmother` (QuestNPC) now has it in additionalQuests. It was REMOVED from `QuestNPC_Villager`'s additionalQuests, so only the grandmother offers and pays it.
- Scene `MainGame`: new `QuestObjectives/QuestDeliveryBox` at (5.4, 0, 11) (QuestCarryItem, box model from `Art/UI/box/source/CardboardBox_LP.fbx`, material `ScrapReal_Cardboard`), and `Player/Main Camera/HeldItemAnchor/HeldDeliveryBox` (carried copy, starts inactive). PlayerMovement and MouseMovement were not touched.
- Destination: `NPC_ChildhoodFriend` keeps its QuestObjectiveTarget, now bound to this quest with requiredItem = the box, prompt `Nhấn E để giao hàng`, hint `Cần cầm gói hàng để giao`, banner `Đã giao hàng!`. The QuestObjectiveTargets on `NPC_Grandfather` and `NPC_MarketVillager` (old delivery targets) were REMOVED, otherwise any of three NPCs could take the box.
- `QuestCarryItem.cs`: new serialized bool `releaseWhenReadyToTurnIn` (default off, so the watering can is unchanged; on for the box so it leaves the hands once delivered).

Verified [VERIFIED-PLAY]: pickup and delivery refused before accept; dialogue shows the required line, then `Nhận nhiệm vụ`; double click does not accept twice; prompt `Nhấn E để nhặt gói hàng` shows near the box; second same-frame pickup refused; HUD shows `Gói hàng: 0 / 1`; prompt `Nhấn E để giao hàng` shows at the friend while carrying the box; delivery moves the quest to ReadyToTurnIn (1/1) and the banner `Hoàn thành mục tiêu! Quay lại gặp NPC để nhận tiền công.` shows; same-frame and later second delivery refused; box leaves the hands, world box stays hidden, re-pickup refused; grandmother offers `Nhận tiền công (+200)`; money 150 -> 350 once, second click, direct TurnIn and re-accept pay/do nothing; banner `Nhận thưởng: +200 tiền`. Watering quest (150) and scrap quest state were not disturbed. [SEEN] the carried box in the lower right of the first-person view (the HUD and hotbar were visible). Compile clean; Console had no project errors (only the old FindFirstObjectByType warnings in ItemPickup.cs and MCP/Account API notices).

[NOT VERIFIED]: real keyboard E play-through; real first-battle loss unlocking the offer (BattleManager.firstBattleCompleted set by hand); the box in the world as seen from walking distance (never captured); box texture/shading on the open top looks dark grey (probably the box interior); the Vietnamese text of this quest rendering correctly: section 23 found that the only TMP font lacks `ă đ ơ ư`, and this quest's text uses them (`Ông`, `để`, `bạn thời thơ ấu`, `Giỏi`...), so expect boxed glyphs until a Vietnamese-capable font is added.

Known limits:
- The grandmother talks about one quest at a time and prefers the first configured one, so the delivery is only offered AFTER the watering quest is completed.
- When both her quests are completed she shows the watering quest's completed lines.
- The destination NPC's E prompt (`Nhấn E để giao hàng`) only appears while the box is held; without the box the hint `Cần cầm gói hàng để giao` is shown instead and E does nothing.
- The box transform on the camera anchor (local pos -0.12, 0.12, 0; rot 22, -28, 0; scale 0.3) was tuned in Play Mode and then saved in Edit Mode; the saved values were not re-captured.
- Leftover scratch files: `Assets/Screenshots/screenshot-20261011-134915.png`, `-134929.png`, `-134944.png` (same blocked-delete problem as in section 21; delete them by hand).
- Git: `git status` cannot be run from Unity MCP. Touched by this task: QuestCarryItem.cs, Quest_VillageDelivery.asset, MainGame.unity, this file. Nothing committed or pushed.

## 25. Week 3 Task 4 - Tool shop (2026-10-11, NOT committed)
Tags: [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (ShopNPC.Interact, the real Buy/Close `Button.onClick`, wallet/inventory API) with frames advanced by EditorApplication.Step(). NO real keyboard or mouse. [NOT VERIFIED] = not tested.

Reused (no second money / inventory / hotbar system): `CurrencyWallet`, `InventorySystem` item layer (`CanAdd/Add`) + `HotbarUI`, `ItemDefinition`, shared `InteractionPromptUI`, `DialogueUI` (as a guard only), `QuestUI_Canvas`, and the existing `NPC_MarketVillager` at (27,0,46) in the market area.

Changed / new:
- `CurrencyWallet.cs` (Economy): added `CanAfford(int)` and `TrySpend(int)` (false and no change for <=0 or too little money; raises BalanceChanged once). `Add` untouched.
- `Assets/Script/Shop/ShopCatalog.cs` - ScriptableObject list of `ShopEntry` (ItemDefinition, price, short description). Add a good = add an entry, no code.
- `Assets/Script/Shop/ShopItemRow.cs` - one list row (icon, name, description, price, Buy button; price turns red if unaffordable, button stays clickable so the 'not enough money' message shows).
- `Assets/Script/Shop/ShopUI.cs` - the window. `TryBuy`: not enough money -> message; bag cannot hold it (`CanAdd`) -> message; else `TrySpend` once then `inventory.Add` (refund if Add unexpectedly fails). Close = close button or Esc (both ignored on the opening frame). While open it frees the cursor and disables PlayerMovement, MouseMovement, HotbarUI and InventorySystem (so keys 1-5/wheel and the 'I' screen cannot interfere), then restores them; same pattern as DialogueUI. Buying only works while open.
- `Assets/Script/Shop/ShopNPC.cs` - prompt `Nhấn E để mua dụng cụ` + E opens the shop. Not opened while the shop, a dialogue or the old inventory screen is open. interactionDistance 1.8 on purpose: `Scrap_Can_11` is 4.1 m away and ScrapCollector range is 2.2, so the two E ranges do not overlap.
- Scene `MainGame`: `QuestUI_Canvas/ShopUI` (panel 1100x720, title, money label, scroll list with a template row, status line, close button); `ShopNPC` added to `NPC_MarketVillager`; its `VillagerFlavorNPC` is DISABLED (not removed) so the shopkeeper owns E. Its old line (market chatter) is no longer shown. Scene saved.
- Data: `Assets/Data/Shop/ShopCatalog_DungCu.asset`; new items `Item_TaTapGa` (maxStack 5), `Item_ThucAnTangLuc` (maxStack 10); reused `Item_BinhTuoiCay` (maxStack 20).

Initial goods (price is a plain field on the catalog): Binh tuoi cay 60, Ta tap cho ga (chicken training tool) 150, Thuc an tang luc (training support) 40. Icons are the coloured placeholder squares (no sprite assets exist for these; the watering can only exists as a 3D model). Items have NO effect yet and chicken stats are not changed (out of scope).

Verified [VERIFIED-PLAY] (32 checks, all passed): prompt shows within range and hides far away and while the shop is open; Interact opens the shop, a second open is refused; player controls frozen while open and restored on close; cursor freed; not enough money -> balance unchanged, nothing added, message `Không đủ tiền mua ...`; enough money -> exact price deducted once, item in slot 0 of the hotbar, money label updated, message `Đã mua ...`; consecutive purchases (can x2, feed x2 -> balance 0) and an unaffordable one refused; two clicks in the same frame with money for one -> charged once; bag full (5 slots x 20 of another item) -> balance unchanged, nothing added or lost, message `Túi đầy ...`, then one purchase works after freeing a slot; close button closes; a stale buy after close does nothing; reopen works; opening is refused while the old inventory screen is open. All 208 visible characters of the shop text have glyphs (the font has a Dynamic fallback with LiberationSans, which contains the Vietnamese letters), so the 'missing ă đ ơ ư' warning in section 23 looks wrong for rendering, though it is still only a code-level check.

[NOT VERIFIED]: real E key, real Esc key, real mouse clicks and wheel (Unity MCP cannot send input; owner must test: walk to the market villager, press E, click Mua, press Esc); how the window looks in the Game view (a screenshot `Assets/Screenshots/shop_check.png` was taken but I could not view it); scroll behaviour with more than 4 rows; Esc interaction with the Editor's own Esc handling in Play Mode.

Known notes: `ShopUI/Panel` is left active in the saved scene (ShopUI.Awake hides it at runtime; DialogueUI's panel is saved inactive); the template row exists as an inactive child and one runtime clone per entry is created on Play; purchases are not saved (everything resets on Play, like money); the old grid inventory and the hotbar item store are still separate (section 22). Delete the scratch screenshot `Assets/Screenshots/shop_check.png` by hand.
Console: no project errors or warnings after compile and after Play (only the editor's Account API notice). Git: `git status` cannot be run from Unity MCP; touched: CurrencyWallet.cs, Script/Shop/* (new), Data/Shop + 2 item assets (new), MainGame.unity, this file. Nothing committed or pushed.

## 26. Week 3 Task 2 re-check - watering quest (2026-10-11, audit + regression only, NOTHING changed in project)
The brief was re-issued, so the existing Task 2 work (section 21, second one) was compared against it. All 11 requirements are already implemented; no script, scene or asset was changed in this check.
- Real asset paths: watering can `Assets/Art/UI/water can/` (the brief's `Assets/UI/water can` does not exist), plant `Assets/Art/UI/plant/source/MonsterPlantv23.fbx` (prefab `Assets/Prefabs/Item/QuestPlant_Monster.prefab`).
- In scene [READ]: `QuestObjectives/QuestWateringCan` at (3.4, 0, 12) (QuestCarryItem, 5.4 m from NPC_Grandmother), `VegetablePlot_1..3` (QuestObjectiveTarget, requiredItem = the can) each with a `Plant_VegetablePlot_n` MonsterPlant model, `Main Camera/HeldItemAnchor/HeldWateringCan` (held model), Quest asset `Quest_GrandparentsHelper` (3 plants, reward 150, not repeatable), giver NPC_Grandmother. No leftover placeholder plants in the grandparents' garden (Bush/Bamboo/Coconut are ordinary scenery and were left).
- Regression [VERIFIED-PLAY] (same code paths, frames via EditorApplication.Step, NO real keyboard/mouse): can pickup and watering refused before accept; accept through the grandmother's dialogue; HUD `Đã tưới: 0 / 3`; watering refused without the can; can prompt shows, pickup works once, held can becomes visible; each plot shows its prompt, counts once and refuses a second press; plots switch to the watered state; ReadyToTurnIn at 3/3 and no money yet; opening the claim dialogue pays 0, the button pays +150 exactly once; second claim, re-accept path and extra watering pay/do nothing. 32 checks, 31 meaningful (one was vacuous and is not counted). Console: no project errors; it shows engine-internal `Internal: JobTempAlloc ... older than 4 frames` warnings, most likely caused by stepping frames by hand with EditorApplication.Step() in this check (not project code; not investigated further).
- [NOT VERIFIED]: real E key; how the can and plants look in the Game view (a screenshot could not be viewed this session); real first-battle loss unlocking the offer (flag set by hand).
Git: nothing committed or pushed. Only this file was edited.

## 24. Week 3 Task 2 (continued) - Watering can for the grandparents' quest (2026-10-13, NOT committed)
Tags: [VERIFIED-PLAY] = driven in Play Mode by calling the same code paths (QuestNPC.Interact, dialogue buttons, TryPickUp, QuestObjectiveTarget.Interact, private Update() via SendMessage). NO real keyboard or mouse. Frames do not advance by themselves under MCP (frameCount stayed 2). [NUMERIC] = checked by rendering cameras to a small RenderTexture and measuring pixels (image captures failed: camera capture error, Scene view >1 MB). [NOT VERIFIED] = not tested.

Assets used (actual paths; the task text said Assets/UI but they live under Assets/Art/UI): `Assets/Art/UI/water can/source/Watering_can_lp.fbx`, `Assets/Art/UI/plant/source/MonsterPlantv23.fbx` (a potted plant, 6 m at import scale, used at 0.15). Nothing was moved, downloaded or installed.

What changed (reused QuestManager, QuestNPC, QuestHUD, QuestFeedback, CurrencyWallet, InteractionPromptUI, MessageBannerUI; no new manager, no inventory):
- NEW `Assets/Script/Quest/QuestCarryItem.cs`: the can pickup. Only pickable while `grandparents_helper` is Active (E, in range, not during dialogue/inventory). `IsHeld` flips before anything else. Stays in hand while Active/ReadyToTurnIn, put away on Completed (or a new cycle of a repeatable quest). Never counts progress or pays.
- `QuestObjectiveTarget.cs`: optional `requiredItem` + `missingItemPromptText`. With no item set (delivery targets) behaviour is unchanged. Without the can only the hint `Cần có bình tưới để tưới` shows and E does nothing; Interact() returns false. Prompt helper now takes text.
- `QuestTargetMaterialSwap.cs` (my Task 1 file): same dry->`Plot_Watered` swap, plus optional `plantVisual` that grows x1.25 while watered (resets with a new cycle).
- Quest asset `Quest_GrandparentsHelper.asset`: objective now `Lấy bình tưới cạnh nhà, tưới 3 cây cho ông bà`, description updated, 3rd offer line mentions the can. Reward (150), amount (3), key untouched. HUD shows `Đã tưới: 0 / 3`.
- Scene `MainGame`: `QuestObjectives/QuestWateringCan` at (3.4, 0, 12.0) (open front of the house, model x2 = 0.77 m tall, slow spin, ~4-5 m from both grandparents, the scrap at (4,8) and all plots so E prompts do not overlap); `Plant_VegetablePlot_1..3` replace the old `Rau` placeholders (deleted) on the three beds; plots wired to the can; prompt `Nhấn E để tưới cây`; `Player/Main Camera/HeldItemAnchor/HeldWateringCan` (inactive until picked up, lower-right of the view). No player script was changed.
- New prefabs `Assets/Prefabs/Item/QuestWateringCan_Model.prefab`, `QuestPlant_Monster.prefab`; new materials `Assets/Materials/QuestPlant_Leaves.mat`, `QuestPlant_Pot.mat` (base colour only, because normal/AO keywords cause the known shader compile failure). The can reuses `ScrapReal_WateringCan.mat`.

Verified [VERIFIED-PLAY], 49 checks, 0 failed: nothing works before accept; accept via grandmother's dialogue; HUD 0/3 -> 3/3; no counting and only a hint without the can; one pickup only; each plant counts once; beds turn wet and plants grow; ReadyToTurnIn text; no money before claim, +150 exactly once; repeat claim/TurnIn pay 0; can put away and HUD hidden after; delivery and scrap quests still count once. Compile clean; Console no project errors.
[NUMERIC] can, plant and held can render with 0 magenta pixels; held can occupies x 0.64-0.77, y 0.06-0.33 of the frame.

[NOT VERIFIED]: real E key presses; how it looks in the Game view (no image could be captured); the pour tilt animation (needs frames); held can clipping into walls when standing against them; repeatable-quest reset path (written, not run); real first-battle loss unlocking the offer (flag set by hand).
Notes: the existing inventory `Pickup_BinhTuoiCay` at (13, 0, 6.5) is a separate teammate item and was left alone (two watering cans now exist in the world). Vietnamese letters ă đ ơ ư are missing from the project's TMP font (section 23), so new texts have the same gap as old ones. The scene was already dirty before this task, so saving it also stored whatever was pending; check the diff. Git: nothing committed or pushed. Files: QuestCarryItem.cs (new), QuestObjectiveTarget.cs, QuestTargetMaterialSwap.cs, Quest_GrandparentsHelper.asset, 2 prefabs, 2 materials, MainGame.unity, this file.


## 24. Village map expansion pass (2026-10-13, NOT committed)
Edited the existing MainGame scene (no new scene). Done through Unity MCP. Player, camera, combat, quest, inventory, hotbar untouched.

State found at start [VERIFIED]: an earlier, undocumented pass had already built VillageMap/KhuDanCu (22 HousePlot, Styloo houses), VillageRoads (stepping stones), ZoneSigns (10), Perimeter, boundary walls (x -90..190, z -70..160). Weaknesses: houses faced their yards with their backs to the lane, fences were closed rings with no gate and no colliders, empty land between plots, no branch lanes, ponds, canals or paddies, trees only Kenney palms/oaks, 2196 non-static renderers.

Done this pass:
- Houses: all 22 rotated 180 deg to face the lane, moved back for a ~5 m front yard; gardens moved if overlapping; gate gap opened in each front fence + 2 brick GatePost prefabs; per-plot FenceColliders (5 boxes, gate left open).
- New root VillageMap/Expansion: Ground_Dirt (main roads 4.6 m, branch roads 3.4 m, plot spurs, alleys 2.2 m, pond banks, paddy bunds), Ground_Paddy (17 paddy blocks), Water_Canals (6 canals), Water_Ponds (6 ponds, NavMeshModifier NotWalkable) + PondBlockers (convex colliders). Each is ONE combined mesh (assets in Assets/Prefabs/Village/Meshes).
- Flora prefabs in Assets/Prefabs/Village (VNP_BambooClump, BananaClump, ArecaPalm, FruitTree_A/B, GatePost), materials in Assets/Materials/Village. Built from existing Kenney meshes + primitives (no downloads), one combined mesh each (384 to 6228 verts).
- ~278 new plants (yards, bamboo hedges, roadside areca rows, orchards, banana groves, pond rings, groves, edge bamboo). Replaced 16 oaks with fruit trees and 37 small bamboo shoots with bamboo clumps; removed 2 old trees that sat on new lanes.
- Perf: 2267 decor renderers flagged BatchingStatic (only objects with no scripts/Animator). NavMesh rebaked (Terrain surface).
- Moved NPC_MarketVillager 1 m (was inside MarketStalls collider).

Checks [VERIFIED, no real keyboard]: 36 NavMesh targets (all 22 yards, NPCs, pet, enemy, pond edge, far fields) PathComplete from the Player spawn; 30 interactables have no blocking colliders; spawn has no overlap.

Not done / unverified:
- Could NOT look at the result (screenshot tool exceeds 1 MB), so visual quality, flora scale/colour, z-fighting of road dirt strips (y 0.03) vs terrain and pond look are UNVERIFIED. Open the scene and walk it.
- Houses are still the Styloo pack (European-looking); the Vietnamese house .glb is only used at the grandparents'. Not swapped.
- Orchards are sparse (grid cells blocked by lanes/ponds). No bamboo fence pieces, no bridges over canals (canals are walkable, decorative).
- Core zones (grandparents, centre, shop, training, battle, scrap, reward) were not moved.
- Overlap of NPC_Grandfather with Wall_East_B left as is (house wall).
- Console not re-read after the pass; Play Mode not run.


## 25. Main story flow - audit and Play Mode verification (2026-10-14, NOT committed, NO project file changed)
The previous task 'COMPLETE MAIN STORY FLOW' was never written into this file. Its code and scene wiring DO exist (found by inspecting code and scene, not from docs): `Assets/Script/Story/` = IntroScreen, GrandfatherChickenRescue, ChildhoodFriendBattleTaunt, ChickenTransformationTrigger, StoryBed; plus Script/PostLossSequence.cs, Quest/QuestCarryItem.cs, Quest/QuestObjectiveTarget.cs, QuestNPC (requireFirstBattleLoss). Quests: ve_nha_tham_ong_ba, nhan_nuoi_chu_ga, thu_suc_voi_ban_cu, grandparents_helper, garden_protein, chuan_bi_tai_dau (+ scrap/delivery side quests). All scene references are assigned (Player is found by tag; the empty player/chicken/deliveryTarget fields are optional). GuguGaga's BattleTrigger is intentionally disabled (the friend starts the fight).

Completed [VERIFIED-PLAY this session; real frames via EditorApplication.Step with Time.captureDeltaTime, dialogue buttons pressed through their onClick, player teleported next to targets, NO real keyboard/mouse]:
1. Spawn (10.5, 3.1) at the entrance, intro screen -> 'Bắt đầu' -> quest 'về nhà thăm ông bà' Active, movement unlocked.
2. Talk to Ông -> arrive quest Completed, rescue quest Active, button 'Xin ông giữ lại con gà' -> rescue Completed, chicken WakingUp -> MainChicken and following after the animation (real frames).
3. Friend: talk -> accepts quest, challenge dialogue, button 'Nhận lời thách đấu' -> BattleManager InBattle (isFirst true).
4. Loss: pet given lethal damage through HealthSystem.TakeDamage (same OnDeath path as a real loss; the real fight itself was NOT watched) -> battle Completed, firstBattleCompleted. With real frames the AUTOMATIC taunt fired by itself (quest Completed, taunt dialogue open) and PostLossSequence revived the chicken (HP 100, Follow). Talking to the friend also gives the taunt if missed.
5. Grandmother (needs the loss): accept -> can pickup -> 3 plants watered -> ReadyToTurnIn -> claim: +150 money, chore Completed, garden_protein starts and Bà's explanation dialogue opens.
6. Garden order enforced: harvest first, feeding first, and fertilising without the bag all returned false. Then bag pickup -> fertilise (message says the bag is PROTEIN powder) -> harvest -> feed the chicken -> garden_protein ReadyToTurnIn.
7. Bed (real time): TrySleep once only (second call false), player frozen, fade to black 'Zzz...', chicken transforms while black (scale x1.35 + red tint, flags set), 'Sáng hôm sau...', fade out, movement restored, 'chuan_bi_tai_dau' Active, ChickenTransformationTrigger.TrainingUnlocked true (it was false before). TrainingStation reads that flag (TrainingStation.cs line ~150).
Console after the runs: 0 project errors. One warning seen: '[BattleManager] Cannot start battle - one of the fighters is already dead.' (source not investigated; it came from my scripted test order and may be benign).

Incomplete / not done:
- The chicken transformation is a PLACEHOLDER (scale + tint). No muscle-chicken model exists in the project (searched). BLOCKED on an asset; ApplyPlaceholderTransform in ChickenTransformationTrigger is the single swap point.
- Flags (Transformed, quests) live only in the play session; no save/load.
- The real combat result of the first battle (pet landing hits, loss by the AI itself) and the real friend-NPC rematch quest 'chuan_bi_tai_dau' content were not exercised.
- A training station actually running after the unlock (start, cost, animation) was not exercised, only the gate flag.
Not tested at all: real E key / mouse, how any of this looks in the Game view (no screenshot possible), the intro text with Vietnamese diacritics (TMP font lacks ă đ ơ ư, see section 23), the story after my village-map rebuild (section 24) by walking, only by teleport.

Exact next step: (a) play the whole chain once by hand with real keys and report anything wrong; (b) owner decides: supply a muscle-chicken model, or accept the placeholder; (c) then exercise one training station after the unlock and the 'chuan_bi_tai_dau' rematch.


## 27. Starter chicken (normal_chicken) -> chicken_pet swap, first fight, story gating (2026-10-19, NOT committed)
Task: replace the starting chicken with `normal_chicken`, make the first fight a scripted loss, open the garden quests only after the taunt, swap back to the old `chicken_pet` after sleeping. On inspection the code and scene wiring for this ALREADY existed (written in an earlier session, never documented). This session AUDITED and PLAY-TESTED it; NO project file was changed by this session (no script, scene, prefab or controller edit).

How it is built (read from code and scene):
- `ChienKe_Pet` stays the ONE gameplay chicken (NavMeshAgent, HealthSystem, PetCombatAI, MainChickenAcquire, ChickenStats, quest target, spawn point, follow logic). `Assets/Script/Story/StarterChicken.cs` only swaps the VISUAL: the old mixamo mesh (`obj1`) is hidden and an instance of `Assets/Prefabs/normal_chicken.prefab` (child `normal_chicken`, connected prefab instance, local scale 0.33) is shown. `SwitchToPet()` destroys that child and re-enables the old mesh, so there is never more than one chicken (one StarterChicken, one PetCombatAI in the scene after the swap).
- Rigs checked, NOT shared: normal_chicken = Generic rig, no Avatar (clips are path-based, 532 curves each, none humanoid); chicken_pet = Humanoid `ChienKe_blendAvatar`. So the normal chicken has its own `Assets/Animations/Chicken/Normal_Chicken.controller` (params isDown / isRunning / isScared; any-state transitions to squat_cycle01 / idle01 / run01 / run01flap / scared01, all looping) and StarterChicken feeds it from the pet Animator (isDead) and the NavMeshAgent. The old `Chicken.controller` is untouched. Culling: pet CullUpdateTransforms (state machine keeps running with the mesh hidden), normal AlwaysAnimate.
- First fight: no compatible fight clip exists for normal_chicken (only bounce/flap/bunnyhop/scared/run/idle/squat/peck; the RPG_Armed_* attack clips belong to another model, "Chicken Warrior", different rig). So while it is not the pet, PetCombatAI is disabled during the battle: it runs toward the rooster, then flees, then cowers; the rooster (existing RoosterCombatAI + first-battle balance) wins. A 45 s failsafe knocks it out through HealthSystem.TakeDamage so the battle cannot hang.
- Gating: `QuestNPC.IsOfferAvailable` (grandmother, requireFirstBattleLoss) waits for `ChildhoodFriendBattleTaunt.AfterLossStoryDone`, which becomes true only after the taunt dialogue ("Hahaha, thua rồi đó! Bữa nào tái đấu không? MÀY SỢ À") was opened AND closed. grandparents_helper -> garden_protein (bón phân, hái rau, cho gà ăn) -> bed -> `ChickenTransformationTrigger` -> StarterChicken.SwitchToPet() + `chuan_bi_tai_dau`.

[VERIFIED-PLAY this session, real frames via Pause + EditorApplication.Step (dt 0.05), NO real keyboard/mouse]:
1. Start: normal_chicken visible and lying down (squat_cycle01), old mesh hidden, one chicken.
2. MainChickenAcquire.WakeUp(): chicken gets up, normal clip -> idle01, PetCombatAI enabled, follows the player (~5.6 m behind after a teleport).
3. Friend: Interact -> challenge dialogue; StartFight (the dialogue button's callback) -> BattleManager InBattle. Normal chicken ran to the rooster (run01flap), then scared01; rooster HP stayed 150 the whole time (the chicken dealt no damage), chicken HP 85 -> 55 -> 10 -> 0 by the rooster's kicks, state Completed at ~12 s after the start, firstBattleCompleted true. Battle did not hang.
4. Gating: during the loss and while the taunt dialogue was open, grandmother offer = False, grandparents_helper NotStarted. After the dialogue closed: AfterLossStoryDone true, offer = True.
5. PostLossSequence finished (IsComplete true) and the chicken followed again.
6. Chore quest accepted/progressed/turned in through QuestManager (this skips the watering minigame, which section 26 covers) -> garden_protein Active -> ReadyToTurnIn -> StoryBed.TrySleep: fade, then Transformed true, garden Completed, `chuan_bi_tai_dau` Active, TrainingUnlocked true; normal_chicken child destroyed, old mesh visible, pet Animator in ChickenIdle, same GameObject (follows the player, agent and PetCombatAI on), exactly 1 StarterChicken / 1 PetCombatAI; player movement restored, sleep overlay removed.
7. Console: no compile errors, no StarterChicken warnings. Only the known URP/Lit `_NORMALMAP` "out of memory while parsing" shader errors (machine issue, see section 21 scrap notes) and an Input Manager deprecation notice.

NOT verified / needs the owner by hand:
- Real keys and mouse (E for the wake-up, the friend, the bed). The Game view look of both chickens (no true Game-view screenshot) - in particular whether normal_chicken at scale 0.33 faces the right way, sits on the ground and its eyes (Icosphere children) show correctly; whether run01flap looks right while it flees.
- The watering minigame and the ordered garden steps were not replayed here (section 25 / 26 did that).
- The old placeholder (scale x1.35 + red tint) in ChickenTransformationTrigger is now only the fallback when no StarterChicken exists; it is not used in this scene.
- The old pet (humanoid) model's animation after the swap was only checked through the pet Animator state name (ChickenIdle), not by eye. Flags are not saved (no save/load).
- The 'Scene' state in Normal_Chicken.controller has no motion (harmless, leftover from the FBX import).

## 28. Week 3 Task 5 - Chicken training + stat progression (2026-10-19, NOT committed)
[VERIFIED-PLAY] = driven in Play Mode with the editor paused and `EditorApplication.Step()` (0.02 s per frame), calling `TrainingStation.TryStart()` and the inventory/wallet APIs. NO real keyboard E, no on-screen look. The Play session ended by itself between some calls, so the tests were re-run in single long calls.

Found already in the project (written earlier today, not by this pass): `Assets/Script/Training/ChickenStats.cs` (Strength, Attack, Speed, MaxHP derived; caps at MaxStatValue 40; `ApplyToCombat()` writes damage, attackCooldown, agent speed, HealthSystem.maxHealth) and `TrainingStation.cs` (E prompt, cost via `CurrencyWallet.TrySpend`, optional required inventory item, story gate `ChickenTransformationTrigger.TrainingUnlocked`, busy/cooldown anti-spam, takes over the chicken with the agent + existing animations, restores combat afterwards). Neither was placed in the scene yet.

Changed / created in this pass:
- `TrainingStation.cs`: (1) the chicken's NavMeshAgent has stoppingDistance 10 in the scene, so it stopped 10 m short and then teleported; now 0.05 during the walk and restored after. (2) walk timeout is distance based (5-20 s) instead of a flat 5 s (the training area is ~45 m from the village). (3) if a spot is above the ground the model is lifted with `agent.baseOffset` and restored (turned out not needed on the treadmill: the runtime NavMesh already covers the belt top at y~0.5).
- Scene `MainGame`: `ChickenStats` added to `ChienKe_Pet`. New root `TrainingStations` inside the fenced training area (x -15..-1, z 47..57), three stations 5+ m apart, interaction range 2.2:
  - `Station_Treadmill` (-12,51.5): the existing model `Assets/Art/UI/Training/treadmill.fbx` (scale 180 because the chicken is ~2 m tall; originals untouched). Chạy máy, 50 coins, +2 Speed. Chicken runs on the belt with the existing `isRunning` run animation.
  - `Station_PushUp` (-6,51): flat green placeholder mat. Chống đẩy, 40 coins, +2 Strength (also +1 damage per 2 Strength and +MaxHP, see ChickenStats). Needs `Item_TaTapGa` (bought in the shop, kept after use). Uses the existing `IsPushing` push-up animation.
  - `Station_PunchingBag` (-9,55.5): primitive frame + red bag on a `SwingPivot`. Đấm bao cát, 60 coins, +2 Attack. Chicken uses its existing attack animation; the bag swings.
  - New materials `Assets/Materials/Training_MatGreen.mat`, `Training_BagRed.mat`, `Training_Frame.mat`.

Verified [VERIFIED-PLAY]: all three scripts compile, no project errors in the Console. Prompt `Nhấn E: Chạy máy (-50 tiền)  |  Tốc độ 10` shows near a station. No money: refused, balance unchanged, banner `Không đủ tiền: cần 50, đang có 0`. With money: cost taken exactly once; spam press while running and starting a second station while busy both return false and charge nothing. Treadmill x3: Speed 10 -> 12 -> 14, balance 120 -> 70 -> 20 (and 250 -> 200 on the repeat), agent speed 8 -> 8.48 -> 8.96, attack cooldown 1.5 -> 1.415 -> 1.339. Push-up without the weights: refused (`Cần có Tạ tập cho gà ...`), no charge; with the weights: Strength 10 -> 12, damage 10 -> 11, MaxHP 100 -> 110, 40 charged, item kept. Bag: Attack 10 -> 12, damage 11 -> 13, 60 charged, bag swing ~27 degrees. Maxed stat (40): refused with `Chỉ số này đã đạt tối đa`, no charge; at Speed 40 agent speed 15.2, cooldown 0.79. Banner shows result with before -> after and cost (e.g. `Chạy máy xong! Tốc độ 12 -> 14  (-50 tiền)`). After every session combat is re-enabled, root motion, stopping distance and push-up flag are restored. The chicken really walks to the station (~7.5 s from the village), runs / pushes / attacks for the whole session (isRunning or IsPushing true, positions and clips logged), not standing still. PetCombatAI hits with `damage` (line 249) so trained damage is used; BattleManager captures and restores the pet's current damage/cooldown/HP, so trained values survive the first fight (read, not run).

[NOT VERIFIED]: real E key and the on-screen look of the stations and animations (no screenshot, no Game view); a real fight against the rooster with trained stats; the chicken HP bar after MaxHP changes; Vietnamese diacritics in the TMP font (`Chống đẩy`, `Đấm bao cát`; see section 23); buying Tạ tập through the real shop UI (the item was added to the inventory by code); `ChickenTransformationTrigger.Transformed` was set by hand because training is story-gated until the garden quest + sleep are done.

No save system exists in the project (no PlayerPrefs / save files), so trained stats and the story flags reset when Play Mode stops. I did not build one. Next step if wanted: persist `ChickenStats` with the (future) save system.

Known issues: the treadmill chicken stands ~0.6 m off the belt centre (the NavMesh snaps it); the push-up and bag stations are placeholders (no props from the asset folders besides the treadmill); the scene was saved after setup and was not dirty at the end. Git status could not be run from Unity MCP; nothing committed or pushed.


## 29. Task 5 re-verification before Task 6 (2026-10-19, NOT committed, NO project file changed in this step)
Decision: Task 5 (section 28) was inspected in the scene and code again and re-tested in Play Mode with real frames (editor paused, `EditorApplication.Step()`, Time.captureDeltaTime 0.02). NO real keyboard/mouse, NO Game-view look. Result: Task 5 is COMPLETE at the level it can be tested here; nothing was rebuilt.

Found in the project (not rebuilt): `ChickenStats` on `ChienKe_Pet`; `TrainingStations/` with `Station_PushUp` (needs Item_TaTapGa), `Station_Treadmill`, `Station_PunchingBag`; story gate in `TrainingStation.TrainingRefusal` (`ChickenTransformationTrigger.TrainingUnlocked`).

[VERIFIED-PLAY] this session:
- Gate: at the start of a fresh Play session (garden quest not done) all 3 `TryStart()` returned False and money stayed 500. After the real story path (`garden_protein` accepted, 3 steps, `TurnIn`) -> `Transformed` true, `TrainingUnlocked` true, `chuan_bi_tai_dau` Active.
- After waking the chicken (WakeUp, 82 frames): push-up without the weights refused (money unchanged). Treadmill session: 611 frames (12.2 s incl. the walk), Speed 10 -> 12, money 500 -> 450. Punching bag: 298 frames (6.0 s), Attack 10 -> 12, money 450 -> 390, banner `Đấm bao cát xong! Tấn công 10 → 12  (-60 tiền)`. Pet combat values changed with it: damage 10 -> 12, attackCooldown 1.5 -> 1.415.
- Combat effect, real-frame fights with `BattleManager.isFirstBattle=false` (rooster HP 150, dmg 18, cd 1.5; pet starts HP 100, dmg 10, cd 1.5): UNTRAINED pet LOST after 15.5 s (pet HP 0, rooster left with 30). Pet with 2 sessions (Attack 12, Speed 12) WON after 9.1 s (pet HP 28 left). One run each, not a statistical test, but it shows trained stats really change the outcome.

Not implemented / not verified (unchanged from section 28): protein as a training item (the shop's `Thức ăn tăng lực` still has no effect; the task says 'if implemented'); a stats HUD; buying Tạ tập through the real shop UI; real E key; how the stations look; save/load.
Console: no project errors during these runs.

## 30b. CORRECTION of an earlier note (2026-10-20)
An earlier session note claimed RematchController was not in the scene and set the quest to 3 sessions. That was wrong: the other session's wiring on `NPC_ChildhoodFriend` and `requiredAmount` 5 are the real state (see the Week 3 Task 6 section above). The duplicate `RematchController` GameObject I added was REMOVED (two controllers would have double-counted progress); the scene was saved. The quest asset is back at 5 sessions. Test numbers (3 sessions, 1000 -> 850 money) in the removed note applied to that temporary 3-session setup only (code-path tests).

## 30. Week 3 Task 6 - Prepare for the rematch (`chuan_bi_tai_dau`) (2026-10-19, NOT committed)
Tags: [VERIFIED-PLAY] = Play Mode, editor paused + `EditorApplication.Step()` (0.02 s per frame), dialogue buttons pressed through `onClick`, player teleported next to the friend, the first fight and the garden/sleep part replaced by QuestManager calls (those two parts were verified in sections 25/27). NO real keyboard/mouse, NO Game-view look.

State found before the change: `chuan_bi_tai_dau` (objective key `prepare_rematch`, 1 step, no reward) was started by `ChickenTransformationTrigger` after the garden quest, but NOTHING ever reported `prepare_rematch` and nothing could start a rematch: the friend only said an idle line, GuguGaga's `BattleTrigger` is switched off, and `BattleManager.isFirstBattle` stays true forever (a second `StartBattle` would re-apply the scripted first-battle damage 1 vs 15).

Changed:
- NEW `Assets/Script/Story/RematchController.cs`, added to `NPC_ChildhoodFriend` and wired in the scene (scene saved). It listens to `ChickenStats.Changed` (= one finished training session) and calls `QuestManager.ReportProgress("prepare_rematch")`; at the required amount the quest is ReadyToTurnIn and ONLY then the friend offers the button `Tái đấu!`. The button sets `BattleManager.isFirstBattle = false`, heals both chickens, places the chicken 4 m from the rooster (agent.Warp, the pet only fights inside its 6 m detection radius) and calls `BattleManager.StartBattle()`. It watches the battle state: rooster dead -> `QuestManager.TurnIn` once (+ the friend talks after 2.5 s, `RematchController.RematchWon`); chicken dead -> banner, after 3 s the chicken is revived with the same steps as `PostLossSequence` (which is one-shot), the rooster is healed, the player can train and retry.
- `ChildhoodFriendBattleTaunt.cs`: ONLY the branch 'first quest already completed' now asks `RematchController.TryInteract()` before the old idle line. His E key and prompt are unchanged.
- `BattleTrigger.cs`: one line `if (!enabled) return;`. Found by a stack trace: the trigger is switched off in the scene but Unity still delivered `OnTriggerEnter`, and the friend now stands inside its 10 m sphere (rooster at 27.5/19.8, the player at 24/17.7), so walking up to the friend with the chicken started a FIRST-battle fight by itself. Without this line the rematch could be bypassed.
- Quest asset `Quest_ChuanBiTaiDau`: objective `Kiếm tiền, mua đồ tập và luyện gà ở khu huấn luyện`, label `Buổi tập`, requiredAmount 5, turn-in text `Gà đã sẵn sàng: gặp bạn thân để tái đấu`, description names ve chai / giao hàng / Tạ tập ở chợ (that is the guidance; the design documents do not give a number, see assumptions). Reward still 0.

[VERIFIED-PLAY]:
- No battle starts by itself when the player stands at the friend with the awake chicken (it did before the BattleTrigger line). Before the preparation is done the friend only says the progress (`Gà mày mới tập 0/3 ...`, later `4/5`) and `StartRematch()` does nothing.
- Each finished training session = +1 (treadmill 1/3 ... punching bag 2/3, 3/3 -> ReadyToTurnIn at the required amount, never above it). Progress does not move before the quest is Active.
- At ReadyToTurnIn the friend shows `Ồ, nhìn con gà mày khác hẳn rồi đấy. Sẵn sàng tái đấu chưa?` with the button `Tái đấu!`. Double click and a third `StartRematch()` while fighting did nothing extra; `isFirstBattle` was false and the pet used its trained damage.
- Loss path (twice): banner `Thua rồi! Luyện thêm ...`, after ~3 s + get-up the chicken was alive (HP 100, PetCombatAI Follow), rooster healed to 150, quest still ReadyToTurnIn, the friend offers `Tái đấu!` again (`Sao, chưa phục à?...`).
- Win path: rooster dead -> quest Completed once (a later `StartRematch` and `TurnIn` do nothing), `RematchWon` true, the friend's win dialogue opened by itself, talking to him again gives the win lines. Money unchanged (reward 0).
- Balance samples (real fights, one run each, NOT statistics; hit timing is noisy): 3 sessions (Attack 12, Speed 14): LOSS; Attack 12 Speed 12: LOSS in 2.2 s then Attack 14: LOSS then Attack 16: WIN (pet HP 64 left); an earlier test with Attack 12 Speed 12 WON. Untrained chicken LOST (rooster left with 30 HP). So requiredAmount was set to 5 sessions (about 250-300 coins, about the money from the scrap + watering + delivery quests) and a loss costs nothing.
- Console: no C# errors. Only the known URP/Lit shader memory errors and MCP/Account notices.

Assumptions (the design documents do not fix these):
- The requirement for the rematch is 5 finished training sessions (any station); owning Tạ tập is not forced (the treadmill and bag need none). The 'obtain equipment' step is only guidance text.
- The rematch is a plain re-fight with GuguGaga (stats as in the scene). The tournaments / medals / final boss of GAME_DESIGN.md are NOT built; winning completes `chuan_bi_tai_dau` and sets `RematchController.RematchWon`, nothing else follows yet (no ending).
- After the win GuguGaga stays dead (no revive, nothing else uses him yet).

NOT verified / known: the real E key and the dialogue button by mouse; how the rematch looks (the chicken is warped 4 m from the rooster); the chicken's real walk from the friend to the arena (not used); the 5th session reaching ReadyToTurnIn with `requiredAmount` 5 (only reached 4/5 in the last run because training has a short anti-spam cooldown; 3/3 -> Ready was verified when the amount was 3); the first fight and the sleep were replaced by quest calls in these tests; Vietnamese diacritics in the TMP font; save/load (flags reset when Play stops).
Exact next step: play it once by hand with real keys: transform -> train 5 times -> talk to the friend -> `Tái đấu!`; then decide whether tournaments / final boss (GAME_DESIGN.md) come next.


## 31. Manual verification checklist (Task 5/6) + M9 NPC tournament, first slice (2026-10-20, NOT committed)

### A. Manual test checklist (to be run BY THE USER in Play Mode - NOT performed by me; status: PENDING)
Real keys and mouse, nothing forced. Console open (Errors only).
1. Story: Play -> intro -> walk home -> E on Ông (rescue, chicken wakes) -> E on the friend (accept challenge, the first fight is scripted to lose) -> taunt -> E on Bà (chore) -> garden quest (water 3 plants with the can, bón, hái, cho gà ăn) -> E on the bed (sleep) -> banner about the transformation. Expect: HUD quest `Chuẩn bị tái đấu` 0/5.
2. Training: before sleeping, a station must refuse (banner). After: earn coins (scrap / delivery / chores). Shop NPC: E, buy `Tạ tập` with the mouse (money drops once, item appears in the hotbar; not enough money -> message, nothing spent). Do 5 sessions (treadmill, bag, push-up needs Tạ tập). Expect HUD 1/5 ... 5/5 and the quest turns ReadyToTurnIn only at 5/5 (the sessions have a short cooldown).
3. Rematch: with < 5 sessions the friend (E) only says the progress; at 5/5 he offers `Tái đấu!`. Click the button with the mouse: fight starts once (clicking twice must not start two).
4. LOSS branch: let the chicken lose (train less). Expect banner, chicken gets up after ~3 s, friend offers the rematch again, quest still ReadyToTurnIn, no reward.
5. WIN branch: quest Completed once (talk again -> win lines, no second completion); the tournament quest `Giải đấu làng` 0/2 appears.
6. M9 (new): walk to Chị Ba (65,-50) and Chú Tư (-60,-38). Chú Tư must refuse until Chị Ba is beaten. E on Chị Ba -> button `Thách đấu!` -> fight. Win = banner `Nhận 1 huy chương và 200 tiền` once; Chú Tư afterwards 350. Re-talk to a beaten opponent: no second reward. Loss: no reward, chicken recovers, retry possible.
7. Check: E prompt appears/disappears near each NPC and station, no double dialogue, Vietnamese letters render, nothing reappears after Esc.

### B. M9 - what was found and built
Found: nothing for tournaments (no Opponent/Medal/Tournament script); `BattleManager` hard-wires one fighter pair; `NPC_ChuTu / CoLan / ChiBa / BacHai` only had `VillagerFlavorNPC` lines. First incomplete M9 requirement = opponent data + order + challenge + result handling with one-time rewards.
New files (`Assets/Script/Tournament/`): `OpponentDefinition.cs` (ScriptableObject: stats, rewardMoney, medals, dialogue), `TournamentManager.cs` (order, unlock, battle start/result, reward once, loss recovery, tournament quest), `TournamentOpponentNPC.cs` (E interaction + dialogue per state). NO existing script changed. BattleManager is not edited: the manager points `BattleManager.enemyHealth/enemyCombat` at the opponent rooster for one fight and restores them afterwards (the rematch keeps using GuguGaga).
Scene (saved): GameObject `TournamentManager`; clones `Rooster_ChiBa` (beside NPC_ChiBa) and `Rooster_ChuTu` (beside NPC_ChuTu) made from GuguGaga (BattleTrigger off); `TournamentOpponentNPC` added to `NPC_ChiBa` (opponent 0) and `NPC_ChuTu` (opponent 1); their `VillagerFlavorNPC` is switched OFF (otherwise two scripts would fight for E; the flavor lines were copied). Assets: `Assets/Data/Opponents/Opponent_ChiBa.asset` (HP 130, dmg 14, +200 coins, 1 medal), `Opponent_ChuTu.asset` (HP 190, dmg 18, +350 coins, 1 medal); quest `Assets/Data/Quests/Quest_GiaiDauLang.asset` (`giai_dau_lang`, key `win_tournament_match`, 2 wins, reward 0).
Assumptions (design leaves them open, GAME_DESIGN 4.8): opponents = Chị Ba then Chú Tư (the two nearest of the four candidates); stats and rewards are my starting values from the proposed ranges; the tournament opens when `chuan_bi_tai_dau` is Completed (the implemented rematch comes before the tournament; GAME_DESIGN 4.9 would make the rematch the final boss - the final boss/ending is NOT built); medals are a counter `TournamentManager.Medals` (exchange = M10, no HUD yet); no save/load.
[VERIFIED code-path, Play Mode, real frames, `Time.captureDeltaTime`, quest state of the story/rematch FORCED through QuestManager calls, dialogue buttons replaced by calling the same `StartChallenge`]: before the rematch is won opponents give only flavor lines and `StartChallenge` does nothing; after it the tournament quest becomes Active 0/2; Chú Tư is locked until Chị Ba is beaten (dialogue + `StartChallenge(1)` refused); loss vs Chị Ba with an untrained chicken: no money/medal, quest 0/2, `BattleManager` refs restored, chicken revived (HP full), retry dialogue; spam `StartChallenge` during the fight did nothing; trained chicken (18/18) won: +200, 1 medal, 1/2, `isFirstBattle` false; challenging the beaten opponent again: no battle, no money; Chú Tư won: +350 (total 550), 2 medals, quest Completed once; challenging again: nothing; Console 0 errors.
[NOT verified]: everything in checklist A (real keys, mouse, shop UI, 5-session flow, real rematch buttons), visuals (the cloned roosters look identical to GuguGaga), the walk to the far NPCs, the first `StartChallenge` call right after `WakeUp()` in the test did not start a fight (chicken likely not fully up yet; a retry after 150 frames worked - not investigated further), balance (one run each), Vietnamese diacritics, save/load.
Remaining M9: balance tuning after a manual run; visual difference for the opponent roosters; optional third opponent (design says cut first). Next milestone: M10 medals + Reward NPC (uses `TournamentManager.Medals`; each medal exchanged once).

## 32. M10 - Medal rewards and redemption, first slice (2026-10-20, NOT committed)

### A. What the design requires (DEVELOPMENT_ROADMAP M10, GAME_DESIGN 4.8)
Count medals; a Reward NPC exchanges medals for rewards (money / items / unlock) reusing `ItemDefinition`, `CurrencyWallet` and the shop-style UI, in the existing `RewardArea`; done when each medal is exchanged only once and there is no betting. Which rewards, and how many medals open the final boss (GAME_DESIGN open question 6), are NOT specified.

### B. Found before coding
Medals were only the counter `TournamentManager.Medals` (earned, never spent). No Reward NPC, no exchange UI, no reward data (`RewardArea` = decoration + empty spawn point `RewardNPCPoint` at (46,0,55)). First incomplete requirement = the exchange itself. Medals/money/items do NOT persist (no save/load in the project); they reset with each Play.

### C. Built (reuse, no new manager / currency / inventory)
- `Assets/Script/Reward/MedalRewardCatalog.cs` - ScriptableObject list of `MedalRewardEntry` (rewardId, name, description, medalCost, optional `ItemDefinition` + count, optional money). Adding a reward = adding an entry.
- `Assets/Script/Reward/RewardUI.cs` - exchange window (clone of the `ShopUI` panel, reuses `ShopItemRow`). `TryRedeem(row)` order: window closed -> nothing; already claimed -> message; not enough medals -> message; item does not fit -> message, medals NOT spent; then medals spent, claimed flag flipped BEFORE granting, item added (refund + unclaim if the add unexpectedly fails), money added to the existing `CurrencyWallet`. Claimed row shows `Da nhan` and its button is disabled.
- `Assets/Script/Reward/RewardNPC.cs` - E interaction like `ShopNPC` (shared prompt, range, blocks while shop / dialogue / inventory is open). Different greeting when no medal is available.
- Small additive edits to existing scripts: `TournamentManager` (+`MedalsSpent`, `AvailableMedals`, `TrySpendMedals`, `RefundMedals`; `Medals` stays the total EARNED so a later final-boss gate can read it), `ShopItemRow` (+`BindText`, `SetButton`). Nothing existing was changed in behaviour.
- Scene (saved): `QuestUI_Canvas/RewardUI`, `NPC_RewardKeeper` at `RewardNPCPoint` (clone of the market villager model - looks identical to the shopkeeper). Asset: `Assets/Data/Rewards/MedalRewardCatalog_PhanThuong.asset`.

### D. Assumptions (design leaves them open)
Two rewards, 1 medal each, each redeemable once: `Thuc an tang luc` x3 (existing item `chicken_feed`) and 300 money. Exactly matches the 2 medals the two M9 opponents give. No unlock-type reward and no final-boss gate (M12). Medals spent for rewards do not reduce `Medals`. The money value 300 and the item are starting values, unbalanced.

### E. Tests
[VERIFIED code-path, Play Mode, real `Button.onClick` for rows, Console 0 errors; medals FORCED to 2 through reflection on `TournamentManager.Medals` (no real battle was fought in this test)]: closed window -> `TryRedeem` false; `RewardNPC.Interact()` opens the window once (second call false); 0 medals -> refused, nothing changed; bag full (5 slots, feed stack full) -> refused, medals not spent, not claimed; after freeing space button 0 -> medals 2->1 available, feed 0->3, claimed, button disabled; button clicked 5 more times + `TryRedeem(0)` -> no change; money row clicked 3 times -> money +300 once, medals 0 available, spent 2, earned still 2; both claimed -> both refused; after `Close()` a stale `TryRedeem` -> false; `TrySpendMedals(0/-1/over balance)` -> false.
[NOT verified]: real E key at the Reward NPC, mouse clicks on the real window, the window layout / Vietnamese text on screen, walking to (46,0,55), earning the medals by really winning the M9 fights, the hotbar showing the feed, the prompt position next to the NPC, Reward NPC visuals.

### F. Manual checklist (PENDING, to be run BY THE USER in Play Mode)
1. Before any medal: walk to the Reward NPC (46,0,55), E -> window opens, `Huy chuong: 0`, rows show `Can 1 huy chuong`; click `Doi` -> red message, nothing changes; Esc closes, movement works again.
2. Win Chi Ba (M9) -> return, E: `Huy chuong: 1`. Click `Doi` on `Thuc an tang luc` -> hotbar shows it x3, row says `Da nhan`, button greyed. Click again -> nothing.
3. Fill the hotbar first, then redeem the feed -> `Tui day` message, medal still there.
4. Win Chu Tu -> exchange the 300 money row once: money +300 once, even if clicked fast.
5. Prompt `Nhan E de doi huy chuong` appears / disappears near the NPC and does not overlap the shop prompt.

### G. Remaining / next
Remaining M10: manual run (F), optional different look for the Reward NPC, medal HUD (design P3 UI mentions it), icon for medal (`Assets/UI/UI/PNG/*/medal1,2.png`, licence not checked), save/load. Next: decide how many medals open the final boss (open question 6) before M12.


## 33. Checkpoint: M10 verification, final-boss gate (open question 6), M12 plan (2026-10-20, NOT committed, NO project file changed)

### A. M10 verification [VERIFIED-PLAY: real frames via Pause + EditorApplication.Step, dialogue/UI buttons pressed through onClick, player teleported (+2 m in y; the capsule is 3.8 m tall and sinks through the ground if put at y=0 - that was a test slip, not a game bug), NO real keyboard/mouse]
Played the REAL story to the sleep (intro skipped by itself this time, Ông -> chicken wakes -> friend: first fight lost by the AI in ~22 s -> automatic taunt + PostLossSequence revive -> Bà chore -> garden -> bed -> chicken swapped to chicken_pet, PetCombatAI on). Only shortcut: `chuan_bi_tai_dau` set Completed through QuestManager (stands for 5 training sessions + the rematch), and `ChickenStats.Train` to raise stats. Then FOUR real tournament fights (AI vs AI):
1. Chi Ba, chicken 10/10/10: LOST (pet 0). No medal, no money (150 stays), `lost` recorded, banner/recovery: chicken revived (HP full), back to ChickenIdle and Follow after ~6 s, `IsBusy` false, retry dialogue shown.
2. Chi Ba, 18/18/18, button clicked twice: ONE fight; WON. Medals 1, money 150 -> 350, banner 'Thắng! Nhận 1 huy chương và 200 tiền', Chu Tu unlocked, BattleManager refs restored (GuguGaga).
3. Chu Tu, 18/18/18: LOST (no reward). 4. Chu Tu, 24/24/24: LOST (foe left 66/190). 5. Chu Tu, 36/36/36: WON. Medals 2, money +350, `giai_dau_lang` Completed, both opponents locked afterwards; StartChallenge on beaten opponents does nothing (medals/money unchanged).
Reward NPC with REAL medals: `RewardNPC.Interact()` opens the window (rows 2); runtime texts are correct Vietnamese with diacritics ('Đổi huy chương', 'Huy chương: 1    Tiền: 350', 'Thức ăn tăng lực', 'Cần 1 huy chương', 'Đổi', 'Đã nhận', 'Bạn đã đổi ... rồi'); 0 rects outside the 1920x1080 canvas. Row 0: 3 clicks -> exactly one redeem (avail 1 -> 0, `Item_ThucAnTangLuc` x3 in the inventory). Row 1 (money): 3 clicks -> +300 once (700 -> 1000); both rows 'Đã nhận'. Medals earned stays 2, spent 2. Close -> window closed, movement back.
Font: `LiberationSans SDF` (static) has all 74 tested Vietnamese letters. Correction: section 32 said the strings were ASCII ('Da nhan'); the code and screen use full diacritics.
Console after the runs: 0 project errors/warnings (only Unity-internal 'JobTempAlloc ... older than 4 frames' notices caused by manual frame stepping, FMOD and Account API messages).
Defects found: NONE confirmed. Cosmetic only: a claimed row still shows 'Cần 1 huy chương' next to 'Đã nhận'. Balance data (not a bug, one run each): Chi Ba is beaten at 18/18/18 (lost at 10); Chu Tu beat 18 and 24 and was beaten at 36/36/36 (HP 230, dmg 49) - the design wants the final boss beatable 'without maxing'; max stat value not checked. Note: a tournament fight started while the starter normal_chicken is still the visible chicken makes both stand still (PetCombatAI off); unreachable in the real flow because the tournament opens only after the rematch quest, which starts after the swap.
STILL PENDING (needs you, real input): (1) E key at the Reward NPC and at each opponent (prompt shows/hides, no overlap with the shop prompt); (2) real mouse clicks on the Reward window; (3) how the window, the banner and the colours look in the Game view (no true screenshot possible; layout checked only numerically); (4) walking to (46,0,55) and to Chi Ba / Chu Tu; (5) the checklists in sections 31-F and 32-F; (6) save/load does not exist, medals reset each Play.

### B. Final-boss gate - open question 6
Exact text, GAME_DESIGN section 12, item 6: 'Cần bao nhiêu huy chương để mở final boss.' Section 4.9: 'quay lại tái đấu bạn thân cũ khi đã vượt các giải đấu (điều kiện: số huy chương cần có)'; proposed boss stats HP 300-360, damage 30-38, 'thắng được khi luyện kha khá, không cần luyện đầy'; win = end of game. ROADMAP M12: quest 'Phục thù — trận đấu cuối cùng' opens 'khi đủ huy chương (quyết định 6)'; pre-fight line 'MÀY SỢ À'; lose = retry, no Game Over; win sets `finalBossDefeated`.
The number is NOT specified anywhere. Unresolved: (a) the threshold; (b) section 4.9 last bullet says the revenge IS the final boss and the tournament sits between, but the built flow (Task 6) has a rematch vs GuguGaga BEFORE the tournament, and the tournament opens only when it is Completed.
What exists to gate on: `TournamentManager.Medals` (total EARNED, never reduced by spending - so redeeming rewards cannot lock the boss), `IsDefeated(i)`, quest `giai_dau_lang` (2/2 Completed when both opponents are beaten), quest `chuan_bi_tai_dau` (Completed = rematch won). Only 2 medals can be earned now (3rd opponent is optional/cut-first), so any threshold is 1 or 2 unless a 3rd opponent is added.
Options for the threshold (smallest first): A) all medals of the existing opponents = 2 (= `giai_dau_lang` Completed; matches 'đã vượt các giải đấu'; no invented number). B) 1 medal (boss opens after the first win; the 2nd opponent becomes optional). C) add the 3rd opponent and require 3 (more work, more balance). Options for the rematch conflict: i) keep the built rematch as a story step and make the boss a SECOND, stronger fight vs the friend (least change, but two fights vs him; differs from 4.9); ii) follow 4.9: remove the rematch from the front (training sessions alone would open the tournament) and make the final-boss quest the rematch (changes the finished Task 6 quest flow).
DECISION NEEDED FROM YOU: threshold A/B/C and conflict i/ii. My suggestion: A + i. Not implemented.

### C. M12 scope and prerequisites (ROADMAP)
M12 = quest 'Phục thù', gate on medals, friend dialogue 'MÀY SỢ À', boss fight reusing the M9 fight mechanism, retry on loss, set `finalBossDefeated`. Depends on M9 (done, first slice), M10 (done, first slice, code+real-fight verified), M11 (NOT built: friend dialogue by flags; the friend only has the existing challenge/taunt/rematch lines). `finalBossDefeated` has no home: M1 `StoryProgress` was never built (no script; flags still live in statics and quest states). M13 (ending/credits) depends on M12. Missing prerequisites: M11 (small, mostly dialogue data) and a place for `finalBossDefeated` (M1 or a bool on the boss component).

### D. Exact next task
After you answer B: (1) M11 minimal slice: friend lines that change with quest/medal state (after taunt, after transformation, after each tournament win), data only; (2) then M12 per your choice. Before that, please run the manual checklists in 31-F and 32-F.


## 34. M11 - Dialogue that changes with story progress (2026-10-20, NOT committed)

### A. What was found
The friend's dialogue (`ChildhoodFriendBattleTaunt.cs`, `RematchController.cs`) already contained state-aware
line sets from an earlier pass in this same task (not yet checkpointed in this file): `transformedLines`,
`tournamentOpenLines`, `tournamentPartLines`, `tournamentDoneLines`, and `CompletedLines()` reading
`TournamentManager`. No new script or NPC was created for M11; this entry verifies and records that existing work.

### B. Roadmap M11 requirements vs implementation
All 5 required branches exist and were verified this session:
1. After initial defeat -> `ChildhoodFriendBattleTaunt.afterLossLines` ("MÀY SỢ À", already in place pre-M9).
2. After transformation / training unlocked -> `RematchController.transformedLines`, shown once (`transformCommentShown`), concatenated with the prep line.
3. While preparing the rematch -> `notReadyLines`, formatted with real `{0}/{1}` session progress.
4. After the rematch is won/lost -> `wonLines` (auto-shown once after victory) / `retryLines` (offered again after a loss).
5. After tournament victories, including the 2-medal transition -> `CompletedLines()`: `tournamentOpenLines` (0 beaten) -> `tournamentPartLines` (1/2, formatted) -> `tournamentDoneLines` (2/2, with the real medal count).

### C. Tests [VERIFIED-PLAY, real combat resolution, Console 0 errors; only the chicken's awake-state and the two training/rematch quests were forced through QuestManager/MainChickenAcquire fields to reach each stage quickly - the fights themselves were real: `TournamentManager.StartChallenge`, `BattleManager.StartBattle`, opponents killed via `HealthSystem.TakeDamage`, resolved by the manager's own `Update()`]
Read the actual `TMP_Text` shown by `DialogueUI` at each stage, in order, in one continuous Play session:
- Active/0 progress, not transformed -> "Gà mày mới tập 0/5 buổi thôi à?..." (notReadyLines).
- Forced `grandparents_helper` + `garden_protein` to Completed -> `ChickenTransformationTrigger` opened Bà's line and `Transformed` became true (this chain itself was already covered in section 25/27-30; re-confirmed only as a setup step here).
- 1st talk after transform -> transformedLines first page ("Khoan... gà mày sao tự dưng to ra..."); 2nd talk -> no repeat, straight to notReadyLines.
- Forced `chuan_bi_tai_dau` to Completed -> talk shows `tournamentOpenLines` (0 beaten): "...giải làng đang mở, chị Ba với chú Tư đang chờ."
- Real win vs Chị Ba (`StartChallenge(0)` -> real `InBattle` -> opponent HP forced to 0 -> manager's `Update()` resolved it) -> Medals 1, `tournamentPartLines` formatted "...hạ được 1/2 người...".
- Real win vs Chú Tư the same way -> Medals 2, both `IsDefeated`, `giai_dau_lang` Completed, dialogue -> `tournamentDoneLines` "Hạ cả chị Ba lẫn chú Tư, lấy đủ 2 huy chương rồi cơ à?...".
Defect found and NOT a product bug: `GameObject.SendMessage("OnWin", int)` reliably fails in this MCP harness ("Failed to call function OnWin" in Console) - switched the test to the real public `StartChallenge` + real HP damage instead, which works correctly. No code change needed; noted here only so a future session does not waste time on the same dead end.

### D. Not verified
Real E key / mouse at the friend NPC for each of these lines; how the text looks in the Game view; the exact
moment-to-moment feel of the auto-taunt and the post-win delay (`tauntDelaySeconds`, `victoryDialogueDelay`) with
real input instead of forced state.

### E. M11 checkpoint: COMPLETE. Next: M12 (final boss), starting now per the task's instruction to not skip ahead.


## 35. M12 - Final Boss: stronger childhood friend rematch (2026-10-20, NOT committed)

### A. Decisions used (owner-confirmed this task, GAME_DESIGN open question 6 now resolved)
Gate = 2 earned tournament medals (`TournamentManager.Medals`, the EARNED total - spending at the Reward NPC only
changes `MedalsSpent`, never `Medals`). Story order preserved: existing rematch (`chuan_bi_tai_dau`, vs GuguGaga,
normal stats) stays before the tournament; the Final Boss is a separate, later, stronger fight against the same
friend/rooster. Not implemented: option (ii) from section 33 (merging the rematch into the boss) - explicitly
rejected by the owner's decision 2/3.

### B. Built (reuse, no duplicate manager/battle system)
- `Assets/Script/Story/FinalBossController.cs` (new) - mirrors `RematchController`'s shape but is a separate,
  smaller controller: gate check (`Medals >= 2`), challenge/taunt dialogue ("MÀY SỢ À"), `StartFinalBattle()`,
  win/loss handling, recovery coroutine (duplicated in miniature from `RematchController`/`TournamentManager`,
  not factored out - acceptable per the task's 'smallest compatible solution', no shared logic was extracted
  into a new manager). Does not touch `BattleManager`, does not clone a rooster: it reuses the exact same
  GuguGaga / `BattleManager.enemyHealth` + `enemyCombat` the rematch already points at, temporarily raising
  `maxHealth` to 330 and `damage` to 34 (middle of GAME_DESIGN 4.9's 300-360 / 30-38 range; `attackCooldown`
  left unchanged, not specified) only for the duration of one fight, restored to the original 150/18
  immediately after - win or lose - in `RestoreFoeStats`.
- One-line additive edit to `RematchController.cs`'s existing `default:` (Completed) branch of `TryInteract()`:
  it now asks `FinalBossController.Instance.TryInteract()` first and only falls back to `CompletedLines()`
  (the M11 tournament-progress text) if that returns false. No other line of `RematchController` or any other
  existing script was changed.
- `Assets/Data/Quests/Quest_PhucThuCuoiCung.asset` (id `phuc_thu_cuoi_cung`, key `final_boss_win`, amount 1,
  reward 0) - the completion flag, using the same 'quest completion = one-shot story flag' convention as every
  other flag in this project (`chickenSaved`, `firstBattleLost`, `chickenTransformed`...). No new flag
  component/framework was added, matching the roadmap's advice (`StoryProgress` was proposed but never built,
  and M12 does not need it: `QuestManager.GetState("phuc_thu_cuoi_cung") == Completed` IS `finalBossDefeated`).
- Scene (saved): `FinalBossController` added to the existing `NPC_ChildhoodFriend` (same GameObject as
  `RematchController` and `ChildhoodFriendBattleTaunt` - no new NPC, no new BattleManager, no new
  TournamentManager; counts verified 1 each after adding).

### C. Design choices left to the smallest consistent implementation (not specified anywhere)
Below 2 medals, asking the friend about the boss shows the EXISTING M11 tournament-progress lines
(`tournamentOpenLines` / `tournamentPartLines`) instead of a separate 'locked' message - these already say
exactly what is missing, so no duplicate text was added for the 0-medal/1-medal cases. Boss stats: HP 330,
damage 34 (midpoints of the proposed range). No separate ending screen/credits: on victory a banner
('Chiến thắng! Bạn đã đánh bại bạn thân cũ!') plus an in-dialogue reaction (`wonLines`) serve as the 'ending',
matching the project's existing level of polish (no cutscene/Timeline work, consistent with every other
milestone's 'smallest implementation' instruction); a dedicated end screen is NOT built.

### D. Tests [VERIFIED-PLAY, real combat resolution, Console 0 project errors; chicken-acquired state and the
rematch quest were forced through `MainChickenAcquire`/`QuestManager` fields as setup shortcuts only - every
tournament and final-boss fight itself was real: `TournamentManager.StartChallenge` / `FinalBossController.
StartFinalBattle` -> real `BattleManager.StartBattle` -> opponents' `HealthSystem.TakeDamage` -> resolved by
the controllers' own `Update()`]
In one continuous fresh Play session: 0 medals -> asking the friend shows the tournament-progress line, final
boss quest stays NotStarted (locked, case covered). Won vs Chị Ba (1 medal) -> still locked, `tournamentPartLines`
shown, quest still NotStarted. Won vs Chú Tư (2 medals) -> `RematchController` handed off correctly: the
challenge/taunt dialogue opened and the quest flipped to Active in the same interaction (unlocked, case covered).
`TrySpendMedals(2)` (simulating redeeming both medals at the Reward NPC) -> `Medals` stayed 2, `AvailableMedals`
dropped to 0 - re-asked the friend afterwards and the boss was still offered (spending does not revoke
eligibility, case covered). `StartFinalBattle()` -> real fight, opponent's `maxHealth`/`damage` measured at
330/34 (boosted, confirmed). Forced a real LOSS (killed the player's chicken) -> quest stayed Active (no
reward), opponent stats restored to 150/18, `FinalBossDefeated` stayed false, recovery ran and the chicken was
alive and idle again by the next check, revisiting offered a retry line + the same button (defeat/retry case
covered, no completion). Fought again, clicking the accept button a 2nd time mid-fight did nothing
(`IsBattleActive` unchanged, one fight only) -> real WIN (killed the opponent) -> quest flipped to Completed
exactly once, `FinalBossDefeated` true, stats restored again, banner shown (victory case covered, exactly once).
Revisiting afterwards showed the idle post-victory line, and calling `StartFinalBattle()` again directly did
nothing (quest no longer Active, no second fight, no duplicate reward - repeat-interaction case covered).
Compilation: clean (0 errors) when checked in Edit Mode; a batch of `MouseMovement.cs` NullReferenceExceptions
appeared ONLY while recompiling in the middle of an already-running Play session (a known MCP/no-real-mouse-focus
artifact, not caused by the new code - confirmed by stopping Play, recompiling in Edit Mode with 0 errors, then
re-entering Play fresh for all the tests above).

### E. Not verified
Real E key / mouse clicks on the friend NPC and the dialogue/rematch buttons; how the challenge dialogue, the
victory banner and the stat numbers read in the Game view; walking the real distance to Chị Ba / Chú Tư and back
to the friend between each step; the 3rd (optional, cut-first) tournament opponent was not added, so only the
2-medal threshold was testable; save/load does not exist - `FinalBossDefeated`, `Medals` and every other flag
reset on each Play, so 'Final Boss defeated' does not persist across sessions.

### F. Manual checklist (PENDING, to be run BY THE USER in Play Mode)
1. Beat the tournament for real (walk + E + mouse clicks at Chị Ba then Chú Tư, 2 medals).
2. Talk to the friend (E): challenge dialogue with "MÀY SỢ À", click `Phục thù!` with the mouse - exactly one
   fight starts even if clicked rapidly.
3. Let the fight resolve a loss on purpose (undertrained chicken) -> banner, chicken gets up after ~3 s, talking
   to the friend again offers `Phục thù!` once more, no money/medal/completion changed.
2. Win for real -> victory banner, friend's in-dialogue reaction, talking again shows the idle post-victory line
   only, no further fights start.
5. Confirm the Reward NPC still lets you redeem both medals before AND after beating the final boss (spending
   does not lock you out of anything retroactively - there is nothing left to lock once it's won, but redeeming
   before the boss fight must not block starting it).

### G. M12 checkpoint: COMPLETE (code-path + Play Mode verified as above). M11 and M12 are both done for this
task. Next (not started, not requested this task): a real ending/credits presentation beyond the banner+dialogue
reaction, balancing the final-boss stats from more than one playtest, the optional 3rd tournament opponent, and
save/load for every story flag in this project (none of it persists yet).


## 36. Checkpoint confirmation + consolidated manual-test checklist (2026-10-20, NOT committed, NO code changed)

### A. Checkpoint confirmed
Read sections 34-35: M11 and M12 are both marked COMPLETE, verified via code-path + real-combat Play Mode
(synthetic setup, real fights). Nothing in M11/M12 was redone or duplicated this pass, per instruction.
Outstanding across the whole project (not just M11/M12): no real E-key/mouse test has been run by anyone yet;
no one has looked at the actual Game View (dialogue, banners, HUD, Reward/Shop windows); save/load does not
exist anywhere.

### B. Static audit done this pass (Edit Mode only, no Play Mode, no system touched)
Checked all 10 interactable NPCs for overlapping E components and proximity: exactly one active
Interact-owning component per NPC (tournament/shop flavor NPCs correctly have `VillagerFlavorNPC` disabled
once their real script takes over; `NPC_ChildhoodFriend` correctly has only `ChildhoodFriendBattleTaunt`
reading the key, with `RematchController`/`FinalBossController` invoked through it, and
`QuestObjectiveTarget` yielding via `ClaimsInteraction` - no double-dialogue risk found).
One pre-existing (not new, not caused by M11/M12) observation, not a confirmed bug: `NPC_Grandfather` and
`NPC_Grandmother` stand 1.3 m apart, closer than either one's ~3 m interaction radius, so both prompts could be
in range at once. No code changed for this - added to the checklist below for a real play-test to confirm
whether it actually causes prompt flicker, since fixing it blind without seeing the Game View risks a
needless change.
No bug confirmed -> nothing was fixed this pass (per instruction 3: only fix what is found).

### C. Manual test checklist - PRIORITY (to be run BY THE USER, real E key + real mouse, Game View open, Console errors-only)
Play once, start to finish, in this order:
1. **Grandparents' house:** E on Ông then Bà from a normal approach distance - confirm only ONE prompt shows
   at a time and it is readable (font renders, not boxes). Confirms/denies the 1.3 m proximity note above.
2. **Friend - first fight:** E opens the challenge, mouse-click the accept button - exactly one fight starts.
   After the scripted loss, confirm the taunt ("MÀY SỢ À") appears once, not repeated on next talk.
3. **Chores -> transform:** Bà's quest, garden steps, sleep prompt - confirm each E prompt appears/disappears
   cleanly and the banner about the transformation is readable.
4. **Training + shop:** E at the Shop NPC, buy `Tạ tập` with a real mouse click (money deducted once, item
   in hotbar); do training sessions until `Chuẩn bị tái đấu` reaches ready; confirm HUD progress text.
5. **Rematch:** E on friend -> `Tái đấu!` button with the mouse - one fight only even on a double-click.
   Check both a loss (retry offered) and the eventual win.
6. **Tournament:** walk to Chị Ba then Chú Tư, E -> `Thách đấu!` - confirm Chú Tư's prompt/dialogue explains
   he is locked before Chị Ba is beaten. Win both; confirm the medal/money banner text is readable and correct.
7. **Reward NPC:** E at (46,0,55), mouse-click `Đổi` on each row - confirm `Đã nhận` state, button disabled,
   medal/money counters update once per click-spam.
8. **Final Boss:** talk to friend with 0, then 1, then 2 medals - confirm the dialogue text actually changes
   at each step (not just that *a* dialogue opens). At 2 medals, mouse-click `Phục thù!` - confirm the fight
   feels harder (visually/HP-bar if any) than the earlier rematch. Test one loss (retry, no reward) and the
   final win (victory banner readable, talking again shows the idle post-victory line, no further fight starts).
9. General, throughout: Esc closes every dialogue/window without leaving the player stuck (cursor relocked,
   movement restored); no two prompts overlap on screen at once; Vietnamese diacritics render everywhere
   (ă đ ơ ư in particular, per the known font gap in section 23/33).
Report back which of 1-9 failed, if any, with what was seen instead - that is what the next session should fix.

### D. Next step proposed (NOT started, per ROADMAP's own sequence: M9 -> M10 -> M12 -> **M13** , M11 parallel - all done except M13)
ROADMAP's final milestone, M13 = the ending ("kết"), is the only story milestone left unbuilt. GAME_DESIGN 4.9
already specifies it in one line: on defeating the Final Boss, "bạn thừa nhận sự tiến bộ, gà ăn mừng, trở về
nhà ông bà, kết ngắn hài hước, credits". Today's M12 win only shows a banner + one friend reaction line
(section 35-C); the homecoming beat, a short credits/closing screen, and locking further input after the true
ending are still missing. This fits the project's established pattern (reuse `DialogueUI`/`MessageBannerUI`,
smallest placeholder text screen like `IntroScreen.cs`, no new systems) and is the natural next task - but it
is NOT implemented in this pass, per instruction to not run multiple milestones at once. Also still pending,
not milestone work: running the checklist above, and deciding whether to spend tokens on the Vietnamese-font
gap (section 23/33) now that more UI text exists to be affected by it.


## 37. M13 - Ending (2026-10-22, NOT committed)

### A. Checkpoint confirmation (read, not redone)
PROJECT_STATUS sections 34-36 mark M11 (story-aware dialogue) and M12 (Final Boss, gate = 2 earned medals, quest `phuc_thu_cuoi_cung`) COMPLETE (code + real-combat Play Mode, no real E/mouse). GAME_DESIGN 4.9 and DEVELOPMENT_ROADMAP M13 specify the ending: friend concedes, chicken celebrates, return to the grandparents' house, short funny epilogue, credits; the roadmap leaves the final state after the credits open (no menu in the project). Before this task the win only gave a banner + the friend's `wonLines` dialogue (section 35-C).

### B. Built
- NEW `Assets/Script/Story/EndingSequence.cs`. Starts once (static `Started`, `BeginEnding()` returns false the 2nd time) when `FinalBossController.VictoryStoryDone` is true and quest `phuc_thu_cuoi_cung` is Completed. Flow: (1) player frozen, banner, the chicken celebrates with the existing push-up animation (Animator bool `IsPushing`, the same one TrainingStation uses; PetCombatAI off and the agent stopped meanwhile, then `ResumeAfterRecovery`); (2) fade to black on a runtime canvas, 'Vài ngày sau...', player and chicken placed at `EndingHomeSpot` near Ông/Bà, fade in; (3) three existing-`DialogueUI` conversations (Ông, Bà, Ông - jokes about protein rau, mì gói, sếp's deadline, 'đang bận đá gà'); (4) fade to black, 3-line epilogue, rolling credits (editable text fields), then 'CUỘC KÊ - Cảm ơn bạn đã chơi!' and a 'Thoát game' button (Application.Quit; in the Editor it stops Play Mode). `DialogueUI` re-enables movement every time it closes, so the sequence re-locks PlayerMovement / MouseMovement after every dialogue and fade. During the credits the player is parked at (5000,200,5000) so no NPC prompt/dialogue can open under the credits.
- EDIT (additive, 5 small replacements) `Assets/Script/Story/FinalBossController.cs`: new read-only `public static bool VictoryStoryDone` (set once the `wonLines` concession dialogue was shown and closed; reset in Awake/OnDestroy like `FinalBossDefeated`). No other behaviour changed; combat/quest scripts untouched.
- SCENE `MainGame.unity` (saved): new root `EndingSequence` (component with all references wired: QuestManager, DialogueUI, MessageBannerUI, quest asset, Player, PlayerMovement, MouseMovement, ChienKe_Pet, look-at = NPC_Grandfather) and an empty `EndingHomeSpot` at (3, 0, 14) (terrain ground, no collider overlap there).
- No replay button on purpose: reloading the scene would keep static flags such as `BattleManager.firstBattleCompleted` set (not reset in Awake), which would break the story gating. A proper restart needs flag reset / a menu (not built).

### C. Tests [VERIFIED-PLAY, real frames via Pause + EditorApplication.Step, NO real keyboard/mouse]
Setup shortcuts (not the thing under test): `MainChickenAcquire.WakeUp()`, the rematch quest `chuan_bi_tai_dau` Accept/Progress/TurnIn, `TournamentManager.Medals = 2` by reflection, player teleported next to the friend. Then: `FinalBossController.TryInteract()` -> accept dialogue -> `StartFinalBattle()` -> real `BattleManager` fight (foe HP 330 / dmg 34 confirmed) -> foe killed through `HealthSystem.TakeDamage` -> win. Observed: quest Completed once; the `wonLines` concession dialogue opened after ~2.5 s and `VictoryStoryDone`/ending stayed false while it was open; closing it started the ending the same frame. Celebration = Animator went IdleToPushUp -> Push Up for ~4 s, then back (PetCombatAI re-enabled, `IsPushing` false). Fade reached alpha 1, 'Vài ngày sau...' shown, player at (3,1.1,14) and the chicken beside it (4.5,0.1,13.5). The three Ông/Bà/Ông dialogues opened in order (speakers and first lines read back); after each close PlayerMovement was disabled again. Epilogue text shown, credits scrolled (title/body positions measured), the final screen text and the 'Thoát game' button appeared, cursor unlocked, player parked away. A 2nd `BeginEnding()` returned false; one `EndingCanvas` only; quest still Completed. `QuitButton.onClick` stopped Play Mode. Console: 0 compile errors, no EndingSequence/FinalBossController warnings; one importer notice about the TMP 'LiberationSans SDF - Fallback' asset (dynamic font atlas, pre-existing, not touched).

### D. NOT verified / owner must test by hand
- The full real chain with real input: E on the friend, mouse on 'Phục thù!', winning the real fight (not a forced kill), pressing E/'Tiếp tục' through every dialogue, mouse click on 'Thoát game' in a build.
- How it LOOKS in the Game view (no true screenshot of the Overlay canvases): fade, centred epilogue text, credits layout and scroll speed (70 px/s on a 1920x1080 reference, ~25 s total), Vietnamese diacritics on the credits (dynamic fallback font), whether the chicken push-up reads as 'celebrating' with the hidden mesh at this scale, and whether the home spot / chicken placement looks right next to Ông and Bà.
- Esc during the ending dialogues just closes them (the sequence continues); skipping the credits is not possible.
- The credits names are placeholders (`Nhóm dự án Cuộc Kê`, asset sources 'sẽ bổ sung'); the owner must fill in real names and asset licences in the `credits` field of `EndingSequence`.
- After the ending there is no way back into the game (by design, see B); no save/load; `FinalBossDefeated` and every flag reset on each Play.
- Edge not exercised: the player pressing E on the friend during the 2.5 s before the concession dialogue (it would just delay it; the ending waits for the concession dialogue itself, not for any dialogue).

### E. M13 status and next step
M13 implemented and Play-Mode verified as above; waiting for the owner's manual run-through. No other milestone was started. Suggested next (owner decision): run the manual checklist in section 36-C plus the ending (steps 8 and onward), then decide on a menu / restart (needs resetting the static flags) and real credits text.


## 38. Checkpoint after M13 - manual-test guide + credits data (2026-10-22, NOT committed)
M11-M13 are untouched (still COMPLETE as in sections 34-37). This checkpoint did NOT run any real E/mouse test: Unity MCP cannot send real input and nobody has looked at the Game view, so every item below is PENDING (not passed, not failed).

### A. One confirmed fix
In section 37 I had typed placeholder credits text containing an invented team name ('Nhóm dự án Cuộc Kê') and a vague asset line. Replaced by visible placeholders `[ĐIỀN TÊN NHÓM / TÁC GIẢ]` and `[ĐIỀN DANH SÁCH NGUỒN ASSET VÀ GIẤY PHÉP]`, both in the script default AND in the saved scene instance (the scene's serialized value overrides the script default, so editing only the .cs would not change the game). Scene saved; compile clean (0 errors).

### B. Where to enter the credits (exact)
Scene `MainGame`, root GameObject `EndingSequence`, component `EndingSequence`, Inspector field `Credits` (multi-line text; `\n` = new line). Also `Credits Title` (default 'CUỘC KÊ'). If you edit the .cs default instead (`Assets/Script/Story/EndingSequence.cs`, `string credits`, around line 63-70) you must also change/reset the field in the scene, otherwise the old value stays. Lines to replace: the two bracketed placeholders. The line 'Công cụ: Unity 6 · Universal Render Pipeline' and the 'Tri ân' joke lines are my wording, change them if you want.
INFORMATION I NEED FROM YOU (not invented): (1) the team / author name(s) as they should appear; (2) confirmation of which asset packs ship in the game, plus any non-CC0 or bought asset and its licence; (3) audio sources (`Assets/Audio`, `Assets/Sound`: no licence file found); (4) sources of the chicken models, the Vietnamese village props (`Assets/Asset game/*`) and UI art (`Assets/Art/UI`, `ui_art`): no licence file found. Licence files that DO exist in the project: Quaternius, CC0 (`Assets/Art/Character/License.txt`, Universal Animation Library, Universal Base Characters Standard); Kenney Nature Kit 2.1, CC0, crediting optional (`Assets/Art/Enviroment/KenneyNatureKit/License.txt`, copy in `Assets/UI/Enviroment/LangQue/`). Those two creators are therefore safe to credit; the rest is unknown until you tell me.

### C. How YOU test now (real E / mouse, Game view open, Console set to errors only)
Do the section 36-C list first (steps 1-9). The ending needs the Final Boss, so the fastest honest route is: play normally up to 2 medals, then:
1. Talk to the friend (E) -> dialogue ends with the 'Phục thù!' button: click it with the mouse ONCE (rapid double click must start one fight).
2. Win the fight for real. Expect: banner 'Chiến thắng...', after ~2.5 s the friend's concession dialogue (E / 'Tiếp tục' to advance). Nothing else must start while it is open.
3. After it closes: banner about the chicken, the chicken does push-ups ~4 s, you cannot move or look.
4. Screen fades to black, 'Vài ngày sau...' (check the diacritics are not boxes), fades in at the grandparents' yard, chicken next to you. Check: chicken stands on the ground, faces sensibly, Ông is in front of you.
5. Three dialogues (Ông, Bà, Ông). Press E / 'Tiếp tục' / Esc. After each you still must not be able to walk or turn.
6. Black screen with the 3-line epilogue (centred, readable, not cut off), then credits scroll (~25 s): check readability, speed, the bracket placeholders (or your real text), diacritics.
7. End screen 'CUỘC KÊ / Cảm ơn bạn đã chơi!' + red 'Thoát game' button; mouse cursor visible; click it (in the Editor it stops Play Mode, in a build it quits).
8. Try pressing E, WASD, mouse movement, I and 1-5 during steps 3-7: nothing should open or move.
Also run once with a LOSS first (undertrained chicken): retry must still work and no ending may start.
Report what failed with what you saw instead (screenshot of the Game view helps). Known not to be possible: replaying without restarting Play Mode (no restart/menu by design, section 37-B).

### D. Status
Test results: none passed or failed in this checkpoint (manual tests pending). Open items: credits team name + asset sources (section B); Game view look of every ending screen unverified; Vietnamese font diacritics in the ending unverified by eye. Next step for another account: wait for the owner's manual results and credits data, fix only what they report, then fill the `Credits` field. Do not start new milestones, restart or menu without the owner asking.


## 38. Checkpoint after M13 (2026-10-22, NOT committed, NO code or scene changed in this step)
Read sections 36-37. M11, M12 and M13 are left as they are (done; M13 Play-Mode verified with synthetic setup, section 37-C). This step did NOT run Play Mode and did NOT perform any real-input test: **the checklist 36-C and the M13 ending are still UNTESTED by hand** (real E key, mouse clicks, Game view). Nothing is claimed as passed.

### A. Where the credits are edited (verified by reading the file and the scene)
- Script `Assets/Script/Story/EndingSequence.cs`, field `credits` (lines ~63-69, default text) and `creditsTitle` (line 62), `quitLabel` (line 70). Epilogue = field `epilogue`; the Ông/Bà dialogue = `grandfatherLines`, `grandmotherLines`, `grandfatherClosingLines`.
- IMPORTANT: the value that actually runs is the one serialized in the scene (`MainGame.unity` > object `EndingSequence` > component Ending Sequence > Credits, a text area in the Inspector); it overrides the script default. Edit it in the Inspector (no recompile) and save the scene. The scene text currently contains the owner's own entries: authors `Nguyễn Trung Hiệp - Nguyễn Lê Anh Tuấn`, and the asset line `Đồ free, sài chùa, không tốn 1 cành nào :')`. The script default still has the generic placeholders `[ĐIỀN TÊN NHÓM / TÁC GIẢ]` and `[ĐIỀN DANH SÁCH NGUỒN ASSET VÀ GIẤY PHÉP]`.
- Known leftover in the scene text: the lines still contain literal square brackets (`[Nhóm mấy quên rồi]`, `[Nguyễn Trung Hiệp - ...]`, `[Đồ free ...]`) and the first of them is a placeholder, not a team name. They will show on screen as typed. Nothing was changed: the text is the owner's.

### B. Information the owner must supply (not invented)
1. The team / studio name (currently `Nhóm mấy quên rồi`, a placeholder) and whether the two author names stay, with their roles (e.g. design / code / art).
2. Remove the square brackets in the Credits text if they are not wanted.
3. Asset sources and licences. Found in the project by reading the license files (facts, not guesses): `Assets/Art/Character/License.txt` = LowPoly models by @Quaternius; `Universal Animation Library[Standard]` = CC0 1.0; `Universal Base Characters[Standard]` = Quaternius free Standard version (see License_Standard.txt for its terms); `Assets/Art/Enviroment/KenneyNatureKit` (also copied under `Assets/UI/Enviroment/LangQue`) = Kenney Nature Kit 2.1 (www.kenney.nl, read License.txt for the terms). The other asset folders have NO license file: chicken models (`Art/Chicken`: normal_chicken, 'Chicken Warrior with motions'), Buildings, Props, Models, Textures, ui_art, VFX, `Asset game`, Audio/Sound, the Mixamo-style animations of the old chicken. For each: author/site and licence, or confirmation that no credit is required. Until then the 'free assets' line is the only statement the game makes about them.

### C. How the owner tests by hand (checklist 36-C items 1-9 still pending, plus M13)
Play the real game in the Unity editor with the Game tab focused (Play Mode must be in the foreground, otherwise it runs slowly), Console open with errors only. Short path to the ending: finish the story as in 36-C; the ending needs 2 tournament medals (win vs Chị Ba and Chú Tư), then talk to the friend (E) after the rematch quest is done and click `Phục thù!` with the mouse. Then watch and write down for each point: (a) the concession dialogue opens ~2.5 s after the win and the ending only starts after you close it; (b) the chicken does push-ups ~4 s; (c) black fade with 'Vài ngày sau...', then you stand next to Ông and Bà with the chicken near you; (d) three dialogues in order, E or the button advances them, the camera/movement stay locked afterwards; (e) epilogue text is centred and readable, Vietnamese diacritics (ă đ ơ ư) are not boxes; (f) credits scroll at a readable speed (~25 s), final 'CUỘC KÊ / Cảm ơn bạn đã chơi!' screen, mouse cursor visible, `Thoát game` click stops Play Mode; (g) after the ending nothing else can be triggered (no NPC prompt, no second ending). Report which of 36-C 1-9 and (a)-(g) failed and what was seen; that is the input for the next fix.
Also still true: no save/load, no restart/menu (deliberately not added), Esc closes ending dialogues but the credits cannot be skipped.

### D. Status and next step
Code and scene: unchanged since section 37. Test status: synthetic Play Mode only (section 37-C); manual 36-C and ending test pending; credits text incomplete (B above). Next for any account: wait for the owner's manual test report, then fix only what it confirms; fill the credits from the owner's answers (Inspector field, section A); only then decide on a menu/restart.


## 38. Checkpoint after M13 (2026-10-22, NOT committed, NO project file changed except this doc)

### A. What was done / not done
- Read sections 36-37. M11, M12 and M13 are left exactly as they were (no code, scene or asset change in this checkpoint).
- Console: 0 errors when checked (Edit Mode).
- **Real-input tests (checklist 36-C items 1-9 and the ending): NOT RUN by me.** Unity MCP cannot send real keys/mouse and I cannot see the Game view, so none of them can be marked passed. They stay PENDING for the owner (guide in C). The only verification of M13 is still the synthetic Play-Mode run of section 37-C.

### B. Where the credits are entered (found by reading `EndingSequence.cs` and the saved scene)
- The text lives in the `EndingSequence` component on the scene root object `EndingSequence` (MainGame.unity), serialized fields `creditsTitle`, `credits`, `quitLabel`, `epilogue`, plus the dialogue arrays. **The scene value is what the game shows**: the script's field initialisers (`EndingSequence.cs` lines 62-70) are only defaults for NEW components; editing them does not change the existing scene instance. Edit the Inspector fields (select `EndingSequence` in the Hierarchy, Credits box) and save the scene.
- Current state: the owner has already edited it. Scene `credits` contains the names 'Nguyễn Trung Hiệp - Nguyễn Lê Anh Tuấn', a joke team line '[Nhóm mấy quên rồi]' and the asset line '[Đồ free, sài chùa, không tốn 1 cành nào :')]'; scene `creditsTitle` is 'CƯỚC KÊ' (differs from the script default 'CUỘC KÊ' and from the game's name elsewhere - owner's choice? confirm). The script defaults (lines 65 and 67) hold a differently-bracketed copy of the same text (extra '[[ ]]'), so scene and script are out of sync: harmless, but whoever edits next should treat the SCENE as the source of truth. I did not change either, and invented no names.

### C. Manual test guide for the owner (real E / mouse, Game view, Console errors-only)
Fastest honest path to the ending (no debug shortcut exists and none was added): follow section 36-C steps 1-8 (story -> train/shop -> rematch -> win Chị Ba and Chú Tư = 2 medals -> friend -> `Phục thù!` -> win). Then check in the Game view:
1. After the win: banner 'Chiến thắng...'; ~2.5 s later the friend's concession dialogue. Press E through it. Expect: nothing else starts until it closes.
2. Chicken does push-ups ~4 s, banner 'Gà ăn mừng...' readable. Player cannot move.
3. Fade to black, 'Vài ngày sau...' centred, fade in at the grandparents' yard: Ông and Bà in front of you, chicken beside you (not inside a wall/NPC).
4. Three dialogues (Ông, Bà, Ông); E or the button advances; Esc closes one and the sequence continues. Diacritics (ă đ ơ ư ế ộ) are not boxes. You must NOT be able to walk or look around between them.
5. Epilogue on black (3 lines, centred, readable), then the credits scroll: text not cut at the sides, readable speed (~25 s), your names and the title as you typed them (check 'CƯỚC KÊ' is intended).
6. End screen: title + 'Cảm ơn bạn đã chơi!' and the 'Thoát game' button; cursor visible; click it (in the Editor it stops Play Mode). Nothing from the village (prompts, dialogue, HUD) may show or react under it.
Report which step failed and what you saw; that is the only thing the next session needs to fix.

### D. Information still needed from the owner (credits) - nothing invented here
1. Real team/studio name (the current line is a joke placeholder) and the exact role wording for each person.
2. Confirm the title spelling 'CƯỚC KÊ' vs 'CUỘC KÊ'.
3. Asset attributions. Facts read from files in the project: `License.txt` files state CC0 for Quaternius content (`Assets/Art/Character/*`, Universal Animation Library, Universal Base Characters Standard) and Kenney Nature Kit (`Assets/Art/Enviroment/KenneyNatureKit`, also `Assets/UI/Enviroment/LangQue`; Kenney says crediting is appreciated, not mandatory). CC0 needs no attribution, but crediting is courteous. **No license file exists for the rest**: e.g. `normal_chicken` (the blend contains a 'Sketchfab_model' node, so it probably came from Sketchfab: check its licence, many require attribution), the mixamo chicken/humanoid (`ChienKe_*`), 'Chicken Warrior with motions', the other ~8500 files under `Assets/Art/Enviroment`, `Assets/Art/Props/Buildings/Models/UI`, any audio. The current credits line says all assets are free; I cannot confirm that. The owner must list the source and licence of each non-CC0 asset (or confirm they are all free to use) before release.

### E. Next step (exact)
Owner: run C and report; supply D. Then, if wanted, a small follow-up: put the confirmed text into the scene `EndingSequence` fields (and sync the script defaults), fix whatever C turns up. Not started: restart/menu (needs resetting static flags, e.g. `BattleManager.firstBattleCompleted`), save/load, a third tournament opponent. No new milestone was begun.


### F. Owner decision on credits (2026-10-22, doc only, nothing else changed)
The owner confirmed the credits wording is INTENTIONAL meme text and must stay exactly as it is: team name '[Nhóm mấy quên rồi]' and asset line '[Đồ free, sài chùa, không tốn 1 cành nào :')]'. So D.1 (real team name) and the 'joke placeholder' remark in B are withdrawn: nothing to replace, do not 'fix' these lines. Still open from D: (a) the title spelling 'CƯỚC KÊ' vs 'CUỘC KÊ' was not answered (leave as is unless told); (b) the factual note stays for the owner's information only - the project's own License files say CC0 for Quaternius/Kenney, while no licence file exists for assets such as `normal_chicken` (Sketchfab node) or the mixamo models, so the joke line is not a licence statement. The owner knows this and chose the meme; no action requested. Remaining next step: the manual real-input test of section 38-C.

### G. Title confirmed by the owner (2026-10-22)
The game's real name is 'Cước Kê' ('CƯỚC KÊ' in the credits is correct). Note found while checking: the IntroScreen headline serialized in the scene is 'CƯỚC KÊ' (script default 'CUỘC KÊ'). So the intro and the credits already agree in the scene; only the script defaults still say 'CUỘC KÊ' (harmless, the scene wins). Nothing changed.

## 40. BUG: friend's rooster (GuguGaga) stays lying and the final-boss fight cannot start (2026-10-22, found in a live Play session, CODE FIX NOT APPLIED YET)
Reported with a screenshot: after the rematch the friend's chicken lies down and never gets up in the quest `Phục thù - trận đấu cuối cùng`.
Live state found (Play Mode, read only): BattleManager `InBattle`, GuguGaga HP 330/330 and `HealthSystem.IsDead` false, but its Animator was on the `Dead` clip, its agent stopped and `RoosterCombatAI`'s private `isDead` was still true.
Cause (read from code, not yet changed): `HealthSystem.Die()` calls `RoosterCombatAI.StopCombat()` which sets `isDead = true`; nothing ever resets it (PetCombatAI has `ResumeAfterRecovery()`, RoosterCombatAI has no equivalent). `HealthSystem.Revive()` only restores HP, colliders, agent and the `isDead` animator bool and says the caller must resume the AI. `RematchController` (line ~184), `FinalBossController` (~163) and `TournamentManager` (~175) call `Revive(1f)` and never resume the rooster AI or play a get-up. So GuguGaga, killed in the rematch win, is dead for the final boss: Update() returns at once, it never moves or attacks. Sections 35/36 never ran 'rematch won, then final boss' in one session.
Done in the running session only (no script changed, nothing needs recompiling): the live GuguGaga was unstuck through reflection (isDead false, state Idle, WakeUp trigger, agent running). Observed over 90 stepped frames: it walked to the chicken and attacked (Hurricane Kick); its pose stayed on the Dead clip until its first attack animation, so it slid while lying; the chicken's HP went 140 -> 38 in that time (boss damage 34).
Done in data (saved assets): the HUD showed `Trận cuối: {0}/{1}: 0 / 1` because four quest assets had `{0}/{1}` inside `progressLabel` and QuestHUD already appends `: n / m`. Labels cleaned to `Đã xin`, `Trận cuối`, `Đã đấu`, `Đã đến nhà` (Quest_NhanNuoiChuGa, Quest_PhucThuCuoiCung, Quest_ThuSucVoiBanCu, Quest_VeNhaThamOngBa).
Proposed permanent fix (needs a recompile, so it ends a running Play session): add `RoosterCombatAI.ResumeAfterRevive()` (isDead false, target null, state Idle, cooldown 0, short get-up delay before engaging) and call it from `HealthSystem.Revive()` through a small optional interface so the three callers need no change; first inspect why the `WakeUp` trigger did not leave the `Dead` state on GuguGaga (the pet gets up with the same controller). Then test: win the rematch, then start the final boss in one session.
Not related: the player model is magenta in the screenshot (missing/failed shader; the Console shows URP/Lit 'out of memory while parsing' errors), see section 15 / 20 notes.

## 41. Fix: pickup items no longer spin (2026-10-22, NOT committed)
Request: the carried-quest pickups (watering can, delivery box, protein bag) turned all the time; they should stand still at their scene position and rotation.
Driver found: ONLY `Assets/Script/Quest/QuestCarryItem.cs`, `Update()` rotated the world model with `worldModel.transform.Rotate(0, worldSpinDegreesPerSecond * Time.deltaTime, 0, Space.World)`; the field defaulted to 35 and all three scene instances had 35. No Animator or Animation component sits on these objects or their children, no prefab drives rotation, and the inventory `ItemPickup` placeholders (`Pickup_BinhTuoiCay`, `Pickup_ThungHang`, `Pickup_VeChai`) never had any spin code. Other `Rotate` calls in the project are the mouse look only.
Files changed: (1) `QuestCarryItem.cs`: default `worldSpinDegreesPerSecond` 35 -> 0 and the tooltip now says 0 = off (the spin code path was left in place, off by default; nothing else in the script was touched). (2) Scene `MainGame` (saved): `worldSpinDegreesPerSecond` = 0 on `QuestWateringCan`, `QuestDeliveryBox`, `QuestProteinBag`. Positions and rotations of their world models were not edited (all rotation (0,0,0), unchanged).
Results [VERIFIED-PLAY, editor paused + `EditorApplication.Step()`]: compile clean, no project errors in the Console. After 200 stepped frames (4 s; the old spin would have turned them 140 degrees) the world model of all three items still had the same rotation (0,0,0) and the same position as before. Pickup logic unchanged and still working for the watering can: prompt `Nhấn E để nhặt bình tưới` showed in range, `TryPickUp()` true once, a second call false, `IsHeld` true. The items have no colliders of their own (as before). Not run: real E key, the delivery box and protein bag pickup path (same script, only the spin value changed).
Next step: look at the three items in the Game view and, if the fixed orientation (all at yaw 0) looks wrong, set the yaw of each `Model` / `WorldModel` child in the scene by hand. If a highlight is wanted, use a static one (outline or glow), not rotation.


## 31. Dialogue UI: typewriter, skip, Player/NPC speaker, Vietnamese check (2026-10-22, NOT committed)
Tags: [VERIFIED-PLAY] = Play Mode, editor paused + `EditorApplication.Step()` (0.02 s/frame), buttons pressed through `onClick`, dialogues opened by calling `DialogueUI.Open` directly; [SEEN] = Game-view screenshot. NO real keyboard/mouse.

Found: one `DialogueUI` (`QuestUI_Canvas/DialogueUI`, panel + Speaker + Body + PrimaryButton + CloseButton) used by every NPC/story script (about 40 `Open()` calls). It already had a framed panel (dark, 93% alpha) and a speaker name label, but showed the whole line at once and every line used the NPC name. No second dialogue manager exists and none was created.

Changed (only `Assets/Script/UI/DialogueUI.cs` + 1 scene wiring, scene saved; `Open(speaker, lines, label, action)` signature unchanged, so no caller was touched):
- Typewriter: the line appears letter by letter (`maxVisibleCharacters`, 45 letters/s, `charactersPerSecond`, `typewriter` on/off in the Inspector, uses unscaled time).
- Show everything at once: E (`confirmKey`) while typing shows the whole line, a second E goes on; a click on the text does the same (a `Button` was added to the `Body` object and wired as `revealTextButton`; public `CompleteLine()`, `IsTyping`). The Next / action button still acts immediately (it skips the typing and goes on), so existing flows and tests that click the button once still work. Esc / Close still close.
- Player vs NPC: a line that starts with `[Player] ` is spoken by the player: the name label becomes `Tôi` (cyan) and the panel turns slightly blue; NPC lines keep the name passed to `Open()` (gold name, dark panel). Colours are restored on Close. Name, prefix and colours are Inspector fields.
- NO existing story text was changed: no current dialogue contains a `[Player]` line yet, so right now every in-game line still shows the NPC name. Writers must add the prefix to the lines the player says.

[VERIFIED-PLAY]:
- Open -> 0/106 letters visible, 18 after 20 frames (0.4 s), 36 after 40 frames; click on the text -> `typing=False`, all visible.
- `[Player]` line: speaker `Tôi` cyan (0.45, 0.85, 1), panel (0.05, 0.10, 0.18); next NPC line: gold `Ông` and the dark panel back; after Close both restored.
- Pressing the action button while the text was still typing ran the action exactly once; a second stale click did nothing; a plain conversation has no action button.
- Vietnamese: a line with `ă â ê ô ơ ư đ ạ ả ấ ầ ẩ ẫ ậ ắ ề ể ỷ ỵ ừ ...` (106 chars, 80 drawn glyphs): TMP drew all 80 with their own glyph (0 replaced by the missing-glyph box). [SEEN] in the Game view: `Ông`, `Tôi`, `đừng đuổi`, `ư`, `điều độ`, `ạ`, `Ừ, được`, `ẩu`, `ỷ` all correct.
- Console: no C# errors (only the known URP shader / MCP / Account notices).

CORRECTION to sections 23 / 25 / 24 (they say the font lacks `ă đ ơ ư`): that was measured with `HasCharacter(c)` on the static `LiberationSans SDF`, which does not create glyphs. The static asset has the dynamic fallback `LiberationSans SDF - Fallback` (source `LiberationSans.ttf`, which contains all Vietnamese letters); with the fallback TMP builds the missing glyphs when text is drawn (`HasCharacter(c, true, true)` = 0 missing out of 74, and the rendering above). So no new font is needed. Remaining risk: the first time a new letter appears the glyph is generated at runtime (tiny hitch), and the fallback atlas is dynamic (not tested in a built player).

NOT verified: the real E key and the real mouse click on the text; the look at other resolutions / on a real play-through of each story dialogue; how the typewriter feels (45 letters/s is a guess); very long lines overflowing the 170 px high body box (not tested; the longest current lines are about 110 letters and fit in 2 lines); a standalone build; sound while typing (none).
Leftover scratch files (my delete calls are blocked): `Assets/Screenshots/screenshot-20261022-173335.png` and `-173344.png` (plus the older ones from sections 24/21).
Next (small, if wanted): add `[Player] ` lines where the story needs the player to speak (for example Grandfather rescue: the request to keep the chicken), then play it once by hand with the real E key.


## 42. Audio integration (2026-10-22, NOT committed)
Found: `Assets/Script/Audio/GameAudio.cs` (the one audio manager, created minutes earlier by another session; it listens to existing events and owns all AudioSources: 2 music + 2 ambience + 8 SFX voices) but it was NOT in the scene and no clip was assigned; the scene had no AudioSource at all (only the AudioListener on Main Camera). `Assets/Sound` (13 files) and `Assets/Audio` (same 13 mp3 duplicated + `Audio/` = Kenney footstep/impact `.ogg`). Duplicate files were left alone (not deleted).
Changed: `GameAudio.cs`: one small addition, `Bank.maxSeconds` (cut a long clip after N seconds with `SetScheduledEndTime`), used for the dialogue voice. Scene: ONE `GameAudio` GameObject added and saved, clips assigned (only files that exist). Import settings: all clips >= 30 s in `Assets/Sound` set to Streaming (10 files).
Mapping (my choices; I could not listen to anything):
- Music: explore = `audiobay_bgm-vietnamese-traditional-happy-melodies...245101`; after the first loss (until transformation) = `audiobay_bgm-audiobay-vietnamese-rivers-melody-mood-sad...150276`; fights = `freesound_community-014264_tradicional-village-carnival-brass-band-57285`; ending = `audiobay_bgm-vietnamese-spring-festival...245127`.
- Ambience: day = `soul_serenity_sounds-distant-village-243574`; evening (after first loss) = `subhamita-evening-sound-effect-in-village-348670`; sleeping = `freesound_community-summer-village-atmosphere-at-nightdog-barking-cicadas-24164`. Unused spares: `village-at-morning-78119`, `ambience-outdoor-rural-wind...51262`, `wolff-sfx-field-ambience-414431`.
- Chicken: `arunangshubanerjee-fussing-rooster-in-indian-village...330927` (idle call every 45-90 s while exploring, and when a fight starts).
- Dialogue voice: `uia-uia-uia-cat.mp3` (the only A-I-U style file; its first phrase is ~1.7 s, cut by `maxSeconds` 1.7, one per dialogue line, min interval 0.8 s). `chieuk-minecraft-villager-289282.mp3` is very quiet (RMS 0.01-0.02) and its licence is unknown (Minecraft sound) -> NOT used.
- Kenney impact/footstep (Assets/Audio/Audio): footsteps = `footstep_grass_000-004`; quest accepted = `impactBell_heavy_002/003`; objective = `impactGlass_light_000-002`; quest done = `impactBell_heavy_000/001`; money gain = `impactMetal_light_000-002`; money spent = `impactTin_medium_000-002`; pickup = `impactSoft_medium_000-003`; training done = `impactPlate_medium_000-002`; hit light/heavy = `impactPunch_medium/heavy_000-004`; knock-out = `impactSoft_heavy_000-002`; fight start = `impactMetal_heavy_000/001`; win = `impactBell_heavy_001`; loss = `impactWood_heavy_000-002`; transformation = `impactMining_000-002`. These are placeholders (impact sounds standing in for UI/coin/jingle sounds).
Anti-spam (already in GameAudio): per-group minimum interval, never the same clip twice in a row, max 8 voices (extra plays are skipped), cross-faded music/ambience, music ducked while a dialogue is open.
[VERIFIED code-path, Play Mode, Console 0 errors, compile OK (only obsolete-API warnings)]: exactly 1 GameAudio and 12 AudioSources in the scene; explore music and day ambience selected; 20 money changes in a row -> 1 played / 19 blocked; 30 hits in a row -> 1 played / 29 blocked; dialogue opened -> voice played once; walking 150 frames -> 5 footsteps.
NOT verified: how anything SOUNDS (no listening possible), volume balance, music switch after the first loss / in fights / ending, night ambience while sleeping, duck, chicken idle call, 3D falloff, whether the cut uia phrase sounds right.
MISSING sounds (no suitable file in the project; nothing was faked): UI click/hover, shop open/buy, coin sound, dedicated quest-complete / win / lose jingles, chicken cluck while following or being hit, chicken wake-up, training sounds (treadmill, punching bag, push-up), crowd cheer for victory, door / bed, real rooster crow for the morning after sleep, a clean A-I-U voice set per vowel (A, I, U separate), narration for the intro/ending, grass-footstep variant for the village road (concrete steps exist: `footstep_concrete_*`, not assigned), licence text for the audiobay / freesound / chieuk files (not in the project).


## 43. 2D quest guide arrow (2026-10-22, NOT committed)
Found: no waypoint / marker / compass code anywhere. Quest system untouched: `QuestManager` (state, progress, `TrackedQuest` = newest accepted quest still running = what `QuestHUD` shows), `QuestCarryItem.IsHeld`, `QuestObjectiveTarget.IsAvailable`, `ScrapPickup.IsCollected` (all already public).
Added: `Assets/Script/Quest/QuestGuide.cs` (READ-ONLY on quest data, no new quest logic) + scene object `QuestUI_Canvas/QuestGuide` (saved). UI: a small dark strip, top centre under the message banner (anchor top-centre, y -150, 460x56) with a yellow arrow (`Assets/UI/UI/PNG/White/2x/arrowUp.png`, existing UI pack) that turns toward the target relative to the camera, and the text `<name>  -  <distance> m` (same TMP font as the quest HUD). It does not touch the screen centre, the dialogue box (bottom), the hotbar, the quest HUD (top-left) or the money (top-right).
Import change: `arrowUp.png` (White/2x) was a default texture in `Multiple` sprite mode and could not be loaded as a sprite; it is now `Single` sprite (nothing else used it as far as I know).
Behaviour: 23 ordered rules in the component (editable in the Inspector: label, quest, state, progress range, held-item condition, targets). Pass 1 = rules of the tracked quest (first match wins, nearest valid target); pass 2 = 'offer' rules (quest NotStarted + prerequisite Completed) point to the quest giver. The target is recomputed every 0.2 s so it follows progress, pickups, turn-ins and a change of tracked quest. Hidden when: nothing matches, dialogue / shop / reward / inventory open, a fight is running, sleeping, ending started, or the player is within 3 m of the target (the E prompt takes over).
Targets covered: home -> Ông (`ve_nha_tham_ong_ba`, `nhan_nuoi_chu_ga`); friend (offer + challenge); chore: Bà offer -> watering can (until held) -> nearest unwatered plot -> Bà turn-in; garden: protein bag -> plot 1 (bón) -> plot 2 (hái) -> pet (cho ăn) -> bed; delivery: box -> friend -> Bà; scrap: nearest uncollected scrap -> villager turn-in; training: nearest station -> friend (rematch); tournament: Chị Ba then Chú Tư; final boss: friend.
Not guided on purpose: offers of optional quests (delivery, scrap), the shop (money/equipment steps stay as text in the quest objective), the Reward NPC, the final-boss quest offer, the ending.
[VERIFIED code-path, Play Mode, Console 0 errors]: label/target followed the whole chain by QuestManager calls and the real component methods (`TryPickUp`, `QuestObjectiveTarget.Interact`, `MarkCollected`, `TurnIn`): home -> Ông; hidden within 3 m, shown again at 15 m; rescue; friend offer and challenge; Bà offer; can -> plots (nearest changes after each watering) -> Bà; protein bag -> plot 1 -> plot 2 -> pet -> bed; hidden while a dialogue is open and back after it closes; transformation -> station; side quests (delivery, scrap) take over while tracked and the story quest comes back when they are turned in; rematch ready -> friend; tournament Chị Ba -> Chú Tư -> hidden at ready-to-turn-in.
NOT verified - MUST be checked in the GAME VIEW by a person: the strip's look and position at 16:9 / other resolutions, readability on bright ground, that the arrow really points the right way while turning the camera (the angle maths is untested visually), that Vietnamese letters render in the label, no overlap with the message banner when a long banner shows, behaviour with the real E key. The guide runs in the story order forced by my test, not a real play-through. No save/load.

## 33. Light particle effects: fight hits, watering, punching bag (2026-10-22, NOT committed)

### A. Found (read, not assumed)
- No usable particle asset existed: no particle prefab anywhere in `Assets` (only `QuestWateringCan_Model.prefab`, which is a model). `Assets/Art/VFX/` holds only `Vfx_Particle.mat` (URP `Particles/Unlit`, shader supported) and `Vfx_SoftDot.png` (soft dot texture).
- The effect system ALREADY existed in code and in the open scene when I started (files dated 2026-10-22 17:56, not mentioned in this document before): `Assets/Script/Vfx/GameVfx.cs` + scene object `GameVfx` with children `HitSpark`, `HitHeavy`, `WaterDrops`. I reviewed it and did not rewrite it; this section documents it.

### B. How it works (all read from the code)
- One component, one place: `GameVfx` (static `Hit`, `BagHit`, `Water`, all null-safe). Three Unity `ParticleSystem`s built in `Awake`, `loop=false`, `playOnAwake=false`, emission module OFF (only explicit `Emit` bursts), `maxParticles` 40 / 40 / 60, lifetimes 0.25-0.9 s, simulation space World, alpha fades to 0, size shrinks.
- Minimum interval per effect (`hitInterval` 0.06 s, `bagInterval` 0.3 s, `waterInterval` 0.25 s, unscaled time) so nothing is emitted every frame; extra calls are counted in `Blocked`.
- Fights: `GameVfx` only LISTENS to `HealthSystem.OnHealthChanged` (every `HealthSystem` found at Start, including the tournament roosters) and emits a spark where the damaged chicken is when health DROPS (heavy burst if the loss is >= 15% of max or >= 25). Healing/reviving emits nothing. `HealthSystem`, `PetCombatAI`, `RoosterCombatAI` were NOT edited.
- Watering: one extra line in `QuestObjectiveTarget.Interact()` (`GameVfx.Water(...)` only when the required item is still held, i.e. the watering can), placed after the quest-flip logic and before `ReportProgress`; no quest value changes.
- Punching bag: `TrainingStation.BagImpact()` (called when the strike lands in the swing code) emits a spark at the bag renderer centre; visual only.

### C. Tests
[VERIFIED code-path, Play Mode, editor unfocused so frames were stepped with `EditorApplication.Step`]: 200 calls each of Hit/BagHit/Water in ONE frame -> exactly 1 burst each (+1/+1/+1, 597 blocked), 40 particles alive (= 14+10+16, under the caps); about 1.6 s of game time later 0 particles alive (nothing lives on); `Rooster_ChiBa.TakeDamage(5)` -> health 150 -> 145 (exactly -5, effect did not change damage) and 1 hit burst; `Heal(10)` -> no burst; rooster restored to 150; all three systems: loop off, emission off, rate 0 usage, max 40/40/60; `TrainingStation.BagImpact()` (private, called by reflection, because the real call needs a paid, story-unlocked training session) -> 1 bag burst at the bag centre (-9.0, 1.55, 55.5); compile clean, Console shows no script errors or warnings from these files (the unrelated pre-existing `FindFirstObjectByType` obsolete warnings remain). Scene saved (it is stored in Unity binary format, so text search of the file proves nothing).
[NOT verified]: how the sparks / drops look in the Game view (no screenshot), a real chicken-vs-rooster fight with the effect, a real watering press (Play Mode of an earlier session showed counters hit=4, water=2 but I did not witness that run), a real punching-bag session, performance on a weak PC, effect size relative to the chicken (all sizes are starting values).
Manual check (PENDING, for you): fight a rooster (small sparks per hit, bigger on strong hits, stop when the fight ends); water a plot with the can (blue drops once per press); start `Đấm bao cát` (spark on each landed punch); nothing keeps emitting afterwards. Tune `hitInterval`, sizes and counts in `GameVfx`.

## 36. First-loss scene order: friend taunt -> player's answer -> then the story moves on (2026-10-22, NOT committed)

### A. Found (read from code + play)
`ChildhoodFriendBattleTaunt.ShowAfterLoss()` completed the quest `thu_suc_voi_ban_cu` (ReportProgress + TurnIn) BEFORE opening the taunt dialogue ("... MÀY SỢ À"), so the quest-completed side effects (audio `questDone`, QuestFeedback, listeners in `ChickenTransformationTrigger` / `TournamentManager`) fired while the friend was still talking, and the player never answered. `AfterLossStoryDone` (the gate of the grandmother's offer) was set the frame after that single dialogue closed. No other script reacts to the taunt; `DialogueUI.Open` silently ignores a call while another dialogue is open, which is why nothing may be opened blind.

### B. Change (ONE file: `Assets/Script/Story/ChildhoodFriendBattleTaunt.cs`; no new component, no scene edit, M11-M13 scripts untouched)
Strict sequence driven by `phase` (None -> FriendSpeaking -> PlayerReplying -> Done); each step starts only when `DialogueUI.IsOpen` is false AND at least one frame has passed since the previous dialogue opened:
1. Friend taunt opens (same lines as before; quest still Active).
2. After that dialogue is closed (E / Esc / close button, any of them) the player's answer opens: new fields `playerSpeakerName` = `Tôi`, `playerReplyLines` = `Mày đợi đó!` (editable in the Inspector; the friend's lines are untouched). If the dialogue cannot open (something else is open) the step is retried next frame and the phase does not advance.
3. After the answer is closed: quest `thu_suc_voi_ban_cu` completes (one-shot, as before), then `AfterLossStoryDone = true`, which is the only thing that opens Bà's offer (`grandparents_helper` is NOT started automatically; Bà still needs E).
While the scene runs (and on the frame it advances) the friend's E interaction and prompt are blocked, so the same E press that closes a dialogue cannot reopen another one; `UpdateAutoTaunt` and `Interact` both refuse to start a second scene.

### C. Tests [VERIFIED-PLAY, real combat, frames stepped with `EditorApplication.Step` (dt ~0.02), NO real keyboard/mouse: dialogue buttons were pressed by calling the same `OnPrimaryPressed` / `Close` the UI calls]
Real first fight (WakeUp -> friend challenge -> accept) lost by the AI, then, in one continuous run: automatic taunt opened (speaker `Bạn thân`, `... MÀY SỢ À`), quest `thu_suc_voi_ban_cu` still Active, `AfterLossStoryDone` false, Bà's offer false; 5x `Interact()` while he speaks changed nothing; 40 frames later the dialogue was still the friend's; after closing it the next dialogue was speaker `Tôi`, `Mày đợi đó!`, phase PlayerReplying, quest still Active, `AfterLossStoryDone` false; 5x `Interact()` during the answer changed nothing; 40 frames later still open and nothing completed; after closing it: quest Completed, `AfterLossStoryDone` true, no dialogue open, Bà's offer true, `grandparents_helper` still NotStarted (not auto-started); talking to the friend afterwards gives the M11 state line (`Gà mày còn yếu lắm. Về giúp ông bà đi đã...`), the taunt does not repeat. Compile clean; no script errors in the Console (only Unity's own `JobTempAlloc ... older than 4 frames` notices, an artefact of stepping paused frames).
[NOT verified]: real E key / mouse (the one-frame guard against the same E press reopening a dialogue is written but could not be tested without real input), how the two dialogues look in the Game view, the pause between the loss and the taunt (`tauntDelaySeconds` 2.5 s), the missed-auto-taunt path (talking to the friend right after the loss; same `ShowAfterLoss`, not run separately), the `Tôi` speaker name and the answer's wording (starting values, editable), the `questDone` sound now playing after the answer instead of during the taunt.
Manual check (PENDING): lose the first fight; the friend taunts; close with E; `Tôi: Mày đợi đó!` appears; close with E; no third dialogue; the quest HUD updates only now; E on Bà offers the chore quest.

## 44. Garden/night/missing-chicken flow - audit, plan and Checkpoint 1 (2026-10-22, NOT committed)
Tags: [VERIFIED-PLAY] = Play Mode, real frames (`Application.runInBackground=true` + `EditorApplication.QueuePlayerLoopUpdate()` makes frames advance between MCP calls; `Time.captureDeltaTime=0.02`), calling the same public methods the E key calls (`TryPickUp`, `QuestObjectiveTarget.Interact`, `FeedingSpot.TryPlace`). NO real keyboard/mouse. [NOT VERIFIED] = not tested.

### A. What was found (audit, before changing anything)
- Garden quest `garden_protein` (`Quest_GardenProtein.asset`, key `garden_step`, 3 steps): fertilise at `VegetablePlot_1` (needs the sack, `QuestProteinBag`) -> harvest at `VegetablePlot_2` -> (old) E on the chicken itself -> ReadyToTurnIn -> `StoryBed` (fade, `TurnIn`) -> `ChickenTransformationTrigger.Transform()` at once (Transformed + TrainingUnlocked + `chuan_bi_tai_dau` start).
- Inventory: `InventorySystem` (Add / Remove / Has / GetCount / CanAdd by `ItemDefinition`, 5 slots). There was NO vegetable item.
- Sleep time: there is NO day/night or clock. One fixed `Directional Light`, Default-Skybox, no script touches lighting. The bed works whenever the garden quest is ReadyToTurnIn. "Allow other activities until evening" therefore needs a new (small) evening mechanism.
- `EndingSequence` only needs the chicken `Transform` (`ChienKe_Pet`) and `NavMeshAgent.Warp`; it does NOT read `Transformed` or `StoryBed`. Readers of the transformation/bed: `RematchController` (starts when `Transformed`), `TrainingStation` (`TrainingUnlocked`), `GameAudio` (`Transformed`, `StoryBed`), `QuestGuide` (`StoryBed`). Moving WHEN `Transformed`/`TrainingUnlocked` becomes true must keep these consistent.
- Training area: stations at (-9,0,55.5) PunchingBag, (-12,0,51.5) Treadmill, (-6,0,51) PushUp. The pet Animator already has the `IsPushing` bool (idle push-up routine in `PetCombatAI`), so a push-up pose needs no new animation.

### B. Requested flow and checkpoints
bón cây -> hái rau -> đặt rau tại một điểm -> gà ăn xong (confirmed) -> [CP1 DONE]
free time until evening -> ngủ [CP2: evening mechanism + bed gate]
sáng dậy không thấy gà -> hỏi ông bà -> quest tìm gà [CP3]
khu tập: gà đang hít đất, camera at the moment of discovery, E near the chicken to take it back [CP4]; only then training unlock / `chuan_bi_tai_dau` (plan: keep `Transformed` for the visual change at the bed, move `TrainingUnlocked` + the next quest to "chicken taken back").

### C. Checkpoint 1 - DONE (garden: vegetables in the bag, feeding spot, chicken eats, confirmation)
Code: `QuestObjectiveTarget.cs` +optional `grantItem/grantCount/bagFullMessage` (target is refused and NOT counted when the bag cannot take the item; no effect on other targets). NEW `Assets/Script/Story/FeedingSpot.cs`: only when the quest is Active with progress >= 2; needs `Item_Rau` in the bag; chicken must be within 15 m (else message); `IsPlaced` flips before the item is removed (no double place); the chicken's `PetCombatAI` is switched off only while it walks to the plate and eats (failsafe warp after 12 s, `OnDisable` restores it); step 3 is reported ONCE, after the meal.
Assets/scene: `Assets/Data/Items/Item_Rau.asset` (`Rau xanh`, placeholder green square in the hotbar, stack 5); `Assets/Materials/QuestPlate.mat`, `QuestVeg.mat`; scene (saved): `QuestObjectives/FeedingSpot` at (4.5,0,30) with a `Plate` (disc + 3 green leaf blobs, placeholders) and an `EatPoint`; the harvest target at `VegetablePlot_2` now gives 1 `Rau xanh`; the old feed target on `ChienKe_Pet` was REMOVED; `QuestGuide` rule 'Cho gà ăn' now points at the feeding spot; Bà's explanation line and the quest objective text mention the plate.
[VERIFIED-PLAY, 14 checks + follow-up]: placing before steps 1+2 refused; harvest before fertilise refused; fertilise -> 1; bag full (5 slots) -> harvest refused, nothing counted or lost; after freeing a slot harvest gives 1 `Rau xanh`, progress 2; harvest again does nothing; chicken 40 m away -> placing refused, vegetable kept; chicken near -> placed, item leaves the bag, second place refused, NOT counted yet, pet AI off; after frames: chicken stood 0.2 m from the eat point, `HasEaten`, progress 3, state ReadyToTurnIn, plate hidden, banner `Gà ăn sạch rau rồi!...`, pet AI back on, and the chicken followed the player again (25.3 m -> 6.1 m). Console: 0 project errors.
[NOT VERIFIED]: real E key / prompt text and position at the feeding spot; how the plate looks; that the chicken visibly walks and "eats" (no eating animation exists - it only stops and faces the plate for 3 s); Item_Rau shown in the real hotbar; Vietnamese rendering; the full chain from the real story start (the test forced the chicken awake and the quest Active).
Known limits: the sleep/transformation part is UNCHANGED in this checkpoint (bed still works right after step 3, transformation still happens at the bed).

### D. M10 verification done in this session (before this task)
- Code-reading of `RewardNPC`, `RewardUI`, `MedalRewardCatalog`, `TournamentManager` found no logic defect; no file was changed.
- Vietnamese glyphs: the primary font `LiberationSans SDF` is Static and lacks `ă ợ ừ ỗ ứ ự` (checked with HasCharacters), BUT a Dynamic multi-atlas fallback `LiberationSans SDF - Fallback` is attached and its source font `LiberationSans` contains all Vietnamese letters -> not a confirmed defect; on-screen rendering is still [NOT VERIFIED].
- Real battle reward [VERIFIED-PLAY]: a real fight (real frames) vs Chị Ba was WON by the trained chicken: banner `Thắng! Nhận 1 huy chương và 200 tiền`, medals 1, wallet 200. Redeeming that medal with the real buttons was not completed in this session (the Play session ended before); redemption was verified earlier with forced medals (section 32).
- Test pitfall found (not a game bug): until `StarterChicken.SwitchToPet()` the starter chicken never attacks (`PetCombatAI` is off by design), so four earlier test fights were lost with 0 damage dealt. Tests that force the story must call `SwitchToPet()`. The player/chicken must also stand near the arena.
- Real keyboard/mouse, window layout and appearance of the exchange window: still PENDING (manual).

### E. Decisions needed from the owner (not guessed)
1. "Camera hướng về mặt Player" at the discovery moment: the game is first person and the Player has only a placeholder capsule (no face model). Which do you mean? (a) the camera turns to look at the CHICKEN doing push-ups (smallest, recommended), (b) a short third-person reaction shot of the Player (shows the capsule - needs a face/model), (c) something else.
2. "Đến tối" (evening): there is no clock. Smallest options: (a) a timer of N seconds after the chicken has eaten, with the directional light fading to evening colour, the bed refusing before that (recommended; N default 90 s), (b) evening after the player completes a number of side activities (needs a rule), (c) evening only as a banner/light change when the player presses E on the bed first time (no wait). What should the player be able to do in that free time (existing scrap/delivery/shop; training stays locked)?
3. Morning: where does the chicken wake the player up in the story - Player appears in bed, chicken hidden/not at home, and WHO gives the hint (Ông, Bà or both)? Proposed: either grandparent opens the new quest `tim_ga` ("Tìm con gà") with a hint `nó chạy về phía khu tập`.
4. Transformation timing: keep the muscle change at the bed (chicken is muscular when found) or reveal it only when the chicken is taken back? Proposed: visual change at the bed, training + `chuan_bi_tai_dau` unlock only when the chicken is taken back.

### F. Exact next task
After the owner answers E1-E4: Checkpoint 2 (evening gate for the bed, light fade, no change to EndingSequence). Manual test of CP1: Play -> (forced or real) garden quest -> sack, fertilise, harvest (check hotbar shows `Rau xanh`), E at the plate near (4.5,0,30) with the chicken close -> watch it walk and stop -> `Việc vườn 3/3` -> HUD says go to bed.


## 45. The friend leaves after the first rematch and returns only for the final boss (2026-10-22, NOT committed)
Request: after the first rematch against the friend the NPC must vanish / be non-interactable and come back only once Chị Ba AND Chú Tư are both beaten. No second NPC, the 2-medal Final Boss condition must not change.
Inspected first (not just docs): `NPC_ChildhoodFriend` (24,0,19.7) is ONE GameObject holding Animator, `QuestObjectiveTarget` (the delivery point of `village_delivery`, Quest_VillageDelivery), `ChildhoodFriendBattleTaunt`, `RematchController`, `FinalBossController`; its model is 3 SkinnedMeshRenderers, NO collider (interaction is distance + E inside `ChildhoodFriendBattleTaunt`). So the GameObject is NOT deactivated (the controllers live on it and must keep running); only the model and the interaction are switched off.
Changes (4 files, additive, all compile, Console 0 project errors):
- `RematchController.cs`: new `static bool FriendAway` (reset in Awake/OnDestroy) and `UpdateFriendPresence()` called from Update. Rule, derived every frame from real state (no stored flag): away = rematch quest `chuan_bi_tai_dau` is Completed AND the victory talk is over (`wonDialogueShown`, dialogue closed, one frame later) AND `FinalBossController.GateOpen` is false. While away: all Renderers of the NPC disabled and his `QuestObjectiveTarget` (delivery point) disabled; both are restored when it turns false.
- `FinalBossController.cs`: one read-only accessor `public bool GateOpen => IsUnlocked()`. The gate itself (`TournamentManager.Medals >= requiredMedals`, 2, EARNED total, spending medals never lowers it) is UNCHANGED. The friend returns exactly when the boss can be started, so the two can never disagree. With the current two opponents `Medals >= 2` is the same as having beaten both Chị Ba and Chú Tư (each win gives 1 medal, once). Assumption: if a 3rd opponent is ever added, the return follows the boss gate (2 medals), not all opponents.
- `ChildhoodFriendBattleTaunt.cs`: `Update` returns early (prompt hidden, no E) and `Interact()` returns immediately while `FriendAway`.
- `QuestGuide.cs` (another module, 8-line additive edit in `Valid`): a target whose `QuestObjectiveTarget` components are all disabled is not a valid guide target, so the arrow no longer points at the empty spot.
Tests [automated Play Mode, real frames via Pause + EditorApplication.Step; quest setup through QuestManager; the rematch win used the controller's real `OnVictory()` through reflection - NO real fight, NO real keyboard/mouse]:
1. Game start (rematch NotStarted): visible, 3/3 renderers, delivery enabled. 2. Rematch ReadyToTurnIn: unchanged (visible).
3. Right after the win (quest Completed): still visible; the victory dialogue opened after the 2.5 s delay and he was still visible while talking; the frame after the dialogue closed: away=true, 0/3 renderers, delivery disabled.
4. While away: `Interact()` opens nothing, prompt flag false, `QuestGuide.Valid(friend)` = false.
5. Medals forced to 1 (reflection): still away, `Interact()` opens nothing. Medals forced to 2: back at once (3/3 renderers, delivery enabled, guide valid) and `Interact()` opens the final-boss challenge (quest `phuc_thu_cuoi_cung` Active).
6. After the real rescue flow + `StarterChicken.SwitchToPet()`: the 'Phục thù!' button (clicked twice) started exactly one final fight (BattleManager InBattle, boss HP 330/330) and the friend stayed visible during it.
7. Components on the NPC: RematchController 1, FinalBossController 1, ChildhoodFriendBattleTaunt 1, QuestObjectiveTarget 1; no second NPC; the scene file was not changed (not dirty).
NOT tested: the real first rematch fight followed by this hide (only its `OnVictory` path); real E key / how the NPC looks disappearing (no fade, he just vanishes the frame after the talk); winning the tournament with real fights and seeing him reappear in the Game view; the boss fight to the end and M13 ending after this change (the ending does not read the friend, grep found no reference, but it was not re-run); a game that starts with the rematch quest already Completed (the same rule would hide him at once, covered only by reasoning). Spending medals at the Reward NPC does not matter by construction (the gate reads `Medals`, not `AvailableMedals`) but was not re-run here.
Known side effect (needs your decision): the delivery side quest `village_delivery` hands the package to this friend, so while he is away (from the rematch win until 2 medals) that delivery CANNOT be completed and the guide arrow is hidden; the HUD text still says to give it to him. It is not a main-story blocker and resumes when he returns. If you want it completable meanwhile, the smallest option is to move that delivery point to another NPC or keep only the delivery target enabled while the model stays hidden.
Exact next task: manual run of the order rematch win -> friend vanishes -> Chị Ba -> Chú Tư -> friend reappears -> boss -> ending, then decide the delivery-quest question above.


## 32. Audio integration (2026-10-23, NOT committed)
Tags: [VERIFIED-PLAY] = Play Mode, editor paused + `EditorApplication.Step()`; sources, clip names, counters and volumes were read from code. [NOT HEARD] = nobody listened: the editor was paused (paused play freezes audio, so `isPlaying` reads false and real overlap of voices could not be measured), and I cannot hear anything. NO real keyboard/mouse.

Found before: NO audio code at all (0 AudioSource, 1 AudioListener on Main Camera, no AudioManager). Audio files: 13 mp3 in `Assets/Sound` (+ `kenney_impact-sounds.zip`, `kenney_game-icons.zip`, which are not audio clips), an exact duplicate set of the same mp3 in `Assets/Audio`, and 125 Kenney `.ogg` (footsteps, impacts) in `Assets/Audio/Audio` (the unpacked impact-sounds pack). No licence file anywhere (section on licences above still open).

Added (one new script + one scene object, nothing duplicated): `Assets/Script/Audio/GameAudio.cs` on the scene object `GameAudio` (the only audio manager; it creates and owns ALL AudioSources: 2 music, 2 ambience cross-fade channels, 8 SFX voices = 12, verified: 12 in the whole scene, none stray, 1 listener). Scene saved (it was clean before). Small edit in `DialogueUI.cs`: event `LineShown(bool byPlayer)`. Long loops (3 BGM, carnival, 3 ambiences) were set to `Streaming` in their import settings. Assets/Audio duplicates are NOT used (the mp3 are taken from `Assets/Sound`).

What plays (only files that exist; `Assets/Sound/...` unless noted; Kenney = `Assets/Audio/Audio/...`):
- Environment (looping, cross-fade 2.5 s, ducked to 50% while a dialogue is open): day `freesound_community-village-at-morning-78119`; after the first lost fight until the transformation `subhamita-evening-sound-effect-in-village-348670`; while the bed is sleeping `freesound_community-summer-village-atmosphere-at-nightdog-barking-cicadas-24164`.
- Music: exploring `audiobay_bgm-vietnamese-traditional-happy-melodies-nhac-nen-video-245101`; after the first loss until the transformation `audiobay_bgm-audiobay-vietnamese-rivers-melody-mood-sad-slowly-150276`; any fight (BattleManager or tournament) `audiobay_bgm-vietnamese-spring-festival-bgm-for-youtube-video-245127`; ending `freesound_community-014264_tradicional-village-carnival-brass-band-57285`.
- Chicken: `arunangshubanerjee-fussing-rooster-in-indian-village-natural-ambience-330927` (3D at the chicken): when a fight starts (at the enemy) and as an idle call from your chicken every 45-90 s (not in a fight / dialogue).
- Dialogue: `chieuk-minecraft-villager-289282` once per shown line (pitch 1.0 for NPC, 1.3 for `[Player]` lines), max one per 0.4 s.
- Interaction: quest accepted `impactBell_heavy_004`, objective done `impactBell_heavy_002/003`, quest done `impactBell_heavy_000/001`; money up `impactTin_medium_000-004`, money down `impactMetal_light_000-004`; inventory change `impactSoft_medium_000-004`; training session done `impactPlate_medium_000-004`; transformation `impactMining_000/001`; player footsteps `footstep_grass_000-004` (taken from the player's movement by position, the player controller was not touched).
- Combat (3D at the hit chicken): damage < 25 and < 15% of max HP `impactPunch_medium_000-004`, bigger `impactPunch_heavy_000-004`, death `impactSoft_heavy_000-004`; fight start `impactBell_heavy_001`; fight won `impactBell_heavy_000`; fight lost `impactPlank_medium_000-004`.
- Anti-spam: per-group minimum interval (0.12 s hits ... 20 s idle rooster), no clip twice in a row, at most 8 voices (a new sound is dropped when all are busy), pitch jitter, music/ambience change only every 0.3 s policy check with a 2.5 s fade. Volumes: music 0.30, ambience 0.45, SFX 0.35-0.9 per group, all in the Inspector of `GameAudio`.

[VERIFIED-PLAY]: one manager, 12 sources; music = explore + ambience = morning started and reached volume 0.30 / 0.45; 30 money gains + 30 dialogue openings in ONE frame -> 1 money sound + 1 voice (29 blocked); 4 dialogue lines in 3 frames -> 1 extra voice, 3 blocked; after the first-loss flag: sad music + evening ambience; after the transformation: explore + morning again, `transformation` played once; quest accept/objective/done sounds fired through QuestManager events (one extra accept/done blocked by the interval); a training session fired `trainDone` and `moneySpend` once each; 6 m of walking -> 2 footsteps (the test walked at 7.5 m/s, faster than real, the rest were blocked by the 0.25 s interval), standing -> 0, a 30 m teleport -> 0; a real fight (8.6 s, the chicken lost): battle music during the fight, `battleStart`, `rooster`, hits (4 light + 3 heavy played, 4 blocked), `deathKnock`, `battleLost` once, explore music + morning ambience again afterwards. Compile: no C# errors (the Console still has the known URP/Lit shader memory errors; the `get time on an audio source` warnings come from my own test code reading empty sources).

[NOT HEARD / NOT TESTED]: how anything sounds (levels, the 24 kHz ambiences vs the 44.1 kHz music, loop seams, the Minecraft villager as a voice); real overlap of voices; `battleWon` and the ending music (no win fight / ending run, `EndingSequence.Started`); the idle rooster call (timer 45-90 s); the night ambience (bed not run); tournament fights (same BattleManager/HealthSystem path, not run); a standalone build; hits on `GuguGaga` and tournament opponents are hooked by scanning all HealthSystems at start and at each fight start.

MISSING sounds (no matching file exists, nothing was invented):
- Voice blips for dialogue: `voice A`, `voice I`, `voice U` (no A/I/U syllable clips; nothing is played per letter). The nearest file by name, `uia-uia-uia-cat.mp3` (8.5 s cat gag), is NOT wired; the villager sound is a stand-in once per line.
- Chicken: hen cluck, rooster crow (morning), hurt squawk, death squawk, wing flap / running, eating (feeding the chicken), power-up chirp for the muscle chicken. Only one rooster clip exists, so every chicken sound is that clip.
- Training: punching-bag hit per punch (the `impactPunch_*` clips exist but no per-punch event is wired), treadmill motor, push-up effort, protein powder / mixing.
- UI and story: button click / hover, dialogue next, shop purchase (cash register), medal fanfare, short victory / defeat jingle, bed / sleep, watering can pouring, harvesting vegetables, box pickup / delivery, door, tournament crowd cheer.
- Environment: footsteps on wood / dirt / concrete exist in `Assets/Audio/Audio` (`footstep_wood`, `footstep_concrete`) but there is no surface detection, only grass is used; no rain / river / water, no vehicle sounds.
- Unused existing files: `freesound_community-ambience-outdoor-rural-wind-parc-casa-de-les-aigues-2-51262`, `wolff-sfx-field-ambience-414431`, `soul_serenity_sounds-distant-village-243574`, `uia-uia-uia-cat`, the other Kenney impact families (glass, tin heavy, wood, mining 2-4...), `kenney_game-icons.zip` is image icons.

Risks to settle: (1) licences: no licence file next to any sound; the Kenney names suggest CC0 but that is not verified; `chieuk-minecraft-villager` is probably a Minecraft sound (third-party game IP), consider replacing it before publishing; (2) the three 24 kHz ambiences sound duller than the 44.1/48 kHz files; (3) `Assets/Audio` duplicates 28 MB of `Assets/Sound` (only referenced assets enter a build, so no build cost, but the project is bigger than needed; I did not delete anything).
Next (if wanted): listen once with headphones and tune the Inspector volumes; wire a per-punch sound from the training animation; replace the villager voice by real A/I/U blips when they exist.


## 44. Light particle effects: fight hits, watering, punching bag (2026-10-23, NOT committed)
Found: no particle asset to reuse (`Assets/Art/VFX` was empty, no prefab in `Assets` has a ParticleSystem, no ParticleSystem in the scene, no VFX code). Hit moments: `PetCombatAI/RoosterCombatAI.OnAttackHit` -> `HealthSystem.TakeDamage` (left untouched); watering: `QuestObjectiveTarget.Interact` -> `QuestCarryItem.PlayUseFeedback`; punching bag: `TrainingStation.RunSession` (strike lands after `pendingSwing`).
Added: `Assets/Script/Vfx/GameVfx.cs` (the only VFX manager; scene object `GameVfx`, saved). It builds 3 Unity ParticleSystems at start: spark (light hit, bag), heavy spark (big hit) and water drops. They are one-shot bursts through `ParticleSystem.Emit` at a world position (emission rate off, not looping), particle life 0.25-0.9 s, `maxParticles` 40/40/60, and each effect has a minimum interval (hit 0.06 s, bag 0.3 s, water 0.25 s) so nothing is spawned every frame and nothing lives forever. Assets created: `Assets/Art/VFX/Vfx_SoftDot.png` (64x64 soft dot generated by code) and `Assets/Art/VFX/Vfx_Particle.mat` (URP Particles/Unlit, transparent).
Hooks (visual only, null-safe static calls): fights = GameVfx listens to every `HealthSystem.OnHealthChanged` and emits at the victim's collider centre when health went DOWN (heavy if the loss >= 15% max or >= 25) - no damage code read or changed, it also covers tournament / final-boss roosters (they exist in the scene); `QuestObjectiveTarget.Interact`: one line after `PlayUseFeedback`, emits water only if the required item is STILL held afterwards (= watering can; the consumed protein bag is not); `TrainingStation`: new tiny `BagImpact()` called where the strike lands (`swingAmplitude = 28f`), emits sparks at the bag's renderer bounds centre.
[VERIFIED code-path, Play Mode, Console 0 errors, compile OK]: 1 GameVfx, 3 ParticleSystems; watering 3 plots: water burst for plot 1, the immediate 2nd was rate-limited (blocked), the 3rd after 0.7 s played, quest progress still exactly 3/3 ReadyToTurnIn; max 32 live particles, 0 after 2 s; protein bag use: no water, garden progress 1; fight (first-battle balance): 7 health-loss events -> 4 effects + 3 blocked by the 0.06 s interval, max 8 live particles, 0 two seconds after the fight, damage numbers as before (pet took 15 per hit, rooster 150 unchanged as the scripted first battle expects); punching bag session: 5 spark bursts over 538 frames, max 10 live, 0 afterwards, Attack 10 -> 12 and the 60 coins as before.
NOT verified (needs a person in the GAME VIEW): how the effects LOOK (colour, size, whether the material renders correctly in URP and not pink / invisible, position of the sparks on the chicken and the bag, water position at the plant), that no particle shows through walls, performance on a real build. Weights of the fixes: if the sparks look off, tune the numbers in `GameVfx.Awake` (size / speed / count) and the intervals in the Inspector.

## 45. Decision E1 answered - camera must show the whole 2D meme picture on the Player (2026-10-22, NOT committed)
Owner answer to section 44 / E1: the Player has a 2D meme picture; at the discovery moment the camera must turn so the WHOLE picture is seen. Items E2-E4 are still open.
Findings: the picture is `Player/meme` (SpriteRenderer, sprite `meme` 461x666 px, scale 0.1 -> world size 0.46 x 0.67 m, 0.19 m in front of the camera, i.e. inside the near clip 0.3, so it is never visible in first person). It is NOT a face model. Measured by rendering: seen from the FRONT of the Player the picture is MIRRORED (diff to the original 34.2 vs 0.6 when mirrored back); it is upright only from behind the Player.
Built: NEW `Assets/Script/Story/PlayerMemeShot.cs` + scene root `PlayerMemeShot` (references: Main Camera, `meme`, MouseMovement + PlayerMovement to freeze, HeldItemAnchor to hide). `Play(onFinished)` swings the camera in front of the Player, distance computed from the picture size and the camera FOV (default `fill` 0.62 -> ~0.93 m), flips the picture back (flipX) for the shot, holds 2.2 s, returns; `ComputePose` also shortens the distance if a wall is behind. Everything is restored afterwards (camera local pose, flip, mouse look, movement, held items), also if the component is disabled in the middle. No controller script was edited. Not yet called by any story step: Checkpoint 4 (discovery at the training area) will call `Play()`.
[VERIFIED numeric] temp camera with the real FOV 60 and 16:9: picture fully inside the frame (x 0.38..0.62, y 0.19..0.81, height share 0.62); rendered picture matches the original upright (diff 9.7 vs 34.6 mirrored).
[VERIFIED-PLAY, real frames, slow motion] second `Play()` while playing is refused; during the shot mouse look/movement off, held items hidden, flipX true, picture fully inside the REAL game camera; after the shot camera local pose (0,1.527,0)/(0,0,0), mouse look, movement, held items, flipX all back to the original values. Console: 0 project errors (only the editor Account API warning).
[NOT VERIFIED]: how it looks on screen (no screenshot was possible; framing was measured with numbers); behaviour with the Player standing against a wall in front; the swing speed/feel (moveSeconds 0.6, hold 2.2, return 0.5 are guesses - tune in the Inspector); the picture's own look from the front (it is a flat sprite on a thin plane, no body behind it).

## 46. Reaction shot: 2x faster swing, hold 3-5 s, caption, and where it is used (2026-10-22, NOT committed)
Owner request: swing speed x2, but the picture stays on screen about 3-5 s before the camera returns. It is used in the story at the moment the Player first SEES the chicken doing push-ups at the training area (after: sleep -> wake up, chicken missing -> go looking). The Player's line shown with it: `Cái héo lá gì dậy fen. Con gà nhìn quen vậy`.
Changed: `PlayerMemeShot.cs`: `moveSeconds` 0.6 -> 0.3, `returnSeconds` 0.5 -> 0.25, `holdSeconds` is now a 3..5 s slider (default 4 s; was 2.2); new `Play(string caption, Action onFinished)` shows the caption on the `MessageBannerUI` for the whole hold (`Play(Action)` still works). Scene object `PlayerMemeShot` updated (0.3 / 4 / 0.25, banner wired; the scene held the old values, which override script defaults) and saved.
[VERIFIED-PLAY, real frames, fixed 0.02 s/frame] `Play(caption)` took 4.60 s of game time in total (0.30 + 4.00 + 0.25 + 1 frame); the caption was on the banner; afterwards camera local pose, mouse look, movement and flip were restored. Console: 0 project errors.
[NOT VERIFIED]: the look/feel on screen; a caption of this length fitting the banner on a real screen.
NOT YET WIRED INTO THE STORY: the trigger `look at the chicken doing push-ups -> Play(caption)` does not exist yet, because the steps before it do not exist yet (Checkpoints 2-3 of section 44: night/bed, chicken missing in the morning, find-the-chicken quest) and CP4 itself (the chicken at the training area doing push-ups, 'seen' detection, E to take it back). Detection plan for CP4: the first time the chicken (push-up pose, `IsPushing`) is within a view distance AND inside the camera view with a clear line of sight, freeze the Player, call `PlayerMemeShot.Play(caption, ...)`, then let the Player walk up and press E. Open decisions E2-E4 of section 44 remain (evening mechanism, who opens the `tim_ga` quest, when the muscle change shows); the owner's outline implies the Player searches without being told the exact place - say if a hint from Ông/Bà is wanted.


## 39. Bug fix: final boss rooster stayed lying dead (2026-10-23, NOT committed)
Owner report (with screenshot, 2026-10-22): in the second fight against the friend ('Phục thù - trận đấu cuối cùng') the opponent chicken lay down and never got up, so the fight could not happen. Live inspection of the owner's Play session showed the rooster `GuguGaga` with `HealthSystem.IsDead = false`, HP 330/330, but Animator clip `Dead`.
Root cause (confirmed in code + controller): the final boss reuses the same rooster that was killed in the earlier rematch. `FinalBossController` revived it with `HealthSystem.Revive(1f)`, which only clears the `isDead` bool. In `Assets/Animations/Chicken.controller` the `Dead` state leaves ONLY through the `WakeUp` trigger (nobody sent it for the rooster), and `RoosterCombatAI.StopCombat()` sets a private `isDead` flag that nothing ever cleared, so `RoosterCombatAI.Update` returned immediately. (The player's chicken was never affected: its recovery code already sends WakeUp and calls `PetCombatAI.ResumeAfterRecovery`.)
Fix (small, additive): `RoosterCombatAI.ResumeAfterRevive()` (new public method: clears the flag and target, resets isRunning, sends WakeUp, un-stops the agent) and one changed line in `FinalBossController.BeginFight`: `if (foe.IsDead && foe.Revive(1f)) foeCombat.ResumeAfterRevive();`. `RematchController` and `TournamentManager` have the same `Revive` pattern but never revive a dead rooster in the current story order (their foe is alive when the fight starts), so they were left unchanged.
Also fixed: the quest HUD showed 'Trận cuối: {0}/{1}: 0 / 1' because `Quest_PhucThuCuoiCung.progressLabel` contained unformatted placeholders; it is now 'Trận cuối' (the HUD appends the counter itself).
Note: `FinalBossController` now contains an arena-arrival step (`pendingArrival`, `arenaRadius` 6) that I did not write in this session; it was added by someone else after M12 and is unchanged. The fight only starts once the chicken and the player are within 6 m of the rooster.
[VERIFIED-PLAY, Pause + Step, no real input] Reproduced the precondition (rooster killed beforehand, lying in `Dead`), moved the chicken and player into the arena, `StartFinalBattle()`: boss stats 330/34, the rooster went Dead -> Kick4 -> Idle -> Run, hit the chicken (HP 100 -> 32 -> 0) and the fight ended Completed. Before the fix the rooster stayed in `Dead` for the whole fight. Compile: 0 errors. NOT verified: the owner's real play-through (E / mouse) of the whole final fight, loss + retry, and win -> ending after this fix.


## 40. Training NPC (prepaid hours) - DESIGN DECISIONS written BEFORE the code change (2026-10-23, NOT committed)
Task: replace the per-station payment by a trainer NPC: the player talks to him, picks a number of hours, pays up front, the chicken completes run + push-up + boxing, every training hour gives +20 damage, the total hours are shown.
Audit result (read, nothing changed yet): `TrainingStation` charges `CurrencyWallet.TrySpend` per exercise (50 / 40 / 60) and raises Speed / Strength / Attack by 2 through `ChickenStats.Train`; `ChickenStats.Changed` (one raise = one event) is what `RematchController` counts as one 'buổi tập' for quest `chuan_bi_tai_dau` (needs 5, key `prepare_rematch`); the push-up station needs the inventory item `Item_TaTapGa` (checked, not consumed); training is story-gated by `ChickenTransformationTrigger.TrainingUnlocked`; `CurrencyWallet.TrySpend/Add` is the only money API (no singleton); `ChickenStats` has no hours and `Damage = base + (Attack-10) + 0.5*(Strength-10)`.
Decisions:
1. **Time split.** One paid hour = one circuit of the three exercises in a fixed order, each exactly 1/3 of the hour: Treadmill (run) -> Push-up -> Punching bag (boxing). An hour is `secondsPerHour` = 12 s of real time (4 s per exercise, the length of the old single sessions) plus the short walk between the stations. N hours = N full circuits, so the chicken always does all three exercises, also for 1 hour (splitting N hours into thirds would not work for N = 1 or 2).
2. **Price rises with the hours.** Hour k costs `firstHourCost + (k-1) * extraHourIncrease` = 150 + 50*(k-1) (150 equals the old three stations 50+40+60 together). Total: 1 h = 150, 2 h = 350, 3 h = 600, 4 h = 900, 5 h = 1250. Choices 1..5 hours (`maxHoursPerSession`). A choice the player cannot afford is disabled in the window and also refused in code.
3. **Payment.** Paid once, up front, with `wallet.TrySpend(total)`; if it returns false nothing else happens (no session, no hours, no damage). A booking while a session runs or while the window is closed returns false (no second charge). If the session is cut short (chicken dies / battle starts / NPC disabled) the cost of the hours NOT completed is refunded; completed hours stay paid and are kept.
4. **Effect.** Each COMPLETED hour = +20 damage (`ChickenStats.damagePerTrainingHour`, exact and additive, shown as before -> after). The old per-exercise stat gains (Speed/Strength/Attack +2) are NOT given by the trainer any more (otherwise damage would grow by more than 20 per hour); the old stat code stays in `ChickenStats`, untouched and unused for now. The hours are credited ONCE at the end of the session (partial hours if interrupted) and raise `ChickenStats.Changed` once: one session = one 'buổi tập' for the rematch quest, whatever its length.
5. **Display.** Total hours shown in the trainer window and permanently under the money on the HUD ('Giờ tập: X').
6. **Stations** keep their models, spots and animations but stop charging / prompting (flag `paidByTrainer`); the trainer drives them. The push-up part still needs `Item_TaTapGa` (checked at booking, nothing charged without it).
Balance warning (owner's requirement, not my choice): +20 damage per hour is huge next to the chicken's base damage 10 (rooster 150 HP, boss 330 HP): 1 hour = 30 damage, 5 sessions of 1 hour = 110. The rematch / tournament / boss tuning was done for the old +2-per-exercise growth, so those fights will become much easier; there is no cap on total hours.

## 47. Training NPC (prepaid hours): implementation, fix and tests (2026-10-23, NOT committed; follows the design decisions of section 40 'Training NPC')
[VERIFIED-PLAY] = Play Mode, editor paused and `EditorApplication.Step()`, the REAL hour buttons were pressed through `Button.onClick`; NO real E key / mouse, no look at the window or the animations on screen. Story gate `ChickenTransformationTrigger.Transformed`, the chicken's wake-up and the Tạ tập item were set by code for the tests.

What exists (written before this pass, checked and wired by name in the scene): `Assets/Script/Training/TrainerNPC.cs` (on `NPC_Trainer` at (-7.5,0,48.3): E prompt `Nhấn E để nói chuyện với Huấn luyện viên`, runtime window with hour buttons 1-5, up-front payment, session coroutine, refund of unfinished hours), `TrainingHoursHUD.cs` (`Giờ tập: X` under the money), `ChickenStats.TrainingHours / DamagePerTrainingHour (20) / AddTrainingHours`, and `TrainingStation` driven mode (`paidByTrainer` true on all three stations: no prompt, no charge). Reused, not rebuilt: CurrencyWallet.TrySpend/Add, InventorySystem, TrainingStation props/spots/animations, the story gate, ShopUI-style player freeze.

TIME ALLOCATION (the rule asked for, also in section 40): one paid hour = one circuit in a fixed order, Treadmill (run) -> Push-up -> Punching bag (boxing), each exactly 1/3 of the hour (`secondsPerHour` 12 s = 4 s per exercise, plus the short walk between stations). N hours = N full circuits, so the chicken always does all three exercises, also for 1 hour. Measured: each exercise banner starts about 5 s after the previous one (4 s exercise + ~1 s walk).
PRICE: hour k costs 150 + 50*(k-1): 1h=150, 2h=350, 3h=600, 4h=900, 5h=1250 (section 40 had 950 / 1400 by mistake; corrected there).

Bug found and fixed in this pass: in the circuit the push-up animator flag `IsPushing` stayed true after the push-up, so the chicken was still in the push-up state during boxing and the whole second hour. `TrainingStation.WalkAndExercise` now clears `IsPushing` before walking and clears `IsPushing`/`isRunning` when an exercise ends. File changed: `Assets/Script/Training/TrainingStation.cs` (two small inserts). Nothing else changed in this pass.

Verified [VERIFIED-PLAY]: compile clean, no project errors in the Console (only the known URP/Lit 'out of memory while parsing' shader messages). Booking while the window is closed: false, no charge. No money: all five buttons disabled and `TryBook` refused, no money/hours/damage change. Money 400: only 1h and 2h enabled; 3h (600) refused; 1h without Tạ tập refused with no charge; hours 0 and 6 refused. Pressing the real 2h button: money 400 -> 50 (350) exactly once; a second press of the same button, the 1h button and a direct `TryBook(2)` right after changed nothing (window closed / session running). The 2h session ran 38.5 s in the order Chạy máy -> Chống đẩy -> Đấm bao cát twice (banner `Giờ i/N: <bài>`), chicken running / pushing / punching at each station; result damage 10 -> 50 (+20 per hour), total hours 2, `combat.damage` = 50 (ChickenStats.Damage is applied to PetCombatAI), HUD `Giờ tập: 2`, banner `Tập xong 2 giờ! Sát thương 10 → 50 | Tổng giờ tập: 2 (-350 tiền)`, `ChickenStats.Changed` fired once (one session = one `buổi tập` for the rematch quest), combat re-enabled, busy flag cleared. 1h session: animator flags per exercise correct after the fix (run only on the treadmill, push only at the mat, neither at the bag), damage 10 -> 30, money -150. Interrupted 3h session (money 850 -> 250, chicken killed in hour 2): refund 450, 1 completed hour kept (damage 30 -> 50, hours 2), money 700, busy and session flags cleared, banner `... [bị gián đoạn, hoàn 450 tiền]`.

Payment-failure rule: `TryBook` spends with ONE `wallet.TrySpend(total)`; when it returns false nothing else runs (no session, no hours, no damage). Tested with 0 money (no payment possible). A forced TrySpend failure after CanAfford passed was not simulated (no way to make the wallet fail in between without changing code).

[NOT VERIFIED]: real E key and mouse clicks on the window; how the window and the stations look on screen (no Game view / screenshot); the Vietnamese letters in the window (`Huấn luyện viên`, `thiếu tiền`, `sát thương`) in the TMP font (see the TMP font note in section 23); the NPC model (`NPC_Trainer` placeholder); training with the real story order (gate set by hand); quest `chuan_bi_tai_dau` progress from a trainer session (the `Changed` event fired once; the quest itself was not run).

Balance warning (unchanged from section 40): +20 damage per hour is far above the old +2 per exercise; rooster 150 HP / boss 330 HP are now beaten in 1-2 hours of training (no cap). Next step: the owner decides the cap or the tuning (e.g. lower `damagePerTrainingHour`, a max total hours, or higher rooster/boss HP) and tests a full run with real keys. Git status could not be run from Unity MCP; nothing committed or pushed.

---

## 48. Thảm tập / Vé số / Phân bón protein - kiểm tra (2026-10-23)

**Hiện trạng tìm thấy (do session khác làm, KHÔNG làm lại):** `Item_ThamTap` (`training_mat`, stack 5), `Item_VeSo` (`lottery_ticket`, stack 20), `Item_PhanBonProtein` (`protein_fertilizer`, stack 10); `ShopCatalog_DungCu` bán: Bình tưới 60, Thảm tập cho gà 150, Thức ăn tăng lực 40, Vé số 20, Phân bón protein 80 (tạ không còn bán). `ItemUseController` (phím F): đặt thảm, cào vé số, bón phân. `Station_PushUp`: requiredItem = Item_ThamTap, requiresPlacement, placedVisual = `Mat`. Không tạo shop/inventory mới.

**Kiểm tra tham chiếu tạ cũ (`Item_TaTapGa`, id `training_weights`):** không có `.cs` nào nhắc; 0 component trong scene tham chiếu; chỉ 10 scene backup `Assets/_Recovery/0 (10..19).unity` còn tham chiếu; tài liệu (GAME_DESIGN, DEVELOPMENT_ROADMAP, PROJECT_STATUS) còn nhắc dạng chữ. **Quyết định: CHƯA xóa `Item_TaTapGa.asset`** (không có git nên không hoàn tác được; scene recovery còn tham chiếu). Xóa được khi người dùng đồng ý. Mục 47 ở trên còn ghi push-up cần Tạ tập = lỗi thời, nay là Thảm tập.

**Sửa của tôi (chỉ chữ):** `RematchController.notReadyLines[1]` và mô tả `Quest_ChuanBiTaiDau` đổi từ Tạ tập sang thảm tập.

**Test (Play Mode, đi qua code: Button.onClick của ShopUI thật + hàm thật của ItemUseController/TrainerNPC; KHÔNG phải phím/chuột thật):**
- Shop 5 dòng đúng giá. Mua thảm với 0 và 140 xu: từ chối, tiền/số lượng không đổi. Với 1140: 1140→990, +1 thảm. Hai click cùng frame: mỗi click trừ 150 riêng.
- Túi đầy (5 loại): mua loại MỚI bị từ chối, không trừ tiền; mua thêm thảm (stack còn chỗ) được; giải phóng ô thì mua vé số 20 và phân 80 được.
- Dùng thảm: xa khu tập → từ chối, không mất; gần Station_PushUp → mất 1, `IsPlaced`, Mat hiện; thảm thứ 2 → từ chối, không mất.
- Vé số: mất 1; hết vé → từ chối; bảng thưởng kỳ vọng 17.5 < giá 20; PrizeForRoll(0)=0, 0.75=20, 0.999=1000. Chưa gặp lượt trúng thưởng thật.
- Phân bón: xa vườn → từ chối; gần `VegetablePlot_1` → mất 1. Chưa có hiệu ứng gameplay (không ảnh hưởng quest vườn).
- Huấn luyện viên: thảm chưa đặt → TryBook(1) từ chối, tiền 940→940; đã đặt → nhận, 940→790 (150), 1 giờ tập xong, damage 10→30.
- Console: 0 lỗi.

**CHƯA kiểm tra:** phím F/E/chuột thật, chọn hotbar bằng phím 1-5/cuộn chuột, giao diện shop, chữ tiếng Việt có dấu, vị trí/hình dạng thảm trong cảnh, nhận thưởng vé số thật, tác dụng thật của phân bón.

**Test thủ công cần làm:**
1. Vào shop, bấm mua Thảm tập: tiền giảm 150, hotbar hiện thảm. Thử khi thiếu tiền: không mua được.
2. Chọn thảm trên hotbar (phím 1-5/cuộn), đứng cạnh khu tập (Station_PushUp), bấm F: thảm hiện đúng chỗ, mất 1. Bấm F lần nữa với thảm thứ 2: bị từ chối, không mất.
3. Đứng xa khu tập bấm F: có thông báo, không mất thảm.
4. Lấp đầy túi bằng 5 loại item khác nhau, thử mua loại mới: bị từ chối và không trừ tiền.
5. Mua vé số, chọn, bấm F nhiều lần: xem thông báo trúng/trượt và tiền cộng khi trúng.
6. Mua phân bón, bấm F cạnh luống rau; xa luống thì phải bị từ chối.
7. Thuê huấn luyện viên: phần chống đẩy chỉ chạy khi thảm đã đặt.
8. Kiểm tra gợi ý nhấn phím, chữ có dấu, vị trí/hình thảm.

Chưa commit/push Git.

## 37. Map: one-Terrain consolidation (started 2026-10-23, NOT committed)

### CP0 - inspection, asset/licence check, backup, baseline (NO scene change yet)
**What the docs say.** GAME_DESIGN has no coordinate plan for the map; only the ZONES exist (section 24 above built them): EntranceGate (10,-6), GrandparentsHouse (-14..4, 3..23), VuonCayAnQua_Garden (-20..0, 19..35), TrainingArea (-16..0, 46..58), BattleArea/arena (18..39, 9..31), ShopArea (23..35, 45..53), RewardArea (41..54, 55..63), ScrapArea, RiceField, 22 house plots, perimeter walls x -94..193, z -73..163. All of them are present in the scene now.
**What the scene really has (found by reading it).** FOUR Terrain objects, not one: `Terrain` (0,0) with layer `Ground`(6), TerrainCollider, NavMeshSurface and 3 painted layers, plus three auto-created neighbour tiles `Terrain_(-1000,0,0)`, `Terrain_(0,0,-1000)`, `Terrain_(-1000,0,-1000)` on layer `Default` with 1 layer each; every tile is 1000x1000, all completely flat (height 0). The village spans x -94..194 and z -75..164, so everything with x<0 or z<0 (garden, training area, bed, Trainer, gate, houses west/south, Chi Ba, Chu Tu) stands on a neighbour tile. The NavMesh (one surface, baked from RenderMeshes) spans x/z +-999.
**Defect confirmed (real, measured):** `PlayerMovement.groundMask` = Ground only (64). On the neighbour tiles `Physics.CheckSphere` is FALSE at (-8,52) training, (-4,14) bed, (10.5,-10) outside the gate; at (10.5,3.1) spawn it is TRUE. So in three quarters of the map `isGrounded` is never true: the player cannot jump and `velocity.y` keeps accumulating gravity until the player steps back onto the main tile.
**Serialized references to any terrain object from other objects: 0** (scanned all components) and no script mentions `Terrain`, so replacing the terrain cannot break quest, M11-M13 or story references.
**Assets (verified by reading files and importers; nothing downloaded):**
- `GTA SA Carl Johnson.fbx` EXISTS (two identical 187,020-byte copies: `Assets/Art/Character/source/` and `.../Universal Base Characters[Standard]/source/`, textures head/legs/shoes/upper body in `Assets/Art/Character/textures`). It is ONE static mesh (1 MeshFilter, 1,724 vertices, 4 materials, no SkinnedMeshRenderer, no bones, no Avatar, Generic importer). Not rigged, so it CANNOT play any animation (UAL included). There is NO licence file for it and it is a ripped Rockstar/GTA San Andreas character: not CC0, not covered by the Quaternius licences next to it. It must not be used as the friend's final model or shipped without the owner's own legal clearance. NOT replaced.
- `Universal Animation Library[Standard]` EXISTS (CC0 1.0 by Quaternius, `License.txt` + `README.txt` read). Same content in two folders (`Assets/Art/Character/` and `Assets/UI/`, plus two .zip files; `UAL1_Standard.fbx`, `UAL1_Standard_RM.fbx` with root motion, `.glb` versions). 43 clips, Generic rig, 65 bones, importer `Generic / NoAvatar`. Compatible only with the Quaternius 65-bone skeleton (Universal Base Characters, also CC0, in the project); it does not fit CJ (no skeleton). The friend `NPC_ChildhoodFriend` currently uses `SuperHero_Male` (Humanoid avatar, no AnimatorController). No friend model change was made.
**Rollback.** Scene copy `Assets/Scenes/Backup/MainGame_BeforeTerrainMerge_20261023.unity` (4,035,012 bytes = identical to MainGame.unity at that moment, scene was saved, not dirty; not in Build Settings). The four TerrainData assets (`Assets/New Terrain.asset`, `Assets/TerrainData_ce7a393f-...`, `_273c1395-...`, `_44dd4570-...`) are NOT modified or deleted. Plan: the new single terrain is added, the four old terrains are only DISABLED (not deleted), so rollback = enable them / delete the new one / or open the backup scene.
**Baseline before any change:** NavMesh `PathComplete` from `PlayerSpawn` to 20/20 targets (all NPCs, pet, enemies, spawn points, bed, gate, training point, ending home spot).

### CP1 - single Terrain built (scene SAVED)
Added `Terrain_Village` (root object): ONE terrain 400 x 400 m (height range 100), world origin (-150, 0, -130) so it covers x -150..250, z -130..270 = the whole perimeter (x -94..194, z -75..164) plus a margin. Nothing was moved: every house, NPC, quest object, spawn point and M11-M13 object keeps its world coordinates. New asset `Assets/TerrainData_VillageSingle.asset`: same 3 `TerrainLayer`s (VillageGrass, VillageDirt, RicePaddy), alphamap 512 (0.78 m/px), painting RESAMPLED from the old painted tile (110,720 of 262,144 pixels copied; the rest = plain grass, exactly what the neighbour tiles had), height 0 flat like before. Layer `Ground` (6), TerrainCollider, material/pixel-error/basemap settings copied from the old main terrain, no neighbours / no auto-connect. New `NavMeshSurface` on it with the same settings as the old one (collect All, RenderMeshes, agent 0), baked: bounds x -149..249, z -129..269, 18,808 vertices (old: +-999, 27,774).
The four old terrain objects were NOT deleted: they are parked under the DISABLED root `OLD_Terrain_Tiles_DISABLED (rollback, see PROJECT_STATUS 37)`; their four TerrainData assets are untouched on disk.
Note for tools: right after moving a terrain in Edit Mode the physics world is stale (a Ground-layer ray missed everywhere with x<0); `Physics.SyncTransforms()` fixes it, Play Mode is not affected.

### CP2 - checks after the change
[VERIFIED, Edit Mode] `Physics.CheckSphere(groundMask 64)` TRUE at all 10 sample points (spawn, training area, bed, outside the gate, Chi Ba, Chu Tu, east and corner points); before it was FALSE on every point with x<0 or z<0. Ground ray hits `Terrain_Village` at y=0 everywhere. NavMesh `PathComplete` from `PlayerSpawn`: 20/20 targets (same list as the baseline: all NPCs, pet, enemies, spawn points, bed, gate, training point, ending home spot). Highest NavMesh point (9.5, 8.55, 126) is NOT reachable from the ground (`PathPartial`), so it is just geometry on top of something, not a walkable area.
[VERIFIED, Play Mode, frames stepped, no real keyboard] only one active Terrain; ChienKe_Pet, GuguGaga, Rooster_ChiBa, Rooster_ChuTu all `isOnNavMesh`; player teleported to the training area and outside the gate: `isGrounded` TRUE and `velocity.y` -2.39 (settled, before the merge it kept falling in these places); the REAL first fight (WakeUp, friend challenge, accept) ran on the new NavMesh and was lost by the AI: battle `Completed`, taunt dialogue open with the `Tôi` answer chain still pending (section 36 unchanged), quest `thu_suc_voi_ban_cu` Active until the dialogues close; Console 0 errors; scene saved, not dirty.
[NOT verified]: how the ground LOOKS (no screenshot: the painted grass/dirt/paddy resampling and the lack of a visible seam are unchecked), real keyboard walking and JUMPING in the training area/garden (isGrounded is now true there, the jump itself was not pressed), the other M11-M13 stages (rematch, tournament, final boss, ending) were not replayed: the change touched no script, reference or coordinate, and a scan found 0 serialized references to any terrain object, but they were not re-run, the opponent fights Chi Ba / Chu Tu on the new NavMesh, wall-top NavMesh pieces (e.g. (9.5, 8.55, 126)) were not inspected.

### CP3 - NOT DONE: moving the zones (the actual 'relayout')
The task also says 're-arrange the map'. No target layout exists in GAME_DESIGN or in the roadmap (the docs list zones but no coordinates plan), and every zone is already present (EntranceGate, grandparents' house, garden, TrainingArea, BattleArea, ShopArea, RewardArea). Moving them would touch ~2,450 renderers, the roads/paddies/ponds meshes and every NavMesh/quest/M11-M13 coordinate, so I stopped after the terrain consolidation and did not invent a layout. DECISION NEEDED from the owner: (A) keep the zone positions as they are (current state); (B) a described target (for example: gate at the village head, grandparents' house right inside the gate, arena and training area on opposite sides, shop and rewards near the centre) - then it is done zone by zone with the same checks (spawn, colliders, NavMesh PathComplete, quest targets, M11-M13 references) after each zone.
Rollback options, from cheapest: (1) enable `OLD_Terrain_Tiles_DISABLED`, disable/delete `Terrain_Village`, re-bake; (2) open `Assets/Scenes/Backup/MainGame_BeforeTerrainMerge_20261023.unity` (identical to the scene before this change).

## 49. Scrap sparkle (quest scrap items) - twinkling stars on uncollected scrap (2026-10-24, NOT committed)
Tags: [VERIFIED-PLAY] = Play Mode with real frames (`runInBackground` + `QueuePlayerLoopUpdate`, `captureDeltaTime 0.02`), calling the same public methods E calls (`ScrapCollector.TryCollect`). NO real keyboard/mouse. [NUMERIC] = rendered to a small texture and measured. [NOT VERIFIED] = not tested.
Request: when the scrap quest is accepted, uncollected scrap of THAT quest shows a star-sparkle particle effect for ~2 s, then off, repeating until the item is picked or the quest no longer needs scrap; picking stops it at once; other pickups unaffected; reuse particle assets; no per-frame spawning; no change to quest acceptance, amount, or reward.

### What was found
- Scrap quest `scrap_collector` (`Quest_EarnMoneyForTraining.asset`, key `scrap`, 10 needed, reward 100). 12 `ScrapPickup` under `ScrapItems`; `ScrapCollector` collects the nearest in range (E); `ScrapPickup.MarkCollected()` switches the pickup GameObject OFF, so a particle child stops at once. Collection is allowed in any state != NotStarted (surplus items still pay item money).
- `ItemPickups/Pickup_VeChai/Scrap_PlasticBottle` carries a DISABLED `ScrapPickup` (inventory item, not a quest item): it is not under `ScrapItems`, so it gets no effect.
- No particle prefab existed. Reused: `Assets/Art/VFX/Vfx_Particle.mat` (soft dot, URP Particles/Unlit) for glints, and the existing `Assets/UI/UI/PNG/White/1x/star.png` (64x64, alpha corner 0 / centre 1) for the stars via a copy of the same material.
- Pre-existing scene Particle Systems: `GameVfx/HitHeavy`, `WaterDrops`, `HitSpark` (3). Baseline PS count in the scene is therefore 27 (24 mine + 3).

### Changed / added
- NEW `Assets/Script/Scrap/ScrapSparkleController.cs`: one instance. Per pickup under `pickupsRoot` it plays/stops the pickup's `SparkleFX` child ONLY when the wanted state changes (no spawning, no per-frame allocation). Wanted = quest state is `Active` AND the pickup is enabled, not collected and active AND inside its window (`sparkleSeconds` 2 s on, `pauseSeconds` 3 s off, items spread over the cycle by `stagger`). End of a burst: stop emitting (last stars fade). Collected / quest ReadyToTurnIn / Completed / not started: `StopEmittingAndClear` (also on disable).
- NEW `Assets/Prefabs/VFX/ScrapSparkle.prefab` (2 Particle Systems: `Stars` 14/s warm-white star sprites rising slowly, shrinking + fading; child `Glints` soft dots 6/s; world space, max 40+20 particles, no shadows).
- NEW `Assets/Art/VFX/Vfx_Star.mat` (copy of `Vfx_Particle.mat` with the star texture). `Vfx_Particle.mat` itself is unchanged.
- Scene `MainGame` (saved): `SparkleFX` (prefab instance, local (0,0.2,0), scale compensated for parent scale) as a child of each of the 12 pickups under `ScrapItems`; new root object `ScrapSparkleController` wired to `ScrapItems`, `Quest_EarnMoneyForTraining` and the `QuestManager`.
- NOT changed: `ScrapPickup`, `ScrapCollector`, `QuestManager`, any quest asset, acceptance rules, amount (10), reward (100), money values.
- ASSUMPTION to confirm: the request gives the ON time (2 s) but not the OFF time; I used 3 s off (5 s cycle). Change `pauseSeconds` in the Inspector (or `sparkleSeconds`).

### Test results
[NUMERIC] star texture alpha ok; rendering one item with 1 s of effect: 1.0% of the frame changed, 0 magenta pixels, 17 particles alive.
[VERIFIED-PLAY] all 4 requested cases:
1. Before accepting the quest: no item emits, 0 particles alive; the inventory `Pickup_VeChai` and everything under `ItemPickups` / `QuestObjectives` has no particle system.
2. Just accepted (forced `AcceptQuest`; the NPC/first-loss gate is existing behaviour): 4 of 12 items emitting (58 particles), 0 mismatches with the schedule; later in time the set of emitting items changed and still matched the schedule (repeat works). Pure schedule check over 20 s: ON windows of 2.00-2.01 s every 5 s (4 windows), duty 0.40; items are staggered (5/12 on at one instant).
3. Collecting a sparkling item: in the same call it is already off (pickup + effect inactive, not emitting), progress counted exactly once, another sparkling item was unaffected.
4. At 10/10 (`ReadyToTurnIn`): the 2 uncollected items stop sparkling and have 0 particles; a surplus item can still be collected (unchanged behaviour); `TurnIn` pays exactly 100 once, a second `TurnIn` pays 0; in `Completed` no item emits. Particle System object count stayed 27 (no leak).
Console: 0 errors from this change. 2 console errors exist that are NOT caused by it: `Prefab instance problem. Missing Prefab Asset: 'Superhero_Female_FullBody (1)'` (guid f63617f13ea6fe945939bb43897696c3) from `Assets/Prefabs/NPC/NPCs_FromVillageMap.prefab` (last written 2026-10-23 11:54, before this task) and `Problem detected while opening the Scene file MainGame.unity` that it causes; plus the editor Account API warning. Not fixed (out of scope).

### Manual checks still needed (with real keyboard/mouse)
1. Accept the scrap quest from the villager (needs the first-battle loss) and LOOK at the stars: size, colour, brightness against the terrain/sky, readable from ~10-20 m? (sizes/rates are guesses: `Stars` start size 0.14-0.24, 14/s.)
2. Walk to a sparkling item, press E: effect gone at once; the next items keep blinking; the prompt/banner/money are as before.
3. Collect 10: remaining scrap stops sparkling; talk to the villager: reward paid once.
4. Check nothing sparkles on `Pickup_VeChai` (hotbar item near (11.5,0,9.5)), the watering can, the sack, the delivery box.
5. Check frame rate with all 12 sparkling (only ~5 are on at once).

### Next task
Owner decisions from section 44 (E2-E4) are still open: evening/night mechanism, who opens the `tim_ga` quest, when the muscle change shows. Then Checkpoint 2 (bed only at evening, light fade). Separate small task if wanted: repair the missing `Superhero_Female_FullBody` prefab reference in `NPCs_FromVillageMap.prefab`.
Git: nothing was committed or pushed. An earlier request to push `feature/Character-Pet-Enemy` was NOT carried out: the Unity tool cannot start a `git` process (`System.Diagnostics.Process` is not available there), so it has to be run from a terminal/GitHub Desktop.


## 50. Training NPC request received again - already implemented, re-checked statically (2026-10-24, NOT committed, NO project file changed)
The same request (trainer NPC, prepaid hours, price rising with hours, run + push-up + boxing, +20 damage per hour, total hours shown, no payment when short of money / no double charge / no stat gain after a failed payment) was found ALREADY BUILT and tested in sections 40 (design incl. time allocation) and 47 (implementation + tests). Nothing was redone. Checked now by reading code/scene only (NOT re-run in Play Mode): `TrainerNPC.cs` (438 lines), `TrainingHoursHUD.cs`, `ChickenStats.cs`, `TrainingStation.cs` exist; `NPC_Trainer` is in MainGame at (-7.5,0,48.3); the three stations have `paidByTrainer = true` (no own prompt or charge); `TrainerNPC.TryBook` refuses when the window is closed or a session runs (no second charge), refuses unaffordable or invalid hours, spends ONCE with `wallet.TrySpend(total)` and returns without any session/hours/damage if that fails, sets `IsSessionActive` in the same frame as the payment, and refunds if the chicken cannot be taken over; Console has 0 compile errors.
Time allocation (as already recorded): one hour = one circuit Treadmill -> Push-up -> Punching bag, each 1/3 of the hour; prices 150 / 350 / 600 / 900 / 1250 for 1-5 hours; +20 damage per COMPLETED hour.
Still open (owner decisions / manual tests, unchanged from section 47): balance (+20/hour makes the rematch, tournament and boss far easier, no cap); real E/mouse test and the on-screen look of the trainer window (Vietnamese letters); NPC_Trainer is a placeholder model; `chuan_bi_tai_dau` progress through a trainer session was only checked via the `Changed` event.


## 51. NPC animations from UAL Standard (2026-10-24, NOT committed)
### A. Audit (before editing)
- `NPCs_FromVillageMap` (prefab `Assets/Prefabs/NPC/NPCs_FromVillageMap.prefab`) is a GROUP of 6 NPCs, not one: NPC_Grandfather (model 'The Boss (1)'), NPC_Grandmother ('Ch13_nonPBR (1)'), NPC_ChuTu ('Big Vegas'), NPC_CoLan ('kaya'), NPC_BacHai ('Ch43_nonPBR'), NPC_ChiBa ('Ch03_nonPBR'). All six are skinned mixamo rigs (`mixamorig:Hips`). Only the grandmother had an Animator (empty: no controller, no avatar, nothing in code referenced it).
- `NPC_MarketVillager` (prefab `NPC_MarketVillager.prefab`): model 'Peasant Nolant(Free Version)', skinned, `metarig` rig (33 bones), had NO Animator.
- `NPC_ChildhoodFriend` (prefab `NPC_ChildhoodFriend.prefab`): the model is 'GTA SA Carl Johnson (1)' = two STATIC MeshFilter/MeshRenderer meshes (no SkinnedMeshRenderer, no bones; an unused empty `Armature/root` is left over). It cannot take an animation, so it was NOT changed (the model was replaced by the owner; not touched). Its root Animator has no controller (unchanged).
- All source FBXs were Generic/NoAvatar. `Assets/Art/Character/NPC/UAL1_Standard.fbx` (the asset asked for) is a Generic rig with 43 clips (also unused by any prefab/controller; the identical copies under `Assets/UI/Universal Animation Library[Standard]/` and `UAL1_Standard_RM.fbx` were not touched).
- Real clip names in `UAL1_Standard.fbx` (all prefixed `Armature|`): A_TPose, Crouch_Fwd_Loop, Crouch_Idle_Loop, Dance_Loop, Death01, Driving_Loop, Fixing_Kneeling, Hit_Chest, Hit_Head, Idle_Loop, Idle_Talking_Loop, Idle_Torch_Loop, Interact, Jog_Fwd_Loop, Jump_Land, Jump_Loop, Jump_Start, PickUp_Table, Pistol_Aim_Down, Pistol_Aim_Neutral, Pistol_Aim_Up, Pistol_Idle_Loop, Pistol_Reload, Pistol_Shoot, Punch_Cross, Punch_Jab, Push_Loop, Roll, Sitting_Enter, Sitting_Exit, Sitting_Idle_Loop, Sitting_Talking_Loop, Spell_Simple_Enter, Spell_Simple_Exit, Spell_Simple_Idle_Loop, Spell_Simple_Shoot, Sprint_Loop, Swim_Fwd_Loop, Swim_Idle_Loop, Sword_Attack, Sword_Idle, Walk_Formal_Loop, Walk_Loop. In the file none of them had the loop flag.
### B. Compatibility and retarget
Different skeletons (UAL bone names vs mixamo vs metarig), so the clips can only be shared through Humanoid retargeting. Changed importer settings (assets edited, nothing replaced or deleted):
- `UAL1_Standard.fbx`: Animation Type Generic -> Humanoid, Avatar 'Create From This Model' -> `UAL1_StandardAvatar` (human, valid). Loop Time + Loop Pose switched ON for every clip whose name contains `_Loop` (others off).
- Each NPC model FBX Generic -> Humanoid / Create From This Model, all avatars human + valid (auto mapping, not hand-checked bone by bone): `Assets/Art/Character/NPC/Big Vegas.fbx`, `kaya.fbx`, `Ch43_nonPBR.fbx`, `Ch03_nonPBR.fbx`, `Peasant Nolant(Free Version).fbx`; `Assets/Art/Character/OngBaBanThan/The Boss (1).fbx`, `Ch13_nonPBR (1).fbx`. Their own embedded T-pose clip is not used.
### C. Controller and wiring
- NEW single shared controller `Assets/Animations/NPC/NPC_UAL.controller`: states Idle (`Armature|Idle_Loop`, default), Walk (`Armature|Walk_Loop`), Talk (`Armature|Idle_Talking_Loop`); bool params `isWalking`, `isTalking`; transitions without exit time, 0.15 s. No script uses the params yet (see E).
- One Animator per animated model (on the model child, the object that matches the avatar's root): controller `NPC_UAL`, the model's own Humanoid avatar, Apply Root Motion OFF, Culling = Cull Update Transforms. Edited in the PREFAB assets (scene instances follow): `NPCs_FromVillageMap.prefab` (6 models) and `NPC_MarketVillager.prefab` (1 model). The empty, controller-less Animator on `NPC_Grandmother` (root) was removed so the grandmother does not have two Animators. Scene `MainGame` not changed by hand (not dirty).
- Not changed: colliders, positions, rotations, scales, `QuestNPC`, `ShopNPC`, `VillagerFlavorNPC`, `GrandfatherChickenRescue`, `TournamentOpponentNPC`, dialogue, quests, models.
### D. Tests [VERIFIED-PLAY, editor paused + EditorApplication.Step, NO real input, NO Game-view look]
Compile/Console: 0 errors. All 7 animated NPCs report state Idle, clip `Armature|Idle_Loop`, avatars human + valid, Animator initialized. Bone rotations of Head / arm / leg really change over 60 frames (up to ~3.5 / 4.4 / 5.5 deg: a subtle idle), identical across NPCs, so the retarget works. `kaya` showed 0 change at first ONLY because she is off-screen (renderers not visible, culling skips the update); with Culling temporarily AlwaysAnimate (runtime only) her leg moved 29 deg. Setting `isWalking` -> `Armature|Walk_Loop` on the market villager and grandmother, root position drift 0.000 (no root motion), back to Idle after clearing; `isTalking` -> `Armature|Idle_Talking_Loop`. All NPC root components/positions unchanged (list checked). `QuestNPC.Interact()` on the grandmother returned true and opened the dialogue; she stayed in Idle with the same position/rotation while talking.
### E. Not done / known issues / manual checks
- `NPC_ChildhoodFriend` (static Carl Johnson mesh) has NO animation. To animate him the owner must supply a rigged model (same Humanoid steps as above) - not done, not guessed.
- NPCs never move in this project (no agent, no patrol), so Walk/Talk are unused; nothing drives `isWalking`/`isTalking` (adding a driver, e.g. a one-line `isTalking` while the dialogue is open, was not requested). Only idle plays.
- `NPC_MarketVillager` has LEFTOVER duplicates at its root: a `Hat` SkinnedMeshRenderer and a `metarig` (a second, static copy next to the nested animated model). The static hat is ~0.15 m off the animated hat while the NPC idles: a possible double-hat artifact. Not removed (owner's layout); suggested fix: delete the root-level `Hat` and `metarig` in the prefab after looking at it.
- Look in the Game view NOT checked: Humanoid auto-mapping quality per model (twisted limbs, hands, feet sinking), foot height (the models' own root offsets), hat/clothing clipping, speed of the idle. Please look at each of the 7 NPCs once. Also check the other URP-shader magenta materials seen earlier are unrelated (not touched).
- Importer changes affect every other use of those 7 FBX files (prefabs reference their meshes/materials only; no other animation use found by a text scan of prefabs/controllers; binary scenes could not be scanned).

## 48. Shop goods: training mat replaces the weights, lottery ticket, protein fertilizer (2026-10-24, NOT committed)
[VERIFIED-PLAY] = Play Mode, editor paused + `EditorApplication.Step()`; the shop was driven through the REAL Buy buttons (`Button.onClick`), items through `ItemUseController.UseSelected()` (the exact method key F calls). NO real F / E key, no mouse, no look at the shop window, hotbar or props on screen. The Play session ended by itself between some calls, so tests were re-run in single calls. The story gate (`Transformed`) and the chicken wake-up were set by code.

STEP 1 - audit of every reference to the old weights (`Item_TaTapGa`, 'Tạ tập') BEFORE any change: (a) scene `MainGame`: only `Station_PushUp` -> `TrainingStation.requiredItem` (checked by scanning all 10513 components with SerializedObject, because the scene file is binary and a text search cannot see GUIDs); (b) asset `Assets/Data/Shop/ShopCatalog_DungCu.asset` (entry, price 150); (c) text: `RematchController.cs` line 47 (dialogue 'Mua Tạ tập ở chợ ...'); (d) docs: GAME_DESIGN.md lines 75/79/80, DEVELOPMENT_ROADMAP.md line 39, older sections of this file (history, left as is); (e) `Assets/_Recovery/0 (10..19).unity` are Unity auto-recovery backups (not in the build) - they still point at the old asset and will show it as missing if opened. TrainerNPC / TrainingStation only use the item generically (`displayName`), no hard-coded weights. After replacing (a)-(d) a new scan found 0 scene references and 0 asset dependencies, then `Item_TaTapGa.asset` was deleted (via manage_asset). Restore it from git if the old build is needed.

STEP 2 - what changed:
- New items (existing `ItemDefinition`, placeholder coloured icons, no sprites): `Assets/Data/Items/Item_ThamTap.asset` (Thảm tập cho gà, stack 5), `Item_VeSo.asset` (Vé số, stack 20), `Item_PhanBonProtein.asset` (Phân bón protein, stack 10).
- Shop (existing `ShopCatalog_DungCu`, no code change to ShopUI/ShopNPC): Bình tưới 60 | Thảm tập 150 (replaced the weights, same row) | Thức ăn tăng lực 40 | Vé số 20 | Phân bón protein 80.
- `TrainingStation.cs`: new optional `requiresPlacement` + `placedVisual`, `IsPlaced`, `CanPlace`, `Place()` (once only) and `RequirementProblem()` (one place that says why the required item is missing; used by the station and the trainer). `TrainerNPC.cs`: the inline inventory check was replaced by `RequirementProblem()`. `RematchController.cs`: one dialogue string.
- New `Assets/Script/Inventory/ItemUseController.cs` (object `ItemUseController` in the scene): key F uses the SELECTED hotbar item. Mat: stand within 3.5 m of the push-up station, F consumes ONE mat and places it (the `Mat` prop appears, the trainer accepts push-up). Ticket: F anywhere consumes ONE ticket FIRST, then rolls a prize (0: 70%, 20: 20%, 50: 7%, 200: 2.5%, 1000: 0.5% = expected 17.5 per 20-coin ticket) and pays with wallet.Add. Fertilizer: F within 3 m of a vegetable plot consumes ONE bag, plays the water effect and a message. Prompts `Nhấn F để trải thảm tập` / `Nhấn F để rắc phân bón protein` use the shared prompt. Blocked while dialogue, shop, old inventory or the trainer window is open.
- Scene: `Station_PushUp` now needs the mat PLACED (requiredItem = mat, requiresPlacement on, the `Mat` child hidden until placed); the `Item_TaTapGa` reference is gone.

Verified [VERIFIED-PLAY]: compile clean, no project errors in the Console. Shop lists 5 rows with the right prices and no weights. With 0 money all 5 buys refused (message 'Không đủ tiền mua ...'), nothing changed. With 1000: mat 150, ticket 20, fertilizer 80 bought through the real buttons, money 850 / 830 / 750 exactly, each item once. Bag full (5 slots used): buying food (needs a new slot) refused with 'Túi đầy ... Tiền không bị trừ.' and money unchanged; a 2nd ticket (stack has room) still bought. A stale click after closing the shop buys nothing. Ticket: prize table checked at rolls 0 / 0.69 / 0.71 / 0.91 / 0.985 / 0.999 -> 0 / 0 / 20 / 50 / 200 / 1000; expected prize 17.5 < price 20; using a ticket consumed exactly 1 (2 -> 1 -> 0), money changed only by the prize, with 0 tickets nothing happens and money is unchanged. Fertilizer: far from the plots F is refused with a message and nothing is consumed; next to plot 1 the prompt shows, F consumed 1 (1 -> 0), prompt disappeared, a second F did nothing. Mat: far from the station refused (mat kept); the trainer refused the 1h booking while the mat was not placed (money unchanged); placing consumed 1 mat, `IsPlaced` true, the `Mat` prop appeared; a second mat could not be placed (kept in the bag, message 'đã được trải rồi'); after placing, a 1h trainer session ran (Chạy máy -> Chống đẩy -> Đấm bao cát, push-up animation seen), money -150, hours 1, damage 10 -> 30.

[NOT VERIFIED]: real F key and clicking the shop buttons with the mouse; how the shop window looks with 5 rows (row height / scroll), the hotbar icons, the mat prop and the `Nhấn F ...` prompts on screen; the mat prompt itself (after the trainer window was opened in the test it was suppressed on purpose; the same code path is verified for the fertilizer prompt); Vietnamese diacritics in the TMP font; buying through `NPC_MarketVillager` interaction (ShopUI was opened by code); a real ticket drawing with Random (only the table and one real draw were checked).

Known gaps / decisions for the owner: (1) the fertilizer has NO real gameplay effect yet - it is only consumed with a water effect and a message; the garden quest still uses its own sack (`QuestProteinBag`). Tell me what it should do (e.g. count for the garden quest, boost something). (2) the mat is consumed when placed, but the shop keeps selling it: a second mat is wasted (the shop could hide it once `IsPlaced`). (3) Bình tưới and Thức ăn tăng lực still have no effect. (4) the lottery table and prices are first guesses.

MANUAL TEST LIST (needs a human with keyboard and mouse): (1) talk to `NPC_MarketVillager`: window shows 5 goods, icons/prices readable and the window scrolls or fits; (2) with 0 money try to buy each good: red message, money unchanged; (3) with enough money buy mat, ticket, fertilizer: money drops by exactly the price once even when double-clicking; (4) fill the 5 hotbar slots and try to buy a good that needs a free slot: refused, money unchanged; (5) select the ticket with keys 1-5 / wheel, press F several times: one ticket per press, messages, money; (6) walk to a vegetable plot, select the fertilizer: prompt `Nhấn F ...` appears only near a plot, F uses one; (7) walk to the push-up place in the training area (the green mat is NOT visible at first), select the mat, press F: mat appears; try pressing F again with a 2nd mat: nothing; (8) talk to the trainer: booking 1 hour works only after the mat is placed, with the chicken doing run, push-up, boxing; (9) E on the trainer / NPCs and F near a plot do not trigger each other; (10) leave Play Mode and re-enter: nothing is saved (no save system), the mat must be bought and placed again.
Git status could not be run from Unity MCP; nothing committed or pushed.

---

## 52. Pet model swap: Chicken Warrior / ROCKY / jacketBlocky (2026-10-24, NOT committed)

**Ánh xạ NPC → model pet (ghi TRƯỚC khi kiểm tra tiếp; không tạo NPC/pet mới, chỉ đổi model của các rooster đang có):**

| NPC | Rooster trong scene | Vai trò | Model mới | Controller |
|---|---|---|---|---|
| NPC_ChildhoodFriend (bạn thân) | `GuguGaga` (BattleManager.enemy, rematch + final boss) | trận tái đấu, boss cuối | **Chicken Warrior** (`Chicken Warrior with motions.fbx`) | `Assets/Animations/ChickenWarrior/ChickenWarrior.controller` |
| NPC_ChiBa (đối thủ 1, yếu hơn: 130 HP / 14 dmg) | `Rooster_ChiBa` | giải đấu, trận đầu | **jacketBlocky** (`jacketBlocky.fbx`, có rig, 0 clip) | `FunnyChicken/Chicken.controller` + chuyển động thủ tục |
| NPC_ChuTu (đối thủ 2, mạnh hơn: 190 HP / 18 dmg) | `Rooster_ChuTu` | giải đấu, trận sau | **ROCKY** (`ROCKY.fbx`, mesh tĩnh, không rig, 0 clip) | `FunnyChicken/Chicken.controller` + chuyển động thủ tục |

Lý do phân công: ROCKY to và nặng nên cho đối thủ mạnh nhất (Chú Tư); jacketBlocky nhỏ hơn cho Chị Ba. Giữ nguyên: `ChienKe_Pet` (gà người chơi, `normal_chicken`), asset gà cũ (`GuguGaga.prefab`, `GuguGaga.blend`, `FunnyChicken/Chicken.controller` vẫn được tham chiếu nên KHÔNG xóa).

**Hiện trạng tìm thấy:** việc đổi model đã được làm sẵn trong editor (chưa lưu ra đĩa; scene đang dirty) bởi session trước: `ChickenWarrior.controller` (7 tham số, state Idle/Run/GetHit/Dead/WakeUp/Attack1-6, tag `Attack`, event `OnAttackHit`), `OpponentModelMotion.cs`. Tôi chỉ kiểm tra và lưu scene, không làm lại.

**Animation đã cấu hình (đọc từ controller thật, không đoán):**
- Chicken Warrior: rig Generic có Avatar riêng và 35 clip `RPG_Armed_*` ngay trong FBX, nên dùng clip của chính model (không retarget). `ChickenWarrior.controller`: Idle=`RPG_Armed_Idle`, Run=`RPG_Armed_Run` (speed 0.86), GetHit=`GetHit_F1`, Dead=`Death1`, WakeUp=`Death1` chạy ngược (-1.5), Attack1-3/6=`Attack_Dual1-3/1`, Attack4/5=`Kick_L1/R1`. Mọi state tấn công có tag `Attack`; mỗi clip tấn công có event `OnAttackHit` (0.46s / 0.26s). Tham số khớp `RoosterCombatAI` (isRunning, Attack, AttackIndex 1-6, isDead, WakeUp) và `OpponentModelMotion` (GetHit). Logic combat không đổi.
- jacketBlocky (có xương, 0 clip) và ROCKY (mesh tĩnh, 0 clip): KHÔNG có animation riêng và không tương thích rig gà cũ, nên không retarget. Dùng `FunnyChicken/Chicken.controller` chỉ để giữ timing combat (tag `Attack`, event), còn hình ảnh do `OpponentModelMotion` (procedural, chỉ đọc HP/Animator): thở, nảy khi chạy, lao tới khi đánh, giật khi trúng đòn, ngã ngửa khi chết. **Hạn chế:** không có clip đánh/trúng đòn/chết thật cho 2 model này. **Asset còn thiếu nếu muốn đẹp hơn:** clip idle/run/attack/hit/death cho jacketBlocky (có rig) và một bản rig+clip cho ROCKY.

**File/asset liên quan:** `Assets/Animations/ChickenWarrior/ChickenWarrior.controller`, `Assets/Script/Pet/OpponentModelMotion.cs`, scene `MainGame.unity` (GuguGaga, Rooster_ChiBa, Rooster_ChuTu; tôi đã LƯU scene vì đổi model chỉ mới nằm trong editor). Không đổi công thức damage/HP/thưởng/quest. Tôi không sửa script nào trong lần này.

**Kích thước/vị trí (đo ở Play Mode):** cao khoảng 2.2-2.3 m, khớp BoxCollider (2.3 m); gốc y 0.06-0.10; chân/ngón của Chicken Warrior cách gốc ~0.1-0.15 m (đứng trên đất); jacketBlocky/ROCKY minY ≈ 0.07-0.08; hướng nhìn: sau trận hướng về gà người chơi (dot 0.99). Chưa nhìn bằng mắt trong Game view (ảnh chụp quá lớn để đọc).

**Test (Play Mode, đi qua code: `StartBattle`/`StartChallenge`/`StartFinalBattle` thật + bước frame; KHÔNG phím/chuột thật; để rút ngắn, HP đối thủ/gà được đặt thấp trước khi đánh):**
- GuguGaga (Chicken Warrior) thắng: Idle→Run→Attack_Dual1→GetHit_F1→Kick_R1→Death1, thắng sau ~270 frame. Thua: gà chết, trận kết thúc. Sau trận chết, `Revive`+`ResumeAfterRevive` đứng dậy lại được.
- Boss cuối (330 HP / 34 dmg): thua → gà hồi, thông số trả về 150/18, quest vẫn Active; thắng → `phuc_thu_cuoi_cung` Completed, `FinalBossDefeated` true, thông số trả về.
- Chị Ba (jacketBlocky): thua → không trả thưởng, gà hồi, đối thủ hồi đầy và về chỗ cũ; thắng → +200 tiền, +1 huy chương, ngã (pitch -88), mở Chú Tư.
- Chú Tư (ROCKY): thắng → +350, huy chương 2, quest `giai_dau_lang` Completed, ngã (pitch -78). Trận THUA của Chú Tư chưa test riêng (cùng code với Chị Ba).
- BattleManager 1, không NPC/pet mới; BattleManager.enemy trả về GuguGaga sau mỗi trận giải đấu. Console: 0 lỗi do thay đổi này; còn cảnh báo cũ của scene: `Missing Prefab Asset: Superhero_Female_FullBody (1)` (không thuộc phần này, cần xử lý riêng).

**CHƯA kiểm tra:** nhìn bằng mắt trong Game view (kích thước, hướng, chân chạm đất, màu/texture, ROCKY/jacketBlocky khi chuyển động), phím/chuột thật, trận đánh thật không chỉnh HP, âm thanh/particle trên model mới, độ lớn của Chicken Warrior so với gà người chơi, tính cân bằng khi Chị Ba/Chú Tư có model khác.

**Test thủ công cần làm:**
1. Đến nhà bạn thân, xem GuguGaga (Chicken Warrior): đứng đúng chỗ, chân chạm đất, quay mặt đúng hướng.
2. Đánh trận tái đấu: thấy animation đánh, trúng đòn, ngã; thử cả thắng và thua.
3. Giải đấu: nói chuyện Chị Ba và Chú Tư, thách đấu; xem jacketBlocky/ROCKY lao tới, giật khi trúng, ngã khi hạ gục; thử cả thắng lẫn thua.
4. Đủ 2 huy chương, đánh boss cuối; xem Chicken Warrior đứng dậy từ trạng thái chết và ending.
5. Kiểm tra va chạm: gà người chơi không xuyên qua model, collider khớp hình.
6. Quyết định: giữ procedural cho ROCKY/jacketBlocky hay bổ sung clip.

Không commit/push Git.

## 53. NPC animation re-check: ChildhoodFriend, RewardKeeper, CoLan (2026-10-24, NOT committed)
Task: check `NPC_ChildhoodFriend` (GTA SA model rigged with Mixamo), `NPC_RewardKeeper`, `NPC_CoLan` against `Assets/Art/Character/NPC/UAL1_Standard.fbx`; give idle/walk to any NPC that lacks them; check what the friend needs for the current gameplay. Tags: [VERIFIED-EDIT] = read/changed in Edit Mode; [VERIFIED-PLAY] = Play Mode, real frames plus manual `Animator.Update` stepping; [NOT VERIFIED] = not looked at.
The Editor was in Play Mode when this started; I exited it (runtime state only, nothing saved was affected).

### A. Result per NPC (state found BEFORE my change -> state now)
| NPC | Model (FBX) | Found | Now |
|---|---|---|---|
| `NPC_ChildhoodFriend` (prefab `Assets/Prefabs/NPC/NPC_ChildhoodFriend 1.prefab`) | `GTA SA Carl Johnson.fbx` (`Assets/Art/Character/OngBaBanThan/`), SkinnedMeshRenderer, 34 bones, root bone `mixamorig:Hips`. Humanoid, `GTA SA Carl JohnsonAvatar` valid (34 human bones mapped, no middle/ring/little fingers, no missing required bone) | ALREADY correct: Animator on the model child, controller `NPC_UAL`, own avatar, root motion off, `CullUpdateTransforms`, plus `NPCTalkAnimator` on the same object | unchanged |
| `NPC_CoLan` (inside `NPCs_FromVillageMap`, model `kaya`) | `kaya.fbx`, Humanoid, `kayaAvatar` valid (54 bones) | ALREADY correct (done in section 51), scene instance connected to the prefab | unchanged |
| `NPC_RewardKeeper` (plain scene object, not a prefab) | `Ch39_nonPBR.fbx`, 52-bone Mixamo skinned mesh. Importer was Generic / NoAvatar | MISSING: no Animator, no avatar | FIXED (see C) |
`UAL1_Standard.fbx`: Humanoid, `UAL1_StandardAvatar` valid, 43 clips; `Idle_Loop`, `Walk_Loop`, `Idle_Talking_Loop` all have Loop Time on. `Assets/Animations/NPC/NPC_UAL.controller` (the ONE shared controller): Idle (default) / Walk / Talk, bool params `isWalking`, `isTalking`, no exit time, 0.15 s. Only one controller with that name exists; each of the 3 NPCs exists once in the scene. No NPC and no controller was created.

### B. Corrections to section 51 (it is outdated, the project is ahead of it)
- 51-A/E said the friend is a static Carl Johnson mesh with no animation. Now he is rigged and animated (see A). His prefab is `NPC_ChildhoodFriend 1.prefab` (the only friend prefab; it was written 10:19 today).
- 51-E said nothing drives `isTalking`. `Assets/Script/Story/NPCTalkAnimator.cs` (found already present, written 10:18 today, NOT by this task) does: while `DialogueUI` is open, the speaker is `Bạn thân` and the shown line is not the player's, it sets `isTalking`. It is on the friend only.
- 51 said `NPC_RewardKeeper` is a clone of the market villager model. Its model is now `Ch39_nonPBR`.

### C. What I changed (2 things, nothing else)
1. `Assets/Art/Character/NPC/Ch39_nonPBR.fbx` (+ its `.meta`): Animation Type Generic -> Humanoid, Avatar `Create From This Model` -> `Ch39_nonPBRAvatar` (human, valid, 52 bones mapped, 0 required bones missing). Rollback: set it back to Generic / No Avatar. This FBX is used only by `NPC_RewardKeeper` in MainGame (and the unused `Assets/_Recovery/0 (20).unity`).
2. Scene `MainGame` (saved): Unity auto-created an Animator on the FBX model root after step 1 (avatar set, controller empty, Apply Root Motion on). I configured that same Animator on `NPC_RewardKeeper/Ch39_nonPBR`: controller `NPC_UAL`, Apply Root Motion OFF, Culling `CullUpdateTransforms` (same as every other NPC). No second Animator was added.
NOT changed: models, positions/rotations/scales, dialogue, quests, `RewardNPC`, `QuestObjectiveTarget`, `ChildhoodFriendBattleTaunt`, `RematchController`, `FinalBossController`, `VillagerFlavorNPC`, `NPCTalkAnimator`, the controller, any script.

### D. Friend: animations the current gameplay needs
- Idle (default): he stands still while his rooster fights, while the delivery/rematch/boss quests wait, and between dialogues. Present.
- Talk: every `dialogue.Open("Bạn thân", ...)` in `ChildhoodFriendBattleTaunt` (taunt, challenge, after-loss), `RematchController` and `FinalBossController` uses exactly the speaker `Bạn thân`, which is what `NPCTalkAnimator.speakerName` expects, so Talk plays for all of them. The player's own lines (speaker `Tôi`) keep him in Idle. Present.
- Walk: wired in the controller (`Armature|Walk_Loop`) but he never walks: he has no agent, and leaving/returning is done by switching his renderers off/on (`RematchController.UpdateFriendPresence`). Unused, kept for later.
- No win/lose/taunt-reaction animation exists and no script asks for one. UAL has candidates (`Dance_Loop`, `Hit_Chest`, `Death01`, `Punch_Jab`) but nothing triggers them, so none was added.

### E. Tests
[VERIFIED-EDIT] compile: forced refresh + compile request, Console has no compile error (only MCP port messages after the domain reload). Scene saved, then reloaded from disk: all 3 Animators still have controller + avatar.
[VERIFIED-PLAY] for all 3 NPCs (culling set to AlwaysAnimate at RUNTIME only, so off-screen NPCs are evaluated): state `Idle`, clip `Armature|Idle_Loop`, avatar human + valid, Animator initialized; over 60 stepped frames the bones really move (head 3.9, upper arm 4.9, upper leg 4.5 degrees, identical for the 3 NPCs = retarget works). `isWalking` true -> state `Walk`, clip `Armature|Walk_Loop`, model and root drift 0.0000 (no root motion); false -> back to `Idle`.
[VERIFIED-PLAY] friend Talk through the real path: `DialogueUI.Open("Bạn thân", ...)` -> `NPCTalkAnimator` set `isTalking` -> state `Talk`, clip `Armature|Idle_Talking_Loop`; `Close()` -> `isTalking` false -> `Idle`. Friend root components unchanged (QuestObjectiveTarget, ChildhoodFriendBattleTaunt, RematchController, FinalBossController); `NPC_RewardKeeper` still has Transform + `RewardNPC` (enabled).
Foot heights measured (world y, ground is 0): ankle L/R and toe-base L/R: friend 0.31/0.30 and 0.14/0.14 (root y 0.12), RewardKeeper 0.26/0.28 and 0.08/0.10, CoLan 0.21/0.18 and 0.03/0.00.

### F. Manual checks still needed (Game view, real E key / mouse) [NOT VERIFIED]
1. Look at each of the 3 NPCs once: twisted limbs, hands, feet sinking or floating. The friend's toes sit at about 0.14 and his root is at y 0.12 (higher than the others): check he does not float. Auto-mapping was not checked bone by bone.
2. Talk to the friend with the real E key: Talk should start with his lines, return to Idle on the player's answer `Tôi: ...` and when the dialogue closes.
3. Friend vanish/return (rematch won -> away -> 2 medals): after he reappears he should be animating (his renderers are toggled, and `CullUpdateTransforms` freezes the pose while hidden). He also has a static tire mesh (`покрышка (1)`) under the Head bone.
4. `NPC_RewardKeeper` and `NPC_CoLan` only play Idle: nothing drives `isTalking`/`isWalking` for them (`NPCTalkAnimator` is on the friend only). Giving the Reward Keeper a Talk gesture would be one `NPCTalkAnimator` with the speaker name used by `RewardNPC`; not done (not requested).
5. Off-screen NPCs do not animate by design (culling); this is not a bug.

### G. Known issues, not touched
- `Assets/Prefabs/NPC/NPCs_FromVillageMap.prefab` still has a missing nested prefab `Superhero_Female_FullBody (1)` (guid f63617f13ea6fe945939bb43897696c3): the Console error `Problem detected while opening the Scene file` repeats on every scene open. Separate small task.
- After leaving Play Mode the scene is marked dirty by `Driving RectTransform` (UI layout); harmless, I reloaded the scene from disk instead of saving it.
- Git: nothing was committed or pushed. For `git status`: `Ch39_nonPBR.fbx.meta`, `Assets/Scenes/MainGame.unity`, this file (plus earlier uncommitted work such as `NPCTalkAnimator.cs` and `NPC_ChildhoodFriend 1.prefab`).
