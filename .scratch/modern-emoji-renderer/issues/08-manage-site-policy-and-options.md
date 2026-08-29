# 08: จัดการ Site Policy และ Options

**What to build:** ให้ผู้ใช้กำหนดขอบเขต Renderer ผ่าน Options ได้ทั้ง allowlist, denylist และ all-sites mode พร้อม reset settings, debug mode, renderer mode และข้อมูลเวอร์ชัน โดยค่าเริ่มต้นอนุญาตเฉพาะ Instagram กับ TikTok และขอสิทธิ์กว้างขึ้นเมื่อผู้ใช้เลือกเท่านั้น

**Blocked by:** 07: ควบคุม Renderer รายเว็บไซต์จาก Popup

**Status:** ready-for-agent

- [ ] Default settings เปิด Renderer เฉพาะ `instagram.com` และ `tiktok.com`
- [ ] ผู้ใช้เพิ่ม/ลบ site, เลือก allowlist/denylist และ reset กลับค่าเริ่มต้นได้โดยมี validation ที่ชัดเจน
- [ ] All-sites mode ขอ optional host permission เมื่อผู้ใช้เปิด และถอน/ลดขอบเขต permission ได้เมื่อปิด
- [ ] Debug mode ปิดโดยค่าเริ่มต้นและ renderer mode เลือกได้เฉพาะวิธีที่ Ticket 02 รับรองไว้
- [ ] แสดง Extension, Unicode Emoji Baseline และ Noto asset version ที่ใช้งานจริง
- [ ] Popup, Options, service worker และ content scripts เห็น settings รุ่นเดียวกันโดยไม่ต้อง reload Extension
- [ ] Settings มี schema/version handling และ automated tests สำหรับ defaults, migration, validation, reset และ permission transitions
