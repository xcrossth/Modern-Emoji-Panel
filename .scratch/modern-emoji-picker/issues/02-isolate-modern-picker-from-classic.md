# 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์

**What to build:** ทำให้ Modern Emoji Picker เป็น resident tray utility ที่มี identity และ lifecycle ของตนเอง อยู่ร่วมกับ Classic ได้โดยไม่แย่ง hotkey หรือแตะข้อมูลของอีกผลิตภัณฑ์

**Blocked by:** 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**Status:** ready-for-agent

- [ ] executable, assembly, mutex, named event, registry Run value, install/uninstall identity, artifact identity และ data identity ของ Modern ไม่ reuse ค่าของ Classic
- [ ] Modern ไม่ import, แชร์, แก้ไข หรือลบ Settings และ Activity Data ของ Classic
- [ ] การเปิด executable ซ้ำส่งสัญญาณไปยัง instance เดิม และการปิดหน้าต่าง Picker เป็น dismissal ไม่ใช่การหยุด process
- [ ] Tray สามารถเปิด Picker และ Exit process ได้อย่างชัดเจน
- [ ] หากตรวจพบ Classic กำลังทำงาน Modern ไม่แย่ง global hotkey และไม่ปิด process อื่น แต่แจ้งวิธี Exit Classic
- [ ] เมื่อ Modern ไม่ทำงาน Windows Emoji Panel เดิมยังเปิดด้วย Win + . ได้ตามปกติ
- [ ] identity ชั่วคราวทั้งหมดแตกต่างจาก Classic โดย final public icon จะสร้างใน ticket release
