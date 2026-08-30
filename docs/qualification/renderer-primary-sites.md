# Matrix ทดสอบ Renderer บนเว็บไซต์หลัก

ไฟล์นี้เป็นจุดบันทึก manual qualification บนบัญชีจริงสำหรับ Instagram Web DM และ TikTok Web Chat หลังโหลด unpacked extension จาก branch Renderer

สถานะปัจจุบัน: **ผ่าน** — automated fixtures, actual-font gate และ manual E2E บนบัญชีจริงผ่านแล้ว รายละเอียดอยู่ที่ `docs/qualification/results/renderer-manual-primary-sites-win10-20260830.md`

| เว็บไซต์ | Scenario | สถานะ | หลักฐาน/หมายเหตุ |
|---|---|---|---|
| Instagram DM | ข้อความที่ส่งเอง/ได้รับ | ผ่าน | Emoji แสดงด้วย Noto ถูกต้องบนบัญชีจริง |
| Instagram DM | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ผ่าน | ข้อความใหม่และสลับห้องไปมายังแสดงถูกต้อง |
| Instagram DM | selection, Copy, layout, typography | ผ่าน | Copy/Paste คง Unicode และหน้าข้อความแสดงปกติ |
| Instagram DM | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ผ่าน | ช่องพิมพ์ยังใช้ renderer เดิมตาม Editable Content design และไม่กระทบการใช้งาน |
| TikTok Chat | ข้อความที่ส่งเอง/ได้รับ | ผ่าน | Emoji แสดงด้วย Noto ถูกต้องบนบัญชีจริง |
| TikTok Chat | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ผ่าน | ข้อความใหม่และสลับห้องไปมายังแสดงถูกต้อง |
| TikTok Chat | selection, Copy, layout, typography | ผ่าน | Copy/Paste คง Unicode และหน้าข้อความแสดงปกติ |
| TikTok Chat | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ผ่าน | ช่องพิมพ์ยังใช้ renderer เดิมตาม Editable Content design และไม่กระทบการใช้งาน |

ชุด Emoji ที่ต้องครอบคลุม: เฉพาะ Emoji, ไทย + Emoji, English + Emoji, Emoji ใหม่ที่ Windows 10 เดิมไม่รองรับ, ZWJ, skin tone, keycap, regional flag และ tag sequence

หลักฐาน automated ปัจจุบัน:

- `apps/renderer-extension/tests/fixtures/instagram-dm.html`
- `apps/renderer-extension/tests/fixtures/tiktok-chat.html`
- `apps/renderer-extension/tests/primary-site-fixtures.test.ts`
- `docs/research/renderer-dom-pipeline/results/report.json`
