# 15 (14B): เตรียมและเผยแพร่ Picker release หลัง qualification

**What to build:** รับเฉพาะ local artifacts ที่ Ticket 14 สร้างและ Ticket 13 รับรองแล้วเพื่อเตรียม Draft GitHub Release จากเครื่อง local โดย public publish ยังคงต้องเป็นคำสั่งโดยเจตนาของผู้ใช้

**Blocked by:** 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP; 14: สร้าง local qualification artifacts

**Status:** ready-for-agent

- [ ] release command รับเฉพาะ artifact manifest/checksum ที่ verifier ของ Ticket 14 รับรองและ commit ตรงกับ clean `HEAD`
- [ ] tag ใช้รูปแบบ `picker-v<version>` และไม่ชนกับ product/version อื่นใน monorepo
- [ ] สร้าง Draft GitHub Release ผ่าน `gh` โดยไม่ใช้ GitHub-hosted build/release workflow
- [ ] Draft Release มี installer, portable ZIP, SHA-256, LICENSE, THIRD-PARTY-NOTICES และเอกสารผู้ใช้ภาษาไทยชุดเดียวกับที่ผ่าน manual verification
- [ ] release notes ระบุ SmartScreen, unsigned MVP, .NET 10 และ Windows support matrix ตามหลักฐานจริงโดยไม่อ้าง support เกิน Ticket 13
- [ ] ขั้น publish สาธารณะเป็นคำสั่งแยกที่ต้องได้รับคำสั่งโดยเจตนาจากผู้ใช้ทุกครั้ง
- [ ] ไม่มี framework-dependent, lite หรือ MSI artifact ใน release payload
- [ ] หลังสร้าง Draft Release ยังไม่มี upload/update polling, telemetry, analytics หรือ cloud sync ใน runtime

## Comments

### 29 สิงหาคม 2026 — สร้างจากการแยก Ticket 14B

Maintainer อนุมัติให้แยก Ticket 14 เดิมเป็น 14A/14B Ticket นี้ใช้หมายเลข 15 เพื่อคง convention ชื่อไฟล์แบบตัวเลขสองหลักของ local issue tracker แต่ชื่อเชิงผลิตภัณฑ์ยังเป็น 14B
