# 03: ตรึงและจัดเก็บแหล่งข้อมูล Emoji Baseline สำหรับงานแบบ offline

**What to build:** จัดเตรียม source inputs และ Noto artwork ของ Emoji Baseline ที่ระบุเวอร์ชันและตรวจสอบความถูกต้องได้ เพื่อให้การสร้างข้อมูลและ release ไม่ขึ้นกับ `latest` หรือ runtime network

**Blocked by:** 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**Status:** resolved

- [x] ตรึง Unicode 17.0.0, Unicode Emoji 17.0, CLDR 48.2, Noto Emoji v2.051 และ Noto commit ตามสเปก
- [x] source lock manifest ระบุ source, version, immutable URL, commit เมื่อมี, checksum, byte length และ license class
- [x] จัดเก็บ Unicode/CLDR inputs ที่กำหนดและ Noto PNG canonical ขนาด 128 กับ 512 ไว้ใน Git โดยไม่ใช้ Git LFS
- [x] คำสั่ง update baseline เป็นการกระทำโดยเจตนาที่ตรวจ checksum ก่อนรับข้อมูลใหม่ และไม่ใช้ URL แบบ latest, draft หรือ branch ที่เปลี่ยนค่าได้
- [x] ordinary baseline verification และ release verification ทำงานจาก source ที่ commit แล้วโดยไม่ดาวน์โหลดข้อมูลเพิ่ม
- [x] provenance และ third-party notices ครอบคลุม Unicode, CLDR, Noto และ region flags ที่นำมาใช้

## Comments

- 28 สิงหาคม 2026: เพิ่ม source lock ของ Unicode 17.0.0 / Emoji 17.0, CLDR 48.2 และ Noto Emoji v2.051 ที่ commit `8998f5dd683424a73e2314a8c1f1e359c19e8742` พร้อม immutable URL, commit/tree, SHA-256, byte length, license class และ inventory รายไฟล์
- 28 สิงหาคม 2026: vendor source และ artwork รวม 7,875 ไฟล์ 130,746,320 bytes โดย canonical PNG มี 7,499 ไฟล์ (128 px จำนวน 3,768 และ 512 px จำนวน 3,731) พร้อม region-flags 359 ไฟล์ และไม่มีไฟล์ใดใช้ Git LFS หรือเกินขีดจำกัด 100 MiB ของ GitHub
- 28 สิงหาคม 2026: `scripts/update-emoji-baseline.ps1 -Confirm:$false` ผ่านแบบ end-to-end จาก source ที่ pin ไว้ โดยตรวจ download hash, Git commit/tree และ inventory ก่อนแทนที่ไฟล์ แล้วล้าง staging directory ชั่วคราวสำเร็จ
- 28 สิงหาคม 2026: commit `a2d9d6a` ผ่าน ordinary และ release verification จาก detached clean worktree โดยไม่ใช้ network (`7,875 files / 130,746,320 bytes`) รวมทั้ง `scripts/verify-foundation.ps1 -SkipPublish` ผ่านด้วย Release build 0 warnings/0 errors และ WPF browse/search smoke ผ่าน
- 28 สิงหาคม 2026: ตั้ง Git attributes ให้ source ที่ vendor ไว้รักษา byte ต้นฉบับข้ามแพลตฟอร์ม เพื่อให้ checksum ยังคงผ่านหลัง clean checkout และบันทึก provenance/license ใน `docs/emoji-baseline-sources.md` กับ `THIRD-PARTY-NOTICES.md`
