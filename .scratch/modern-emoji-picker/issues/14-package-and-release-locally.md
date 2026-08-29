# 14 (14A): สร้าง local qualification artifacts

**What to build:** สร้าง Picker artifacts จากเครื่อง local พร้อม identity และ artwork ใหม่ ตรวจสอบ license/checksum/size และ release preconditions เพื่อให้ Ticket 13 ใช้ qualification ได้ โดยยังไม่สร้าง tag, GitHub Release หรือเผยแพร่ artifact

**Blocked by:** 04: สร้าง Emoji Baseline ที่ครบและตรวจสอบได้; 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว

**Status:** ready-for-agent

- [ ] สร้าง product icon ใหม่ที่ไม่ reuse Classic และใช้กับ executable, installer และ portable identity ของ Modern พร้อมไฟล์ต้นฉบับ/วิธีสร้างที่ทำซ้ำได้
- [ ] local package script ตรวจ clean commit, product-scoped semantic version, Emoji Baseline lock, generator, automated tests และ performance/release preconditions ก่อนสร้าง artifact
- [ ] สร้าง self-contained win-x64 Inno per-user installer และ portable ZIP โดยไม่มี framework-dependent package หรือ MSI
- [ ] installer และ portable ใช้ Modern identity, ไม่แตะ Classic data และมีพฤติกรรมเก็บ/ลบ Settings กับ Activity Data ตามสเปก
- [ ] ทุก artifact รวม LICENSE และ THIRD-PARTY-NOTICES ที่ครบ พร้อม SHA-256 และรายงาน raw assets, installer และ ZIP size
- [ ] เอกสารผู้ใช้ภาษาไทยอธิบาย SmartScreen, unsigned MVP และข้อเท็จจริงเรื่อง .NET 10/Windows 10 support matrix อย่างตรงไปตรงมา
- [ ] package script ไม่ upload, tag หรือสร้าง GitHub Release และ workflow เริ่มต้นไม่พึ่ง GitHub Actions minutes
- [ ] verifier ตรวจ contents, identity, architecture, checksum, size budget และยืนยันว่า output ไม่มี MSI/framework-dependent artifact
- [ ] artifacts และ machine-readable report ใช้เป็นหลักฐานย้อนกลับไปยัง Ticket 13 ได้โดยไม่อ้างว่า manual qualification หรือ public release ผ่านแล้ว

## Comments

### 29 สิงหาคม 2026 — maintainer อนุมัติแยก dependency

แยก Ticket 14 เดิมเป็น 14A (ไฟล์นี้) และ 14B (Ticket 15) ตามคำแนะนำของ agent เพื่อคลี่วงจร dependency: 14A สร้าง local artifacts ให้ Ticket 13 วัด package/release preconditions ส่วน 14B รอ Ticket 13 และรักษา explicit publish gate
