# 14: สร้าง installer, portable package และ local-first release

**What to build:** สร้าง official Picker artifacts จากเครื่อง local พร้อม identity และ artwork ใหม่ ตรวจสอบ license/checksum ครบ และเตรียม Draft GitHub Release โดยไม่ใช้ GitHub-hosted CI/CD เป็นค่าเริ่มต้น

**Blocked by:** 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP

**Status:** ready-for-agent

- [ ] สร้าง product icon ใหม่ที่ไม่ reuse Classic และใช้กับ executable, installer และ public release identity ของ Modern
- [ ] release script ตรวจ clean commit, product-scoped semantic version, Emoji Baseline lock, generator, automated tests และ performance/release preconditions ก่อนสร้าง artifact
- [ ] สร้าง self-contained win-x64 Inno per-user installer และ portable ZIP โดยไม่มี framework-dependent package หรือ MSI
- [ ] installer และ portable ใช้ Modern identity, ไม่แตะ Classic data และมีพฤติกรรมเก็บ/ลบ Settings กับ Activity Data ตามสเปก
- [ ] ทุก artifact รวม LICENSE และ THIRD-PARTY-NOTICES ที่ครบ พร้อม SHA-256 และรายงาน raw assets, installer และ ZIP size
- [ ] เอกสารผู้ใช้ภาษาไทยอธิบาย SmartScreen, unsigned MVP และข้อเท็จจริงเรื่อง .NET 10/Windows 10 support matrix อย่างตรงไปตรงมา
- [ ] build ไม่ upload อัตโนมัติ และคำสั่ง publish รับเฉพาะ local artifacts ที่ผ่าน verification เพื่อสร้าง Draft GitHub Release ผ่าน `gh`
- [ ] tag ใช้รูปแบบ `picker-v<version>` และขั้น Publish จริงยังต้องเป็นคำสั่งโดยเจตนาของผู้ใช้
- [ ] workflow เริ่มต้นไม่พึ่ง GitHub Actions minutes และ release payload ผ่าน manual verification ก่อน public MVP
