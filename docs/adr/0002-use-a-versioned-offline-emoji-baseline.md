# ใช้ Emoji Baseline แบบตรึงเวอร์ชันและ bundle สำหรับ offline runtime

โครงการเลือกตรึง Unicode 17.0.0, Emoji 17.0, CLDR 48.2 และ Noto Emoji v2.051 เป็น baseline เดียว แล้ว commit ข้อมูลกับ PNG canonical ขนาด 128 และ 512 px โดยตรงใน Git แอปจึงค้นหา แสดงและส่ง Emoji ได้แบบ deterministic และ offline แม้ต้องแลกกับ artwork ดิบประมาณ 110 MiB

## ตัวเลือกที่พิจารณา

- ใช้ข้อมูลหรือฟอนต์ของ Windows: package เล็กกว่า แต่ไม่แก้ปัญหา Emoji ใหม่บน Windows 10
- ดาวน์โหลด latest ตอน build หรือ runtime: ลดขนาด repository แต่ผลลัพธ์เปลี่ยนตามเวลาและต้องพึ่งเครือข่าย
- ใช้ PNG 512 ชุดเดียว: ลด asset ซ้ำ แต่ visual spike พบว่า grid ไม่คมกว่าสม่ำเสมอและ decode ช้ากว่า 128 ประมาณห้าเท่า

## ผลที่ตามมา

- Generator ต้องตรวจ checksum, aliases และ coverage ของทุก fully-qualified sequence
- Ordinary build และ runtime ต้องไม่ดาวน์โหลดข้อมูล
- Baseline update เป็นการเปลี่ยนโดยตั้งใจและต้องอัปเดต artifact ของทุกผลิตภัณฑ์ใน commit เดียวกัน
- Release ต้องรวม Unicode, Noto และ region-flag notices ที่ถูกต้อง
