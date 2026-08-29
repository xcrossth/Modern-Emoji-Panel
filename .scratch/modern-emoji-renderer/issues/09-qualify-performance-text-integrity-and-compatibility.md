# 09: รับรอง Performance, Text Integrity และ Compatibility

**What to build:** ให้ maintainer มีหลักฐานอัตโนมัติและ manual ว่า Renderer ใช้งานบน Instagram DM และ TikTok Web Chat ได้โดยไม่ทำข้อความ, accessibility, Editable Content หรือ responsiveness เสีย พร้อม regression coverage สำหรับ all-sites mode

**Blocked by:** 05: Render Instagram DM แบบ End-to-End; 06: Render TikTok Web Chat แบบ End-to-End; 08: จัดการ Site Policy และ Options

**Status:** ready-for-human

- [x] กำหนดและบันทึก performance budgets ที่วัดซ้ำได้สำหรับ initial scan, mutation batch, long transcript และ repeated navigation ก่อนสรุปผล qualification
- [x] Stress fixture ยืนยันว่า long transcript, mutation burst, scrolling และ conversation switching ไม่สร้าง long freeze หรือ memory/observer growth ต่อเนื่อง
- [x] Text-integrity suite ยืนยัน `textContent`, Copy, selection, Browser Find และ DOM extraction สำหรับ Thai/English/Emoji sequences
- [x] Accessibility checks ครอบคลุม wrapper semantics, keyboard UI, accessible names และไม่อ่านข้อความซ้ำโดยไม่จำเป็น
- [x] Editable Content suite ครอบคลุม caret, selection, Thai IME composition และการส่งข้อความโดย Renderer ไม่แก้ DOM ภายใน editor
- [ ] Manual matrix ผ่านบน Chrome stable/Windows 10 สำหรับ Instagram DM และ TikTok Web Chat รวมข้อความส่งเอง/ได้รับ, dynamic messages, history และ navigation
- [x] All-sites regression fixture หรือ smoke coverage ครอบคลุม Instagram feed/comments, Google, GitHub, Reddit, Facebook และ Discord โดยไม่เปลี่ยน typography หรือ editable behavior
- [x] ไม่มี outbound runtime network จาก Extension นอกจาก traffic ปกติของเว็บไซต์ และไม่มี remote code path

## Comments

- `scripts/verify-renderer-qualification.ps1` ผ่าน 62 tests / 26 suites และ Chrome fixtures ทั้ง rendering, DOM, UI, performance, load smoke
- ผลรอบ Windows 10: initial 2,000 ข้อความ 62.3 ms, burst 1,000 ข้อความ 29.7 ms, batch สูงสุด 9.1 ms, navigation processing 134.6 ms, retained heap 296,536 bytes ทุกค่าอยู่ใน budget
- production bundle ไม่มี outbound network API หรือ remote-code path จาก static audit; details อยู่ที่ `docs/qualification/results/renderer-automated-win10-20260830/`
- เหลือ checkbox manual matrix บน Instagram/TikTok บัญชีจริงเท่านั้น จึงตั้ง `ready-for-human` และไม่ใช้ผลอัตโนมัติแทน E2E จริง
