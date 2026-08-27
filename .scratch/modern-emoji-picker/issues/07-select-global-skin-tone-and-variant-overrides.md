# 07: เลือกสีผิวและ Variant Override ได้ครบทุก sequence

**What to build:** ให้ผู้ใช้เลือกสีผิวเริ่มต้นและเข้าถึง sequence ที่มีคนหลายคนหรือสีผิวผสมได้ โดย global preference และ override เฉพาะครั้งไม่รบกวนกัน

**Blocked by:** 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**Status:** ready-for-agent

- [ ] สีผิวเริ่มต้นเป็นค่าระดับ global มีค่าเริ่มต้น neutral สีเหลือง และคงอยู่ข้าม Picker Session
- [ ] Emoji Entry ที่รองรับ modifier resolve เป็น sequence ของสีผิวเริ่มต้นอย่างถูกต้อง
- [ ] mixed-tone sequence ที่แทนด้วย global setting ค่าเดียวไม่ได้สามารถเลือกผ่าน Variant Override ได้ครบ
- [ ] Variant Override มีผลเฉพาะการเลือกครั้งนั้นและไม่เปลี่ยนสีผิวเริ่มต้น
- [ ] sequence ที่ resolve แล้วตรงกับ fully-qualified sequence ใน Emoji Baseline
- [ ] flags, keycaps, ZWJ และ sequence ซับซ้อนทุกกลุ่มยังเข้าถึงได้ ไม่ถูกตัดออกเพราะ variant UI
- [ ] automated tests ครอบคลุม neutral, single-tone, mixed-tone และ entry ที่ไม่รองรับ skin tone
