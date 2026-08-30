# 10: สร้าง Renderer Extension Release Package

**What to build:** ให้ maintainer สร้าง Renderer Extension ZIP จาก clean checkout ในเครื่องได้อย่าง deterministic พร้อมเอกสารภาษาไทย, licenses, checksums, version metadata และ known limitations เพื่อใช้ติดตั้ง manual หรือแนบ GitHub Release โดยไม่พึ่ง GitHub Actions minutes

**Blocked by:** 09: รับรอง Performance, Text Integrity และ Compatibility

**Status:** resolved

- [x] Local release command สร้าง production extension และ ZIP ที่ load/install manual ได้จาก clean checkout
- [x] Package มีเฉพาะ production assets ที่จำเป็น ไม่มี source maps, debug defaults, test fixtures, secrets หรือ remote-code references
- [x] Manifest และ release metadata ระบุ Extension version, Unicode Emoji Baseline version และ Noto version ตรงกับ assets จริง
- [x] รวม source license, Noto/Unicode attribution และ third-party notices ครบถ้วนโดยไม่ bundle Apple Emoji
- [x] README ภาษาไทยอธิบายการติดตั้ง, update, เปิด/ปิดต่อ site, Options, privacy, troubleshooting และการถอนติดตั้ง
- [x] Known limitations ระบุ Editable Content ใน v1, server normalization, canvas/image/video และ closed Shadow DOM อย่างตรงไปตรงมา
- [x] สร้าง SHA-256 และ verification report ที่ยืนยัน contents, permissions, licenses, offline assets และ qualification evidence
- [x] Release workflow หลัก build ในเครื่องและไม่บังคับใช้ CI/CD ที่กิน GitHub Actions minutes

## Comments

- คำสั่งหลักคือ `scripts/build-renderer-release.ps1` และไม่ใช้ GitHub Actions; รอบ final release จาก clean commit `d6b1f27` ผ่าน qualification, production build, deterministic packaging, package verification, Chrome load smoke และ actual bundled-font gate
- ZIP `modern-emoji-renderer-0.0.1.zip` ถูกสร้างซ้ำ 2 รอบได้ SHA-256 เดียวกันคือ `3292bee1965f5b41ab221a65ddc54e4665891e080f6490065f67b3dae94c1a1c`; output และรายงานอยู่ใต้ `artifacts/renderer-extension/release/`
- ตัวตรวจเปิดอ่าน ZIP กลับมาเทียบ staging ทุกไบต์ และตรวจ required/prohibited contents, manifest permissions, SHA256SUMS, licenses, Unicode/Noto metadata, font hash, debug default, Apple asset exclusion, runtime network API และ qualification report
- Production package โหลด service worker ใน Chrome for Testing 152.0.7977.64 ได้โดยใช้ temporary profile
- Manual E2E บน Instagram DM และ TikTok Web Chat ผ่านแล้ว metadata จึงระบุ `releaseKind: release`, `qualification: passed`, `manualEvidence: passed` และ Ticket 10 ปิดได้
