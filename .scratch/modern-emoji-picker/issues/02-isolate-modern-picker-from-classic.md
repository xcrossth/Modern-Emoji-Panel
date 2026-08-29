# 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์

**What to build:** ทำให้ Modern Emoji Picker เป็น resident tray utility ที่มี identity และ lifecycle ของตนเอง อยู่ร่วมกับ Classic ได้โดยไม่แย่ง hotkey หรือแตะข้อมูลของอีกผลิตภัณฑ์

**Blocked by:** 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**Status:** resolved

- [x] executable, assembly, mutex, named event, registry Run value, install/uninstall identity, artifact identity และ data identity ของ Modern ไม่ reuse ค่าของ Classic
- [x] Modern ไม่ import, แชร์, แก้ไข หรือลบ Settings และ Activity Data ของ Classic
- [x] การเปิด executable ซ้ำส่งสัญญาณไปยัง instance เดิม และการปิดหน้าต่าง Picker เป็น dismissal ไม่ใช่การหยุด process
- [x] Tray สามารถเปิด Picker และ Exit process ได้อย่างชัดเจน
- [x] หากตรวจพบ Classic กำลังทำงาน Modern ไม่แย่ง global hotkey และไม่ปิด process อื่น แต่แจ้งวิธี Exit Classic
- [x] เมื่อ Modern ไม่ทำงาน Windows Emoji Panel เดิมยังเปิดด้วย Win + . ได้ตามปกติ
- [x] identity ชั่วคราวทั้งหมดแตกต่างจาก Classic โดย final public icon จะสร้างใน ticket release

## Comments

- 28 สิงหาคม 2026: แยก executable/assembly เป็น `ModernEmojiPicker`, ใช้ mutex/event ใต้ `Local\\XCroSs.ModernEmojiPicker.*`, Run value `ModernEmojiPicker`, data directory `%APPDATA%\\ModernEmojiPicker` และ GUID ของ Inno/WiX ชุดใหม่ โดยไม่ฝัง icon ของ Classic
- 28 สิงหาคม 2026: เพิ่ม single-instance coordinator ซึ่งสร้าง signal ก่อน mutex เพื่อรองรับ startup race; secondary launch ส่ง show signal ไป primary แล้วออก ส่วนการปิด WPF window เป็น dismissal และ tray มีคำสั่งเปิด/Exit process แยกชัดเจน
- 28 สิงหาคม 2026: Classic conflict detector เปิดอ่านเฉพาะ named mutex `ClassicEmojiPicker.SingleInstance`; เมื่อพบจะไม่สร้าง Modern hotkey hook, ไม่ส่ง signal, ไม่ kill process และไม่เปิดอ่าน Classic data พร้อมแสดงขั้นตอนให้ผู้ใช้ Exit Classic เอง
- 28 สิงหาคม 2026: hook เป็นทรัพยากรภายใน process และถูก dispose ใน `OnExit`; ไม่มีการเปลี่ยน shell/registry hotkey ของ Windows ดังนั้นเมื่อ Modern หยุดทำงาน Win + . จะกลับไปยัง Windows Emoji Panel ตามปกติ ส่วน end-to-end interactive compatibility จะตรวจซ้ำใน Ticket 13
- 28 สิงหาคม 2026: `scripts/verify-product-identity.ps1` ตรวจ identity ของ assembly/runtime/installer/data/icon, named-mutex probe, Classic conflict seam และ secondary-launch signal ผ่าน โดยยังคง copyright ของ upstream ใน MSI license
- 28 สิงหาคม 2026: commit `d1e0eb0` ผ่านการตรวจจาก detached clean worktree: locked restore, Release build 0 warnings/0 errors, identity/lifecycle smoke, self-contained win-x64 publish, WPF close-as-dismiss/browse/search smoke และ `dotnet format --verify-no-changes`
