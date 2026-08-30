# 14 (14A): สร้าง local qualification artifacts

**What to build:** สร้าง Picker artifacts จากเครื่อง local พร้อม identity และ artwork ใหม่ ตรวจสอบ license/checksum/size และ release preconditions เพื่อให้ Ticket 13 ใช้ qualification ได้ โดยยังไม่สร้าง tag, GitHub Release หรือเผยแพร่ artifact

**Blocked by:** 04: สร้าง Emoji Baseline ที่ครบและตรวจสอบได้; 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว

**Status:** resolved

- [x] สร้าง product icon ใหม่ที่ไม่ reuse Classic และใช้กับ executable, installer และ portable identity ของ Modern พร้อมไฟล์ต้นฉบับ/วิธีสร้างที่ทำซ้ำได้
- [x] local package script ตรวจ clean commit, product-scoped semantic version, Emoji Baseline lock, generator, automated tests และ performance/release preconditions ก่อนสร้าง artifact
- [x] สร้าง self-contained win-x64 Inno per-user installer และ portable ZIP โดยไม่มี framework-dependent package หรือ MSI
- [x] installer และ portable ใช้ Modern identity, ไม่แตะ Classic data และมีพฤติกรรมเก็บ/ลบ Settings กับ Activity Data ตามสเปก
- [x] ทุก artifact รวม LICENSE และ THIRD-PARTY-NOTICES ที่ครบ พร้อม SHA-256 และรายงาน raw assets, installer และ ZIP size
- [x] เอกสารผู้ใช้ภาษาไทยอธิบาย SmartScreen, unsigned MVP และข้อเท็จจริงเรื่อง .NET 10/Windows 10 support matrix อย่างตรงไปตรงมา
- [x] package script ไม่ upload, tag หรือสร้าง GitHub Release และ workflow เริ่มต้นไม่พึ่ง GitHub Actions minutes
- [x] verifier ตรวจ contents, identity, architecture, checksum, size budget และยืนยันว่า output ไม่มี MSI/framework-dependent artifact
- [x] artifacts และ machine-readable report ใช้เป็นหลักฐานย้อนกลับไปยัง Ticket 13 ได้โดยไม่อ้างว่า manual qualification หรือ public release ผ่านแล้ว

## Comments

### 29 สิงหาคม 2026 — maintainer อนุมัติแยก dependency

แยก Ticket 14 เดิมเป็น 14A (ไฟล์นี้) และ 14B (Ticket 15) ตามคำแนะนำของ agent เพื่อคลี่วงจร dependency: 14A สร้าง local artifacts ให้ Ticket 13 วัด package/release preconditions ส่วน 14B รอ Ticket 13 และรักษา explicit publish gate

### 29 สิงหาคม 2026 — local artifacts ผ่านการตรวจครบสาย

รัน `scripts/release.ps1 -Version 0.1.9` จาก clean commit `3dc39c679d7faef0a9431188369f2eec76555ca8` บน Windows 10 Enterprise N build 19045 และ .NET SDK 10.0.400 สำเร็จ โดยสคริปต์รัน baseline/generator/regression/performance gates ก่อน publish และยืนยันตอนจบว่าไม่ได้สร้าง tag, upload หรือ GitHub Release

- Inno per-user installer 174,151,850 bytes, SHA-256 `f62e881d9a143bbe74486f4b82c75a902ee53b7083eb998893fdf76b43146582`
- portable self-contained win-x64 ZIP 202,376,122 bytes, SHA-256 `1fe2a0226bea343b3817c40c3c28d48fea1c401af47e73a153a3986ecbaba110`
- publish directory 313,246,732 bytes และ raw Noto assets 127,309,639 bytes
- verifier เปิด ZIP ตรวจ executable/architecture/runtime/notices, ทดสอบ product identity, ตรวจ checksum และยืนยันนโยบาย local-only ผ่าน
- หลักฐานที่ commit ได้อยู่ใน `docs/qualification/results/local-artifacts-v0.1.9-win10-19045.json` และ `docs/qualification/results/automated-win10-19045.json`; binary artifacts อยู่ใน `artifacts/release/picker-v0.1.9/` และถูก ignore โดย Git ตามเจตนา

งานเผยแพร่ยังเป็น Ticket 15 (14B) และยังไม่เริ่ม เพราะต้องรอ manual qualification ของ Ticket 13 และคำสั่งเผยแพร่โดยเจตนาจาก maintainer
