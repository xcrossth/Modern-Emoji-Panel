# 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview

**What to build:** ให้ผู้ใช้ค้นหา Emoji Entry ด้วยภาษาไทยหรืออังกฤษและตรวจสอบรายละเอียดจากภาพขยายก่อนเลือก โดยผลลัพธ์คงลำดับ match ที่คาดเดาได้และ UI รายละเอียดยังเข้าถึงได้ด้วย keyboard

**Blocked by:** 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**Status:** ready-for-agent

- [ ] Search ค้น short name และ keyword ภาษาไทยกับอังกฤษได้ตลอดโดยไม่ขึ้นกับ UI locale
- [ ] exact short name, term prefix, keyword และ substring เรียงเป็น match tiers ตามสเปก และ tie ใช้ลำดับ CLDR แบบ deterministic
- [ ] ผู้ใช้เข้าช่องค้นหาด้วย click หรือ Ctrl+F และผลลัพธ์ตอบสนองระหว่างพิมพ์โดยไม่ค้าง UI
- [ ] Hover Preview ปรากฏหลังชี้ค้างประมาณ 400 ms โดยใช้ PNG 512 ขนาดประมาณ 160 DIP และไม่แย่ง focus
- [ ] preview แสดง localized name, ชื่ออังกฤษบรรทัดรองเมื่อไม่ซ้ำ และ Emoji version
- [ ] tile ที่ focus เปิดข้อมูลเดียวกันด้วย F1 ได้ และ tile ทุกตัวมี accessible name จาก localized short name
- [ ] preview หายเมื่อ pointer ออก, กด Esc หรือเริ่ม insert
