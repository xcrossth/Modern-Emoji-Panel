# 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว

**What to build:** ให้ผู้ใช้ควบคุมพฤติกรรม Picker จาก Settings เดียว พร้อม onboarding ที่อธิบายข้อจำกัดสำคัญและค่าความเป็นส่วนตัวที่ไม่มี telemetry หรือข้อมูลข้อความหลุดลง log

**Blocked by:** 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์; 07: เลือกสีผิวและ Variant Override ได้ครบทุก sequence; 08: ส่ง Emoji หนึ่งรายการไปยังแอปเป้าหมายอย่างปลอดภัย; 11: เรียนรู้ Recent และลำดับความชอบบนเครื่อง

**Status:** resolved

- [x] Settings เปลี่ยนหรือปิด global hotkey, Start with Windows, UI language, System/Light/Dark theme และ global skin tone ได้
- [x] Settings เลือก Hybrid, Keystroke only หรือ Paste always และ Advanced Settings ปรับ/reset `pasteRestoreDelayMs` ได้พร้อมคำอธิบายข้อจำกัด
- [x] UI ตาม Windows display language ระหว่างไทยกับอังกฤษ เปลี่ยนเองได้ และภาษาอื่น fallback อังกฤษโดย Search ยังรองรับสองภาษาเสมอ
- [x] ผู้ใช้เรียก Clear Recent, Reset learned ranking และ Clear all activity จาก Settings พร้อมผลลัพธ์ที่ชัดเจน
- [x] first-run Welcome แสดงครั้งเดียวและอธิบาย Win + ., Classic Conflict, Temporary Paste, autostart และทางเข้า Settings โดยไม่มี account/network onboarding
- [x] diagnostic logging ปิดเป็นค่าเริ่มต้นและเมื่อเปิดจะไม่เก็บ query, Emoji ที่เลือก, clipboard/text หรือชื่อหน้าต่างเป้าหมาย
- [x] portable ไม่เปิด autostart จนผู้ใช้สั่ง ส่วน installer เตรียมค่าเริ่มต้นให้เปิด Start with Windows โดยใช้ identity ของ Modern
- [x] Settings และ Activity Data อยู่เฉพาะในเครื่องภายใต้พื้นที่ข้อมูลของ Modern และไม่มี provider, account, sync, telemetry หรือ upload code ใน v1

## หลักฐานการตรวจรับ

- commit งาน `08ca11c` และ merge integration `3e3077e`; merge เข้าสายงาน MVP รักษา queue/activity behavior และตัด log ที่อาจมี user content
- `scripts/verify-settings-privacy.ps1` ผ่าน: Settings model เดียว, Welcome, bilingual fallback, theme/hotkey/autostart/insertion controls, Activity Data commands และ metadata-only logging แบบ opt-in
- Release build/self-contained publish ผ่านด้วย .NET 10 โดยไม่มี warning/error และ runtime qualification ไม่พบ socket ระหว่าง sample
- portable ไม่มีการเปิด autostart เอง; Inno task ใช้ Modern run identity และค่า startup ที่แนะนำ ขณะที่ runtime แยก installer-managed value ออกจาก per-user control
- clean-checkout ล่าสุดผ่าน Settings/privacy พร้อม foundation และ regressions Ticket 01–12 ครบ
- รายละเอียดผู้ใช้อยู่ที่ `docs/settings-welcome-and-privacy.md`
