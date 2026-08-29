# เครื่องมือสร้าง Emoji Baseline

โปรเจกต์ .NET 10 นี้อ่าน source ที่ตรึงไว้ใต้ `vendor/`, ตรวจ checksum ทุกไฟล์ แล้วสร้าง JSON กลางที่ Picker ใช้งานได้และ Renderer Extension นำไปใช้ภายหลังได้โดยไม่ผูกกับชนิดข้อมูลของ .NET

ขอบเขตของเครื่องมือ:

- อ่าน fully-qualified sequence ตามลำดับใน Unicode Emoji 17
- รวมชื่อย่อและ keyword จาก CLDR `annotations` กับ `annotationsDerived` ทั้งภาษาอังกฤษและไทย
- สร้าง stable ID และเก็บ code point sequence เดิมสำหรับ insert/copy
- สร้าง asset mapping จาก inventory ของ Noto โดยมี key และ aliases ชัดเจน
- map ธงประเทศ/เขตย่อยไปยัง region-flags source ที่ตรึงไว้ โดย resolve alias ของ upstream ไปยัง PNG ปลายทางจริงและตรวจ PNG signature ก่อนสร้าง baseline
- fail เมื่อพบ source checksum ผิด, sequence/ID ซ้ำ, metadata ขาด หรือ asset role สำหรับ grid/preview ขาด
- สร้าง manifest และรายงาน delta/asset anomaly แบบ deterministic

ใช้งานผ่าน wrapper จาก repository root:

```powershell
.\scripts\generate-emoji-baseline.ps1
.\scripts\verify-generated-emoji-baseline.ps1
```

ไม่ควรเรียก executable โดยตรงจาก Picker runtime และเครื่องมือนี้ไม่มี network client การดาวน์โหลด source เป็นหน้าที่ของคำสั่ง update baseline ที่ผู้ใช้เรียกโดยตั้งใจเท่านั้น
