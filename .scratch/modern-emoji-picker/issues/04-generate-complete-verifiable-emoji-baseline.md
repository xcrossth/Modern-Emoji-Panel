# 04: สร้าง Emoji Baseline ที่สมบูรณ์และตรวจสอบซ้ำได้

**What to build:** สร้าง generator ที่เปลี่ยน source inputs ที่ตรึงไว้เป็น Emoji Entry และ manifest กลางแบบ deterministic ซึ่ง Picker ใช้งานได้ทันทีและ Renderer Extension นำไปใช้ภายหลังได้โดยไม่ผูกกับ .NET

**Blocked by:** 03: ตรึงและจัดเก็บแหล่งข้อมูล Emoji Baseline สำหรับงานแบบ offline

**Status:** resolved

- [x] output ครอบคลุม fully-qualified sequence ทั้งหมด รวม flags, keycaps, ZWJ, variation selectors และ variants โดยรักษา sequence ดั้งเดิมสำหรับ insert/copy
- [x] Emoji Entry มี stable identifier, canonical sequence, Unicode group/subgroup, Emoji version และลำดับ deterministic
- [x] metadata รวม short names และ keywords จาก CLDR annotations กับ annotationsDerived ทั้งภาษาไทยและอังกฤษ
- [x] Noto asset mapping ใช้ key และ alias ที่ตรวจสอบได้ ไม่อนุมานชื่อไฟล์จาก sequence โดยตรง
- [x] generator ตรวจ duplicate, stable identifier, source checksum และ coverage ของ PNG 128/512 พร้อม fail เมื่อข้อมูลหรือ asset ที่กำหนดขาด
- [x] การรันด้วย input เดิมให้ output byte-for-byte เหมือนเดิมและมี automated determinism test
- [x] มีรายงานรายการเพิ่ม, ลบ, เปลี่ยน, asset ผิดปกติ และ versioned source manifest สำหรับ review การอัปเดต baseline

## หลักฐานการตรวจรับ

- commit งาน: `5a76c86` (`feat(ticket-04): generate deterministic emoji 17 baseline`)
- `scripts/verify-generated-emoji-baseline.ps1` ผ่าน โดยสร้าง baseline สองรอบและยืนยันผล byte-for-byte จำนวน 3,944 รายการ
- baseline ครอบคลุม region flags 262 รายการ, ตรวจ alias collision 37 รายการ และไม่พบ asymmetric asset key
- `scripts/test-clean-checkout.ps1 -Revision HEAD` ผ่านบน detached clean checkout พร้อม restore, build, self-contained publish, WPF smoke test และ generator determinism test
- ทดสอบโหมดเปรียบเทียบกับ baseline เดิมแล้วได้ Added=0, Removed=0 และ Changed=0
