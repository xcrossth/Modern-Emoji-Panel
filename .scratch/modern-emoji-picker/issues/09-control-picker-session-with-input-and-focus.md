# 09: ควบคุม Picker Session ด้วย keyboard, pointer และ focus

**What to build:** ทำให้ Picker Session มี Browse Mode และ Search Mode ที่คาดเดาได้ พร้อม Commit Gestures, dismissal, window placement และ focus behavior ที่พาผู้ใช้กลับไปยังแอปเป้าหมายอย่างถูกต้อง

**Blocked by:** 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview; 08: ส่ง Emoji หนึ่งรายการไปยังแอปเป้าหมายอย่างปลอดภัย

**Status:** resolved

- [x] Picker เปิดใน Browse Mode แบบ pointer-first; ทุก non-modifier key ยกเว้น Esc handoff กลับ target และไม่ใช้ keyboard selection ใน Browse
- [x] click ส่งแล้วคง Picker แบบ visible; Enter ส่งแล้ว dismiss และ Shift+Enter ส่งแล้วคง Picker เมื่ออยู่ใน Search Mode
- [x] หลัง click หรือ Shift+Enter Picker ไม่ดับ–ติดและกลับมา active โดยคง selection, query, category และ scroll เดิม
- [x] Esc ใน Search Mode ครั้งแรกกลับ Browse Mode และ Esc ถัดไป dismiss; Esc ใน Browse Mode dismiss ได้ทันที
- [x] close button และ click ภายนอกจริง dismiss ได้ โดย click ภายนอกเคารพ focus ของหน้าต่างที่ผู้ใช้คลิกและไม่แย่งกลับ
- [x] Picker เปิดใกล้ text caret หรือ fallback กลางหน้าต่างเป้าหมายบน monitor เดียวกัน พร้อม clamp ใน working area
- [x] หน้าต่างปรับขนาดและจำขนาดได้ รองรับ multi-monitor/DPI และการกด hotkey ซ้ำขณะเปิดไม่เปิด Windows panel ซ้อน
- [x] focus, selection, busy และ error states ถูกประกาศให้ accessibility API ใช้งานได้

## Answer

เพิ่ม state model ของ Picker Session ที่แยก Browse/Search Mode, Commit Gesture, visibility ระหว่าง insertion และ dismissal focus policy ออกจาก WPF shell แล้ว Browse ใช้ pointer-first และ handoff physical key กลับ target ส่วน click/Shift+Enter ที่ดำเนิน session ต่อคงหน้าต่าง visible ระหว่าง target activation จึงไม่เกิดภาพดับ–ติด ส่วน click ภายนอก dismiss โดยไม่ activate target เดิมซ้ำ

การวางหน้าต่างใช้ text caret ก่อน แล้ว fallback กึ่งกลางหน้าต่างเป้าหมายบน monitor เดียวกัน พร้อม clamp ใน working area ตาม DPI หน้าต่างปรับขนาดได้และเก็บขนาดใน settings ขณะที่ query/category/ตำแหน่งไม่ถูกนำข้าม session การประกาศสถานะใช้ UI Automation live region, ItemStatus, accessible name และ focus indicator

หลักฐานการตรวจสอบ:

- `scripts/verify-picker-session.ps1`: ผ่าน 14 policy checks และ WPF wiring checks โดยไม่ส่ง input จริง
- `scripts/verify-foundation.ps1 -SkipPublish`: ผ่านด้วย .NET SDK 10.0.400, build 0 warnings/errors และ WPF smoke
- `scripts/verify-safe-insertion.ps1 -SkipBuild`: ผ่าน 18 policy checks
- `scripts/verify-search-preview.ps1 -SkipBuild`: ผ่าน 3,944 entries และ 100 searches
- `scripts/verify-noto-grid.ps1 -SkipBuild`: ผ่าน 3,944 entries, 9 categories และ DPI 100–250%
- `scripts/verify-emoji-variants.ps1 -SkipBuild`: ผ่าน 3,944 baseline sequences

ข้อจำกัดการตรวจ: ไม่ใช้ screenshot helper ตามข้อจำกัด Windows 10 build 19045 ของ repository การคลิก desktop จริงและ screen reader matrix จะถูกรวมใน qualification ticket 13; Ticket นี้ตรวจ transition/focus policy ผ่าน pure smoke seam และ WPF accessibility wiring
