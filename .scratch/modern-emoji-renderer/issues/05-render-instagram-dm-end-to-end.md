# 05: Render Instagram DM แบบ End-to-End

**What to build:** ให้ผู้ใช้ Chrome บน Windows 10 เห็น Emoji ใหม่ด้วย Noto ใน Instagram Web DM ทั้งข้อความที่ส่งเองและได้รับ โดยรองรับข้อความใหม่, รายการสนทนา, message preview, การเปลี่ยนห้องและประวัติย้อนหลัง ขณะที่ composer และ Thai IME ยังทำงานตามเดิม

**Blocked by:** 04: Render Dynamic Display Content แบบ Incremental

**Status:** resolved

- [x] ข้อความที่ส่งเองและข้อความที่ได้รับใน conversation transcript แสดง Emoji ใหม่ที่ Windows 10 เดิมเป็น Tofu ได้ถูก
- [x] ข้อความใหม่แบบ dynamic, message preview, การเปลี่ยนห้อง และประวัติที่โหลดเพิ่มถูก render โดยไม่ต้อง reload หน้า
- [x] ทดสอบข้อความเฉพาะ Emoji, Thai + Emoji, English + Emoji, ZWJ, skin tone, keycap และ flag
- [x] Surrounding typography, layout, selection และ Copy ยังคงถูกต้องบนหน้า Instagram จริง
- [x] Composer ถูกจัดเป็น Editable Content และการพิมพ์ภาษาไทย, caret, selection, composition และการส่งข้อความไม่พัง
- [x] มี site fixture หรือ regression harness ที่จับ DOM behavior สำคัญได้โดยไม่พึ่ง selector ที่เปราะเกินจำเป็น
- [x] บันทึกผล manual บน Chrome/Windows 10 พร้อม version และหลักฐานที่ตรวจย้อนกลับได้

## Comments

- Automated Instagram DM fixture ผ่าน sent/received/preview/live/history, Emoji matrix และ Editable Content boundary โดยใช้ generic DOM traversal ไม่ผูก production pipeline กับ selector ของ Instagram
- จาก manual รอบแรกพบ wrapper ทำงานแต่ bundled font ไม่ถูกใช้; repro ผ่าน Extension จริงยืนยันว่า relative font URL ถูก resolve เป็น origin ของ Instagram แล้วแก้ให้ใช้ `chrome.runtime.getURL` พร้อม regression ที่ตรวจ actual platform font จาก Chrome โดยตรง
- Manual E2E บนบัญชีจริงผ่านข้อความใหม่, สลับห้อง, Copy/Paste และ Editable Content behavior ตาม `docs/qualification/results/renderer-manual-primary-sites-win10-20260830.md`
