# Modern Emoji Renderer 0.0.3

Release นี้อัปเดต Chrome Extension โดยไม่เปลี่ยน Modern Emoji Picker 0.1.9 และ build/ตรวจแพ็กเกจจากเครื่อง local โดยไม่ใช้ GitHub Actions

## ไฟล์ดาวน์โหลด

- `modern-emoji-renderer-0.0.3.zip` — แตก ZIP แล้ว Load unpacked ใน Chrome
- `modern-emoji-renderer-0.0.3.zip.sha256` — ค่า SHA-256 สำหรับตรวจ ZIP

## การเปลี่ยนแปลง

- เพิ่ม Facebook Messages/Inbox และ Messenger.com Inbox เป็นเว็บไซต์หลักร่วมกับ Instagram DM และ TikTok Chat
- แทน Emoji ที่ Meta ส่งมาเป็น `<img>` ในข้อความ, bubble, reaction และข้อความ Emoji-only ด้วย bundled Noto Color Emoji
- รักษาขนาดและตำแหน่งของ Emoji รวมถึง Quick Emoji ที่ Facebook เปลี่ยนขนาดภายหลังด้วย React และใช้ transform ระหว่างกดค้าง
- ไม่เปลี่ยนรูปโปรไฟล์ รูปทั่วไป รูป story หรือ Editable Content/composer
- อัปเกรด settings schema โดยเพิ่ม Facebook/Messenger ให้ผู้ใช้ค่าเริ่มต้นเดิมโดยไม่ทับ custom allowlist

## Qualification

- ชุดทดสอบอัตโนมัติ, font/runtime, UI, performance และ release verification ผ่าน
- Manual E2E ผ่านบน Instagram DM, TikTok Chat, Facebook Messages/Inbox และ Messenger.com Inbox
- Facebook qualification ครอบคลุม Messages/Inbox เท่านั้น ไม่รวม post หรือ comment

## ข้อจำกัด

- Emoji ในช่องพิมพ์ไม่ถูกเปลี่ยน เพื่อรักษา caret, selection, keyboard layout และ IME
- ไม่แก้ Emoji ที่วาดใน canvas, video, รูปทั่วไป หรือ closed shadow root
- เว็บไซต์อาจเปลี่ยน DOM ในอนาคตและต้องทดสอบใหม่เมื่อเกิด regression

## ติดตั้ง

ตรวจค่า SHA-256, แตก ZIP ไปยังโฟลเดอร์ถาวร เปิด `chrome://extensions`, เปิด Developer mode แล้วกด Load unpacked จากโฟลเดอร์ที่มี `manifest.json` จากนั้น refresh หน้าแชทที่เปิดอยู่
