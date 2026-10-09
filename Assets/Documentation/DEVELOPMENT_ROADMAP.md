# DEVELOPMENT_ROADMAP.md

## 0. Rules

Priority: 1. Playable gameplay 2. Story flow 3. Core systems 4.
Vietnamese identity 5. UI 6. Audio 7. Polish 8. Optional features

AI workflow:

``` text
Inspect → plan small change → implement → compile → test → fix blocking issues → report → stop
```

Do not auto-commit or auto-push.

## 1. Current Baseline

According to the live project handoff: - Player movement/camera:
present - Chicken combat: present - HealthSystem: present - Health bars:
present - Chicken wake-up/acquire: present -
BattleManager/BattleTrigger: present - First-battle loss balance:
present - Quest: not started - Scrap: not started - Money: not started -
Shop: not started - Training progression: not started - Medal/Reward:
not started - Intro/story dialogue/NPCs/final boss/ending: not
implemented

Before new systems, manually test:

``` text
E → wake → follow → enter battle → chicken attacks → first battle loses
```

Known technical issues to verify include pet stoppingDistance currently
being 10 while attackRange is 1, lack of real keyboard/mouse testing,
and unverified wake-up/push-up visual quality.

## 2. Milestone 0 --- Stabilize Prototype

Tasks: - Manual keyboard/mouse playthrough. - Verify E wake-up. - Verify
follow. - Verify battle starts. - Verify chicken actually attacks. -
Verify natural first loss. - Fix only blocking bugs.

Exit criterion: The first battle works from beginning to end in Play
Mode.

## 3. Milestone 1 --- Documentation

Maintain:

``` text
Assets/Documentation/
├── PROJECT_STATUS.md
├── GAME_DESIGN.md
└── DEVELOPMENT_ROADMAP.md
```

Update PROJECT_STATUS after major milestones.

## 4. Milestone 2 --- Intro + Grandparents

Build: - 30--60 second intro/cinematic. - Quest: Về nhà thăm ông bà. -
Grandpa. - Grandma. - Simple interaction/dialogue. - Special chicken
discovery. - Quest: Nhận nuôi chú gà.

Exit: Player understands why the protagonist returned and receives the
chicken.

## 5. Milestone 3 --- Childhood Friend + First Battle

Build: - Childhood friend NPC. - Simple dialogue. - Quest: Thử sức với
bạn cũ. - First battle trigger. - Existing intentional-loss balance.

Exit:

``` text
Grandparents → chicken → friend → challenge → battle → loss → continue
```

Do not rewrite combat.

## 6. Milestone 4 --- Minimal Quest System

Support: - ID - title - description - objective - progress -
completion - reward

Initial quests: 1. Về nhà thăm ông bà 2. Nhận nuôi chú gà 3. Thử sức với
bạn cũ 4. Kiếm tiền mua đồ tập

Do not build a large RPG quest framework.

## 7. Milestone 5 --- Scrap Collection

Implement: - Bottle - Can - Cardboard - Scrap metal

Interaction:

``` text
E → collect → increase count → prevent duplicate
```

First objective: about 10 scrap.

Exit: Player can collect all required objects and complete the quest.

## 8. Milestone 6 --- Money

Implement simple currency.

Example values: - Bottle 5 - Can 10 - Cardboard 8 - Metal 15

UI:

``` text
Money: 50
Scrap: 7/10
```

No complex economy.

## 9. Milestone 7 --- Shop

Initial shop items: - Push-up mat - Dumbbell/barbell - Punching bag -
Treadmill

Flow:

``` text
Earn money → shop → buy training item → item usable
```

## 10. Milestone 8 --- Training

Priority: 1. Push-up 2. Weightlifting 3. Treadmill 4. Boxing

Start with only the activities that can be made reliable.

Training should visibly improve simple stats, preferably HP and/or
damage.

## 11. Milestone 9 --- Rematch

Quest: Phục thù.

Flow:

``` text
Train → return to friend → rematch → win → Medal
```

## 12. Milestone 10 --- Medal + Reward NPC

Flow:

``` text
Win → Medal → Reward NPC → exchange → reward
```

No betting/gambling.

## 13. Milestone 11 --- Additional Opponents

Add only about 2--4 meaningful opponents.

Each needs: - name - chicken - difficulty - short introduction -
battle - reward

## 14. Milestone 12 --- Additional Quests

Optional examples: - Find chicken food - Deliver training equipment -
Clean training area - Test training equipment - Help village NPC -
Challenge next opponent

Only keep quests that strengthen the demo.

## 15. Milestone 13 --- Vietnamese Environment Pass

Priority: - Vietnamese house - market/shop - bamboo fence - nón lá -
hammock - Vietnamese altar - plastic chairs - electric poles -
lanterns - Vietnamese signs - rice fields - bamboo/banana/coconut -
ponds/irrigation - rural dirt roads

Do not replace the whole environment just for polish.

## 16. Milestone 14 --- Rival Continuity

Keep the childhood friend visible in the story after the first
encounter.

He should: - defeat the player initially - react to progress - appear
during the journey - remain competitive - return as final boss

## 17. Milestone 15 --- Final Boss

Quest: Trận đấu cuối cùng.

Flow:

``` text
Major challenges complete
→ return to village
→ friend appears
→ final dialogue
→ final battle
→ victory
```

Reuse the existing combat architecture.

## 18. Milestone 16 --- Ending

Short ending: - friend acknowledges growth - chicken celebration -
return to grandparents - humorous final scene - credits

## 19. Milestone 17 --- Audio

Add: - rural ambience - rooster/chicken - birds - dogs - insects -
attack/hit/death - pickup - quest complete - money/shop/training/medal -
UI clicks - intro/village/training/battle/victory/final/ending music

## 20. Milestone 18 --- UI

Minimum: - Quest - Objective - Money - Scrap - Chicken HP/stats - \[E\]
interaction - Shop - Training - Medal

## 21. Milestone 19 --- Final Cleanup

Check: - Console errors - Missing scripts/references - Materials -
Animations - Duplicate objects - Large unnecessary assets - Texture
sizes - NavMesh - Colliders - Audio - UI scaling - Scene flow

Do not delete suspicious assets automatically.

## 22. Milestone 20 --- Final Playthrough

Required:

``` text
INTRO
→ VỀ NHÀ
→ ÔNG BÀ
→ NHẬN GÀ
→ GẶP BẠN CŨ
→ FIRST BATTLE
→ LOSE
→ SCRAP QUEST
→ MONEY
→ SHOP
→ TRAINING
→ REMATCH
→ WIN
→ MEDAL
→ REWARD NPC
→ OTHER CHALLENGES
→ FINAL BOSS
→ ENDING
```

Every step must work without editor intervention.

## 23. Asset Acquisition Order

Do not download everything at once.

### A --- Story

-   Grandpa/Grandma base
-   Childhood Friend
-   NPC animation
-   Vietnamese house
-   Vietnamese props
-   village base

### B --- Economy

-   bottle
-   can
-   cardboard
-   scrap metal
-   boxes/trash

### C --- Training

-   dumbbell
-   barbell
-   punching bag
-   treadmill
-   simple training props

### D --- Audio

-   rural ambience
-   chicken/rooster
-   birds/dogs/insects
-   UI/impact SFX
-   Vietnamese-inspired music

### E --- Polish

-   extra props
-   clothing
-   VFX
-   decoration

For every new asset, verify license and avoid unnecessary large packs.

## 24. Suggested Commit Units

``` text
feat: stabilize first battle flow
feat: add intro quest foundation
feat: add childhood friend NPC
feat: add quest system
feat: add scrap collection
feat: add currency
feat: add shop
feat: add chicken training
feat: add rematch and medal
feat: add reward NPC
feat: add additional opponents
feat: add final boss
feat: add ending
polish: improve vietnamese environment
polish: add audio and vfx
fix: resolve gameplay issues
```

## 25. Definition of Done

A milestone is done only when: - Code compiles. - No new project
errors. - Feature works in Play Mode. - Existing gameplay still works. -
No unrelated systems were modified. - No unnecessary assets/packages
were added. - PROJECT_STATUS is updated.

AI must not call a feature complete merely because code exists.

## 26. Target Final Scope

Approximate target: - 1 player - 1 main chicken - 1 childhood
friend/final boss - 2--4 additional opponents - 2 grandparents - 1 shop
NPC - 1 reward NPC - several village NPCs - 1 main countryside hub -
grandparents' house - training area - shop - 1--2 battle areas - 6--10
main quests - up to 4 training activities - simple economy -
medal/reward system - final boss - ending

## 27. Emergency Scope Cut

Cut first: - extra NPCs - cosmetics - extra training activities -
optional side quests - extra environments - extra bosses

Keep: - intro - grandparents - main chicken - childhood friend - first
loss - scrap quest - money - at least one training activity - rematch -
Medal - Reward NPC - final boss - ending

## 28. Final Rule

Prefer:

> Fewer features that work from start to finish

over:

> More features that remain unfinished.
