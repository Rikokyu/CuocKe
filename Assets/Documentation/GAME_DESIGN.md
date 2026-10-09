# GAME_DESIGN.md

## 1. Game Overview

A humorous Vietnamese-style 3D first-person game about an IT worker
returning to the countryside, discovering a special chicken at his
grandparents' home, raising and training it, competing in fictional
chicken sport battles, and eventually facing his childhood friend as the
final boss.

The game is a fictional competitive sport. It has **no betting or
gambling mechanics**. Winning a battle gives a Medal that is later
exchanged with a separate Reward NPC.

## 2. Story Spine

``` text
IT worker under work pressure
        ↓
Returns to hometown
        ↓
Quest: Về nhà thăm ông bà
        ↓
Grandparents' house
        ↓
Discovers strange/special chicken
        ↓
Grandfather considers preparing it as food
        ↓
Player asks to keep the chicken
        ↓
Chicken acquisition
        ↓
Meets childhood friend
        ↓
First battle
        ↓
Player loses
        ↓
Scrap / money quest
        ↓
Shop
        ↓
Training
        ↓
Rematch
        ↓
Win → Medal
        ↓
Reward NPC
        ↓
Additional challenges
        ↓
Childhood friend returns
        ↓
Final battle
        ↓
Ending
```

## 3. Intro

The game starts with a 30--60 second cinematic: - IT work -
Coding/errors/deadline pressure - Tired protagonist - Closes laptop -
Packs bag - Returns to the countryside

First quest: **Về nhà thăm ông bà**.

The intro should be simple enough to build with Unity
Timeline/Cinemachine and existing/free assets.

## 4. Grandparents and Chicken

At the grandparents' house: 1. Meet Grandpa/Grandma. 2. Notice a strange
chicken. 3. Grandpa comments that it is unusual. 4. Grandpa considers
preparing it as food. 5. Player asks to keep it. 6. Player receives the
chicken.

Chicken tutorial:

``` text
Chicken lying down
→ Press E
→ WakeUp
→ ChickenIdle
→ MainChicken
→ Follow Player
```

The current technical project already has this intended acquisition
flow; visual/manual verification remains important.

## 5. Childhood Friend

The childhood friend is: - First serious opponent - Recurring rival -
Motivation for the protagonist - Final boss

He should appear early and remain relevant.

First encounter: - Friend recognizes the protagonist. - Notices the
chicken. - Teases that it looks weak. - Challenges the player.

Quest: **Thử sức với bạn cũ**.

## 6. First Battle

The first battle is intentionally difficult and should end in a natural
loss.

Current first-battle design: - Player chicken damage: 1 - Enemy damage:
15 - Balance override belongs in BattleManager and must be restored
afterward.

Do not fake the loss, instantly kill the chicken, destroy it, or show
Game Over.

After loss, the story continues.

## 7. Quest System

Keep the quest system lightweight: - ID - Title - Description -
Objective - Progress - Completion - Reward

Initial quests: 1. Về nhà thăm ông bà 2. Nhận nuôi chú gà 3. Thử sức với
bạn cũ 4. Kiếm tiền mua đồ tập 5. Tập luyện 6. Phục thù

Avoid an over-engineered RPG quest framework.

## 8. Scrap Collection

Initial scrap types: - Plastic bottle - Aluminum can - Cardboard - Scrap
metal

Optional: - Tire - Broken wood - Old box

Interaction:

``` text
Approach → E → collect → increase count → prevent duplicate collection
```

First economy quest: collect about 10 scrap objects.

## 9. Money

Example early values: - Bottle: 5 - Can: 10 - Cardboard: 8 - Scrap
metal: 15

These are tunable examples, not immutable requirements.

Money is mainly for training/shop progression. Do not create a complex
economy.

## 10. Training

Priority training: 1. Push-up 2. Weightlifting 3. Treadmill 4. Boxing

Optional: - Tire pulling - Obstacle course - Other humorous training

The current project already contains a push-up code/Animator prototype,
so reuse it rather than rebuilding it.

Recommended simple progression stats: - HP - Damage

Optional later: - Attack speed - Defense - Stamina

## 11. Shop

The shop should initially sell only items needed for training: - Push-up
mat - Dumbbell/barbell - Punching bag - Treadmill

Do not build a huge RPG inventory/equipment system unless necessary.

## 12. Rematch

Quest: **Phục thù**

``` text
Train → return to childhood friend → rematch → win → Medal
```

The second fight should be genuinely winnable.

## 13. Medal / Reward

``` text
Battle Win
→ Medal
→ Reward NPC
→ Exchange Medal
→ Reward
```

No betting or wagered money.

## 14. Mid Game

Use a small number of meaningful opponents, around 2--4 additional
opponents.

Possible side quests: - Find food for chicken - Deliver training
equipment - Clean the training area - Test training equipment - Help a
village NPC - Challenge the next opponent

Do not build dozens of quests.

## 15. Final Boss

The childhood friend returns as the final boss.

Final quest: **Trận đấu cuối cùng**.

The final battle should feel like the payoff of the entire journey, not
just another battle.

## 16. Ending

Keep it short: - Friend acknowledges the player's growth. - Chicken
celebrates. - Return to grandparents/village. - Short humorous ending. -
Credits.

## 17. Vietnamese Visual Identity

Vietnamese identity should come from many small details rather than only
a flag.

### Architecture

-   Vietnamese countryside house
-   Grandparents' house
-   Small shop
-   Market stalls
-   Bamboo fences

### Props

-   Nón lá
-   Vietnamese altar
-   Hammock
-   Plastic chairs
-   Electric poles
-   Clothesline
-   Lanterns
-   Vietnamese signs
-   Selected Vietnamese flags

### Nature

-   Rice fields
-   Bamboo
-   Banana/coconut trees
-   Tropical vegetation
-   Dirt roads
-   Ponds
-   Irrigation channels
-   Village grass

### Audio

-   Rooster/chicken
-   Birds
-   Dogs
-   Crickets/cicadas
-   Rural ambience
-   Motorbike/bicycle sounds
-   Vietnamese-inspired/traditional music with clear licensing

The target is a stylized Vietnamese countryside, not a generic fantasy
village.

## 18. NPC Strategy

Use reusable humanoid bases plus clothing/accessories instead of a
unique model for every NPC.

Core NPCs: - Grandpa - Grandma - Childhood Friend - Shop NPC - Reward
NPC - Village NPCs - Opponent NPCs

## 19. Asset Policy

Preferred: 1. Quaternius 2. Kenney 3. Poly Haven 4. CC0 itch.io assets
5. OpenGameArt when license is clear 6. Pixabay for suitable audio 7.
Sketchfab only when each asset's license is verified

Prefer CC0/Public Domain. CC BY is acceptable only when attribution is
recorded.

Avoid: - Paid assets - Ripped assets - Unclear licenses - Huge
unnecessary packs - High-poly scans - 8K textures without a real need

## 20. Technical Principles

-   Keep systems modular.
-   Avoid giant scripts.
-   Avoid unnecessary dependencies.
-   Quest must not depend on exact scene object paths.
-   Do not rewrite stable combat.
-   Reuse existing systems.
-   Keep milestones small and testable.
-   Do not expand scope without approval.

## 21. Stable Existing Systems

Current project already has: - Player movement/camera - Chicken combat -
HealthSystem - Health bars - Chicken acquisition/wake-up -
BattleManager - BattleTrigger - First-battle loss balance

Treat these as frozen unless a direct blocking bug is found.

## 22. Definition of Successful Demo

The player can: 1. Watch intro. 2. Return to grandparents. 3. Meet
grandparents. 4. Receive chicken. 5. Walk with chicken. 6. Meet
childhood friend. 7. Lose first battle. 8. Collect scrap. 9. Earn money.
10. Buy/use training. 11. Train chicken. 12. Rematch. 13. Win. 14.
Receive Medal. 15. Exchange Medal at Reward NPC. 16. Complete several
challenges. 17. Fight childhood friend as final boss. 18. See ending.

A smaller polished version is preferable to a larger unfinished game.
