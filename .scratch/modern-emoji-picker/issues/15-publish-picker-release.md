# 15 (14B): เตรียมและเผยแพร่ Picker release หลัง qualification

**What to build:** รับเฉพาะ local artifacts ที่ Ticket 14 สร้างและ Ticket 13 รับรองแล้วเพื่อเตรียม Draft GitHub Release จากเครื่อง local โดย public publish ยังคงต้องเป็นคำสั่งโดยเจตนาของผู้ใช้

**Blocked by:** 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP; 14: สร้าง local qualification artifacts

**Status:** resolved

- [x] release command รับเฉพาะ artifact manifest/checksum ที่ verifier ของ Ticket 14 รับรอง
- [x] ใช้ tag `v0.1.9` สำหรับ Release ที่รวม Picker 0.1.9 และ Renderer 0.0.2 โดยไม่ชนกับ tag อื่น
- [x] สร้างและเผยแพร่ GitHub Release ผ่าน `gh` โดยไม่ใช้ GitHub-hosted build/release workflow
- [x] Release มี installer, portable ZIP, Renderer ZIP และ SHA-256 พร้อม LICENSE/notices ภายใน package
- [x] release notes ระบุ SmartScreen, unsigned MVP, .NET 10 และขอบเขต Windows qualification ตามหลักฐานจริง
- [x] ขั้น publish สาธารณะทำหลังได้รับคำสั่งโดยเจตนาจากผู้ใช้
- [x] ไม่มี framework-dependent, lite หรือ MSI artifact ใน release payload
- [x] runtime ไม่มี update polling, telemetry, analytics หรือ cloud sync

## Comments

### 29 สิงหาคม 2026 — สร้างจากการแยก Ticket 14B

Maintainer อนุมัติให้แยก Ticket 14 เดิมเป็น 14A/14B Ticket นี้ใช้หมายเลข 15 เพื่อคง convention ชื่อไฟล์แบบตัวเลขสองหลักของ local issue tracker แต่ชื่อเชิงผลิตภัณฑ์ยังเป็น 14B

### 30 สิงหาคม 2026 — เผยแพร่ Public MVP

Maintainer อนุมัติให้เผยแพร่ Picker และ Renderer ร่วมกัน ใช้ tag `v0.1.9` และชื่อ Release `Modern Emoji Picker 0.1.9 + Renderer 0.0.2` อัปโหลดตัวติดตั้ง, portable ZIP, Renderer ZIP และ checksum จาก local artifacts ที่ตรวจแล้ว Release ไม่เป็น Draft/Prerelease และไม่มี GitHub Actions workflow
