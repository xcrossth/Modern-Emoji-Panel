# Matrix ทดสอบ Renderer บนเว็บไซต์หลัก

ไฟล์นี้เป็นจุดบันทึก manual qualification บนบัญชีจริงสำหรับ Instagram Web DM, TikTok Web Chat, Facebook Messages และ Messenger.com หลังโหลด unpacked extension

สถานะปัจจุบัน: **Instagram/TikTok/Facebook Messages/Messenger.com ผ่านสำหรับรุ่น 0.0.3** — automated fixtures และ manual qualification บนบัญชีจริงผ่านแล้ว ส่วน Facebook รับรองเฉพาะ Messages/Inbox ไม่รวม post หรือ comment ผล manual เดิมอยู่ที่ `docs/qualification/results/renderer-manual-primary-sites-win10-20260830.md`

| เว็บไซต์ | Scenario | สถานะ | หลักฐาน/หมายเหตุ |
|---|---|---|---|
| Instagram DM | ข้อความที่ส่งเอง/ได้รับ | ผ่าน | Emoji-only, bubble แบบ reply story/note, ข้อความผสม และ reaction แสดงด้วย Noto ถูกต้องบนบัญชีจริง |
| Instagram DM | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ผ่าน | ข้อความใหม่และสลับห้องไปมายังแสดงถูกต้อง |
| Instagram DM | selection, Copy, layout, typography | ผ่าน | Copy/Paste คง Unicode และหน้าข้อความแสดงปกติ |
| Instagram DM | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ผ่าน | ช่องพิมพ์ยังใช้ renderer เดิมตาม Editable Content design และไม่กระทบการใช้งาน |
| TikTok Chat | ข้อความที่ส่งเอง/ได้รับ | ผ่าน | Emoji แสดงด้วย Noto ถูกต้องบนบัญชีจริง |
| TikTok Chat | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ผ่าน | ข้อความใหม่และสลับห้องไปมายังแสดงถูกต้อง |
| TikTok Chat | selection, Copy, layout, typography | ผ่าน | Copy/Paste คง Unicode และหน้าข้อความแสดงปกติ |
| TikTok Chat | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ผ่าน | ช่องพิมพ์ยังใช้ renderer เดิมตาม Editable Content design และไม่กระทบการใช้งาน |
| Facebook Messages/Inbox | Emoji เดี่ยว, Emoji ใน bubble, reaction และ Quick Emoji แบบ image | ผ่าน | ทดสอบด้วยบัญชีจริงแล้ว; ใช้ Noto และคงขนาด 16/32px รวมถึงขนาดฐาน 56px ที่ Facebook ใช้ย่อ/ขยาย Quick Emoji ไม่ครอบคลุม post/comment |
| Facebook Messages | รูปโปรไฟล์/รูปทั่วไป และ composer | ผ่าน | ทดสอบหน้าเว็บจริงแล้ว; รูปทั่วไปและพื้นที่แก้ไขข้อความไม่ถูกเปลี่ยนแปลง |
| Messenger.com Inbox | Emoji เดี่ยว, Emoji ใน bubble และ reaction แบบ image | ผ่าน | ทดสอบด้วยบัญชีจริงแล้วและให้ผลเหมือน Facebook Messages/Inbox |
| Messenger.com Inbox | ข้อความใหม่, เปลี่ยนห้อง, รูปทั่วไป และ composer | ผ่าน | ข้อความใหม่และการสลับห้องทำงานต่อเนื่อง รูปทั่วไปและพื้นที่แก้ไขข้อความไม่ถูกเปลี่ยนแปลง |

ชุด Emoji ที่ต้องครอบคลุม: เฉพาะ Emoji, ไทย + Emoji, English + Emoji, Emoji ใหม่ที่ Windows 10 เดิมไม่รองรับ, ZWJ, skin tone, keycap, regional flag และ tag sequence

หลักฐาน automated ปัจจุบัน:

- `apps/renderer-extension/tests/fixtures/instagram-dm.html`
- `apps/renderer-extension/tests/fixtures/tiktok-chat.html`
- `apps/renderer-extension/tests/fixtures/facebook-messenger.html`
- `apps/renderer-extension/tests/primary-site-fixtures.test.ts`
- `docs/research/renderer-dom-pipeline/results/report.json`
- `docs/qualification/results/renderer-instagram-emoji-images-win10-20260830.md`
