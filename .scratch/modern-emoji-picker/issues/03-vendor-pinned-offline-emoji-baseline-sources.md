# 03: ตรึงและจัดเก็บแหล่งข้อมูล Emoji Baseline สำหรับงานแบบ offline

**What to build:** จัดเตรียม source inputs และ Noto artwork ของ Emoji Baseline ที่ระบุเวอร์ชันและตรวจสอบความถูกต้องได้ เพื่อให้การสร้างข้อมูลและ release ไม่ขึ้นกับ `latest` หรือ runtime network

**Blocked by:** 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**Status:** ready-for-agent

- [ ] ตรึง Unicode 17.0.0, Unicode Emoji 17.0, CLDR 48.2, Noto Emoji v2.051 และ Noto commit ตามสเปก
- [ ] source lock manifest ระบุ source, version, immutable URL, commit เมื่อมี, checksum, byte length และ license class
- [ ] จัดเก็บ Unicode/CLDR inputs ที่กำหนดและ Noto PNG canonical ขนาด 128 กับ 512 ไว้ใน Git โดยไม่ใช้ Git LFS
- [ ] คำสั่ง update baseline เป็นการกระทำโดยเจตนาที่ตรวจ checksum ก่อนรับข้อมูลใหม่ และไม่ใช้ URL แบบ latest, draft หรือ branch ที่เปลี่ยนค่าได้
- [ ] ordinary baseline verification และ release verification ทำงานจาก source ที่ commit แล้วโดยไม่ดาวน์โหลดข้อมูลเพิ่ม
- [ ] provenance และ third-party notices ครอบคลุม Unicode, CLDR, Noto และ region flags ที่นำมาใช้
