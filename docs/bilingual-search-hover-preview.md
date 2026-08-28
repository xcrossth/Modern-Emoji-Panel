# การค้นหาสองภาษาและ Hover Preview

Picker สร้างดัชนีค้นหาจากชื่อย่อและ keyword ของ CLDR 48.2 ทั้งภาษาไทยและอังกฤษเมื่อโหลด Emoji Baseline ดังนั้นผู้ใช้ค้นหาได้ทั้งสองภาษาเสมอ ไม่ว่าภาษา UI ปัจจุบันจะเป็นภาษาใด การค้นหาไม่เรียกเครือข่ายและไม่บันทึกข้อความที่ค้นหา

## ลำดับผลการค้นหา

ข้อความค้นหาถูก normalize แบบ Unicode NFKC, ไม่แยกตัวพิมพ์ใหญ่–เล็ก และรวมช่องว่างซ้ำ ก่อนจัดผลลัพธ์เป็นสี่ชั้นตามลำดับนี้:

1. ตรงกับ short name ภาษาไทยหรืออังกฤษทั้งชื่อ
2. ตรงกับต้น term ใน short name
3. ตรงกับต้น keyword ของ CLDR
4. เป็น substring ภายใน short name หรือ keyword

ผลลัพธ์ในชั้นเดียวกันเรียงตาม `order` ของ Emoji Baseline ซึ่งมาจากลำดับ CLDR/Unicode และใช้ stable ID เป็นตัวตัดสินสุดท้าย จึงได้ผลเหมือนเดิมทุกครั้ง คะแนนความนิยมเดิมไม่สามารถทำให้ผลที่ match แย่กว่าแซงผลที่ดีกว่าได้ ส่วน Learned Ranking จะเพิ่มใน Ticket 11 และต้องจัดลำดับได้เฉพาะภายในชั้นเดียวกันเท่านั้น

UI หน่วงการกรอง 120 ms หลังการพิมพ์ครั้งล่าสุดเพื่อลดการเปลี่ยนรายการถี่เกินไป ผู้ใช้คลิกช่องค้นหาหรือกด `Ctrl+F` เพื่อย้าย focus ไปยังช่องค้นหาได้

## Hover Preview

- ชี้ tile ค้าง 400 ms เพื่อเปิด preview โดย popup ไม่รับ focus
- กด `F1` เพื่อเปิดข้อมูลของ tile ที่เลือกหรือมี keyboard focus
- ภาพมีขนาด 160 DIP และอ่าน path บทบาท `png512` จาก manifest โดยตรง
- region flag ใช้ high-resolution source ร่วมตามที่ generator ระบุด้วย `sharedSourceForSizes` ไม่อนุมานชื่อไฟล์จาก sequence
- แสดงชื่อตามภาษา UI, แสดงชื่ออังกฤษบรรทัดรองเฉพาะเมื่อไม่ซ้ำ และแสดงรุ่น Emoji
- preview ปิดเมื่อ pointer ออกจาก tile, กด `Esc`, เปลี่ยนรายการ/ผลค้นหา, เริ่มเลือก Emoji หรือ dismiss Picker

ตัวโหลดภาพยังคง decode ตาม DPI, ทำงานเบื้องหลัง, freeze bitmap และใช้ bounded cache เดียวกับ grid หากภาพรายการเดียวเสีย preview แสดง placeholder แต่ Emoji Entry และ Unicode sequence ยังใช้งานได้

## การตรวจสอบ

รันจาก repository root:

```powershell
.\scripts\verify-search-preview.ps1
```

สคริปต์ตรวจ Emoji Entry 3,944 รายการและ asset preview ครบทุก path, การค้นชื่อ/keyword ไทย–อังกฤษ, match tiers และ CLDR tie-break แบบ deterministic, accessible name, focus ของ popup, รายละเอียด preview, PNG 512 และ guardrail การตอบสนองของดัชนี 100 ครั้ง สคริปต์นี้ทำงาน offline
