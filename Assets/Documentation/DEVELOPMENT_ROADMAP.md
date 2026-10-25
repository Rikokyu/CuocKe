# DEVELOPMENT_ROADMAP.md

Cập nhật 2026-10-13 theo **cốt truyện chính thức v2** (`GAME_DESIGN.md`). Tài liệu này chỉ lập kế hoạch: **chưa triển khai cốt truyện trong lượt cập nhật này**. Trạng thái thực tế luôn lấy từ `PROJECT_STATUS.md` và từ project.

## 0. Quy tắc

Thứ tự ưu tiên: 1. gameplay chơi được; 2. mạch truyện; 3. hệ thống lõi; 4. bản sắc Việt Nam; 5. UI; 6. âm thanh; 7. trau chuốt; 8. tính năng tùy chọn.

Quy trình mỗi lượt làm:

``` text
Kiểm tra → lập kế hoạch thay đổi nhỏ → làm → compile → test → sửa lỗi chặn → báo cáo → dừng
```

- Không tự commit / push.
- Không viết lại combat, Inventory, Hotbar, shop, quest khi chưa có lỗi chặn. Thay đổi vào hệ thống đang chạy phải nhỏ nhất có thể và được báo cáo.
- Không gọi một tính năng là xong chỉ vì code đã tồn tại. Xong = chạy được trong Play Mode, Console sạch, hệ thống cũ không hỏng, `PROJECT_STATUS.md` đã cập nhật.
- Thẻ trạng thái: **[ĐÃ CÓ]**, **[ĐANG LÀM]**, **[CHƯA LÀM]**, **[CẦN QUYẾT ĐỊNH]** (nghĩa như trong `GAME_DESIGN.md` mục 2).
- Lưu ý kiểm thử: Unity MCP không gửi được phím/chuột thật. Mọi thẻ [ĐÃ CÓ] dưới đây mới được kiểm bằng code path; mỗi mốc cần một lượt chơi tay của chủ dự án.

## 1. Đối chiếu hiện trạng (đọc project ngày 2026-10-13)

| Hạng mục theo cốt truyện | Trạng thái | Ghi chú |
|---|---|---|
| Intro 2D | [CHƯA LÀM] | không có VideoClip/Timeline/PlayableDirector; chỉ một scene `MainGame` |
| Spawn ở đầu làng | [CHƯA LÀM] | Player đang xuất phát gần nhà ông bà |
| Quest 1 Tìm đường về nhà | [CHƯA LÀM] | chưa có asset quest, vùng đích, chỉ dẫn |
| Hạ tầng quest (QuestManager, QuestNPC, HUD, Feedback, CarryItem, ObjectiveTarget) | [ĐÃ CÓ] | 3 quest chạy qua code path |
| Gà tỉnh dậy, đi theo | [ĐÃ CÓ] | `MainChickenAcquire`; chưa ai xem hình ảnh |
| Thoại Ông định làm thịt gà / xin giữ | [CHƯA LÀM] | `NPC_Grandfather` chỉ có thoại flavor |
| Trận đầu thua theo cân bằng, gà hồi sinh | [ĐÃ CÓ] | `BattleManager`, `BattleTrigger`, `PostLossSequence`; chưa thua thật bằng tay |
| Bạn thân cũ thách đấu, câu "MÀY SỢ À" | [CHƯA LÀM] | `NPC_ChildhoodFriend` và `GuguGaga` đang tách rời, cách nhau ~20 m |
| Quest tưới rau (+150) | [ĐÃ CÓ] | `grandparents_helper` |
| Bón nhầm protein, hái rau, cho gà ăn, ngủ | [CHƯA LÀM] | |
| Biến hình một lần | [CHƯA LÀM] | không có mô hình gà cơ bắp, không có cờ, không có giường |
| Tiền, HUD tiền | [ĐÃ CÓ] | `CurrencyWallet` (`Add`, `TrySpend`) |
| Quest kiếm tiền (ve chai, giao hàng) | [ĐÃ CÓ] | `scrap_collector` 100, `village_delivery` 200 |
| Inventory + Hotbar | [ĐÃ CÓ] | hai kho riêng chưa đồng bộ (lưới cũ và hotbar) |
| Cửa hàng dụng cụ | [ĐÃ CÓ] | bình tưới 60, thảm tập 150, thức ăn tăng lực 40, vé số 20, phân bón protein 80; bình tưới và thức ăn tăng lực chưa có hiệu ứng |
| `ChickenStats` + 3 `TrainingStation` (hít đất, máy chạy, bao cát) | [ĐANG LÀM] | có code và scene, sửa 2026-10-13, chưa vào `PROJECT_STATUS.md`, chưa kiểm chứng |
| Khóa huấn luyện cho đến khi biến hình | [CHƯA LÀM] | trạm hiện dùng được ngay |
| Protein có hiệu ứng | [CHƯA LÀM] | |
| HUD chỉ số gà | [CHƯA LÀM] | |
| Đối thủ giải đấu NPC | [CHƯA LÀM] | chỉ có `GuguGaga`; `BattleManager` gắn cứng một cặp |
| Huy chương, Reward NPC | [CHƯA LÀM] | `RewardArea` mới có trang trí placeholder, không script |
| Final boss, màn kết | [CHƯA LÀM] | |
| Lưu / tải | [CHƯA LÀM] | tiền, chỉ số, vật phẩm, cờ đều reset khi tắt Play |

Vấn đề đã biết cần nhớ (xem `PROJECT_STATUS.md` mục 15): `stoppingDistance` của gà là 10 trong khi tầm đánh là 1; `PlayerMovement` và phím E dùng Input cũ; `PROJECT_STATUS.md` có mục đánh số trùng và hai mục (11, 12) bị cắt; font TMP có thể thiếu một số glyph tiếng Việt (mục 23 và 25 của status mâu thuẫn nhau, cần nhìn thực tế trong Game view).

## 2. Chuỗi phụ thuộc

``` text
M0 ──► M1 ──► M2 ──► M3 ──► M4 ──► M5 ──► M6 ──► M7 ──► M8 ──► M9 ──► M10 ──► M12 ──► M13
       (cờ)  (intro) (quest1)(cứu gà)(thua) (rau)  (biến) (luyện)(giải) (huy chương)(boss) (kết)
                                         └── chạy song song từ M5: M11 (bạn thân xuất hiện lại)
Polish P1..P5 xen kẽ, không chặn đường chính.
```

Phụ thuộc quan trọng:
- **M7** (biến hình) phải có trước khi huấn luyện được coi là hợp lệ: hiện `TrainingStation` chưa bị khóa.
- **M9** (giải đấu) cần mở rộng `BattleManager` để chọn đối thủ. Đây là thay đổi vào hệ thống combat ổn định, rủi ro lớn nhất của cả kế hoạch.
- **M10, M12** cần cờ `medals` và `finalBossDefeated` từ M1.
- Mọi mốc cần lượt chơi tay ở cuối.

## 3. Các mốc (theo đúng thứ tự cốt truyện)

### M0 — Ổn định và chốt quyết định (không thêm tính năng)
- Chơi tay toàn bộ phần đã có: E đánh thức → đi theo → vào vùng đấu → thua thật → gà hồi sinh → nhận quest bà → tưới rau → quay lại nhận tiền → ve chai → giao hàng → cửa hàng → 3 trạm huấn luyện.
- Chốt lỗi `stoppingDistance` của gà (cần chủ dự án quyết).
- Kiểm chứng và ghi `ChickenStats`/`TrainingStation` vào `PROJECT_STATUS.md`; dọn đánh số mục bị trùng/cắt.
- Chốt các **quyết định mở** ở `GAME_DESIGN.md` mục 12 (ít nhất 1, 2, 3, 4 trước M2–M7).
- **Hoàn thành khi:** có danh sách lỗi chặn thật từ lượt chơi tay; status khớp project.

### M1 — Cờ tiến trình cốt truyện (nền tảng nhỏ)
- Một component nhỏ (ví dụ `StoryProgress`): cờ `introDone`, `arrivedHome`, `chickenTransformed`, `trainingUnlocked`, `medals`, `finalBossDefeated` + sự kiện thay đổi. Các cờ đã có (`MainChickenAcquire.IsAcquired`, `BattleManager.firstBattleCompleted`, trạng thái QuestManager) **được đọc lại, không thay thế**.
- Mỗi cờ chỉ bật một lần; chỉ sống trong phiên chơi (chưa có lưu/tải).
- **Phụ thuộc:** M0. Có thể làm muộn nhất ngay trước M7 nếu M2–M6 dùng được trạng thái quest.
- **Rủi ro:** thêm một nguồn sự thật thứ hai song song với cờ static cũ; phải đọc, không sao chép.

### M2 — Intro 2D và vào gameplay tại đầu làng
- Video 2D (hoặc placeholder ảnh tĩnh + chữ + fade) phát trên UI; có nút bỏ qua; Player bị khóa điều khiển khi phát; hết video thì đặt Player tại điểm spawn đầu làng, trả cursor/camera về bình thường.
- **Tái dùng:** UI Canvas, PlayerMovement/MouseMovement (chỉ bật/tắt, không sửa).
- **Phụ thuộc:** video hoặc placeholder; quyết định 1 (scene riêng hay lớp UI); điểm spawn đầu làng cần được đặt trong scene.
- **Rủi ro:** codec và dung lượng video; bản quyền video; tắt/bật điều khiển không được làm rò trạng thái.
- **Hoàn thành khi:** bấm Play → intro → Player đứng ở đầu làng và đi lại được; không lỗi Console.

### M3 — Quest 1: Tìm đường về nhà ông bà
- `QuestDefinition` mới + vùng đích quanh nhà ông bà (tự báo tiến độ khi bước vào) + chỉ dẫn đường nhẹ.
- **Tái dùng:** QuestManager/HUD/Feedback. Cần thêm một khóa mục tiêu mới (thay đổi nhỏ trong `QuestManager`), và có thể một script vùng trigger rất nhỏ.
- **Phụ thuộc:** M2 (điểm xuất phát).
- **Hoàn thành khi:** hoàn thành chỉ một lần, HUD đúng, không tính hai lần.

### M4 — Cứu gà
- Thoại Ông (định làm thịt gà thường) → lựa chọn xin giữ lại → Ông đồng ý → gà tỉnh và đi theo; quest "Nhận nuôi chú gà".
- **Tái dùng:** `DialogueUI`, `QuestNPC`, `MainChickenAcquire`/`PetCombatAI` (gọi lại luồng đánh thức có sẵn, hoặc để dialogue mở khóa phím E; không viết lại).
- **Phụ thuộc:** M3; kiểm tra `MainChickenAcquire` có điểm vào công khai phù hợp không.
- **Rủi ro:** `DialogueUI` hiện là một dòng + một nút; nếu cần lựa chọn "xin giữ lại / không" thì phải mở rộng nhỏ hoặc dùng chuỗi dòng + một nút xác nhận.
- **Hoàn thành khi:** chỉ nhận gà một lần; gà đi theo; chưa có lối tắt bỏ qua.

### M5 — Nhà bạn thân cũ và trận thua đầu
- Chốt vị trí và quan hệ giữa `NPC_ChildhoodFriend` và `GuguGaga` (quyết định 2); bạn nhận ra Player, chê gà, thách đấu (quest "Thử sức với bạn cũ"); trận mở bằng cơ chế sẵn có; sau thua và hồi sinh, bạn nói **“Bữa nào tái đấu không? MÀY SỢ À”**; mở khóa các quest NPC bằng cờ có sẵn.
- **Tái dùng:** `BattleManager`, `BattleTrigger`, `PostLossSequence`, cân bằng trận đầu, `QuestNPC`.
- **Phụ thuộc:** M4 (gà phải đi theo trước khi trận bắt đầu, điều kiện này `BattleTrigger` đã kiểm).
- **Rủi ro:** `NPC_ChildhoodFriend` còn là điểm nhận hàng của `village_delivery`; thay đổi vai trò không được làm hỏng quest giao hàng. Không ép thua bằng HP giả hay tắt tấn công.
- **Hoàn thành khi:** thua thật bằng chơi tay, câu thoại xuất hiện đúng một lần, quest bà mở ra.

### M6 — Chăm rau: bón nhầm protein, hái rau, cho gà ăn, đi ngủ
- Giữ nguyên `grandparents_helper`; thêm quest nối tiếp: bón (nhầm bột protein) → hái rau → cho gà ăn → đi ngủ. Vật phẩm bột protein bằng `ItemDefinition`; giường + màn tối chuyển sang "Sáng hôm sau".
- **Tái dùng:** `QuestObjectiveTarget`, `QuestCarryItem`, bãi cây hiện có, Inventory/Hotbar, `ItemPickup`.
- **Phụ thuộc:** M5; quyết định 4 (protein).
- **Rủi ro:** không chạm kho lưới cũ của đồng đội; không trả tiền hai lần; ba luống cây đã dùng cho quest tưới, cần đặt lại trạng thái có chủ đích.
- **Hoàn thành khi:** chuỗi hoàn thành đúng một lần, không còn tiến độ lệch.

### M7 — Biến hình một lần và mở khóa huấn luyện
- Sau khi ngủ: sáng hôm sau, gà biến thành gà cơ bắp (placeholder: đổi tỉ lệ/màu + hiệu ứng, thay bằng asset thật khi có). Cờ `chickenTransformed` chặn lần thứ hai. `TrainingStation` kiểm cờ `trainingUnlocked`.
- **Tái dùng:** `ChickenStats`, `TrainingStation`. Một thay đổi nhỏ vào điều kiện từ chối của `TrainingStation` (file đang được làm; phải trao đổi với người phụ trách).
- **Phụ thuộc:** M6, M1, quyết định 3 và 7.
- **Rủi ro:** đổi tỉ lệ mô hình ảnh hưởng collider/NavMeshAgent; phải kiểm gà vẫn đi theo và đánh nhau bình thường sau biến hình.
- **Hoàn thành khi:** biến hình đúng một lần, trước đó trạm từ chối, sau đó trạm chạy.

### M8 — Progression: kiếm tiền, mua, huấn luyện, chỉ số ảnh hưởng combat
- Kiểm chứng đầu-cuối `ChickenStats` + 3 trạm bằng chơi tay (gà thật làm động tác, chỉ số tăng, tiền trừ một lần, hủy thì hoàn tiền).
- Chứng minh ảnh hưởng combat: so sánh gà chưa luyện và gà đã luyện trước cùng một đối thủ (thời gian hạ địch, máu còn lại).
- Làm cho protein có hiệu ứng (quyết định 4); HUD chỉ số gà; cân bằng giá/phần thưởng với con số ở `GAME_DESIGN.md` 4.7 (cần ~2250 tiền để luyện đầy ba chỉ số, quest hiện chỉ cho ~554).
- **Tái dùng:** `ChickenStats`, `TrainingStation`, `ShopUI`, `CurrencyWallet`, `HotbarUI`.
- **Phụ thuộc:** M7.
- **Rủi ro:** `ChickenStats` ghi `combat.damage` ngoài trận trong khi `BattleManager` ghi đè trong trận; kiểm lại thứ tự khôi phục sau trận đầu và các trận sau.
- **Hoàn thành khi:** có số liệu trước/sau; không lỗi Console; shop không mất tiền oan.

### M9 — Giải đấu NPC (2–3 đối thủ)
- `OpponentDefinition` (ScriptableObject: tên, chỉ số gà, phần thưởng tiền, huy chương, thoại giới thiệu); mỗi đối thủ có `RoosterCombatAI`/`HealthSystem` đã tinh chỉnh; mỗi NPC có thoại giới thiệu và thách đấu.
- **Thay đổi bắt buộc vào hệ thống combat:** `BattleManager` cần chọn đối thủ hiện tại thay vì gắn cứng một cặp. Chỉ thay đổi tối thiểu; cân bằng trận đầu (gà mình sát thương 1, địch 15) vẫn chỉ áp dụng cho trận đầu.
- **Phụ thuộc:** M8 (người chơi phải đủ khả năng thắng), quyết định 5; `PostLossSequence` chỉ chạy một lần (`HasStarted`), nên cần cách xử lý thua ở các trận sau (đứng dậy, thử lại, không Game Over).
- **Rủi ro:** lớn nhất của kế hoạch (xem mục 2); phải chạy lại trận đầu để chắc không vỡ.
- **Hoàn thành khi:** đánh bại 2 đối thủ liên tiếp, mỗi người thưởng đúng một lần.

### M10 — Huy chương và Reward NPC
- Đếm huy chương (cờ `medals` hoặc vật phẩm), Reward NPC đổi huy chương lấy phần thưởng (tiền/vật phẩm/mở khóa), dùng lại khu `RewardArea` đã có trang trí.
- **Tái dùng:** `ItemDefinition`, `CurrencyWallet`, `DialogueUI`/UI cửa hàng; icon huy chương (kiểm giấy phép).
- **Phụ thuộc:** M9.
- **Hoàn thành khi:** mỗi huy chương chỉ đổi một lần; không có yếu tố cá cược.

### M11 — Bạn thân cũ xuất hiện xuyên suốt (làm song song từ M5)
- Thoại của bạn thay đổi theo cờ (sau trận thua, sau biến hình, sau mỗi giải đấu); có mặt xem giải. Chi phí thấp, chủ yếu dữ liệu thoại.

### M12 — Final boss: tái đấu bạn thân cũ
- Quest "Phục thù — trận đấu cuối cùng" mở khi đủ huy chương (quyết định 6). Gà của bạn mạnh hơn mọi đối thủ giải đấu; thoại trước trận nhắc lại "MÀY SỢ À"; thua thì thử lại, không Game Over; thắng đặt `finalBossDefeated`.
- **Tái dùng:** cơ chế trận từ M9, cùng kiến trúc combat.
- **Phụ thuộc:** M9, M10, M11.

### M13 — Màn kết
- Bạn thừa nhận sự tiến bộ, gà ăn mừng, trở về nhà ông bà, kết ngắn hài hước, credits. Sau credits quyết định trạng thái cuối (quay về menu/thoát; project chưa có menu).
- **Phụ thuộc:** M12.
- **Hiện trạng (2026-10-22):** đã làm và kiểm tra Play Mode (`EndingSequence`), chưa test tay; trạng thái sau credits = chỉ nút Thoát (chưa có menu). Xem PROJECT_STATUS mục 37.

## 4. Các luồng trau chuốt (không chặn đường chính)

- **P1 Môi trường Việt Nam:** nhà, quán chợ, hàng rào tre, nón lá, võng, bàn thờ, ghế nhựa, cột điện, đèn lồng, biển hiệu, ruộng lúa, tre/chuối/dừa, ao, đường đất. Không thay toàn bộ môi trường chỉ để làm đẹp.
- **P2 Âm thanh:** ambience làng quê, gà, chim, chó, côn trùng; đánh/trúng/gục; nhặt đồ, hoàn thành quest, tiền, shop, huấn luyện, huy chương; nhạc intro/làng/huấn luyện/trận/chiến thắng/boss/kết. Giấy phép rõ ràng.
- **P3 UI tối thiểu:** quest, mục tiêu, tiền, chỉ số gà (HP/Sức mạnh/Tấn công/Tốc độ), nhắc [E], shop, huấn luyện, huy chương.
- **P4 Dọn dẹp cuối:** lỗi Console, script/tham chiếu thiếu, vật liệu, animation, đối tượng trùng (`Rooster` và `ChienKe_blend` đang tắt, các cảnh `_Recovery`), file scratch (ảnh trong `Assets/Screenshots`), NavMesh, collider, âm thanh, scale UI. Không tự xóa asset khả nghi.
- **P5 Phiên chơi cuối:** bắt buộc chạy được không cần can thiệp editor:

``` text
INTRO → ĐẦU LÀNG → QUEST 1 → NHÀ ÔNG BÀ → CỨU GÀ → NHÀ BẠN → TRẬN ĐẦU → THUA
→ CHĂM RAU / PROTEIN → NGỦ → BIẾN HÌNH → KIẾM TIỀN → MUA DỤNG CỤ → HUẤN LUYỆN
→ GIẢI ĐẤU NPC → HUY CHƯƠNG / REWARD NPC → TÁI ĐẤU BẠN CŨ → THẮNG → KẾT
```

## 5. Thứ tự lấy asset (không tải hết một lần)

- **A Mạch truyện:** video/ảnh intro, mô hình bạn thân cũ, thân NPC dựng sẵn, nhà Việt, props Việt, làng.
- **B Kinh tế:** phế liệu (đã dùng asset sẵn có), hộp/rác.
- **C Huấn luyện:** máy chạy bộ (đã có `Assets/Art/UI/Training/treadmill.fbx`), tạ, bao cát, vật phẩm protein, mô hình gà cơ bắp.
- **D Âm thanh.** **E Trau chuốt:** props, quần áo, VFX, trang trí.
- Mỗi asset mới phải kiểm giấy phép; tránh gói lớn không cần.

## 6. Đề xuất đơn vị commit (khi chủ dự án cho phép)

``` text
docs: update main story and roadmap
feat: add story progress flags
feat: add 2D intro and village-entrance start
feat: add find-the-way-home quest
feat: add chicken rescue dialogue
feat: add friend challenge and first-loss line
feat: add garden protein quest and bed
feat: add one-time chicken transformation and training unlock
feat: verify and tune chicken training and stats HUD
feat: add NPC tournament opponents
feat: add medals and reward NPC
feat: add final boss rematch
feat: add ending
polish: vietnamese environment / audio / vfx
fix: gameplay issues
```

## 7. Định nghĩa hoàn thành một mốc

Code compile; không lỗi project mới trong Console; tính năng chạy trong Play Mode **và** được chủ dự án chơi tay; gameplay cũ vẫn chạy; không sửa hệ thống không liên quan; không thêm asset/package không cần; `PROJECT_STATUS.md` được cập nhật bằng kết quả đã kiểm chứng, tách bạch "đã chạy code path" với "đã chơi tay".

## 8. Phạm vi mục tiêu

1 Player, 1 gà chính, 1 bạn thân cũ (đối thủ đầu và final boss), 2–3 đối thủ giải đấu, ông, bà, 1 NPC cửa hàng, 1 Reward NPC, vài NPC làng, 1 khu làng chính, nhà ông bà, khu huấn luyện, cửa hàng, 1–2 sân đấu, khoảng 8–12 quest chính, 3 hoạt động huấn luyện (+ protein), kinh tế đơn giản, huy chương/phần thưởng, final boss, màn kết.

## 9. Cắt phạm vi khi gấp

Cắt trước: NPC thừa, đồ trang trí, hoạt động huấn luyện thêm, quest phụ, môi trường thêm, boss thêm, đối thủ giải đấu thứ ba, video thật (dùng placeholder). **Giữ:** intro (dù placeholder), nhà ông bà, cứu gà, bạn thân cũ và trận thua đầu, chăm rau + biến hình, tiền, ít nhất một trạm huấn luyện, ít nhất một đối thủ giải đấu có huy chương, final boss, màn kết.

## 10. Luật cuối

Ưu tiên ít tính năng chạy được từ đầu đến cuối hơn nhiều tính năng dang dở.
