# DEVELOPMENT_ROADMAP.md

Rút gọn 2026-10-28. Đây là kế hoạch / việc còn lại; không phải trạng thái. Trạng thái thật: `PROJECT_STATUS.md`. Thiết kế: `GAME_DESIGN.md`. Nhật ký lịch sử: `PROJECT_HISTORY.md`. Kế hoạch gốc 13/10 (từng mốc M0-M13: phụ thuộc, rủi ro, điều kiện hoàn thành, đề xuất đơn vị commit, bảng đối chiếu cũ) được giữ NGUYÊN VĂN ở `DEVELOPMENT_ROADMAP_ARCHIVE_2026-10-13.md`.

## 0. Quy tắc làm việc
Thứ tự ưu tiên: 1. gameplay chơi được; 2. mạch truyện; 3. hệ thống lõi; 4. bản sắc Việt Nam; 5. UI; 6. âm thanh; 7. trau chuốt; 8. tính năng tùy chọn.

``` text
Kiểm tra → lập kế hoạch thay đổi nhỏ → làm → compile → test → sửa lỗi chặn → báo cáo → dừng
```
- Không tự commit / push. Không viết lại combat, Inventory, Hotbar, shop, quest khi chưa có lỗi chặn; thay đổi vào hệ thống đang chạy phải nhỏ nhất và được báo cáo.
- Một mốc chỉ xong khi: code compile; không lỗi project mới trong Console; chạy trong Play Mode VÀ được chủ dự án chơi tay; gameplay cũ không hỏng; không sửa hệ thống không liên quan; không thêm asset/package không cần; `PROJECT_STATUS.md` cập nhật bằng kết quả đã kiểm chứng, tách bạch 'đã chạy code path' và 'đã chơi tay'. Không gọi một tính năng là xong chỉ vì code đã tồn tại.
- Unity MCP không gửi được phím/chuột thật: mọi thẻ [ĐÃ CÓ] mới được kiểm bằng code path; mỗi mốc cần một lượt chơi tay (checklist: `PROJECT_STATUS.md` mục 7).
- Thẻ: [ĐÃ CÓ], [MỘT PHẦN], [CHƯA LÀM], [CẦN QUYẾT ĐỊNH] (nghĩa như `GAME_DESIGN.md` mục 2).

## 1. Tiến độ các mốc M0-M13
| Mốc | Nội dung | Trạng thái (2026-10-28) |
|---|---|---|
| M0 | Ổn định, chơi tay phần đã có, chốt quyết định | [MỘT PHẦN] chưa có lượt chơi tay đầy đủ; quyết định mở còn lại ở `GAME_DESIGN.md` mục 12 |
| M1 | Cờ tiến trình | [ĐÃ CÓ, theo cách khác] không tạo `StoryProgress`; dùng trạng thái quest + cờ sẵn có (`GAME_DESIGN.md` mục 5) |
| M2 | Intro, spawn ở cổng làng | [ĐÃ CÓ] `IntroScreenController` (màn UI); chưa có video 2D thật |
| M3 | Quest 1 về nhà ông bà | [ĐÃ CÓ] |
| M4 | Cứu gà (xin Ông giữ gà) | [ĐÃ CÓ]; quest giao hàng của Bà mở ngay sau đó |
| M5 | Bạn thân + trận thua đầu | [ĐÃ CÓ] đổi thứ tự: sau giao hàng; thua bằng sát thương thật; 'MÀY SỢ À' + câu đáp của Player |
| M6 | Chăm vườn, protein, ngủ trưa | [ĐÃ CÓ]; video ngủ chưa xác nhận chạy thật |
| M7 | Biến hình một lần, mở khóa luyện | [ĐÃ CÓ] |
| M8 | Kiếm tiền, mua, huấn luyện, chỉ số ảnh hưởng combat | [ĐÃ CÓ] code; cân bằng cần chơi tay; protein / Thức ăn tăng lực / Bình tưới chưa có tác dụng thật |
| M9 | Giải đấu NPC (Chị Ba, Chú Tư) | [ĐÃ CÓ] |
| M10 | Huy chương, Reward NPC | [ĐÃ CÓ] |
| M11 | Bạn thân xuyên suốt (thoại theo tiến trình) | [ĐÃ CÓ] |
| M12 | Final boss: tái đấu cuối | [ĐÃ CÓ] |
| M13 | Màn kết + credits | [ĐÃ CÓ]; credits placeholder; chỉ nút Thoát |
Chi tiết triển khai từng mốc: `PROJECT_HISTORY.md` (tìm theo tiêu đề; chỉ mục ở cuối `PROJECT_STATUS.md`).

## 2. Việc đang mở / tiếp theo (theo thứ tự)
1. Chủ dự án chơi tay toàn chuỗi (`PROJECT_STATUS.md` mục 7) và báo lỗi.
2. Chốt các quyết định ở `GAME_DESIGN.md` mục 12 (mở quest kiếm tiền/luyện sớm hơn hay không, cân bằng tái đấu, tác dụng gameplay của vật phẩm, intro video, menu/lưu-tải, đối thủ thứ ba).
3. Bản đồ: xác nhận trong Game view Terrain mới và việc không còn root `VillageMap` (`PROJECT_STATUS.md` mục 6.6).
4. Yêu cầu đã nhận nhưng CHƯA làm, đổi luồng tái đấu: tìm lại gà ở phòng tập → tái đấu bạn thân và BẮT BUỘC thua theo kịch bản (sát thương thật, không HP giả) → bạn khuyên 'Đánh bại Chị Ba và Chú Tư rồi quay lại đấu với tao!' → mở quest giải đấu; chỉ khi thắng cả hai mới mở khóa tái đấu cuối. Hiện tại: tái đấu thắng được → bạn đi vắng tới khi đủ 2 huy chương; quest kiếm tiền/luyện mở sau tái đấu + Bà trả công; boss mở ở 2 huy chương. Làm: đọc `RematchController`, `FinalBossController`, `TournamentManager` và dùng lại quest sẵn có, không tạo logic trùng; test cả luồng; cập nhật status.
5. Sau đó mới polish (mục 3).

## 3. Các luồng trau chuốt (không chặn đường chính)
- **P1 Môi trường Việt Nam:** nhà, quán chợ, hàng rào tre, nón lá, võng, bàn thờ, ghế nhựa, cột điện, đèn lồng, biển hiệu, ruộng lúa, tre/chuối/dừa, ao, đường đất. Không thay toàn bộ môi trường chỉ để làm đẹp.
- **P2 Âm thanh:** ambience làng quê, gà, chim, chó, côn trùng; đánh/trúng/gục; nhặt đồ, hoàn thành quest, tiền, shop, huấn luyện, huy chương; nhạc intro/làng/huấn luyện/trận/chiến thắng/boss/kết. Giấy phép rõ ràng. (`GameAudio` đã có; còn thiếu: click UI, xu, gà kêu khi đi/bị đánh, âm thanh luyện tập, cổ vũ.)
- **P3 UI tối thiểu:** quest, mục tiêu, tiền, chỉ số gà (HP/Sức mạnh/Tấn công/Tốc độ), nhắc [E], shop, huấn luyện, huy chương. (HUD chỉ số chi tiết chưa làm.)
- **P4 Dọn dẹp cuối:** lỗi Console, script/tham chiếu thiếu, vật liệu, animation, đối tượng trùng (`Rooster`, `ChienKe_blend` đang tắt, các cảnh `_Recovery`, prefab `HealthBarUI (1)`), file scratch (`Assets/Screenshots`, `Captures/`), NavMesh, collider, âm thanh, scale UI. Không tự xóa asset khả nghi.
- **P5 Phiên chơi cuối:** chạy được không cần can thiệp editor:
``` text
INTRO → CỔNG LÀNG → QUEST 1 → NHÀ ÔNG BÀ → CỨU GÀ → GIAO HÀNG → TRẬN ĐẦU → THUA
→ CHĂM VƯỜN / PROTEIN → NGỦ → BIẾN HÌNH → TÌM GÀ → TÁI ĐẤU → KIẾM TIỀN → MUA DỤNG CỤ → HUẤN LUYỆN
→ GIẢI ĐẤU NPC → HUY CHƯƠNG / REWARD NPC → FINAL BOSS → THẮNG → KẾT
```
- Asset (thứ tự lấy, không tải hết một lần): A mạch truyện (video/ảnh intro, thân NPC, nhà Việt, props Việt); B kinh tế (phế liệu đã có); C huấn luyện (máy chạy đã có `Assets/Art/UI/Training/treadmill.fbx`, tạ, bao cát, mô hình gà có rig cho ROCKY); D âm thanh; E trau chuốt (props, quần áo, VFX). Mỗi asset mới phải kiểm giấy phép (và điền vào credits).

## 4. Phạm vi và cắt phạm vi
- Mục tiêu: 1 Player, 1 gà chính, 1 bạn thân cũ (đối thủ đầu và boss cuối), 2-3 đối thủ giải đấu, ông, bà, 1 NPC cửa hàng, 1 Reward NPC, vài NPC làng, nhà ông bà, khu huấn luyện, cửa hàng, 1-2 sân đấu, ~8-12 quest chính, 3 hoạt động huấn luyện, kinh tế đơn giản, huy chương/phần thưởng, final boss, màn kết.
- Cắt trước khi gấp: NPC thừa, đồ trang trí, hoạt động huấn luyện thêm, quest phụ, môi trường thêm, boss thêm, đối thủ thứ ba, video thật (dùng placeholder). GIỮ: intro (dù placeholder), nhà ông bà, cứu gà, bạn thân cũ và trận thua đầu, chăm vườn + biến hình, tiền, ít nhất một trạm huấn luyện, ít nhất một đối thủ giải có huy chương, final boss, màn kết.

## 5. Luật cuối
Ưu tiên ít tính năng chạy được từ đầu đến cuối hơn nhiều tính năng dang dở.
