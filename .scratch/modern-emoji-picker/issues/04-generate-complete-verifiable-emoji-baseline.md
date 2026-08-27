# 04: สร้าง Emoji Baseline ที่สมบูรณ์และตรวจสอบซ้ำได้

**What to build:** สร้าง generator ที่เปลี่ยน source inputs ที่ตรึงไว้เป็น Emoji Entry และ manifest กลางแบบ deterministic ซึ่ง Picker ใช้งานได้ทันทีและ Renderer Extension นำไปใช้ภายหลังได้โดยไม่ผูกกับ .NET

**Blocked by:** 03: ตรึงและจัดเก็บแหล่งข้อมูล Emoji Baseline สำหรับงานแบบ offline

**Status:** ready-for-agent

- [ ] output ครอบคลุม fully-qualified sequence ทั้งหมด รวม flags, keycaps, ZWJ, variation selectors และ variants โดยรักษา sequence ดั้งเดิมสำหรับ insert/copy
- [ ] Emoji Entry มี stable identifier, canonical sequence, Unicode group/subgroup, Emoji version และลำดับ deterministic
- [ ] metadata รวม short names และ keywords จาก CLDR annotations กับ annotationsDerived ทั้งภาษาไทยและอังกฤษ
- [ ] Noto asset mapping ใช้ key และ alias ที่ตรวจสอบได้ ไม่อนุมานชื่อไฟล์จาก sequence โดยตรง
- [ ] generator ตรวจ duplicate, stable identifier, source checksum และ coverage ของ PNG 128/512 พร้อม fail เมื่อข้อมูลหรือ asset ที่กำหนดขาด
- [ ] การรันด้วย input เดิมให้ output byte-for-byte เหมือนเดิมและมี automated determinism test
- [ ] มีรายงานรายการเพิ่ม, ลบ, เปลี่ยน, asset ผิดปกติ และ versioned source manifest สำหรับ review การอัปเดต baseline
