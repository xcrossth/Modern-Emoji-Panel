# 09: รับรอง Performance, Text Integrity และ Compatibility

**What to build:** ให้ maintainer มีหลักฐานอัตโนมัติและ manual ว่า Renderer ใช้งานบน Instagram DM และ TikTok Web Chat ได้โดยไม่ทำข้อความ, accessibility, Editable Content หรือ responsiveness เสีย พร้อม regression coverage สำหรับ all-sites mode

**Blocked by:** 05: Render Instagram DM แบบ End-to-End; 06: Render TikTok Web Chat แบบ End-to-End; 08: จัดการ Site Policy และ Options

**Status:** ready-for-agent

- [ ] กำหนดและบันทึก performance budgets ที่วัดซ้ำได้สำหรับ initial scan, mutation batch, long transcript และ repeated navigation ก่อนสรุปผล qualification
- [ ] Stress fixture ยืนยันว่า long transcript, mutation burst, scrolling และ conversation switching ไม่สร้าง long freeze หรือ memory/observer growth ต่อเนื่อง
- [ ] Text-integrity suite ยืนยัน `textContent`, Copy, selection, Browser Find และ DOM extraction สำหรับ Thai/English/Emoji sequences
- [ ] Accessibility checks ครอบคลุม wrapper semantics, keyboard UI, accessible names และไม่อ่านข้อความซ้ำโดยไม่จำเป็น
- [ ] Editable Content suite ครอบคลุม caret, selection, Thai IME composition และการส่งข้อความโดย Renderer ไม่แก้ DOM ภายใน editor
- [ ] Manual matrix ผ่านบน Chrome stable/Windows 10 สำหรับ Instagram DM และ TikTok Web Chat รวมข้อความส่งเอง/ได้รับ, dynamic messages, history และ navigation
- [ ] All-sites regression fixture หรือ smoke coverage ครอบคลุม Instagram feed/comments, Google, GitHub, Reddit, Facebook และ Discord โดยไม่เปลี่ยน typography หรือ editable behavior
- [ ] ไม่มี outbound runtime network จาก Extension นอกจาก traffic ปกติของเว็บไซต์ และไม่มี remote code path
