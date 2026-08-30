# ผลทดสอบ Instagram Emoji Image Hotfix

สถานะ: **ผ่าน**

ทดสอบวันที่ 30 สิงหาคม 2026 บน Windows 10 Enterprise N 22H2 build 19045 และ Chrome Stable 151.0.7922.174 โดยใช้ test build จาก commit `f9d4f1f` ซึ่งเป็น code สำหรับ patch release 0.0.2

## ผลทดสอบบนบัญชีจริง

- Emoji ภายใน message bubble เช่น reply story/note หรือ bubble ที่มีข้อความผสม แสดงด้วย Noto แล้ว
- Emoji ใน reaction picker และ reaction ที่แสดงบนข้อความใช้ Noto ถูกต้อง
- รูป story และ profile แสดงตามเดิมและไม่ได้รับผลกระทบ
- การสลับห้องสนทนายังทำงานปกติ

## ขอบเขตการแก้

Renderer ยอมรับเฉพาะ `<img>` จาก `cdninstagram.com/images/emoji.php` ซึ่งมี `alt` เป็น Emoji sequence ใน baseline รูปทั่วไปและ Editable Content จึงไม่เข้าสู่ image-Emoji pipeline
