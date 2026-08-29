# การแสดง Emoji 17 ด้วย Noto grid

Picker โหลดรายการ 3,944 รายการจาก Emoji Baseline 17.0 ที่สร้างและ commit ไว้ใน repository โดยตรง จากนั้นแบ่งรายการตามหมวด Unicode มาตรฐาน 9 หมวด และเพิ่ม Recent เป็นแท็บแรก หาก Recent ยังว่าง แอปเปิดที่ Smileys & Emotion

## การ bundle และค้นหา asset

ตอน build ไฟล์ `emoji.json`, Noto PNG 128 และ region flags จะถูกคัดลอกไปใต้ `EmojiBaseline/` โดยรักษา path เดียวกับ manifest ตัว runtime อ่าน path ที่ generator ตรวจสอบไว้เท่านั้นและไม่อนุมานชื่อไฟล์จาก Unicode sequence จึงรองรับ canonical alias และ region flags ได้เหมือนผลของ generator สำหรับธงที่ upstream เก็บเป็น alias เช่น Bouvet Island ใช้ภาพ Norway ตัว baseline จะชี้ไปยัง PNG ปลายทางจริงแทนไฟล์ข้อความ alias

แอปไม่ดาวน์โหลดข้อมูลหรือ artwork ระหว่าง build ปกติและ runtime อีกทั้งไม่ใช้ `Emoji.Wpf` หรือ Segoe UI Emoji เป็น primary renderer ของ grid

## Lazy decode, DPI และ cache

- artwork ใน grid มีขนาด 32 DIP และอ่านจาก PNG 128
- คำนวณ `DecodePixelWidth` จาก 32 DIP คูณ DPI จริงของจอ เช่น 32 px ที่ 100% และ 80 px ที่ 250%
- เมื่อหน้าต่างย้ายข้ามจอ ตัวควบคุมที่มองเห็นอยู่จะขอภาพใหม่ตาม DPI ใหม่
- `VirtualizingWrapPanel` ใช้ recycling และ cache ก่อน/หลัง viewport อย่างละหนึ่งหน้า จึงสร้างตัวควบคุมเฉพาะช่วงที่มองเห็นหรืออยู่ใกล้ viewport
- ตัวโหลดทำงานเบื้องหลัง, รวมคำขอภาพเดียวกัน และเก็บ LRU cache ไม่เกิน 256 ภาพ
- `BitmapImage` ทุกภาพใช้ `OnLoad` แล้ว `Freeze()` ก่อนส่งให้ UI เพื่อไม่ให้ภาพถูกแก้ไขภายหลัง

หาก PNG ของรายการเดียวอ่านไม่ได้ tile จะแสดง placeholder และชื่อยังอยู่ใน accessible name ผู้ใช้ยังเลือก Unicode sequence เดิมได้ หาก baseline หรือชุด artwork หาย แอปจะแสดงคำแนะนำให้ Repair/Reinstall แทนการ crash

การค้นหาและภาพขยาย 512 อธิบายแยกใน [การค้นหาสองภาษาและ Hover Preview](./bilingual-search-hover-preview.md)

## การตรวจสอบ

รันจาก repository root:

```powershell
.\scripts\verify-noto-grid.ps1
```

สคริปต์ตรวจจำนวนรายการและหมวด, uniqueness, coverage ของ asset ใน output, การถอดรหัสธง Emoji 17 ทั้ง 270 รายการด้วย WPF, การถอด `Emoji.Wpf`, frozen bitmap, missing-image fallback, cache bound, DPI 100–250% และหน้าคำแนะนำเมื่อชุด asset หาย
