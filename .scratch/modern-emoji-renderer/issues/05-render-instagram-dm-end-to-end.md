# 05: Render Instagram DM แบบ End-to-End

**What to build:** ให้ผู้ใช้ Chrome บน Windows 10 เห็น Emoji ใหม่ด้วย Noto ใน Instagram Web DM ทั้งข้อความที่ส่งเองและได้รับ โดยรองรับข้อความใหม่, รายการสนทนา, message preview, การเปลี่ยนห้องและประวัติย้อนหลัง ขณะที่ composer และ Thai IME ยังทำงานตามเดิม

**Blocked by:** 04: Render Dynamic Display Content แบบ Incremental

**Status:** ready-for-agent

- [ ] ข้อความที่ส่งเองและข้อความที่ได้รับใน conversation transcript แสดง Emoji ใหม่ที่ Windows 10 เดิมเป็น Tofu ได้ถูก
- [ ] ข้อความใหม่แบบ dynamic, message preview, การเปลี่ยนห้อง และประวัติที่โหลดเพิ่มถูก render โดยไม่ต้อง reload หน้า
- [ ] ทดสอบข้อความเฉพาะ Emoji, Thai + Emoji, English + Emoji, ZWJ, skin tone, keycap และ flag
- [ ] Surrounding typography, layout, selection และ Copy ยังคงถูกต้องบนหน้า Instagram จริง
- [ ] Composer ถูกจัดเป็น Editable Content และการพิมพ์ภาษาไทย, caret, selection, composition และการส่งข้อความไม่พัง
- [ ] มี site fixture หรือ regression harness ที่จับ DOM behavior สำคัญได้โดยไม่พึ่ง selector ที่เปราะเกินจำเป็น
- [ ] บันทึกผล manual บน Chrome/Windows 10 พร้อม version และหลักฐานที่ตรวจย้อนกลับได้
