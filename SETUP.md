# Hướng dẫn clone & mở project CuocKe

## 1. Yêu cầu
- **Unity 6000.5.8f1** (cài đúng bản này qua Unity Hub, KHÔNG mở bằng bản thấp hơn như 6000.4.x).
- Git cho Windows, bật đường dẫn dài: `git config --global core.longpaths true`
- Clone vào thư mục ngắn, ví dụ `D:\CuocKe` (tránh đường dẫn quá 260 ký tự).

## 2. Clone đúng nhánh
```
git clone https://github.com/Rikokyu/CuocKe.git
cd CuocKe
git checkout feat/map
git pull
```
Kiểm tra: `git log -1` phải thấy commit mới nhất của nhánh `feat/map`.

## 3. Mở project
- Mở bằng Unity Hub, chọn Unity 6000.5.8f1. Lần đầu Unity sẽ tự tải package theo `Packages/manifest.json` và import lại asset (chờ vài phút).
- Package `com.danielgruginski.medievalkit` nằm sẵn trong thư mục `Packages/` (embedded), không cần import thêm.
- Mở scene `Assets/Scenes/MainMenu.unity` hoặc `MainGame.unity`.

## 4. Nếu bị hồng / Missing Prefab
1. Đóng Unity, xoá thư mục `Library/` rồi mở lại.
2. Kiểm tra Console xem package nào lỗi resolve (Window > Package Manager).
3. Edit > Project Settings > Graphics: Default Render Pipeline phải là `Assets/Settings/PC_RPAsset`.
4. Đảm bảo đúng Unity 6000.5.8f1.

## 5. Lưu ý
- Luôn commit cả file `.meta`.
- Không để script/prefab đang dùng trong thư mục `Delete_Review` hoặc `_Recovery`.
