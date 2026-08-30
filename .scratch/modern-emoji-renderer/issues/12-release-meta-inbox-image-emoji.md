# 12: รองรับและเผยแพร่ Meta Inbox Image Emoji

**What to build:** เพิ่ม Facebook Messages/Inbox และ Messenger.com Inbox เป็นเว็บไซต์หลัก แทน image Emoji ของ Meta ด้วย bundled Noto โดยไม่กระทบรูปทั่วไปหรือ composer แล้วเผยแพร่ Renderer 0.0.3 จากเครื่อง local

**Blocked by:** 10: สร้าง Renderer Extension Release Package; 11: แสดง Instagram Emoji image ใน message bubble

**Status:** resolved

- [x] Manifest, site context และ default settings รองรับ `facebook.com` กับ `messenger.com` พร้อม migration ที่ไม่ทับ custom allowlist
- [x] Emoji เดี่ยว, bubble และ reaction จาก Meta CDN แสดงด้วย Noto โดยคง text/alt และคืน DOM ได้
- [x] Wrapper รักษาขนาด 16/32/56px และซิงก์ Quick Emoji เมื่อ React เปลี่ยน `width`/`height` ภายหลัง
- [x] รูปโปรไฟล์ รูปทั่วไป story และ Editable Content/composer ไม่ถูกเปลี่ยนแปลง
- [x] Automated fixture และ manual E2E ผ่าน Facebook Messages/Inbox กับ Messenger.com Inbox
- [x] Facebook scope ระบุชัดว่าไม่รวม post หรือ comment
- [x] Release 0.0.3 สร้างและตรวจจากเครื่อง local โดยไม่ใช้ GitHub Actions

## Comments

### 30 สิงหาคม 2026 — qualification และการปิด scope

Maintainer ทดสอบ Facebook Messages/Inbox และ Messenger.com Inbox แล้วให้ผลเหมือนกัน ทั้งข้อความใหม่ การสลับห้อง Emoji-only, bubble, reaction, รูปทั่วไปและ composer ส่วน Quick Emoji ของ Facebook ใช้ภาพฐาน 56px กับ parent transform ระหว่างกดค้าง จึงเพิ่ม observer ให้ซิงก์ wrapper เมื่อ React อัปเดตขนาดภายหลัง และล็อกด้วย regression test ผู้ดูแลยืนยันว่าจะไม่ขยายไป Facebook post/comment, Editable Content, Chromium browser อื่นหรือ Chrome Web Store
