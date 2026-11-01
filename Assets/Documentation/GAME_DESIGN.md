# GAME_DESIGN.md

Cập nhật 2026-10-28 — cốt truyện chính thức v2, đã đối chiếu với project. Đây là tài liệu THIẾT KẾ (game là gì). Trạng thái thực tế, số liệu, bug, checklist nằm ở `PROJECT_STATUS.md`; việc còn lại ở `DEVELOPMENT_ROADMAP.md`; nhật ký lịch sử ở `PROJECT_HISTORY.md`.

## 1. Tổng quan
Game 3D góc nhìn thứ nhất, hài hước kiểu Việt Nam. Một lập trình viên IT kiệt sức vì deadline về quê thăm ông bà, cứu một con gà thường khỏi nồi, thua trận đấu gà đầu tiên trước bạn thân thuở nhỏ, rồi nhờ một buổi 'bón nhầm bột protein' mà con gà biến thành gà cơ bắp kỳ lạ. Người chơi kiếm tiền từ việc vặt trong làng, mua dụng cụ, huấn luyện gà (chỉ số tăng thật và ảnh hưởng combat), thắng các giải đấu NPC để lấy huy chương, cuối cùng tái đấu bạn thân cũ. Thắng trận này là kết thúc game.
Đấu gà là môn thể thao hư cấu. **Không cá cược / cờ bạc**: thắng nhận huy chương và phần thưởng cố định.

## 2. Quy ước trạng thái
- **[ĐÃ CÓ]** đã triển khai (kiểm bằng code path trong Play Mode; chưa ai chơi tay bằng phím/chuột thật, xem `PROJECT_STATUS.md` mục 7).
- **[MỘT PHẦN]** đã có nhưng thiếu một số phần ghi rõ.
- **[CHƯA LÀM]** chưa triển khai.
- **[CẦN QUYẾT ĐỊNH]** chủ dự án phải chọn.

## 3. Cốt truyện tổng thể (thứ tự đã cài đặt)
``` text
INTRO (màn intro, Enter / nút Bắt đầu)
   ↓
CỔNG LÀNG — Quest 1: Về nhà thăm ông bà
   ↓
NHÀ ÔNG BÀ — Ông định làm thịt gà → 'Xin ông giữ lại con gà' → gà đi theo
   ↓  (ngay lúc đó Bà giao quest giao hàng)
GIAO HÀNG cho bạn thân → bạn cảm ơn rồi thách đấu → THUA (kịch bản, sát thương thật) → 'MÀY SỢ À' → Bà trả công 200
   ↓
CHĂM VƯỜN — tưới cây → hái rau, đặt lên đĩa, hũ nhầm protein, gà ăn → xin Ông cho ngủ trưa → ngủ (video The Next Day)
   ↓
GÀ BIẾN HÌNH (một lần) → gà đi mất → tìm gà → nhận lại gà → mở khóa huấn luyện
   ↓
TÁI ĐẤU bạn thân (thắng) → bạn đi vắng → mở quest kiếm tiền + luyện (cần Bà đã trả công)
   ↓
KIẾM TIỀN → MUA DỤNG CỤ → HUẤN LUYỆN (huấn luyện viên trả trước theo giờ)
   ↓
GIẢI ĐẤU NPC (Chị Ba → Chú Tư) → huy chương → Reward NPC
   ↓
NGỦ Ở NHÀ ÔNG BÀ (sau khi hạ cả Chị Ba lẫn Chú Tư) → gà lại biến mất → Ông/Bà: 'Nó đi theo thằng tóc xanh lá rồi!' → tìm thấy gà đứng xa nhìn Zoro Tsunami → lại gần: thoại + nhận lại gà + MỞ KHÓA HURRICANE
   ↓
FINAL BOSS — bạn thân quay lại khi đủ 2 huy chương → THẮNG → MÀN KẾT + credits
```

## 4. Từng chương

### 4.1 Intro
- **Thiết kế:** màn intro ngắn: nhân vật IT kiệt sức vì deadline, quyết định về thăm ông bà; sau đó vào gameplay 3D ở cổng làng.
- **Hiện trạng:** [ĐÃ CÓ] `IntroScreenController` (lớp UI trong `MainGame`, Enter hoặc nút Bắt đầu; Player bị khóa khi intro hiện; spawn tại cổng làng (10.5,0.1,-9)). Là màn chữ/UI, chưa có video 2D thật. [CẦN QUYẾT ĐỊNH] có làm video 30-60 s không (placeholder là đủ).

### 4.2 Quest 1 — Về nhà thăm ông bà
- Quest `ve_nha_tham_ong_ba`, nhận khi bắt đầu game (hoặc dự phòng khi nói chuyện với Ông); hoàn thành khi nói chuyện với Ông. Mũi tên `QuestGuide` chỉ đường. [ĐÃ CÓ]

### 4.3 Cứu gà
- **Thiết kế:** Ông định làm thịt một con gà BÌNH THƯỜNG. Player xin giữ lại; Ông đồng ý; gà tỉnh dậy đi theo. Quest `nhan_nuoi_chu_ga`. Ngay khi Ông đồng ý, quest giao hàng của Bà mở (không chờ sự kiện nào khác).
- **Hiện trạng:** [ĐÃ CÓ] (`GrandfatherChickenRescue`, `MainChickenAcquire`, `PetCombatAI`). Gà chỉ trở nên đặc biệt sau 4.6.
- **Ending phụ 'Vegetariann't Ending' (2026-11-01):** nếu Player chọn 'Đồng ý' để Ông làm thịt gà (không nhận nuôi), sau video kết màn hình đen hiện đúng một dòng "Vegetariann't Ending" (~3,5 giây) rồi chạy credits hết game. Đi qua `GrandfatherChickenRescue.OnAgreeToCook` → `EndingSequence.BeginCreditsOnly()`; cốt truyện chính không bị đụng (nhánh này đã kết thúc game). [ĐÃ CÓ] [VERIFIED-PLAY] bằng cách gọi `BeginCreditsOnly()`.

### 4.4 Giao hàng và trận thua đầu tiên
- **Thiết kế:** Bà nhờ giao một gói hàng cho bạn thân cũ (quest `village_delivery`, +200 một lần). Chưa giao thì bạn chỉ đòi gói hàng. Giao xong bạn cảm ơn rồi thách đấu ('Nhận lời thách đấu'). Trận chỉ bắt đầu khi Player và gà đều trong `BattleArena`. Thua tự nhiên bằng sát thương thật (balance trận đầu: gà mình sát thương 1, địch 15, rồi khôi phục). Bạn nói **'Bữa nào tái đấu không? MÀY SỢ À'**, Player đáp lại, rồi câu chuyện đi tiếp. Không Game Over, không hủy gà; gà tự đứng dậy sau ~13 s. Bà chỉ trả công sau khi quest `thu_suc_voi_ban_cu` xong.
- **Hiện trạng:** [ĐÃ CÓ] (`ChildhoodFriendBattleTaunt`, `BattleManager`, `BattleArena`, `BattleTrigger`). Thua thật bằng tay chưa chơi.

### 4.5 Chuỗi chăm vườn và ngủ trưa
- **Thiết kế:** Bà nhờ tưới cây (quest `grandparents_helper`: nhặt bình tưới, tưới 3 cây, +150). Rồi Ông giao `garden_protein` (4 bước: hái rau, đặt rau lên đĩa ở `FeedingSpot`, lấy hũ 'bột cám' thật ra ghi BỘT PROTEIN - gag, gà ăn; đĩa hiển thị 3 trạng thái: đĩa trơn / đĩa + lá rau / bánh hoàn chỉnh). Sau đó quest `xin_phep_ngu_trua` (phải xin Ông, đóng hội thoại giữa chừng thì chưa được). Giường `StoryBed`: thoại 'Ơ cái giường với con mèo này cứ quen quen ấy nhờ' → ngủ → video The Next Day → thoại 'Ngủ ngon quá. Ra sau kiếm con gà thử'.
- **Hiện trạng:** [ĐÃ CÓ] (`GardenFlowStory`, `FeedingSpot`, `StoryBed`). Video chưa xác nhận chạy thật (xem status). Không có chu kỳ ngày/đêm; cơ chế 'đến tối' bị bỏ.

### 4.6 Biến hình và tìm gà
- **Thiết kế:** sáng hôm sau gà biến thành gà cơ bắp kỳ lạ (CHỈ MỘT LẦN, cờ bảo vệ) và đi mất. Hỏi Bà/Ông → quest `tim_ga`; gà ở khu tập đang hít đất; thấy gà → cảnh camera meme (một lần) → E nhận lại gà → MỞ KHÓA huấn luyện. Biến hình chỉ đổi hình dạng + mở khóa (chỉ số do huấn luyện quyết định). Sau biến hình gà CHỈ dùng các đòn đánh có sẵn (Kick 1-5); Hurricane bị khóa (`HurricaneSkill.Unlocked` = false, `PetCombatAI.TryPlayHurricane` từ chối) cho tới khi gặp Zoro Tsunami (mục 4.8b).
- **Hiện trạng:** [ĐÃ CÓ] (`ChickenTransformationTrigger`, `MissingChickenStory`; model gà đã đổi sang Chicken Warrior).

### 4.7 Tái đấu, kiếm tiền, huấn luyện
- **Tái đấu:** sau khi nhận lại gà, quest `tai_dau_ban_than`. Bạn thân thoại 'gà mày sao tự dưng to ra...', nút 'Tái đấu!' (trong đấu trường). Thắng → bạn đi vắng đến khi đủ 2 huy chương. Thua → bạn gợi ý tập thêm, đấu lại được, gà hồi sinh.
- **Quest kiếm tiền/luyện:** chỉ mở sau thắng tái đấu VÀ Bà đã trả công: `chuan_bi_tai_dau` ('Kiếm tiền luyện gà', 5 buổi = tổng giờ tập) và `scrap_collector` ('Ve Chai Đại Chiến', 10 phế liệu, +100, NPC Medea). [CẦN QUYẾT ĐỊNH] mở sớm hơn (ngay sau Bà trả công)? Và cân bằng: gà chưa luyện thua tái đấu.
- **Nguồn tiền:** ve chai 100 (+ tiền nhặt), tưới cây 150, giao hàng 200, thưởng giải đấu và Reward NPC.
- **Cửa hàng** (`NPC_MarketVillager`): Bình tưới 60, Thảm tập 60, Thức ăn tăng lực 40, Vé số 20 (cào bằng F, có thể trúng tiền), Phân bón protein 80 (F). Bình tưới, Thức ăn tăng lực, Phân bón chưa có tác dụng gameplay thật [CẦN QUYẾT ĐỊNH].
- **Huấn luyện [ĐÃ CÓ]:** `ChickenStats` (Strength/Attack/Speed, khởi điểm 10, trần 40). Huấn luyện viên `TrainerNPC`: trả trước 1-5 giờ, giá giờ k = 150 + 50(k-1) (150/350/600/900/1250); mỗi giờ = một vòng chạy máy → hít đất → đấm bao cát (mỗi môn 1/3 giờ; hít đất cần thảm tập đã trải); mỗi giờ hoàn thành +7 sát thương (đã giảm từ +20); hủy giữa chừng thì hoàn tiền phần chưa tập; tổng giờ tập hiện trên HUD. Gà chưa luyện thì combat không đổi (HP 100, sát thương 10).
- **[CHƯA LÀM]:** hiệu ứng thật của protein; lưu/tải; HUD chỉ số chi tiết.

### 4.8 Giải đấu NPC
- **Thiết kế:** 2 đối thủ NPC (có thể mở rộng), mỗi người một gà, thoại giới thiệu, một trận, thưởng huy chương + tiền (quest `giai_dau_lang`). Huy chương đổi quà ở Reward NPC.
- **Hiện trạng:** [ĐÃ CÓ] Chị Ba (`Rooster_ChiBa`) rồi Chú Tư (`Rooster_ChuTu`, khóa tới khi thắng Chị Ba), `TournamentManager` + `OpponentDefinition`, trận trong đấu trường. Reward NPC (`NPC_RewardKeeper`, (46,0,55)): 2 phần thưởng, 1 huy chương mỗi cái, mỗi cái đổi một lần (Thức ăn tăng lực x3; 300 tiền) — giá trị chưa cân bằng. [MỘT PHẦN] ROCKY (Chú Tư) là mesh tĩnh, chỉ chuyển động procedural.
- Ứng viên NPC còn lại nếu muốn mở rộng: `NPC_CoLan`, `NPC_BacHai` (hiện chỉ thoại flavor).

### 4.8b Ngủ lần 2, gà đi theo Zoro Tsunami, mở khóa Hurricane
- **Thiết kế (theo thứ tự, mỗi bước chỉ chạy một lần và bị chặn tới khi bước trước xong):**
  1. Hạ cả Chị Ba lẫn Chú Tư (`giai_dau_lang` Completed) → mở quest `ve_nha_nghi_ngoi`: về nhà ông bà ngủ trên giường (`StoryBed`, lần ngủ thứ hai chỉ khả dụng lúc này; Ông/Bà nhắc ngủ nếu Player hỏi).
  2. Ngủ (video The Next Day) → gà biến mất (ẩn, không tạo gà thứ hai) → tỉnh dậy: banner 'con gà lại biến mất'.
  3. Hỏi Ông hoặc Bà → 'Nó đi theo thằng tóc xanh lá rồi!' → Player: 'Ơ, thằng mù đường mà?' → nút 'Đi tìm con gà' nhận quest `tim_ga_zoro`.
  4. Gà (vẫn là gà đã biến hình, tắt AI) đứng cách Zoro Tsunami 15 m (`ZoroWatchSpot`), quay mặt về phía Zoro. Player vào tầm nhìn (camera + raycast, ≤22 m) → banner gợi ý; lại gần ≤4,5 m → thoại Zoro tự mở.
  5. Đóng thoại → gà đi theo lại, `tim_ga_zoro` Completed, `HurricaneSkill.Unlock()` (đúng một lần), hiệu ứng gió 'học được kỹ năng' + banner.
- **Chặn bỏ qua:** trận tái đấu cuối (`FinalBossController.GateOpen`) cần đủ huy chương VÀ `HurricaneSkill.Unlocked`; huấn luyện chờ gà được nhận lại (`ChickenTransformationTrigger`).
- **Hiện trạng:** [ĐÃ CÓ] `ZoroReunionStory`, `HurricaneSkill`, quest `ve_nha_nghi_ngoi`, `tim_ga_zoro`, object scene `ZoroReunionStory` / `HurricaneSkill` / `HurricaneFinisher` / `ZoroWatchSpot`. Zoro Tsunami = `NPCs_FromVillageMap/NPC_BacHai/Zoro` (5,0,108).

### 4.9 Final boss và màn kết
- **Thiết kế:** bạn thân quay lại khi đủ 2 huy chương đã kiếm (tiêu huy chương ở Reward NPC không làm giảm số đã kiếm). Thoại gọi lại 'MÀY SỢ À', nút 'Phục thù!'. Boss (GuguGaga) tạm HP 330, sát thương 34 trong đúng một trận (rồi khôi phục 150/18). Thua → thử lại, không thưởng. Thắng → bạn thừa nhận sự tiến bộ, gà ăn mừng, về nhà ông bà, kết hài, credits, kết thúc (chỉ có nút Thoát). Khi gà bạn thân còn ≤50% HP, gà Player TỰ ĐỘNG tung HURRICANE KICK (đúng một lần mỗi trận): dừng hai gà, animation Hurricane Kick thật + gió xoáy + rung/phóng camera + chữ 'HURRICANE KICK!', cú đá gây sát thương kết liễu qua `HealthSystem.TakeDamage` (cùng đường thắng thường: `BattleManager` → `PlayerWon` → `FinalBossController`) rồi gà đối thủ bị hất bay ~30 m. Chỉ xảy ra ở trận cuối này (`FinalBossController.FightActive`).
- **Hiện trạng:** [ĐÃ CÓ] (`FinalBossController`, quest `phuc_thu_cuoi_cung`, `EndingSequence`). Credits đang là placeholder. Thêm `HurricaneFinisher` (Assets/Script/Skills).
- **Ending phụ 'Falsen't Ending' (2026-11-01):** ngay sau câu kết hài (epilogue) của màn kết thường, màn hình đen hiện đúng một dòng "Falsen't Ending" (~3,5 giây) rồi mới chạy credits. Dùng lại `EndingSequence.Run()` (thêm `ShowEndingCard`); không đổi điều kiện kích hoạt, không đụng `BattleManager`/`FinalBossController`. [ĐÃ CÓ] [VERIFIED-PLAY] bằng cách gọi `BeginEnding()`.

## 5. Cờ tiến trình cốt truyện
Quy ước: trạng thái quest Completed = cờ một lần; không có framework cờ riêng (`StoryProgress` được đề xuất nhưng không cần). Hiện chỉ sống trong phiên chơi.

| Cờ | Nguồn thật trong project |
|---|---|
| gà đã nhận / đi theo | `MainChickenAcquire.IsAcquired` |
| thua trận đầu | `BattleManager.firstBattleCompleted` (static), quest `thu_suc_voi_ban_cu` |
| bà đã trả công giao hàng | quest `village_delivery` Completed |
| đã biến hình / mở khóa huấn luyện | `ChickenTransformationTrigger.Transformed`, `TrainingUnlocked` |
| nhận lại gà / tái đấu thắng | `tim_ga`, `tai_dau_ban_than` Completed |
| bạn đi vắng | `RematchController.FriendAway` (tính từ trạng thái, không lưu) |
| số huy chương | `TournamentManager.Medals` (đã kiếm), `MedalsSpent` |
| ngủ lần 2 / gà đi theo Zoro | `ZoroReunionStory.CurrentStage` (Waiting→Resting→ChickenAway→Searching→Found→Reunion→Recovered), quest `ve_nha_nghi_ngoi`, `tim_ga_zoro` |
| mở khóa Hurricane | `HurricaneSkill.Unlocked` (static, một chiều, chỉ `ZoroReunionStory.CompleteReunion` mở) |
| Hurricane đã dùng ở trận cuối | `HurricaneFinisher.PlayCount` (1 mỗi trận cuối) |
| thắng boss / kết | `phuc_thu_cuoi_cung` Completed, `FinalBossController.VictoryStoryDone`, `EndingSequence.Started` (một lần cho cả hai ending có credits; `Started` đặt trước khi chạy nên gọi lại không chạy lại) |

## 6. Nhân vật và NPC
- **Ông** (`NPC_Grandfather`), **Bà** (`NPC_Grandmother`, QuestNPC): nhà ông bà. **Bạn thân cũ** (`NPC_ChildhoodFriend`): đối thủ đầu, tái đấu, boss cuối; thoại đổi theo tiến trình (0/1/2 huy chương...). **Medea** (`QuestNPC_Villager`): ve chai. **Dân chợ** (`NPC_MarketVillager`): cửa hàng. **Huấn luyện viên** (`NPC_Trainer`). **Reward NPC** (`NPC_RewardKeeper`). **Chị Ba, Chú Tư**: đối thủ giải. **Cô Lan, Bác Hai**: chưa có vai trò. **Zoro Tsunami** (`NPC_BacHai/Zoro`, tóc xanh lá, mù đường): gà đứng xem anh quay 'cơn lốc' → dạy Hurricane.
- Dùng lại thân người dựng sẵn + quần áo/phụ kiện; animation chung `NPC_UAL`.

## 7. Danh sách quest
| Quest | id | Trạng thái |
|---|---|---|
| Về nhà thăm ông bà | `ve_nha_tham_ong_ba` | [ĐÃ CÓ] |
| Nhận nuôi chú gà | `nhan_nuoi_chu_ga` | [ĐÃ CÓ] |
| Ship hàng làng quê (+200) | `village_delivery` | [ĐÃ CÓ] |
| Thử sức với bạn cũ (trận đầu) | `thu_suc_voi_ban_cu` | [ĐÃ CÓ] |
| Cháu ngoan bất đắc dĩ (tưới cây, +150) | `grandparents_helper` | [ĐÃ CÓ] |
| Rau nhà trồng / bột protein | `garden_protein` | [ĐÃ CÓ] |
| Xin phép ngủ trưa | `xin_phep_ngu_trua` | [ĐÃ CÓ] |
| Tìm con gà | `tim_ga` | [ĐÃ CÓ] |
| Đấu lại với bạn thân | `tai_dau_ban_than` | [ĐÃ CÓ] |
| Kiếm tiền luyện gà (5 buổi) | `chuan_bi_tai_dau` | [ĐÃ CÓ] |
| Ve Chai Đại Chiến (+100) | `scrap_collector` | [ĐÃ CÓ] |
| Giải đấu làng (2 trận) | `giai_dau_lang` | [ĐÃ CÓ] |
| Về nhà nghỉ ngơi | `ve_nha_nghi_ngoi` | [ĐÃ CÓ] |
| Tìm con gà đi theo Zoro | `tim_ga_zoro` | [ĐÃ CÓ] |
| Phục thù — trận cuối | `phuc_thu_cuoi_cung` | [ĐÃ CÓ] |
Không làm hàng chục quest; chỉ thêm quest nhỏ khi củng cố bản demo.

## 8. Hệ thống đã có và phải giữ nguyên
Di chuyển/camera Player, combat gà (PetCombatAI, RoosterCombatAI, HealthSystem), thanh máu duy nhất `HealthBarUI`, đánh thức/nhận gà, BattleManager/BattleTrigger/BattleArena, QuestManager và các thành phần quest, CurrencyWallet/MoneyHUD, Scrap, Inventory + Hotbar, DialogueUI/InteractionPromptUI/MessageBannerUI, cửa hàng, Reward, huấn luyện, GameAudio, QuestGuide. Không viết lại nếu không có lỗi chặn; thay đổi vào combat/BattleManager phải nhỏ nhất và được báo cáo.

## 9. Bản sắc Việt Nam, NPC, tài sản
- Nét Việt nằm ở nhiều chi tiết nhỏ: nhà quê, hàng quán chợ, hàng rào tre, nón lá, bàn thờ, võng, ghế nhựa, cột điện, dây phơi đồ, đèn lồng, biển hiệu tiếng Việt; ruộng lúa, tre, chuối/dừa, ao, mương, đường đất; âm thanh gà gáy, chim, chó, dế/ve, xe máy/xe đạp, nhạc dân gian có giấy phép rõ ràng. Mục tiêu: làng quê cách điệu, không phải làng fantasy chung chung.
- Nguồn asset ưu tiên: Quaternius, Kenney, Poly Haven, CC0 itch.io, OpenGameArt (giấy phép rõ), Pixabay (âm thanh), Sketchfab chỉ khi kiểm từng giấy phép. Ưu tiên CC0; CC BY chỉ khi ghi nguồn. Tránh asset trả phí, asset rip, giấy phép mơ hồ, gói quá lớn, texture 8K vô cớ. Mỗi asset mới phải kiểm giấy phép (và điền vào credits).

## 10. Nguyên tắc kỹ thuật
Module nhỏ, không script khổng lồ; quest không phụ thuộc đường dẫn đối tượng cứng; tái dùng hệ thống hiện có; không mở rộng phạm vi khi chưa được duyệt; milestone nhỏ, kiểm thử được. Không dùng Destroy/HP giả/tắt tấn công để ép thua. Không Game Over. Không cờ bạc.

## 11. Bản demo thành công
Người chơi đi hết chuỗi ở mục 3 (cổng làng → ... → màn kết) mà không cần can thiệp editor, thấy chỉ số gà tăng thật nhờ huấn luyện. Phiên bản nhỏ mà trau chuốt hơn bản lớn dang dở.

## 12. Quyết định đang mở
(Đã chốt: gate boss = 2 huy chương; phục thù boss là trận SAU tái đấu và giải đấu; giường/ngủ có video; chọn Chị Ba + Chú Tư làm đối thủ.)
1. Quest kiếm tiền/luyện mở sau thắng tái đấu + Bà trả công (hiện tại) hay mở ngay khi Bà trả công.
2. Cân bằng tái đấu: gà chưa luyện thua; giảm độ khó hay hướng dẫn người chơi luyện trước.
3. Sau khi gà ăn xong: đĩa trơn hiện lại hay ẩn hẳn. Bà có cấp phép ngủ trưa không.
4. Tác dụng gameplay của Bình tưới / Thức ăn tăng lực / Phân bón protein.
5. Intro có cần video 2D thật không (đang là màn UI).
6. Có làm menu / chơi lại và lưu/tải không (cờ static làm khó nạp lại scene).
7. Thêm đối thủ thứ ba (Cô Lan / Bác Hai) hay giữ 2.
8. Hiệu ứng Hurricane (gió, rung camera, bay xa 34 m, cỡ chữ) và vị trí `ZoroWatchSpot` cần chủ dự án nhìn bằng mắt để chỉnh cảm giác.
