# Recent และ Learned Ranking บนเครื่อง

Modern Emoji Picker เก็บ Activity Data เฉพาะบนเครื่องผู้ใช้ใต้ `%APPDATA%\ModernEmojiPicker` โดยไม่มี telemetry, cloud sync หรือการอ่านข้อมูลจาก Classic Emoji Picker ข้อมูลแบ่งเป็นสองไฟล์เพื่อให้ล้างและกู้คืนแยกกันได้:

- `recent.json` เก็บ Recent แบบ MRU สูงสุด 50 รายการ แต่ละรายการมี stable ID ของ Emoji Entry ที่ resolve แล้วและ Unicode sequence จริง จึงรักษาสีผิวหรือ Variant Override ที่เลือกไว้
- `learned-ranking.json` เก็บคะแนนระดับ Emoji Entry ฐาน สีผิวต่างกันของ entry เดียวกันจึงรวมเป็นคะแนนเดียว

ทั้งสองไฟล์มี `schemaVersion` และเขียนผ่านไฟล์ชั่วคราวก่อนแทนที่ไฟล์จริงแบบ atomic ไม่มีการส่งข้อมูลออกจากเครื่อง

## การจัดลำดับ

ทุก Commit Gesture เพิ่ม Recent และ Learned Ranking ทันที ก่อนเริ่มส่ง Emoji ไปยังแอปเป้าหมาย ดังนั้นการเลือกยังถูกเรียนรู้แม้ insertion ล้มเหลว

คะแนนเป็น frequency ที่ลดลงตามเวลาแบบ half-life 90 วัน Search ใช้คะแนนนี้เฉพาะระหว่าง Emoji Entry ที่อยู่ใน match tier เดียวกันเท่านั้น ลำดับ exact name, prefix, keyword และ substring จึงไม่เปลี่ยน ส่วนคะแนนที่เท่ากันใช้ลำดับ CLDR และ stable ID เป็นตัวตัดสินแบบ deterministic

เมื่อเปิด Picker:

- หากมี Recent จะเปิดหมวด Recent
- หากไม่มี Recent จะเปิด Smileys & Emotion
- query เริ่มว่างเสมอ

## การกู้คืนไฟล์เสีย

หากไฟล์ใดอ่านไม่ได้ Picker จะไม่หยุดเปิด แต่จะ:

1. สำรองไฟล์เดิมเป็น `<ชื่อไฟล์>.corrupt-<เวลา UTC>`
2. รีเซ็ตเฉพาะ Recent หรือ Learned Ranking ที่เสีย
3. แสดงข้อความแจ้งใน Picker
4. รักษา Activity Data อีกส่วนไว้

รูปแบบ Recent รุ่นเก่าของ Modern ซึ่งเป็น JSON array ของ Unicode sequence จะถูกแปลงเป็น schema ปัจจุบันโดยจับคู่กับ Emoji Baseline ที่ bundle อยู่ การย้ายนี้ไม่อ่านหรือ import ข้อมูลของ Classic

## คำสั่งล้างข้อมูล

โดเมนมีคำสั่งแยกกันสำหรับ `Clear Recent`, `Reset learned ranking` และ `Clear all activity` การนำคำสั่งเหล่านี้ไปแสดงในหน้า Settings อยู่ใน ticket ของ Settings โดยไม่ต้องเปลี่ยนรูปแบบไฟล์หรือกติกาโดเมน

ตรวจพฤติกรรมทั้งหมดได้ด้วย:

```powershell
.\scripts\verify-activity-data.ps1
```

ชุดทดสอบครอบคลุม MRU 50 รายการ, การย้ายรายการซ้ำขึ้นหน้า, resolved sequence, การรวมคะแนนตาม base entry, half-life, ขอบเขต match tier, schema version, migration, atomic write, corruption backup/reset และคำสั่งล้างข้อมูลทั้งสามแบบ
