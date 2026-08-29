# 03: Render Emoji Baseline ใน Static Display Content โดยรักษาข้อความเดิม

**What to build:** ให้ข้อความ HTML แบบ static ที่มี Emoji จาก Emoji Baseline แสดงด้วย renderer ที่พิสูจน์แล้ว เฉพาะ grapheme cluster ของ Emoji โดย surrounding text และ Unicode ต้นฉบับยังเหมือนเดิม และ Editable Content ไม่ถูกแตะ

**Blocked by:** 02: พิสูจน์ Renderer ที่แสดง Noto บน Chrome และ Windows 10 ได้จริง

**Status:** resolved

- [x] สร้าง web-consumable Emoji data แบบ deterministic จาก Emoji Baseline รุ่นเดียวกับ Picker โดยไม่ hardcode รายการ Emoji ซ้ำด้วยมือ
- [x] Detector แยก grapheme cluster และรู้จักชุด Emoji ที่รองรับครบทั้ง VS16, modifiers, ZWJ, keycaps, regional flags และ tag sequences
- [x] DOM transformation แก้เฉพาะ Emoji cluster และไม่เปลี่ยน `textContent` ของข้อความไทย/อังกฤษผสม Emoji
- [x] Browser Find, text selection, Copy และ DOM text extraction ยังคงคืน Unicode เดิม
- [x] ข้าม SCRIPT, STYLE, NOSCRIPT, INPUT, TEXTAREA, CODE, PRE, Editable Content และ subtree ที่ Renderer สร้างเอง
- [x] ไม่มีการ wrap ซ้ำเมื่อประมวลผล node เดิมมากกว่าหนึ่งครั้ง
- [x] Unit/integration tests ครอบคลุม Emoji, mixed text, plain text, node ที่ต้องข้าม และ text-integrity regressions

## Comments

- `npm run check:data` ยืนยันไฟล์ generated 3,944 sequence ตรงกับ `data/emoji-baseline/17.0/emoji.json`
- Vitest ครอบคลุม detector, text integrity, skip boundary และ idempotency; Chrome fixture ยืนยัน Selection, Copy ที่มี user gesture และ Browser Find จริง
- หลักฐานและวิธีรันซ้ำอยู่ที่ `docs/research/renderer-dom-pipeline/README.md`
