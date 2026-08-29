# Matrix ทดสอบ Renderer บนเว็บไซต์หลัก

ไฟล์นี้เป็นจุดบันทึก manual qualification บนบัญชีจริงสำหรับ Instagram Web DM และ TikTok Web Chat หลังโหลด unpacked extension จาก branch Renderer

สถานะปัจจุบัน: **รอผู้ใช้ทดสอบบนบัญชีจริง** — automated site fixtures และ Chrome DOM fixture ผ่านแล้ว แต่ยังไม่ถือเป็นหลักฐาน End-to-End ของเว็บไซต์ production

| เว็บไซต์ | Scenario | สถานะ | หลักฐาน/หมายเหตุ |
|---|---|---|---|
| Instagram DM | ข้อความที่ส่งเอง/ได้รับ | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| Instagram DM | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| Instagram DM | selection, Copy, layout, typography | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| Instagram DM | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ยังไม่ทดสอบ | Renderer ข้าม Editable Content โดย design |
| TikTok Chat | ข้อความที่ส่งเอง/ได้รับ | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| TikTok Chat | ข้อความใหม่, preview, เปลี่ยนห้อง, ประวัติย้อนหลัง | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| TikTok Chat | selection, Copy, layout, typography | ยังไม่ทดสอบ | ต้องใช้บัญชีจริง |
| TikTok Chat | composer, caret, selection, Thai IME, composition, ส่งข้อความ | ยังไม่ทดสอบ | Renderer ข้าม Editable Content โดย design |

ชุด Emoji ที่ต้องครอบคลุม: เฉพาะ Emoji, ไทย + Emoji, English + Emoji, Emoji ใหม่ที่ Windows 10 เดิมไม่รองรับ, ZWJ, skin tone, keycap, regional flag และ tag sequence

หลักฐาน automated ปัจจุบัน:

- `apps/renderer-extension/tests/fixtures/instagram-dm.html`
- `apps/renderer-extension/tests/fixtures/tiktok-chat.html`
- `apps/renderer-extension/tests/primary-site-fixtures.test.ts`
- `docs/research/renderer-dom-pipeline/results/report.json`
