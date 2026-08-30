# ผลปิด Modern Emoji Picker MVP — Windows 10

สถานะ: **อนุมัติสำหรับให้บุคคลทั่วไปใช้งาน โดยมีข้อจำกัดที่ระบุไว้**

- วันที่ปิดผล: 30 สิงหาคม 2026
- รุ่นที่เผยแพร่: Modern Emoji Picker 0.1.9
- แพลตฟอร์มหลัก: Windows 10 Enterprise N 22H2 x64 build 19045
- หลักฐานรอบ manual เดิม: [`manual-win10-19045-20260829.md`](./manual-win10-19045-20260829.md)
- หลักฐาน automated: [`automated-win10-19045.json`](./automated-win10-19045.json)
- หลักฐาน global hotkey: [`global-hotkey-win10-19045.json`](./global-hotkey-win10-19045.json)

## ผลที่ใช้ปิด MVP

- Workflow หลักบน Notepad, Chrome และ VS Code ผ่านบนเครื่องจริง รวมการเปิดด้วย `Win + .`, Browse/Search, การคืน focus, Thai/English input และการส่ง Emoji sequence ซับซ้อน
- ปัญหา rapid clicks, UI ค้าง, surrogate/replacement character, Chrome omnibox และ High Contrast ถูกล็อกด้วย desktop regression หลังแก้ และ qualification อัตโนมัติผ่าน
- Clipboard ว่าง, Unicode text, image, FileDrop, Chromium custom formats, target ปิด และการเปลี่ยน Clipboard ระหว่าง delay ผ่านชุด manual เดิม
- Performance budgets ของ Modern ผ่านทั้ง global hotkey-to-visible, search, virtualized scroll, working set, decode/cache และขนาดแพ็กเกจ
- Static/runtime verification ไม่พบ telemetry, analytics, update polling, cloud sync หรือ runtime network ของผลิตภัณฑ์

## ข้อจำกัดที่ยอมรับและไม่ทำต่อใน effort นี้

- Explorer address bar อาจกลับเป็น breadcrumb เมื่อเสีย focus ผู้ดูแลยอมรับเพราะไม่ใช่ target ใช้งานหลัก
- ไม่ได้รับรอง Windows Terminal, Windows 11, Tier B apps, RDP, Citrix, NVDA, จอหลายตัวต่าง DPI หรือ DPI 125–250% เนื่องจากไม่มี environment สำหรับทดสอบครบ
- High Contrast มี desktop regression หลังแก้ แต่ไม่มีผล manual รอบใหม่ที่แก้หลักฐานเดิมย้อนหลัง
- Queue full แบบ active + 20 pending, elevated target, Explicit Copy ผ่าน failure UI, Clipboard History และ Clipboard manager ภายนอกไม่มีหลักฐาน manual ครบ
- ไม่มี raw upstream metrics ครบทุกมิติ จึงใช้ performance budgets ของ Modern เป็น release gate โดยไม่อ้างว่าเร็วกว่า upstream ในทุกกรณี
- ตัวติดตั้งยังไม่มี code-signing certificate และ Windows SmartScreen อาจแจ้ง unknown publisher

## การตัดสินใจปิด

ผู้ดูแลยืนยันว่า Picker ตรงตามการใช้งานที่ต้องการ อนุมัติข้อจำกัดข้างต้น และไม่ขยายไปทำงานเสริมเหล่านี้ โครงการจึงปิด Ticket 13 เป็น `resolved` และถือว่า Picker 0.1.9 พร้อมให้บุคคลทั่วไปใช้งานภายในขอบเขตที่เอกสารระบุ
