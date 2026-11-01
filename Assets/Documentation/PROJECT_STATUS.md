# PROJECT_STATUS.md - Trạng thái hiện tại của dự án

Viết lại gọn ngày 2026-10-28. Lịch sử dài (nhật ký từng lượt làm, 2026-10-08 -> 2026-10-28) đã chuyển nguyên văn sang `PROJECT_HISTORY.md`. File này chỉ giữ TRẠNG THÁI HIỆN TẠI. Nếu file này và project mâu thuẫn, tin project rồi sửa file này.

Thiết kế game: `GAME_DESIGN.md`. Kế hoạch / việc còn lại: `DEVELOPMENT_ROADMAP.md` (kế hoạch gốc 13/10 nguyên văn: `DEVELOPMENT_ROADMAP_ARCHIVE_2026-10-13.md`).

Thẻ: [VERIFIED-PLAY] = chạy trong Play Mode bằng cách gọi đúng code path (Interact(), onClick của nút, BattleManager...) NHƯNG KHÔNG dùng phím/chuột thật. [MANUAL-PENDING] = cần chủ dự án chơi tay. [NOT VERIFIED] = chưa kiểm tra.

---

## 1. Tổng quan
- Unity 6000.4.1f1, URP, scene chính `Assets/Scenes/MainGame.unity`. Dự án sinh viên, hạn tháng 11/2026. Hai dev làm song song, chủ dự án ít lượt AI nên làm có mục tiêu, đừng audit lại từ đầu.
- Game 3D góc nhìn thứ nhất, hài hước kiểu làng quê Việt Nam: lập trình viên IT về quê, cứu một con gà, thua bạn thân thuở nhỏ, gà biến hình, luyện gà, thắng giải đấu NPC, đánh boss cuối (bạn thân) rồi kết thúc.
- KHÔNG cá cược/cờ bạc: thắng nhận huy chương + phần thưởng cố định.
- Làm việc qua Unity MCP. Claude không có Git; KHÔNG commit/push trừ khi chủ dự án nói. Mọi thay đổi hiện đều chưa commit (nhánh `feature/Character-Pet-Enemy`, commit cuối `fix character combat` 2026-10-09; `git status` không chạy được từ MCP nên không biết chính xác file nào đang pending).
- Chưa có lưu/tải: tiền, chỉ số gà, vật phẩm, cờ cốt truyện đều reset khi tắt Play.

## 2. Luồng cốt truyện hiện tại (đã cài đặt, thứ tự đúng)
1. Vào game: Player xuất hiện ở cổng làng (10.5, 0.1, -9). `IntroScreenController` (bấm Enter hoặc nút Bắt đầu) -> nhận quest `ve_nha_tham_ong_ba`. Muốn bỏ intro: tắt object này (quest vẫn nhận dự phòng khi nói chuyện với Ông).
2. Nhà ông bà: Ông (-11.1, 0, 12.1) định làm thịt gà -> Player chọn 'Xin ông giữ lại con gà' -> `nhan_nuoi_chu_ga` xong, quest giao hàng `village_delivery` MỞ NGAY (Bà giao việc sau khi hội thoại Ông đóng). Gà tỉnh dậy đi theo (`MainChickenAcquire`).
3. Giao gói hàng cho bạn thân (`NPC_ChildhoodFriend`): lúc chưa giao bạn chỉ đòi gói hàng; giao xong bạn cảm ơn rồi thách đấu cùng một hội thoại, nút 'Nhận lời thách đấu'. Trận đầu chỉ bắt đầu khi Player VÀ gà đều ở trong `BattleArena`. Thua theo kịch bản bằng sát thương thật -> bạn châm chọc 'MÀY SỢ À' + câu đáp của Player -> `thu_suc_voi_ban_cu` Completed.
4. Bà trả công +200 (một lần, chỉ sau khi `thu_suc_voi_ban_cu` Completed).
5. Chăm vườn: Bà `grandparents_helper` (bình tưới, tưới 3 cây, +150) -> Ông giao `garden_protein` (4 bước: hái rau, đặt rau lên đĩa `FeedingSpot`, lấy hũ bột cám - hũ ghi BỘT PROTEIN (gag), gà ăn) -> quest `xin_phep_ngu_trua` (xin Ông; đóng hội thoại giữa chừng thì chưa được) -> giường `StoryBed`: thoại Player 'Ơ cái giường với con mèo này cứ quen quen ấy nhờ' -> fade -> video The Next Day -> thoại 'Ngủ ngon quá. Ra sau kiếm con gà thử'.
6. Gà biến hình (đúng một lần, `ChickenTransformationTrigger`), gà mất (banner), hỏi Bà/Ông -> quest `tim_ga` -> gà ở (-9.5, 0, 53) đang hít đất; nhìn thấy trong tầm -> cảnh camera meme `PlayerMemeShot` (một lần) -> E nhận lại gà -> `tim_ga` xong, `TrainingUnlocked`, mở quest `tai_dau_ban_than`.
7. Tái đấu lần 1 với bạn thân (`RematchController`, quest `tai_dau_ban_than`, nút 'Tái đấu!', trong BattleArena): THUA THEO KỊCH BẢN bằng sát thương thật (`scriptedLoss`, dùng cân bằng trận đầu của `BattleManager`, không sửa HP/damage giả). Gà đứng dậy -> bạn khuyên: 'Đánh bại Chị Ba và Chú Tư rồi quay lại đấu với tao!' -> khi đóng hội thoại: `tai_dau_ban_than` Completed (một lần) -> `TournamentManager` mở `giai_dau_lang` NGAY. Sau đó bạn BIẾN MẤT (`RematchController.FriendAway`) cho đến khi đủ 2 huy chương. Thua kịch bản chỉ xảy ra một lần (`scriptedLossDone`).
8. Giải đấu làng `giai_dau_lang`: Chị Ba (`Rooster_ChiBa`) rồi Chú Tư (`Rooster_ChuTu`, khóa tới khi thắng Chị Ba). Mỗi thắng +1 huy chương + tiền (một lần); Reward NPC (`NPC_RewardKeeper`, (46,0,55)) đổi huy chương. Gà phải ĐÃ BIẾN HÌNH (`StarterChicken.SwitchToPet`) mới chiến đấu được.
8b. Sau giải đấu (Chị Ba + Chú Tư thắng -> `giai_dau_lang` Completed): `ZoroReunionStory` mở `ve_nha_nghi_ngoi` -> Player ngủ ở giường (`StoryBed.TrySleepAgain`, chỉ khi stage Resting) -> gà ẩn đi, banner khi tỉnh -> hỏi Ông/Bà: 'Nó đi theo thằng tóc xanh lá rồi!' / 'Ơ, thằng mù đường mà?' -> nút 'Đi tìm con gà' nhận `tim_ga_zoro` -> gà đứng ở `ZoroWatchSpot` (5,0.08,93), 15 m cách Zoro Tsunami (5,0,108), quay mặt về Zoro -> thấy gà (≤22 m, trong tầm nhìn) banner, lại gần ≤4,5 m thoại tự mở -> đóng thoại: gà đi theo lại, `tim_ga_zoro` Completed, `HurricaneSkill.Unlock()` một lần + hiệu ứng gió. Từ biến hình tới đây Hurricane bị KHÓA (`PetCombatAI.TryPlayHurricane` trả false, đòn ngẫu nhiên không bao giờ có Hurricane).
9. Song song: sau tái đấu lần 1 VÀ Bà đã trả tiền giao hàng: mở `chuan_bi_tai_dau` (Kiếm tiền luyện gà, 5 'buổi tập' = tổng giờ tập) và Medea (`QuestNPC_Villager`) mời `scrap_collector` (+100). Cổng này nằm ở `QuestNPC.requiredCompletedQuest` + `RematchController.UpdateTrainingQuest`; không còn mở giải đấu (giải đã mở ở bước 7). Huấn luyện: `TrainerNPC` (trả trước 1-5 giờ).
10. Đủ 2 huy chương (`FinalBossController.GateOpen`): bạn thân quay lại -> tái đấu CUỐI (nút 'Phục thù!', HP 330, sát thương 34, tạm thời); dưới 2 huy chương `TryInteract` trả false nên bạn chỉ nói lời nhắc tiến độ giải. Thắng: `phuc_thu_cuoi_cung` Completed -> `EndingSequence` (gà ăn mừng, về nhà ông bà, thoại hài, credits ~25 s, nút Thoát). Không có nút chơi lại (cờ static sẽ làm hỏng gating nếu nạp lại scene). Từ bước 8b: cổng còn cần `HurricaneSkill.Unlocked`. Khi gà bạn thân ≤50% HP, `HurricaneFinisher` tự chạy ĐÚNG MỘT LẦN: Hurricane Kick + gió + camera + chữ, cú đá kết liễu qua `HealthSystem.TakeDamage` (cùng đường thắng thường), gà đối thủ bị hất bay ~32 m.
Quest chi tiết/ghi chú từng lượt: xem `PROJECT_HISTORY.md` (chỉ mục ở cuối file này).

## 3. Hệ thống đã có [ĐÃ CÓ, compile sạch, Console 0 lỗi dự án ở lần kiểm 2026-10-28]
- Player (CharacterController + `PlayerMovement`, camera), gà chiến đấu (`PetCombatAI`, `RoosterCombatAI`, `HealthSystem` idempotent death + `OnDeath`), `BattleManager` (trận đầu: pet damage 1 / địch 15 rồi khôi phục 10 / 18), `BattleTrigger`, `BattleArena` ((28.5,0,19.7), bán kính 8.5; mọi trận 1-1 chỉ bắt đầu khi Player và gà ở trong).
- `HealthBarUI`: HAI thanh (cùng prefab `HealthBarUI.prefab`, scene `HealthBarUI` = vai trò Player và `HealthBarUI_Opponent` = vai trò Opponent). Ngoài trận cả hai ẨN; khi `BattleManager.IsBattleActive` cả hai hiện cùng lúc, đọc `HealthSystem.currentHealth/maxHealth` mỗi frame của `mainChickenHealth` và `enemyHealth` (tự theo Chị Ba / Chú Tư / gà bạn thân tái đấu / boss); hết trận ẩn ngay. Chỉ ĐỌC BattleManager/HealthSystem, không sửa combat.
- Quest: `QuestManager` (AcceptQuest/ReportProgress/TurnIn một lần, `TrackedQuest`), `QuestDefinition` (assets `Assets/Data/Quests/`), `QuestNPC` (`requiredCompletedQuest`), `QuestObjectiveTarget`, `QuestCarryItem`, `QuestHUD`, `QuestGuide` (mũi tên 2D, 23 luật).
- Câu chuyện: `StoryBed` (ngủ + video, có fallback màn đen nếu video lỗi), `GardenFlowStory` (`NapAllowed`), `FeedingSpot` (3 trạng thái hiển thị đĩa trơn / đĩa + 3 lá / Sprinkle_Cake hoàn chỉnh), `MissingChickenStory`, `ChickenTransformationTrigger`, `GrandfatherChickenRescue`, `ChildhoodFriendBattleTaunt`, `RematchController`, `FinalBossController`, `EndingSequence`, `TournamentManager` (OpponentDefinition, huy chương), `NPCTalkAnimator`. Mới: `ZoroReunionStory`, `HurricaneSkill`, `HurricaneFinisher` (Assets/Script/Story, Assets/Script/Skills).
- Kinh tế: `CurrencyWallet` (`Add`, `CanAfford`, `TrySpend`) + MoneyHUD; `ShopNPC`/`ShopUI`/`ShopCatalog` tại `NPC_MarketVillager` (27,0,46); `RewardNPC`/`RewardUI`/`MedalRewardCatalog` tại `NPC_RewardKeeper`. Giá hàng và phần thưởng: `GAME_DESIGN.md` mục 4.7-4.8.
- Inventory + Hotbar (`InventorySystem`, `HotbarUI`), `ItemUseController` (phím F dùng vật phẩm đang chọn: vé số, phân bón; thảm tập trải ở khu tập bằng phím E, chỉ khi tâm ngắm không trỏ vào NPC/vật khác; khi đang mang thảm mà chưa trải thì chỗ trải phát âm thanh leng keng kiểu ve chai (bank `matCue` qua `GameAudio`, clip impactTin_medium) - [COMPILED, chưa thử bằng phím/tai thật]).
- Huấn luyện: `ChickenStats` (Strength/Attack/Speed, trần 40; trường `damagePerTrainingHour`), `TrainerNPC` (trả trước 1-5 giờ; mỗi giờ = một vòng chạy máy -> hít đất -> đấm bao cát, 12 s/giờ; giá và số liệu: `GAME_DESIGN.md` 4.7), 3 `TrainingStation` (khu x -15..-1, z 47..57; hít đất cần thảm tập đã trải). Tiến độ `chuan_bi_tai_dau` = min(tổng giờ tập, 5).
- Thu thập: Scrap (12 món + hiệu ứng lấp lánh), `ScrapCollector`.
- Âm thanh: `GameAudio` (một manager; nhạc nền theo giai đoạn, ambience, giọng thoại, SFX, chống spam). Chưa ai nghe thử.
- Hội thoại: `DialogueUI` (typewriter, bỏ qua, speaker Player 'Tôi' khi dòng có tiền tố '[Player] ', giọng nói lặp khi gõ chữ). Hiệu ứng hạt nhẹ (đánh trúng, tưới cây, bao cát).
- NPC: model + animation dùng chung `Assets/Animations/NPC/NPC_UAL.controller` (clip UAL), gà `Rooster_ChiBa` (jacketBlocky, clip đã bake `Assets/Animations/JacketBlocky/`), gà bạn thân GuguGaga = Chicken Warrior, gà người chơi `ChienKe_Pet` (đã đổi model, `Normal_Chicken.controller`), bạn thân = model CJ (Mixamo, Humanoid).

## 4. Scene `MainGame` (tóm tắt)
- Gốc chính: Directional Light, Global Volume, `Terrain_Village`, Player (tag `Player`), `ChienKe_Pet` (tag `Pet`), `GuguGaga` (tag `Rooster`, gà bạn thân), `Rooster_ChiBa`, `Rooster_ChuTu`, NPC (Ông, Bà, bạn thân, dân chợ/Medea, Huấn luyện viên (-7.5,0,48.3), RewardKeeper...), `QuestUI_Canvas` (QuestHUD, QuestGuide, ShopUI, RewardUI), `BattleManager`, `BattleArena`, `GameAudio`, `ItemUseController`, `EndingSequence`, `TrainingStations`, `SpawnPoints`, hai `HealthBarUI` (Player + Opponent). Mới (2026-10-30): `ZoroReunionStory`, `HurricaneSkill`, `HurricaneFinisher`, `ZoroWatchSpot`.
- Prefab: `Assets/Prefabs/` (Model/, NPC/, Temp/, featureCharacter/). Dữ liệu: `Assets/Data/` (Quests, Shop, Rewards, Items).
- Object tắt/cũ (KHÔNG xóa nếu chưa hỏi chủ dự án): `Rooster`, `ChienKe_blend`, `OLD_Terrain_Tiles_DISABLED`, `_Recovery/0 (23).unity` (đã chuyển sang `Assets/Delete/_Recovery/` ngày 2026-10-31; prefab `HealthBarUI (1)` trong Prefabs chỉ scene này dùng), `FollowPlayer.cs`, `ChickenPushUp.controller`.
- Scene lưu dạng binary: kiểm tra bằng SerializedObject / quét component, không grep text được.

## 5. Ràng buộc của chủ dự án (MUST-NOT-CHANGE)
- Không thay/viết lại hệ combat (PetCombatAI, RoosterCombatAI, HealthSystem) trừ khi có lỗi chặn thật; sửa nhỏ nhất và báo cáo.
- Không đổi sát thương toàn cục; cân bằng trận đầu chỉ nằm trong BattleManager (override rồi khôi phục).
- Không ép thua bằng HP giả / chết tức thì / Destroy / tắt tấn công: thua phải đến từ sát thương thật. Không hủy gà chính, không Game Over, không về menu sau trận thua đầu.
- Không cá cược. Không đổi Unity version, render pipeline (cấu trúc thư mục đã được chủ dự án cho phép sắp xếp lại ngày 2026-10-31, xem mục 14); không cài package, không tải asset.
- Không xóa file chỉ vì trông như không dùng. Giữ tên Animator parameter (isDead, isRunning, Attack, AttackIndex, IsPushing, WakeUp) và tag state `Attack`.
- Không đụng hệ của đồng đội (inventory, interaction) nếu chưa kiểm tra. Dùng lại hệ thống sẵn có: không tạo manager / quest / NPC trùng.
- Sửa script qua MCP: `create_script` không ghi đè file đang có; KHÔNG xóa + tạo lại script (đổi GUID, scene mất tham chiếu). Dùng sửa chuỗi tại chỗ với mốc một dòng (file lẫn CRLF/LF).

## 6. Lỗi / vấn đề đã biết
1. Cân bằng: gà chưa luyện THUA trận tái đấu (gây 60/150 HP); HUD chưa chỉ sang việc luyện cho đến khi thắng. Người chơi phải tự luyện bằng huấn luyện viên (không bị khóa), thảm tập, quest giao hàng +200. Cần chủ dự án quyết định (mục 8).
2. `stoppingDistance` của NavMeshAgent gà Player = 10 (tầm đánh 1) có thể làm gà dừng xa địch; trạm luyện đã tự đặt 0.05 khi đi bộ. Chưa chơi tay trận để xác nhận.
3. Chưa có phiên chơi bằng phím/chuột thật cho gần như mọi bước; video ngủ KHÔNG xác nhận được là thực sự chạy hình/tiếng (cửa sổ Unity không focus nên VideoPlayer không vào isPlaying; chuỗi vẫn tiếp tục bằng cơ chế cắt sau độ dài + 4 s). Video chỉ dài 1 s; `fadeSeconds` trong scene = 1.
4. Hiệu ứng chưa thiết kế: Bình tưới shop, Thức ăn tăng lực (dùng ở Reward), Phân bón protein không có tác dụng gameplay thật.
5. `Rooster_ChuTu` (ROCKY) là mesh tĩnh, chỉ có chuyển động procedural; cần asset có rig. Chỉ có 2 clip đấm cho 6 trạng thái tấn công của ChiBa; hình dạng chuyển động chưa ai nhìn.
6. Bản đồ: lần kiểm 2026-10-24 không còn root `VillageMap` (không còn nhà ông bà/cổng/sân đấu) và đã có đợt gộp một Terrain; hãy xác nhận trong Game view trước khi coi là hoàn tất. Cảnh báo `Missing Prefab Asset: Superhero_Female_FullBody (1)` khi mở scene. Ảnh test còn lại trong `Assets/Screenshots/` và `Captures/`.
7. Input trộn (legacy Input cho Player + phím E/F, EventSystem dùng Input System): chỉ cảnh báo. `PlayerMovement` có `velocity.y =- 2f;` (giữ nguyên). Animator applyRootMotion = true trong khi NavMeshAgent cũng di chuyển (tay/chân chìm khi hít đất, chưa áp dụng sửa). Font TMP có thể thiếu glyph tiếng Việt trong một số UI (cần nhìn Game view).
8. `NPC_Grandfather` và `NPC_Grandmother` cách nhau 1.3 m, nhỏ hơn bán kính tương tác ~3 m: có thể hiện hai prompt cùng lúc (chưa xác nhận bằng chơi tay).
9. `village_delivery` đã nhận ở đầu game nên không còn vướng việc bạn thân biến mất; chỉ lưu ý bạn vắng từ lúc thắng tái đấu đến khi đủ 2 huy chương.
10. MCP đôi khi báo Unity đang reload / không thấy Editor: thử lại sau vài giây. Trong test: Pause + `EditorApplication.Step()`, Player có CharacterController khi teleport phải tắt/bật lại.

## 7. Việc cần chủ dự án chơi tay (checklist, phím E/F, chuột, Game view)
1. Vào game ở cổng -> Enter/Bắt đầu -> đi bộ tới Ông -> 'Xin ông giữ lại con gà' -> Bà giao việc (HUD đúng, không hai prompt Ông/Bà chồng nhau).
2. Giao hộp cho bạn thân -> thoại cảm ơn -> thách đấu -> dẫn gà vào đấu trường -> thua thật (câu châm chọc một lần) -> Bà trả 200 một lần.
3. Tưới cây -> Ông giao quest vườn -> hái rau, đặt rau, hũ protein (nhìn đĩa/bánh trên Game view), gà ăn -> xin phép Ông -> giường: thoại -> VIDEO có chạy hình/tiếng không -> thoại thức dậy -> banner gà mất.
4. Tìm gà (cảnh camera meme, caption), nhận gà (E), quest `tai_dau_ban_than`.
5. Tái đấu: một trận duy nhất kể cả bấm đúp; thua rồi thử lại; thắng -> bạn biến mất; mở quest luyện + Medea.
6. Shop (5 món, giá, cuộn; mua bằng chuột, tiền trừ một lần), F trải thảm, huấn luyện viên (nút 1-5 giờ, gà thật sự chạy/hít đất/đấm), HUD 'Giờ tập'.
7. Chị Ba -> Chú Tư (thoại khóa khi chưa thắng Chị Ba) -> Reward NPC (mỗi dòng đổi một lần).
8. Bạn quay lại ở 2 huy chương: thoại đổi theo 0/1/2 huy chương; 'Phục thù!' khó hơn rõ; thua -> thử lại; thắng -> kết + credits. Credits đang là placeholder (`Nhóm dự án Cuộc Kê`, nguồn asset 'sẽ bổ sung'): điền tên thật và license vào trường `credits` của `EndingSequence`.
9. Chung: Esc đóng mọi hội thoại/cửa sổ không kẹt chuột; chữ Việt hiển thị đủ (ă đ ơ ư); âm thanh nghe được và cân bằng; mũi tên QuestGuide chỉ đúng hướng.

## 8. Quyết định đang chờ chủ dự án
- Quest kiếm tiền/luyện tập mở sau thắng tái đấu + Bà trả tiền (hiện tại) hay mở ngay khi Bà trả công (cần bỏ điều kiện `Quest_TaiDauBanThan` ở Medea và `RematchController`).
- Cân bằng tái đấu: gà chưa luyện thua; muốn giảm độ khó địch hay chỉ HUD hướng dẫn luyện trước?
- Sau khi gà ăn xong: đĩa trơn hiện lại (hành vi hiện tại) hay ẩn hẳn.
- Bà có cấp phép ngủ trưa không (hiện chỉ Ông cấp).
- Phân bón protein / Thức ăn tăng lực / Bình tưới: tác dụng gameplay thật là gì.
- `damagePerTrainingHour` = 7 đã chỉnh: cần chơi thử để biết đủ để thắng Chị Ba / Chú Tư / boss (330 HP) mà không quá dễ.
- Menu / chơi lại, lưu/tải: chưa làm.

## 9. Việc làm tiếp theo đề xuất
1. Chơi tay toàn chuỗi (mục 7) và báo các mục lỗi.
2. Chốt các quyết định ở mục 8; xử lý bản đồ (xác nhận Terrain mới / VillageMap).
3. Mới sau đó mới làm tiếp polish (âm thanh thiếu: click UI, xu, gà kêu khi đi/bị đánh, âm thanh luyện tập, cổ vũ; asset ROCKY có rig; môi trường Việt Nam).

## 10. Chỉ mục: nội dung lịch sử đã chuyển sang `PROJECT_HISTORY.md`
(Tên mục cũ; số mục KHÔNG duy nhất, hãy tìm theo tiêu đề.)
- Baseline audit 2026-10-08 (mục 1-18 cũ: luồng gà đầu tiên, combat, chicken wake-up, lỗi cũ, thư mục asset).
- Kinh tế & quest ban đầu: 'Week 3 Task 1 - Meme money quests', 'Week 3 audit - scrap...', 'scrap asset replacement', 'Grandparents chore quest', 'Inventory + Hotbar' (+ re-check), 'Delivery quest Ship hàng làng quê', 'Tool shop', 'watering quest' (3 mục), 'Village map expansion pass'.
- Cốt truyện chính: 'Main story flow - audit', 'Starter chicken -> chicken_pet swap', 'Fights start only inside the arena', 'Sửa luồng đầu game: spawn tại cổng làng', 'Garden/night/missing-chicken flow', 'The friend leaves after the first rematch', 'First-loss scene order', 'Luồng: gà ăn rau -> ngủ -> gà mất...', 'Chuỗi vườn mới', 'Story order: take the chicken back -> rematch -> money quest', 'Quest giao hàng của bà mở ngay khi ông cho giữ gà', 'Quest gà ăn FeedingSpot 3 trạng thái', 'Chuỗi ngủ trưa + video'.
- Huấn luyện: 'Chicken training + stat progression', 'Task 5 re-verification', 'Prepare for the rematch', 'Training NPC (prepaid hours) - DESIGN DECISIONS + implementation', 'Thảm tập / Vé số / Phân bón', 'Shop goods', 'chuan_bi_tai_dau = total training hours', 'Training mat price 150 -> 60', 'Giảm sức mạnh mỗi buổi tập (+7)'.
- Milestone: 'M9 NPC tournament', 'M10 Medal rewards', 'M11 Dialogue', 'M12 Final Boss', 'M13 Ending', các 'Checkpoint ... manual-test checklist' và các bản kiểm thử/bug fix 'final boss rooster stayed lying'.
- Âm thanh / UI / hiệu ứng: 'Audio integration' (2 mục), 'Dialogue UI typewriter', 'Dialogue voice loops', '2D quest guide arrow', 'Light particle effects', 'Scrap sparkle', 'Reaction shot / camera meme', 'IntroScreen: Enter', 'Fix: player fell through the terrain', 'Fix: pickup items no longer spin', 'Một thanh máu HealthBarUI cho cả 4 gà' (2 mục).
- Model/animation: 'NPC animations from UAL', 'NPC animation re-check', 'NPC role gestures', 'NPC animation with UAL clips', 'NPC chickens in T-pose + friend (Mixamo CJ)', 'Pet model swap', 'Chicken fighter model swap (UNFINISHED checkpoint)'.
- Map: 'Map: one-Terrain consolidation'.

## 11. Lượt 2026-10-28: sửa luồng tái đấu bạn thân (thua kịch bản -> giải đấu -> tái đấu cuối)
- Code đã đúng luồng từ trước (`RematchController.scriptedLoss`/`UpdateAdvice`, `TournamentManager.friendRematchQuest`, `FinalBossController` gate 2 huy chương); lượt này CHỈ sửa text 2 quest asset đang nói sai ('Thắng rồi!'): `Quest_TaiDauBanThan` (description, objective, turnInObjective, objectiveCompletedMessage) và `Quest_ChuanBiTaiDau` (description, objectiveCompletedMessage). Không sửa script, không sửa scene.
- [VERIFIED-PLAY] (Play Mode, Step, gọi đúng code path, không phím/chuột thật): gà thức dậy, `tai_dau_ban_than` Active -> `StartRematch()` -> gà thua bằng damage thật (gà 0 HP, rooster 150/150); thoại khuyên mở, lúc đó quest còn Active và `giai_dau_lang` NotStarted -> đóng thoại -> `tai_dau_ban_than` Completed, `giai_dau_lang` Active, `chuan_bi_tai_dau` vẫn NotStarted (Bà chưa trả tiền), Chị Ba mở, Chú Tư khóa; `FinalBossController.TryInteract()` = false, quest cuối NotStarted. Thắng Chị Ba (1 huy chương, cổng vẫn đóng) rồi Chú Tư (2 huy chương): `giai_dau_lang` Completed, `GateOpen` = true, bạn thân hiện lại (`FriendAway` = false), `TryInteract()` mở `phuc_thu_cuoi_cung` Active + nút 'Phục thù!'.
- Lưu ý khi test: gà chưa biến hình (`StarterChicken.SwitchToPet`) đứng yên không đánh; test thắng giải phải luyện đủ (`ChickenStats`, trần 40) và `ApplyToCombat()`. Lượt test này đã dừng và chạy lại Play Mode (không còn trạng thái cũ).
- [MANUAL-PENDING] chơi tay cả luồng bằng phím/chuột thật: E với bạn thân khi tái đấu, câu thoại khuyên, HUD/mũi tên chuyển sang `giai_dau_lang`, bạn thân biến mất rồi quay lại, trận cuối. Chưa thử thắng trận cuối trong lượt này (không đổi). Cần chủ dự án quyết: mục 8 (cân bằng, thời điểm quest kiếm tiền).
- Không commit/push Git.


## 12. Lượt 2026-10-28: hai thanh máu cùng lúc (thay cho thanh đơn nhảy mục tiêu)
- Sửa `Assets/Script/HealthBarUI.cs` (viết lại, GUID giữ nguyên): bỏ `target`, nhảy mục tiêu, `minHoldSeconds`, `lingerAfterBattleSeconds`; thêm `role` (Player/Opponent) và `IsShown`. Scene `MainGame`: thêm `HealthBarUI_Opponent` (bản sao prefab `HealthBarUI.prefab`, giống hệt thanh cũ) -> đây là thanh thứ hai DUY NHẤT thêm vào, vì yêu cầu hiện 2 gà cùng lúc không làm được với 1 thanh. Không sửa BattleManager/HealthSystem/ChickenStats.
- [VERIFIED-PLAY] (Play Mode, Step; so fillAmount với HP mỗi frame): ngoài trận cả 2 ẩn; tái đấu gà bạn thân (thua kịch bản) 867 frame cả 2 cùng hiện, 0 lệch HP; Chị Ba và Chú Tư: thanh đối thủ đúng `Rooster_ChiBa` / `Rooster_ChuTu`, 0 lệch; boss (max HP 330): 245 frame cả 2 hiện, 0 lệch; sau mỗi trận (thắng hay thua) cả 2 ẩn ngay. Console: 0 lỗi dự án (chỉ cảnh báo intro video/Account API có sẵn).
- [MANUAL-PENDING] nhìn bằng mắt: vị trí/độ cao hai thanh trên từng gà (đo bằng renderer, chưa chụp màn hình), hai thanh có bị che hoặc chồng lên nhau khi 2 gà sát nhau không, thanh đối thủ cùng màu xanh với thanh Player (có muốn đổi sang đỏ không?). Chưa test thanh khi gà biến hình giữa trận (không xảy ra trong luồng).
- Mục 8: không còn câu hỏi xoá `HealthBarUI (1)` (object đó không còn trong project). 3 ảnh `Assets/Screenshots/healthbar_*.png` vẫn chưa xoá, cần hỏi chủ dự án. Không commit/push Git.


## 13. Lượt 2026-10-30: gà khóa Hurricane -> ngủ lần 2 -> Zoro Tsunami -> Hurricane kết thúc trận cuối
- Hiện trạng đầu lượt: script `ZoroReunionStory`, `HurricaneSkill`, `HurricaneFinisher`, các hook trong `StoryBed`/`QuestNPC`/`GrandfatherChickenRescue`/`PetCombatAI`/`FinalBossController` ĐÃ có và compile sạch, nhưng scene chưa có object nào, thiếu 2 quest asset, docs chưa nhắc. Lượt này KHÔNG sửa script nào.
- Đã làm: tạo `Assets/Data/Quests/Quest_VeNhaNghiNgoi.asset` (`ve_nha_nghi_ngoi`) và `Quest_TimGaZoro.asset` (`tim_ga_zoro`); thêm vào scene `MainGame` (đã lưu) `HurricaneSkill`, `HurricaneFinisher`, `ZoroReunionStory` (đủ tham chiếu, lấy giống `MissingChickenStory`), `ZoroWatchSpot`. Cập nhật `GAME_DESIGN.md` (3, 4.6, 4.8b mới, 4.9, 5, 6, 7, 12) và file này.
- [VERIFIED-PLAY] (Play Mode, gọi đúng code path, KHÔNG phím/chuột thật; tiền đề được dựng bằng code: `QuestManager` hoàn thành `giai_dau_lang` thay cho 2 trận thật, `MainChickenAcquire.WakeUp()`, teleport Player, `DialogueUI.Close()` thay cho bấm qua thoại, `Medals` = 2 bằng reflection chỉ để test): trước giải đấu `TrySleepAgain`, `OnPlayerSlept`, `AcceptSearch`, `CompleteReunion` đều trả false/không đổi stage, `TryPlayHurricane` = false; giải đấu xong -> stage Resting, `ve_nha_nghi_ngoi` Active; ngủ (video chạy hết) -> gà ẩn, stage ChickenAway; Ông: `TryHandleGrandparent` = true, nút nhận `tim_ga_zoro`, gà xuất hiện (5,0.08,93) cách Zoro 15 m, AI gà tắt, bấm nút lần 2 không dịch chuyển lại; vào tầm nhìn -> stage Found; lại gần 3,5 m -> stage Reunion, thoại speaker 'Zoro', Hurricane vẫn khóa khi thoại còn mở; đóng thoại -> Recovered, `tim_ga_zoro` Completed, `Unlocked` = true, gọi lại `CompleteReunion`/`Unlock` đều false.
- [VERIFIED-PLAY] trận cuối (`FinalBossController.TryInteract` -> `StartFinalBattle`, boss HP 330, gà thật đánh nhau): ở 54% HP `PlayCount` = 0; trận tự chạy tiếp và `HurricaneFinisher` tự kích hoạt ở HP 158/330 (log '[HurricaneFinisher] Hurricane!'); kết thúc: `PlayCount` = 1, `LaunchedOpponent`, gà đối thủ chết và rơi ở (-2.46,0.15,31.10) (~32 m), `BattleManager` Completed qua đường thắng thường, `phuc_thu_cuoi_cung` Completed, `FinalBossDefeated` = true, HP tối đa/damage gà đối thủ khôi phục 150/18, gà Player sống, AI bật lại, damage 10, overlay hiệu ứng đã huỷ. Console: 0 lỗi dự án (chỉ cảnh báo 'Account API' của editor).
- [MANUAL-PENDING] chơi tay bằng phím/chuột thật cả luồng 8b -> 10; nhìn bằng mắt animation Hurricane Kick (Animator `AttackIndex` 6), gió/camera/chữ, độ bay của gà đối thủ, vị trí `ZoroWatchSpot` (gà có thấy được Zoro không, có bị vật cản). `QuestGuide` (mũi tên) chưa có luật cho `ve_nha_nghi_ngoi` / `tim_ga_zoro` (HUD vẫn hiện mục tiêu).
- [NOT VERIFIED] màn kết (`EndingSequence`) sau trận cuối có Hurricane (đã dừng Play trước đó); thua trận cuối rồi đấu lại có kích hoạt Hurricane lại đúng một lần (cờ `triggeredThisFight` đặt lại khi hết trận); hai trận Chị Ba / Chú Tư thật dẫn tới stage Resting.
- Không commit/push Git.


## Polish: UIIA cat spin, line-triggered sound, hats follow heads (2026-10-31)
- Added ContinuousSpin (Assets/Script/Story): StoryBed/bed/cat spins 720 deg/s round the vertical axis and loops Assets/Sound/uia-uia-uia-cat.mp3 (3D, max distance 18). The conical hat is a child of the cat, so it turns with it.
- Added FollowBone (Assets/Script/Story): Ong's pith helmet (Uncle Grandpa/VN) follows the Pelvis/Head bone, Ba's conical hat (Yubaba/NonLa) follows Armature/Hips/Spine/Head. Offsets are captured in Awake, applied in LateUpdate; nothing re-parented.
- Added DialogueLineSound (Assets/Script/Audio) + DialogueUI.LineTextShown event (read-only hook). Object DialogueLineSounds: when 'Ban than' shows a line containing 'here we go again', the AUDIO of Assets/Sound/AhSh_t,HereWeGoAgain(GreenScreen).mp4 plays (VideoPlayer in API-Only mode: never drawn).
- Assumption: the line 'Ah sh*t, here we go again.' did not exist; it is now the first FinalBossController.wonLines entry (concession after the FINAL fight). Move/rename it in the Inspector if another fight is meant; the sound follows the text.
- NOT yet tested in Play Mode (manual checklist): cat spins + UIIA audible near bed; hats follow heads during idle/talk/gesture; after the final win the first friend line plays the sound once with no picture; E/skip on that line does not break the dialogue.

## 14. Lượt 2026-10-31 (lượt 2): dọn dẹp và sắp xếp lại cấu trúc dự án
Yêu cầu: tối ưu/sắp xếp lại toàn bộ dự án; KHÔNG xóa vĩnh viễn, KHÔNG đụng `Assets/Prefabs`, KHÔNG đổi gameplay, KHÔNG commit/push. Mọi đường dẫn đã chuyển nằm ở `Assets/Delete/_MOVE_LOG_2026-10-31.txt` (cột trái = vị trí cũ, cột phải = vị trí mới; muốn khôi phục chỉ cần kéo lại).

Cách kiểm reference: trước và sau MỖI nhóm, tính 'vân tay' dependency trực tiếp (theo GUID) của toàn bộ 1106 asset đang được MainGame.unity + Prefabs + Settings + TMP + Editor tham chiếu. Nếu lệch dù 1 asset thì tự hoàn tác nhóm. Kết quả các nhóm A, B, C, D, E, G: lệch = 0. Di chuyển bằng `AssetDatabase.MoveAsset` nên GUID không đổi (Git sẽ thấy là rename).

### 14.1 Cấu trúc mới (ngoài `Prefabs`, `Delete`, `TextMesh Pro`)
- `Animations/` (Chicken, ChickenWarrior, FunnyChicken, JacketBlocky, NPC), `Art/` (Character, Chicken, Equipment, Icons, ItemIcons, Materials, Models, Props, Textures, UI, ui_art, VFX), `Data/` (Items, Opponents, Quests, Rewards, Scrap, Shop), `Documentation/`, `Editor/`, `Scenes/` (MainGame, Backup), `Settings/`.
- MỚI `Audio/SFX` (trước là `Audio/Audio`), `Audio/Music` (10 mp3 nhạc nền + ambience, trước là `Sound/`), `Video/` (mp4 'The Next Day'), `Environment/Terrain` (4 TerrainData + `New Terrain.asset`, trước nằm ở gốc Assets), `Art/Materials` (trước là `Materials/`), `Art/Icons/PNG` (trước là `UI/UI/PNG`), `Art/VFX/UniversalFX` (trước là `Univeral FX Shader`, đã sửa lỗi chính tả).
- `Script/` còn 12 thư mục chức năng: Audio, Chicken (+Pet, +Skills), Combat, Economy (+Scrap, +Shop, +Reward), Inventory, Player, Quest, Story, Tournament, Training, UI, Vfx. 16 script đã đổi thư mục (chỉ đổi vị trí, không sửa code); GUID giữ nguyên nên mọi component trong scene/prefab vẫn trỏ đúng.
- Gốc `Assets/` chỉ còn `InputSystem_Actions.inputactions` (giữ nguyên vị trí theo mặc định của Unity).
- Sửa duy nhất trong code: 2 nhãn `[Header]` trong `GameAudio.cs` đổi chữ 'Assets/Sound' thành 'Assets/Audio/Music' (chỉ là chuỗi hiển thị Inspector).

### 14.2 Đã chuyển vào `Assets/Delete/` (100 file + 21 thư mục rỗng; không xóa gì)
- Nhóm A (4): scene `_Recovery/0 (23).unity`, `Readme.asset`, `TutorialInfo/Layout.wlt`, `TutorialInfo/Icons/URP.png` (tài liệu mẫu URP).
- Nhóm B (10): 9 SFX `impact*` (Glass/Metal/Mining/Wood) chưa gán ở đâu + mp3 ambience `soul_serenity_sounds-distant-village` (đã có bản y hệt trong `Delete/Audio`). Lưu ý: kế hoạch P2 có ambience làng quê, có thể lấy lại từ đây.
- Nhóm C (49): material không scene/prefab nào dùng: 7 ở `Materials/` (NPC_Placeholder, QuestBed, QuestPlate, Training_BagRed, Training_MatGreen, Village_FlagRed, Village_Wood), 18 `Materials/Village/VNM_*` (cây cau/tre/chuối/... của bản đồ làng cũ đã bỏ), 20 ở `Art/Character/NPC/Material`, 7 ở `Art/Character/OngBaBanThan`.
- Nhóm D (36): 15 texture `Art/Character/NPC/Texture`, 5 texture + 2 FBX bản sao `Ch13_nonPBR (1).fbx`, `The Boss (1).fbx` ở `OngBaBanThan`, 2 file trùng byte ở `Art/Props/VanHoaVN` (`source/NonLa.fbx`, `textures/Front_BC.jpg`), 12 PNG icon UI (locked/target/video, Black/White, 1x/2x) không dùng.
- Khác: `Documentation/_ScriptMoveLog.tmp.txt` (log tạm của lượt trước) và 21 thư mục rỗng vào `Delete/_EmptyFolders/` (16 thư mục rỗng cũ/mới phát sinh + 5 thư mục Script đã rỗng sau khi gom).
- Đã có sẵn từ lượt dọn trước (không ghi trong status cũ): `Delete/` ban đầu có 1425 file (37 scene `_Recovery`, 1022 file UI, 182 Art, 92 Screenshots, 71 Audio...). Đã kiểm: 0 asset trong `Delete` còn bị scene/prefab tham chiếu.

### 14.3 Code đã comment
- `Assets/Script/AI_Movement.cs` đã được comment toàn bộ từ lượt trước (ghi rõ lý do ở đầu file; không gắn ở scene/prefab nào, không script nào nhắc tên). Lượt này KHÔNG comment thêm file nào.
- KHÔNG đụng: `InteractObjectAutoRegister`, `InteractionAimHUD` (tự tạo lúc chạy bằng `RuntimeInitializeOnLoadMethod`), `CinematicCamera` (tạo qua `CinematicCamera.Get()`), `InteractObject` (lớp nền, 19 script dùng): nhìn bề ngoài là 'không gắn' nhưng ĐANG ĐƯỢC DÙNG.

### 14.4 Mục trùng lặp
- Đã xử lý (trùng từng byte, bản thừa không ai dùng): `VanHoaVN/source/NonLa.fbx` (bản dùng là `Art/Props/NonLa.fbx`), `VanHoaVN/textures/Front_BC.jpg` (bản dùng là `Art/Props/textures/Front_BC.jpg`), mp3 ambience nêu ở nhóm B.
- Chỉ báo cáo, CHƯA gộp (gộp sẽ phải sửa material/import setting nên chưa chắc tương đương): 9 texture Emission/Metallic giống hệt nhau ở `Art/Props/Chicken/Textture`; `ESTEIRA_X___TELA.png` và `ESTEIRA_X___TELA_2.png` ở treadmill; `Art/ui_art/card_1.png` và `card_1 1.png` trong `Art/VFX/UniversalFX`.
- Trong `Delete/`: 10 mp3 trong `Delete/Audio` trùng byte với `Audio/Music`, và nhiều bản sao trong `Delete/Art/UI` trùng với `Art/UI` đang dùng. Các bản trong `Delete` có thể xóa vĩnh viễn khi chủ dự án chắc chắn.

### 14.5 Giữ nguyên vì chưa chắc (chủ dự án quyết)
- `Scenes/Backup/*` (3 scene, có bản tạo hôm nay `BeforeChickenRig_20261031`): lưới an toàn, không đưa vào Delete.
- `Scenes/MainGame/NavMesh-Terrain.asset`: scene không tham chiếu trực tiếp nhưng gà/NPC dùng NavMeshAgent; cần xác nhận NavMesh thật nằm ở đâu trước khi động vào.
- `Animations/FunnyChicken/ChickenPushUp.controller`: status cũ ghi 'không xóa nếu chưa hỏi'; hiện không ai tham chiếu.
- Tên đặc thù cốt truyện, hiện chưa tham chiếu nhưng có thể là nguồn của model đang dùng: `Art/Character/NPC/Yubaba.blend`, `Zoro.blend`, `Rigged/MrBeast/MrBeast_Rigged.prefab`, `Art/Chicken/normal_chicken/normal_chicken.blend`, `Art/Models/Rooster.blend` (+ `Art/Textures/Rooster 1.png`).
- FBX mà Editor tool trỏ bằng đường dẫn cứng (KHÔNG được đổi vị trí kẻo hỏng tool): `Art/Chicken/source/dino.fbx`, `Art/Props/Chicken/{ostrich,chicken_family,chicken_smart,chicken_cook}.fbx` (ChickenRigBuilder), `Art/Character/NPC/{MrBeast,Uncle Grandpa}.fbx` + `NPC/Rigged` + `Animations/NPC/NPC_UAL.controller` (NPCRigTool), `Data/Items/*` + `Art/ItemIcons` (ItemIconGenerator). Vì vậy `Art/Character`, `Art/Props`, `Art/Chicken`, `Data`, `Animations` được giữ nguyên tên và vị trí.
- `Art/VFX/UniversalFX` (prefab Tornado + shader + texture): không ai tham chiếu, nghi trùng với `Prefabs/Tornado` nhưng chưa xác minh.
- `Art/Props/VanHoaVN`: `vegetable stands.fbx`, `Vietnamese Lantern.fbx` chưa đặt vào scene nhưng nằm trong kế hoạch P1 (môi trường Việt Nam).
- Script `Inventory/DragDrop.cs`, `Inventory/InteractableObject.cs` (không gắn, 1 script khác nhắc tên; thuộc hệ của đồng đội), `TutorialInfo/Scripts` (2 script mẫu của Unity).
- 4 `TerrainData_*` + `New Terrain.asset`: scene vẫn tham chiếu (có object cũ `OLD_Terrain_Tiles_DISABLED`), chỉ đổi vị trí.

### 14.6 Đã kiểm bằng code / cần chủ dự án test trong Unity
- ĐÃ KIỂM bằng code: vân tay dependency 1106/1106 không lệch sau mỗi nhóm; 0 tham chiếu từ scene/prefab vào `Delete`; 0 dependency trỏ tới file không tồn tại; scene `MainGame` đang mở có 4198 GameObject, 0 script bị mất, không dirty; Build Settings vẫn chỉ có `MainGame`; compile thành công, Console 0 lỗi (chỉ cảnh báo CS0618 `FindFirstObjectByType`... có từ trước); `Assets/Prefabs` 0 file bị ghi sau mốc bắt đầu lượt.
- CHƯA KIỂM (không có phím/chuột thật, không chạy Play Mode lượt này): chạy trọn luồng intro -> quest -> hội thoại -> tương tác -> inventory -> training -> chiến đấu -> giải đấu -> boss -> ending. Lượt này không sửa logic nên rủi ro thấp, nhưng cần chơi tay theo checklist mục 7.
- Cần nhìn bằng mắt: mở lại scene `MainGame` (File > Open) cho chắc; Game view: Terrain/bản đồ, texture NPC (Medea, Elvis, Rasta, ông bà, bạn thân), vật liệu bàn/giường/đĩa/bao cát/thảm tập; nhạc nền + ambience + SFX phát đúng; video ngủ 'The Next Day' (đã đổi sang `Assets/Video`, tham chiếu theo GUID); icon UI.

### 14.7 Vấn đề còn tồn tại
- `InventorySystem.cs` dòng 72 dùng `Resources.Load<GameObject>(itemName)` nhưng chỉ có thư mục `Resources` của TextMesh Pro; chưa xác minh nhánh code này có chạy trong gameplay hay không (có từ trước, không đụng).
- `ItemIconGenerator.cs` trỏ `Assets/Prefabs/featureCharacter/ItemPickups.prefab` còn thư mục thật là `Prefabs/feature-Character`; đường dẫn có vẻ lệch (chưa xác minh file prefab; không sửa vì liên quan `Prefabs`).
- `Delete/` hiện có 1526 file; nên duyệt và xóa tay ngoài Unity khi đã chắc (đặc biệt các bản sao trùng ở 14.4).
- Cảnh báo CS0618 (API Find* lỗi thời) ở ~20 script; chưa sửa vì ngoài phạm vi dọn dẹp.
- Chưa commit/push. Git không chạy được qua MCP nên không kiểm được `git status`.

### 14.8 Sắp xếp lại cấu trúc Art (2026-11-01)
- Chỉ di chuyển bằng `AssetDatabase.MoveAsset` (giữ GUID/meta), không xóa, không đổi tên asset, không đụng `Prefabs`, script, scene. Chưa commit.
- `Art/Character` -> `Art/Characters` (`NPCs/<tên NPC>/`, `Player/`, `Shared/`); `Art/Chicken` + `Props/Chicken` + `Animations/Chicken|FunnyChicken` -> `Art/Characters/Chickens/<tên gà>/` (bộ FunnyChicken dùng chung ở `Chickens/Shared`); `Props/source` + `ItemIcons` -> `Art/Items/<tên item>/`. Mỗi NPC/gà gồm model, `Materials`, `Textures`, `Animations`, `Rigged` (nếu có). `Animations/` ở gốc không còn file.
- Dùng chung: `Characters/NPCs/Shared/UAL` (UAL1_Standard + NPC_UAL controller), `Characters/Shared/3122_Low.anim` (Zoro + FunnyChicken).
- Đã kiểm: tham chiếu GUID thiếu vẫn 22 file/68 ref đúng bằng trước khi di chuyển (không phát sinh mới); 78 material không lỗi shader; 35 model load được. Chưa kiểm bằng mắt trong Game view/Play Mode.
- Editor tool còn trỏ đường dẫn cũ (chưa sửa vì quy định không sửa script): `NPCRigTool.cs` (Art/Character/NPC/..., Animations/NPC/NPC_UAL.controller), `ChickenRigBuilder.cs` (Animations/Chicken/Feature, Art/Props/Chicken/..., Art/Chicken/source/dino.fbx), `ItemIconGenerator.cs` (Art/ItemIcons, Art/Props/source/...). Runtime không ảnh hưởng; cần cập nhật hằng số đường dẫn trước khi chạy lại các tool này. Các đường dẫn Art/Chicken, Art/Props/Chicken, Animations/... ở các mục cũ của file này là vị trí trước 2026-11-01.
- Thư mục rỗng còn lại (xóa tay nếu muốn): `Art/Chicken`, `Art/ItemIcons`, `Art/Props/Chicken`, `Art/Props/source`, `Art/Props/textures`, `Animations`, `Art/Characters/NPCs/{Material,Texture,Rigged}`.
- Chưa xác định/giữ nguyên: `Characters/License.txt` (không rõ thuộc asset nào), `Art/Materials/*` (vật liệu dùng chung do prefab/scene tham chiếu, không gắn model), `MrBeast_Rigged.prefab` nay nằm ở `NPCs/MrBeast/Rigged` (chỉ di chuyển, nội dung không đổi).


## 15. Lượt 2026-11-01: hai ending phụ (Vegetariann't / Falsen't)
- Chỉ sửa `Assets/Script/Story/EndingSequence.cs`: thêm 4 field Inspector (`endingCardFontSize` = 140, `vegetarianEndingTitle` = "Vegetariann't Ending", `falseEndingTitle` = "Falsen't Ending", `endingCardSeconds` = 3.5) và coroutine `ShowEndingCard(title)` (chữ trắng trên overlay đen sẵn có, khoá player trong lúc hiện). Không thêm script/scene object mới; không đổi `BattleManager`, `FinalBossController`, `GrandfatherChickenRescue`, quest.
- Nhánh 'Đồng ý' với Ông (không nhận gà): `GrandfatherChickenRescue` -> video kết -> `EndingSequence.BeginCreditsOnly()` -> `RunCreditsOnly()`: màn đen -> thẻ "Vegetariann't Ending" -> credits -> nút 'Thoát game'.
- Nhánh thắng bạn thân trận cuối: `EndingSequence.Update()` -> `BeginEnding()` -> `Run()`: ăn mừng, về nhà, thoại Ông/Bà, epilogue -> thẻ "Falsen't Ending" -> credits.
- Chỉ một lần: cả hai đường vào đều qua cờ `EndingSequence.Started` (đặt trước khi chạy coroutine). Đã thử: `BeginCreditsOnly()` lần 2 = false, `BeginEnding()` sau `BeginCreditsOnly()` = false, `BeginEnding()` lần 2 = false, `BeginCreditsOnly()` sau `BeginEnding()` = false.
- [VERIFIED-PLAY] hai nhánh, mỗi nhánh một phiên Play Mode riêng (gọi thẳng code path, không dùng phím/chuột; rút ngắn thời gian bằng reflection chỉ trong phiên test, không lưu): thẻ hiện đúng chữ rồi mới có `Credits`, `CreditsShown` = true, Console 0 lỗi. Nhánh Falsen't đã đóng tay 3 hội thoại Ông/Bà/Ông bằng `DialogueUI.Close()`.
- [MANUAL-PENDING] chơi tay thật: bấm 'Đồng ý' với Ông rồi xem video -> thẻ -> credits; đánh thắng trận cuối (có Hurricane) rồi xem epilogue -> thẻ -> credits; xem bằng mắt cỡ chữ/độ đọc được của thẻ (đã phóng lên cỡ 140, đậm, field `endingCardFontSize`; trước đó dùng cỡ 48 bị nhỏ), thời gian 3,5 giây có hợp không (chỉnh `endingCardSeconds`).
- Giả định cần chủ dự án xác nhận: 'đánh bại xong bạn thân' được hiểu là kết thúc chính (thắng trận cuối), nên 'Falsen't Ending' gắn vào màn kết đó, không phải sau trận thắng đầu/tái đấu thường. Nhánh thắng này vẫn là ending chính có credits; nếu muốn ending riêng thì cần quyết định thêm.
- Chưa commit/push.

### 15.1 Khoảng cách tương tác xa thêm 1 m (2026-11-01)
- Mọi khoảng cách bấm E với vật phẩm/NPC/điểm tương tác tăng thêm +1,0 m: `interactionDistance` của RewardNPC, ShopNPC, ScrapCollector, ItemPickup, QuestCarryItem, QuestNPC, QuestObjectiveTarget, ChildhoodFriendBattleTaunt, FeedingSpot, GrandfatherChickenRescue, StoryBed, VillagerFlavorNPC, TournamentOpponentNPC, TrainerNPC, TrainingStation; `ZoroReunionStory.talkDistance` 4,5 -> 5,5; `MissingChickenStory.takeDistance` 2,5 -> 3,5. Ví dụ: Bà/Ông/QuestNPC 3 -> 4, vật phẩm nhặt 2 -> 3, shop/phần thưởng 1,8 -> 2,8.
- Sửa ở 3 nơi: giá trị mặc định trong script, giá trị serialized trong các prefab (trừ `Assets/Delete`) và trong scene `MainGame` (đã lưu scene). Không đổi logic. Không đổi: `InteractObject.aimDistance` (12), trigger `MainChickenAcquire` (bán kính 2,5 x scale 3), `NPCRoleAnimator.talkRange` (7, chỉ animation).
- [VERIFIED] đọc lại giá trị sau khi sửa (36 component trong scene khớp 'cũ + 1'); [MANUAL-PENDING] đi thử trong game xem 1 m có đủ 'xa hơn một chút' không; còn quá gần/xa thì chỉnh từng `interactionDistance` trong Inspector.
