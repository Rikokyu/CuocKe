# GAME_DESIGN.md

Cập nhật 2026-10-13 — **cốt truyện chính thức v2**. Đây là tài liệu thiết kế (cái game *sẽ* là). Trạng thái thực tế của project nằm ở `PROJECT_STATUS.md`; thứ tự triển khai nằm ở `DEVELOPMENT_ROADMAP.md`.

## 1. Tổng quan

Game 3D góc nhìn thứ nhất, hài hước kiểu Việt Nam. Một lập trình viên IT kiệt sức vì deadline về quê thăm ông bà, cứu một con gà thường khỏi nồi, thua trận đấu gà đầu tiên trước bạn thân thuở nhỏ, rồi nhờ một đêm "bón nhầm bột protein" mà con gà biến thành gà cơ bắp kỳ lạ. Người chơi kiếm tiền từ các việc vặt trong làng, mua dụng cụ, huấn luyện gà (chỉ số tăng thật và ảnh hưởng combat), thắng các giải đấu NPC để lấy huy chương, và cuối cùng tái đấu bạn thân cũ. Thắng trận này là kết thúc game.

Đấu gà ở đây là môn thể thao hư cấu. **Không có cá cược / cờ bạc**: thắng nhận huy chương và phần thưởng cố định.

## 2. Quy ước trạng thái (dùng xuyên suốt)

- **[ĐÃ CÓ]** đã triển khai và được `PROJECT_STATUS.md` ghi nhận. Lưu ý: hầu hết mới được kiểm chứng bằng cách gọi code path trong Play Mode, **chưa có phiên chơi bằng bàn phím/chuột thật**.
- **[ĐANG LÀM]** có trong project (đọc code/scene ngày 2026-10-13) nhưng `PROJECT_STATUS.md` chưa ghi nhận hoặc chưa kiểm chứng.
- **[CHƯA LÀM]** chưa triển khai.
- **[CẦN QUYẾT ĐỊNH]** điểm chủ dự án phải chọn trước khi làm.

## 3. Cốt truyện chính — tổng thể

``` text
INTRO 2D (video)
   ↓
ĐẦU LÀNG (gameplay 3D)  — Quest 1: Tìm đường về nhà ông bà
   ↓
NHÀ ÔNG BÀ — Ông định làm thịt gà → xin giữ lại → gà đi theo
   ↓
NHÀ BẠN THÂN CŨ — bị thách đấu → THUA → "Bữa nào tái đấu không? MÀY SỢ À"
   ↓
CHĂM RAU — giúp bà tưới rau → bón nhầm bột protein → hái rau cho gà ăn → đi ngủ
   ↓
SÁNG HÔM SAU — gà biến hình thành gà cơ bắp (MỘT LẦN) → mở khóa huấn luyện
   ↓
KIẾM TIỀN → MUA DỤNG CỤ → HUẤN LUYỆN (máy chạy bộ, hít đất, đấm bao cát, protein)
   ↓
GIẢI ĐẤU NPC — thắng → huy chương / phần thưởng → tiền → huấn luyện tiếp
   ↓
FINAL BOSS — tái đấu bạn thân cũ → THẮNG → KẾT THÚC GAME
```

## 4. Từng chương

### 4.1 Intro 2D
- **Thiết kế:** video/hoạt ảnh 2D ngắn (30–60 giây): nhân vật IT kiệt sức vì deadline (lỗi, tin nhắn đòi việc), đóng laptop, quyết định về thăm ông bà. Video kết thúc thì chuyển sang gameplay 3D tại **đầu làng**.
- **Hiện trạng:** [CHƯA LÀM]. Project chỉ có một scene (`MainGame`), không có VideoClip, Timeline hay PlayableDirector. Điểm xuất phát của Player hiện ở gần nhà ông bà (khoảng x=3.6, z=16), không phải đầu làng.
- **Thay đổi so với bản cũ:** bản cũ ghi cinematic 3D bằng Timeline/Cinemachine; nay là video 2D.
- **Phụ thuộc:** có video (hoặc placeholder ảnh tĩnh + chữ + fade nếu chưa có video); điểm spawn "đầu làng"; [CẦN QUYẾT ĐỊNH] intro là scene riêng hay lớp UI phủ trong `MainGame`.

### 4.2 Quest 1 — Tìm đường về nhà ông bà
- **Thiết kế:** Player đứng ở đầu làng, HUD hiện mục tiêu "Tìm đường về nhà ông bà"; hoàn thành khi tới nhà (vùng trigger quanh nhà ông bà). Có thể có gợi ý nhẹ (NPC chỉ đường, biển báo).
- **Hiện trạng:** hạ tầng quest (QuestManager, QuestDefinition, QuestHUD, QuestFeedback) [ĐÃ CÓ] nhưng chưa có asset quest này; khóa mục tiêu hiện chỉ có `scrap`, `chore`, `delivery`; chưa có điểm đầu làng, vùng đích, hay chỉ dẫn đường. → [CHƯA LÀM].

### 4.3 Cứu gà
- **Thiết kế:** ở nhà ông bà, Ông định làm thịt một con gà **bình thường** (lúc này nó chưa có gì đặc biệt). Player xin giữ lại; Ông đồng ý; gà tỉnh dậy và đi theo Player. Quest "Nhận nuôi chú gà".
- **Hiện trạng:** chuỗi gà nằm sẵn → bấm E → WakeUp → ChickenIdle → thành MainChicken → đi theo (`MainChickenAcquire`, `PetCombatAI`) là [ĐÃ CÓ] ở mức logic; hình ảnh chưa ai đánh giá. `NPC_Grandfather` hiện chỉ có `VillagerFlavorNPC` (thoại chung), **chưa có thoại "định làm thịt"/"xin giữ lại"** và chưa có quest nhận nuôi. → thoại + quest [CHƯA LÀM].
- **Thay đổi so với bản cũ:** bản cũ gọi đây là "gà đặc biệt/lạ". Nay gà chỉ thành đặc biệt sau chương 4.6.

### 4.4 Trận thua đầu tiên
- **Thiết kế:** Player dẫn gà tới nhà bạn thân cũ. Bạn nhận ra Player, chê gà yếu, thách đấu. Trận đấu được thiết kế để thua tự nhiên (sát thương thật). Sau khi thua bạn nói: **“Bữa nào tái đấu không? MÀY SỢ À”**. Đây là động lực phục thù cho toàn game. Không Game Over, không hủy gà.
- **Hiện trạng:** cân bằng trận đầu (gà mình sát thương 1, địch 15, rồi khôi phục) trong `BattleManager`, `BattleTrigger` và `PostLossSequence` (gà tự đứng dậy sau thua) là [ĐÃ CÓ], đã chạy qua code path nhưng **chưa thua thật bằng chơi tay**; `BattleManager.firstBattleCompleted` đang là cờ mở khóa các quest NPC (`requireFirstBattleLoss`). Thoại thách đấu và câu "MÀY SỢ À" [CHƯA LÀM].
- **Lệch cần xử lý [CẦN QUYẾT ĐỊNH]:** `NPC_ChildhoodFriend` (14, 0, 35) hiện chỉ có thoại flavor và là điểm nhận hàng của quest giao hàng; đối thủ thực sự là `GuguGaga` (27.6, 0, 19.7) với `BattleTrigger`. Hai đối tượng đang tách rời và cách nhau khoảng 20 m. Cần chốt: GuguGaga là gà của bạn thân, và sân đấu đặt cạnh nhà bạn.

### 4.5 Quest chăm rau
- **Thiết kế:** Player giúp bà tưới rau; **bón nhầm bột protein thay phân bón**; hái rau cho gà ăn; đi ngủ.
- **Hiện trạng:** quest `grandparents_helper` "Cháu ngoan bất đắc dĩ" (nhặt bình tưới, tưới 3 cây, về bà nhận 150 tiền) [ĐÃ CÓ], chỉ được chạy qua code path. Phần bón nhầm protein, hái rau, cho gà ăn, giường ngủ [CHƯA LÀM]. Quest chỉ mở sau trận thua đầu (đúng thứ tự cốt truyện).
- **Đề xuất (giữ nguyên quest đang chạy):** không sửa `grandparents_helper`; thêm một quest nối tiếp (ví dụ `garden_protein`) gồm: bón (nhầm bột protein) → hái rau → cho gà ăn → ngủ. Tái dùng QuestManager/QuestObjectiveTarget/QuestCarryItem; thêm vật phẩm bột protein bằng `ItemDefinition` hiện có.

### 4.6 Biến hình
- **Thiết kế:** sáng hôm sau gà biến thành gà cơ bắp kỳ lạ. **Chỉ xảy ra một lần** (cờ bảo vệ), và chính sự kiện này **mở khóa huấn luyện**.
- **Hiện trạng:** [CHƯA LÀM]. Không có giường/ngủ/chuyển ngày, không có mô hình gà cơ bắp (không tìm thấy asset tên muscle/buff), không có cờ "đã biến hình". **Mâu thuẫn hiện tại:** các trạm huấn luyện đang dùng được ngay khi gà tỉnh dậy, chưa bị khóa bởi biến hình.
- **Placeholder đề xuất:** đổi tỉ lệ/màu mô hình gà hiện có + hiệu ứng khói; thay bằng mô hình thật sau nếu có asset hợp lệ.
- **[CẦN QUYẾT ĐỊNH]:** biến hình chỉ đổi hình dạng + mở khóa (khuyến nghị, để chỉ số do huấn luyện quyết định) hay cộng sẵn chỉ số.

### 4.7 Progression: kiếm tiền → mua → huấn luyện
- **Nguồn tiền hiện có [ĐÃ CÓ]:** ví tiền `CurrencyWallet` + HUD; quest ve chai 100 (+ 104 tiền nhặt đủ 12 món), chăm rau 150, giao hàng 200 → tổng khoảng 554 tiền một lần.
- **Cửa hàng [ĐÃ CÓ], ở `NPC_MarketVillager`:** Bình tưới 60, Thảm tập cho gà 150 (thay Tạ tập; trải ở khu tập, cần cho hít đất), Thức ăn tăng lực 40, Vé số 20 (cào bằng F, có thể trúng tiền), Phân bón protein 80 (rắc cạnh luống rau bằng F, hiệu ứng thật chưa thiết kế). Bình tưới và Thức ăn tăng lực **chưa có hiệu ứng**.
- **Huấn luyện [ĐANG LÀM]** (code và scene có, sửa lần cuối 2026-10-13, `PROJECT_STATUS.md` chưa ghi, chưa được chạy thử khi viết tài liệu này):
  - `ChickenStats`: Sức mạnh, Tấn công, Tốc độ (khởi điểm 10, trần 40) + Máu tối đa suy ra. Gà chưa luyện thì combat **không đổi**.
  - Ảnh hưởng combat qua `PetCombatAI`/`HealthSystem`/`NavMeshAgent` (không sửa script combat): Tấn công +1 sát thương/điểm; Sức mạnh +0.5 sát thương và +5 máu/điểm; Tốc độ +3% tốc độ di chuyển và nhịp đánh/điểm. Đủ 40/40/40: sát thương 10 → 55, máu 100 → 250, tốc độ ×1.9. Trong trận chỉ số sát thương không bị ghi đè (BattleManager vẫn giữ cân bằng trận đầu).
  - 3 trạm ở khu `TrainingArea` (x -15..-1, z 47..57): hít đất (40 tiền, +2 Sức mạnh, **cần Thảm tập đã trải ở khu tập**), máy chạy bộ (50, +2 Tốc độ), bao cát (60, +2 Tấn công); gà thật sự làm động tác bằng Animator hiện có; một phiên 4 giây; không tập khi đang đánh; hủy ngang thì hoàn tiền.
  - Kinh tế: mỗi chỉ số cần 15 phiên để từ 10 lên 40 → 600 / 750 / 900 tiền = 2250, cộng 150 Thảm tập. Tiền từ quest hiện chỉ khoảng 554, nên tiền thưởng giải đấu là nguồn chính để luyện tiếp (đúng ý "dùng tiền tiếp tục huấn luyện").
- **[CHƯA LÀM]:** khóa huấn luyện cho đến khi biến hình; **protein** thành vật phẩm có hiệu ứng (dùng vật phẩm "Thức ăn tăng lực" hay thêm `Item_BotProtein` — [CẦN QUYẾT ĐỊNH]); hiển thị chỉ số gà trên HUD; lưu/tải (chỉ số, tiền, vật phẩm hiện reset khi tắt Play).

### 4.8 Giải đấu NPC
- **Thiết kế:** 2–3 đối thủ NPC, mỗi người có một gà, một đoạn giới thiệu ngắn, một trận, phần thưởng gồm **huy chương** và tiền. Huy chương đổi phần thưởng ở Reward NPC (giữ ý "Medal → Reward NPC → Exchange"). Tiền thưởng được dùng để huấn luyện tiếp.
- **Ứng viên đối thủ [CẦN QUYẾT ĐỊNH]:** `NPC_ChuTu`, `NPC_CoLan`, `NPC_ChiBa`, `NPC_BacHai` đã có trong scene (hiện chỉ có thoại flavor, nằm xa: khoảng 60–125 m khỏi làng; chọn 2–3 người).
- **Hiện trạng:** [CHƯA LÀM]. Chỉ có một đối thủ (`GuguGaga`), `BattleManager` đang gắn cứng một cặp gà mình/địch. Khu `RewardArea` mới có vật thể trang trí (sân khấu, cột, đèn lồng, cờ đỏ placeholder), không có script, NPC hay hệ huy chương. Có icon huy chương trong bộ UI (`Assets/UI/UI/PNG/*/medal1,2.png`), giấy phép chưa kiểm.
- **Chỉ số đề xuất để khởi đầu (chưa cân bằng):** địch 1 máu 120–150 / sát thương 12–15; địch 2 170–200 / 16–20; địch 3 230–270 / 22–28. Tham chiếu: `GuguGaga` hiện máu 150, sát thương 18.

### 4.9 Final boss
- **Thiết kế:** quay lại tái đấu bạn thân cũ khi đã vượt các giải đấu (điều kiện: số huy chương cần có). Đây là cao trào, không chỉ là một trận nữa: thoại trước trận gọi lại "MÀY SỢ À". **Thắng = kết thúc game**: bạn thừa nhận sự tiến bộ, gà ăn mừng, trở về nhà ông bà, kết ngắn hài hước, credits.
- **Chỉ số đề xuất (chưa cân bằng):** máu 300–360, sát thương 30–38, mạnh hơn mọi đối thủ giải đấu nhưng thắng được khi luyện kha khá, không cần luyện đầy.
- **Hiện trạng:** [ĐÃ CÓ] trận boss (M12, `FinalBossController`) và màn kết (M13, `EndingSequence`: gà ăn mừng, về nhà ông bà, thoại hài, credits). Chi tiết và phần chưa kiểm thử tay: PROJECT_STATUS mục 37.
- **Thay đổi so với bản cũ:** bản cũ có một trận "Phục thù" riêng sau khi luyện, rồi mới đến boss cuối. Nay phục thù **chính là** final boss; giải đấu NPC nằm giữa.

## 5. Cờ tiến trình cốt truyện

Mỗi cờ chỉ bật một lần, nằm trong một chỗ duy nhất (đề xuất component nhỏ `StoryProgress`, không phải framework). Chưa có lưu/tải nên hiện chỉ sống trong phiên chơi.

| Cờ | Ý nghĩa | Trạng thái hiện có |
|---|---|---|
| introDone | xem xong intro | [CHƯA LÀM] |
| arrivedHome | tới nhà ông bà (Quest 1 xong) | [CHƯA LÀM] |
| chickenSaved | gà tỉnh và đi theo | [ĐÃ CÓ] `MainChickenAcquire.IsAcquired` |
| firstBattleLost | thua trận đầu | [ĐÃ CÓ] `BattleManager.firstBattleCompleted` (static) |
| gardenDone / proteinFed / slept | chuỗi chăm rau | quest tưới rau [ĐÃ CÓ]; phần còn lại [CHƯA LÀM] |
| chickenTransformed | đã biến hình (một lần) | [CHƯA LÀM] |
| trainingUnlocked | được dùng trạm huấn luyện | [CHƯA LÀM] (hiện không khóa) |
| medals | số huy chương | [CHƯA LÀM] |
| finalBossDefeated | thắng boss | [CHƯA LÀM] |

## 6. Nhân vật và NPC hiện có

- **Ông** (`NPC_Grandfather`), **Bà** (`NPC_Grandmother`, đã là QuestNPC: chăm rau, giao hàng), cả hai ở nhà ông bà.
- **Bạn thân cũ** (`NPC_ChildhoodFriend`): đối thủ đầu tiên, kẻ thù xuyên suốt, final boss. Phải xuất hiện sớm và còn hiện diện giữa game (nhắn tin, đứng xem giải, chọc ghẹo).
- **Dân làng bán hàng** (`NPC_MarketVillager`: cửa hàng); **dân làng nhận quest** (`QuestNPC_Villager`: ve chai).
- **Chưa có vai trò:** `NPC_ChuTu`, `NPC_CoLan`, `NPC_ChiBa`, `NPC_BacHai`; Reward NPC [CHƯA LÀM].
- Dùng lại thân người dựng sẵn + quần áo/phụ kiện, không làm model riêng cho từng NPC.

## 7. Danh sách quest

| Quest | id | Trạng thái |
|---|---|---|
| Tìm đường về nhà ông bà | (mới) | [CHƯA LÀM] |
| Nhận nuôi chú gà | (mới) | [CHƯA LÀM] |
| Thử sức với bạn cũ (trận đầu) | (mới) | [CHƯA LÀM] (trận có sẵn, quest chưa có) |
| Cháu ngoan bất đắc dĩ (tưới rau, +150) | `grandparents_helper` | [ĐÃ CÓ] |
| Rau nhà trồng / bột protein (đề xuất) | (mới) | [CHƯA LÀM] |
| Ve Chai Đại Chiến (10 phế liệu, +100) | `scrap_collector` | [ĐÃ CÓ] |
| Ship hàng làng quê (1 gói, +200) | `village_delivery` | [ĐÃ CÓ] |
| Huấn luyện (khung "Kiếm tiền mua đồ tập" và "Tập luyện") | — | trạm [ĐANG LÀM], quest [CHƯA LÀM] |
| Giải đấu làng (2–3 trận) | (mới) | [CHƯA LÀM] |
| Phục thù — trận đấu cuối cùng | (mới) | [CHƯA LÀM] |

Ba quest [ĐÃ CÓ] giữ nguyên. Quest nhỏ khác chỉ giữ khi củng cố bản demo; không làm hàng chục quest.

## 8. Hệ thống đã có và phải giữ nguyên

Di chuyển/camera Player, combat gà (PetCombatAI, RoosterCombatAI, HealthSystem), thanh máu, đánh thức/nhận gà, BattleManager/BattleTrigger/PostLossSequence, QuestManager và các thành phần quest, CurrencyWallet/MoneyHUD, phế liệu (Scrap), Inventory + Hotbar, DialogueUI/InteractionPromptUI/MessageBannerUI, cửa hàng. Không viết lại nếu không có lỗi chặn. Mọi thay đổi vào combat/BattleManager phải nhỏ nhất có thể và được báo cáo.

## 9. Bản sắc Việt Nam, NPC, tài sản

- Nét Việt nằm ở nhiều chi tiết nhỏ: nhà quê, hàng quán chợ, hàng rào tre, nón lá, bàn thờ, võng, ghế nhựa, cột điện, dây phơi đồ, đèn lồng, biển hiệu tiếng Việt; ruộng lúa, tre, chuối/dừa, ao, mương, đường đất; âm thanh gà gáy, chim, chó, dế/ve, xe máy/xe đạp, nhạc dân gian có giấy phép rõ ràng. Mục tiêu là làng quê cách điệu, không phải làng fantasy chung chung.
- Nguồn asset ưu tiên: Quaternius, Kenney, Poly Haven, CC0 itch.io, OpenGameArt (giấy phép rõ), Pixabay (âm thanh), Sketchfab chỉ khi kiểm từng giấy phép. Ưu tiên CC0; CC BY chỉ khi ghi nguồn. Tránh asset trả phí, asset rip, giấy phép mơ hồ, gói quá lớn, texture 8K vô cớ. Mỗi asset mới phải kiểm giấy phép.

## 10. Nguyên tắc kỹ thuật

Module nhỏ, không script khổng lồ; quest không phụ thuộc đường dẫn đối tượng cứng; tái dùng hệ thống hiện có; không mở rộng phạm vi khi chưa được duyệt; milestone nhỏ, kiểm thử được. Không dùng Destroy/HP giả/tắt tấn công để ép thua. Không Game Over. Không cờ bạc.

## 11. Bản demo thành công

Người chơi: xem intro 2D → vào làng, tìm đường về nhà ông bà → cứu gà → bị thách đấu và thua → làm quest chăm rau, bón nhầm protein, cho gà ăn, đi ngủ → thấy gà biến hình → kiếm tiền, mua dụng cụ → luyện gà bằng máy chạy, hít đất, bao cát, protein và thấy chỉ số tăng → thắng các giải đấu NPC, nhận huy chương và phần thưởng → tái đấu bạn thân cũ → thắng → màn kết.

Phiên bản nhỏ mà trau chuốt hơn bản lớn dang dở.

## 12. Quyết định đang mở

1. Intro là scene riêng hay lớp UI trong `MainGame`; có video thật hay dùng placeholder?
2. Bạn thân cũ và `GuguGaga`: gộp thế nào; sân đấu đặt ở đâu (hiện cách `NPC_ChildhoodFriend` khoảng 20 m)?
3. Biến hình có cộng chỉ số sẵn không (khuyến nghị không).
4. Protein: dùng lại "Thức ăn tăng lực" hay thêm `Item_BotProtein`; protein có hiệu ứng huấn luyện gì?
5. Chọn 2–3 trong 4 NPC (`ChuTu`, `CoLan`, `ChiBa`, `BacHai`) làm đối thủ; khoảng cách xa thì có cần điểm dịch chuyển nhanh không.
6. Cần bao nhiêu huy chương để mở final boss.
7. Mô hình gà cơ bắp: dùng placeholder đổi tỉ lệ, hay tìm asset giấy phép hợp lệ.
8. Có làm lưu/tải không (cờ "một lần" và chỉ số hiện reset khi tắt Play).
