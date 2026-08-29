# 10: สร้าง Renderer Extension Release Package

**What to build:** ให้ maintainer สร้าง Renderer Extension ZIP จาก clean checkout ในเครื่องได้อย่าง deterministic พร้อมเอกสารภาษาไทย, licenses, checksums, version metadata และ known limitations เพื่อใช้ติดตั้ง manual หรือแนบ GitHub Release โดยไม่พึ่ง GitHub Actions minutes

**Blocked by:** 09: รับรอง Performance, Text Integrity และ Compatibility

**Status:** ready-for-agent

- [ ] Local release command สร้าง production extension และ ZIP ที่ load/install manual ได้จาก clean checkout
- [ ] Package มีเฉพาะ production assets ที่จำเป็น ไม่มี source maps, debug defaults, test fixtures, secrets หรือ remote-code references
- [ ] Manifest และ release metadata ระบุ Extension version, Unicode Emoji Baseline version และ Noto version ตรงกับ assets จริง
- [ ] รวม source license, Noto/Unicode attribution และ third-party notices ครบถ้วนโดยไม่ bundle Apple Emoji
- [ ] README ภาษาไทยอธิบายการติดตั้ง, update, เปิด/ปิดต่อ site, Options, privacy, troubleshooting และการถอนติดตั้ง
- [ ] Known limitations ระบุ Editable Content ใน v1, server normalization, canvas/image/video และ closed Shadow DOM อย่างตรงไปตรงมา
- [ ] สร้าง SHA-256 และ verification report ที่ยืนยัน contents, permissions, licenses, offline assets และ qualification evidence
- [ ] Release workflow หลัก build ในเครื่องและไม่บังคับใช้ CI/CD ที่กิน GitHub Actions minutes
