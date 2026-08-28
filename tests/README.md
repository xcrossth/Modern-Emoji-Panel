# Automated tests

โฟลเดอร์นี้เป็นรากของ test projects ที่ใช้ร่วมกันใน monorepo ส่วน regression gate ปัจจุบันอยู่ที่:

- `scripts/verify-foundation.ps1` สำหรับ build/publish และ WPF smoke
- `scripts/test-clean-checkout.ps1` สำหรับ foundation จาก checkout ใหม่
- `scripts/verify-generated-emoji-baseline.ps1` สำหรับ source checksum, full Emoji 17 coverage และ generator determinism
- `scripts/verify-noto-grid.ps1` สำหรับ category/asset coverage, Noto lazy decode, DPI, cache และ failure states
- `scripts/verify-safe-insertion.ps1` สำหรับ Insertion Mode, target validation และ clipboard restore rules โดยไม่ส่ง input จริง
- `scripts/verify-search-preview.ps1` สำหรับการค้นชื่อ/keyword ไทย–อังกฤษ, match tiers, deterministic order, accessibility และ Noto 512 Hover Preview
- `scripts/verify-emoji-variants.ps1` สำหรับสีผิวเริ่มต้น, Variant Override แบบหนึ่งครั้ง และการเข้าถึง fully-qualified sequence ทุกตัว
- `scripts/verify-picker-session.ps1` สำหรับ Browse/Search Mode, Commit Gesture, dismissal/focus policy, placement, ขนาดหน้าต่าง และ accessibility state
- `scripts/verify-activity-data.ps1` สำหรับ Recent MRU, Learned Ranking, schema/migration, atomic persistence, corruption recovery และคำสั่งล้าง Activity Data
- `scripts/verify-settings-privacy.ps1` สำหรับ Settings model เดียว, ภาษา fallback, theme/hotkey/insertion choices, advanced reset, Welcome และ privacy-safe logging

เมื่อเพิ่ม test project ให้เพิ่มเข้า `ModernEmojiPanel.sln` และเก็บ NuGet lock file ของ project นั้นใน Git
