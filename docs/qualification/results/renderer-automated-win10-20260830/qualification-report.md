# รายงาน Qualification ของ Modern Emoji Renderer

สถานะ: **ผ่านทั้ง automated และ manual E2E**

สร้างเมื่อ: 2026-08-29T21:40:36.287Z

## Environment

- OS: Windows 10 Enterprise N (10.0.19045, x64)
- Chrome for Testing: 152.0.7977.64
- Automated tests: 62 tests / 26 suites ผ่าน, 0 ล้มเหลว

## Performance

| Scenario | ผล | Budget |
|---|---:|---:|
| Initial 2,000 ข้อความ | 62.3 ms | ≤ 1000 ms |
| Mutation burst 1,000 ข้อความ | 29.7 ms | ≤ 750 ms |
| Batch ที่ช้าที่สุด | 9.1 ms | ≤ 50 ms |
| สลับห้อง 50 รอบ (processing time) | 134.6 ms | ≤ 1500 ms |
| Heap หลัง GC เพิ่ม | 296,536 bytes | ≤ 33,554,432 bytes |

wrapper ไม่โตจาก scrolling, repeated start ไม่สร้าง observer/wrapper ซ้ำ และ Editable Content คงเดิม

## Integrity, Accessibility และ Privacy

- Text, DOM extraction, Selection, Copy ที่มี user gesture และ Browser Find ผ่าน
- Thai/English typography และ Unicode sequence คงเดิม
- Wrapper ใช้ text semantic เดิม ไม่มี role/aria-label ซ้ำ
- Composer/caret/selection/composition events ไม่ถูกแก้ DOM; หลัง submit จึง render เฉพาะ display content
- all-sites fixtures ผ่านสำหรับ Instagram feed/comments, Google, GitHub, Reddit, Facebook และ Discord Web
- Extension E2E fixture ยืนยันว่า glyph ใช้ bundled Noto Color Emoji จริงและโหลดด้วย `chrome-extension://` ดู [หลักฐานฟอนต์](../renderer-font-runtime-win10-20260830.md)
- production bundles ไม่มี Fetch/XHR/WebSocket/EventSource/importScripts/remote import/eval และ font/style/script มาจาก package เท่านั้น

## Manual E2E บนเว็บไซต์หลัก

- Instagram Web DM: ข้อความเดิม/ใหม่, การสลับห้อง และ Copy/Paste ผ่าน
- TikTok Web Chat: ข้อความเดิม/ใหม่, การสลับห้อง และ Copy/Paste ผ่าน
- Composer คง renderer เดิมตาม Editable Content boundary ที่ตั้งใจไว้ และยังใช้งานได้
- ดู [matrix](../../renderer-primary-sites.md) และ [manual evidence](../renderer-manual-primary-sites-win10-20260830.md)
