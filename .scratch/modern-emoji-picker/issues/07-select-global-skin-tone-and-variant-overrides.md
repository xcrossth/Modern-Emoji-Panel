# 07: เลือกสีผิวและ Variant Override ได้ครบทุก sequence

**What to build:** ให้ผู้ใช้เลือกสีผิวเริ่มต้นและเข้าถึง sequence ที่มีคนหลายคนหรือสีผิวผสมได้ โดย global preference และ override เฉพาะครั้งไม่รบกวนกัน

**Blocked by:** 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**Status:** resolved

- [x] สีผิวเริ่มต้นเป็นค่าระดับ global มีค่าเริ่มต้น neutral สีเหลือง และคงอยู่ข้าม Picker Session
- [x] Emoji Entry ที่รองรับ modifier resolve เป็น sequence ของสีผิวเริ่มต้นอย่างถูกต้อง
- [x] mixed-tone sequence ที่แทนด้วย global setting ค่าเดียวไม่ได้สามารถเลือกผ่าน Variant Override ได้ครบ
- [x] Variant Override มีผลเฉพาะการเลือกครั้งนั้นและไม่เปลี่ยนสีผิวเริ่มต้น
- [x] sequence ที่ resolve แล้วตรงกับ fully-qualified sequence ใน Emoji Baseline
- [x] flags, keycaps, ZWJ และ sequence ซับซ้อนทุกกลุ่มยังเข้าถึงได้ ไม่ถูกตัดออกเพราะ variant UI
- [x] automated tests ครอบคลุม neutral, single-tone, mixed-tone และ entry ที่ไม่รองรับ skin tone

## หลักฐานการตรวจรับ

- commit งาน: `31675ff` (`feat(ticket-07): add global skin tone and variant overrides`)
- merge เข้าสายงาน MVP: `210d90e` โดยรักษาพฤติกรรม search/preview ของ Ticket 06 และ safe insertion ของ Ticket 08
- `scripts/verify-emoji-variants.ps1` ผ่าน: baseline 3,944 sequence, browse entry 1,914 รายการ, global tone และ one-shot mixed-tone override
- domain tests ตรวจ neutral, single-tone, multi-person uniform tone, mixed tone, entry ที่ไม่มี modifier, legacy handshake, flags, keycaps และ complex ZWJ
- Release build และ self-contained publish ผ่านด้วย .NET SDK 10.0.400 โดยไม่มี warning/error
- clean-checkout ของ merge commit ผ่าน foundation/WPF smoke, generator determinism, Noto grid, safe insertion, bilingual search/preview และ emoji variant verification
- ไม่ใช้ screenshot helper ตามข้อจำกัด Windows 10 ของ repository; UI wiring และ keyboard path ตรวจผ่าน build/smoke test แทน
