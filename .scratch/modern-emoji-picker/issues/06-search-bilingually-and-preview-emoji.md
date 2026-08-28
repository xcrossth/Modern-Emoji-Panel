# 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview

**What to build:** ให้ผู้ใช้ค้นหา Emoji Entry ด้วยภาษาไทยหรืออังกฤษและตรวจสอบรายละเอียดจากภาพขยายก่อนเลือก โดยผลลัพธ์คงลำดับ match ที่คาดเดาได้และ UI รายละเอียดยังเข้าถึงได้ด้วย keyboard

**Blocked by:** 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**Status:** resolved

- [x] Search ค้น short name และ keyword ภาษาไทยกับอังกฤษได้ตลอดโดยไม่ขึ้นกับ UI locale
- [x] exact short name, term prefix, keyword และ substring เรียงเป็น match tiers ตามสเปก และ tie ใช้ลำดับ CLDR แบบ deterministic
- [x] ผู้ใช้เข้าช่องค้นหาด้วย click หรือ Ctrl+F และผลลัพธ์ตอบสนองระหว่างพิมพ์โดยไม่ค้าง UI
- [x] Hover Preview ปรากฏหลังชี้ค้างประมาณ 400 ms โดยใช้ PNG 512 ขนาดประมาณ 160 DIP และไม่แย่ง focus
- [x] preview แสดง localized name, ชื่ออังกฤษบรรทัดรองเมื่อไม่ซ้ำ และ Emoji version
- [x] tile ที่ focus เปิดข้อมูลเดียวกันด้วย F1 ได้ และ tile ทุกตัวมี accessible name จาก localized short name
- [x] preview หายเมื่อ pointer ออก, กด Esc หรือเริ่ม insert

## หลักฐานการตรวจรับ

- commit งาน: `856137a` (`feat(ticket-06): add bilingual search and Noto hover preview`)
- merge integration หลัง Ticket 08: `a783dc0` รักษาทั้ง search/preview และ safe insertion behavior
- `scripts/verify-search-preview.ps1` ผ่าน: 3,944 entries, four deterministic tiers, 100 searches ประมาณ 300–333 ms และ preview asset ครบ 3,944 รายการ
- WPF smoke ตรวจ Ctrl+F, accessible name, popup ไม่รับ focus, hover delay 400 ms, preview 160 DIP จาก PNG 512, F1 และการปิด preview
- `scripts/verify-safe-insertion.ps1` และ `scripts/verify-noto-grid.ps1` ผ่านหลัง merge integration
- clean-checkout ของ commit Ticket 06 ผ่านทั้ง foundation/publish, generator determinism, Noto grid และ search/preview verification
- ไม่ใช้ screenshot helper ตามข้อจำกัด Windows 10 ของ repository; geometry, focus และ decode path ตรวจผ่าน WPF smoke แทน
