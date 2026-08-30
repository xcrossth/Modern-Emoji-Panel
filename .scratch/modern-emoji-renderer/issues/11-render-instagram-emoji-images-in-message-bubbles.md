# 11: Render Instagram Emoji Images ใน Message Bubble

**What to build:** ให้ Emoji ภายใน Instagram DM bubble ซึ่งเว็บไซต์แปลงเป็น `<img>` จาก `cdninstagram.com/images/emoji.php` แสดงด้วย bundled Noto เช่นเดียวกับ Emoji-only message โดยไม่แตะรูปทั่วไปหรือ Editable Content

**Blocked by:** ไม่มี

**Status:** needs-info

- [x] จำแนกเฉพาะ Instagram Emoji image ที่ URL และ `alt` เป็น Emoji sequence ใน baseline
- [x] รองรับทั้งข้อความที่มีอยู่ตอน initial scan และข้อความที่เพิ่มภายหลัง
- [x] เก็บ source image ไว้แบบซ่อนและคืน DOM เดิมเมื่อปิด Renderer
- [x] ไม่เปลี่ยนรูปทั่วไป แม้ `alt` จะเป็น Emoji และไม่เปลี่ยนรูปภายใน Editable Content
- [x] Chrome E2E ยืนยันว่า wrapper ชนิด image ใช้ bundled `Noto Color Emoji` จริง
- [ ] Manual E2E บน Instagram จริงผ่าน reply story/note และ bubble ที่มีข้อความผสม Emoji
- [ ] สร้าง patch release หลัง manual gate ผ่าน

## Comments

- ภาพจากผู้ใช้แสดงว่า Instagram ใช้ `<img alt="🥺" src="https://static.cdninstagram.com/images/emoji.php/...">` ภายใน bubble ทำให้ renderer เดิมซึ่งสแกนเฉพาะ Text node มองไม่เห็น Emoji
- Regression ครอบคลุม static, dynamic, restore, ordinary-image allowlist และ Editable Content boundary
- รอผู้ใช้ reload test build และยืนยันผลบนบัญชีจริงก่อนปิด ticket และออก patch release
