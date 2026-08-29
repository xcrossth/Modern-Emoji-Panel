# 07: ควบคุม Renderer รายเว็บไซต์จาก Popup

**What to build:** ให้ผู้ใช้เปิด popup ขนาดเล็กเพื่อดูว่าเว็บไซต์ปัจจุบันได้รับการแก้ Emoji อยู่หรือไม่ เปิด/ปิดต่อ site ได้ และเห็นจำนวน Emoji nodes ที่ Renderer แก้บนหน้าปัจจุบันโดยไม่เปิดเผยข้อมูลข้อความ

**Blocked by:** 04: Render Dynamic Display Content แบบ Incremental

**Status:** ready-for-agent

- [ ] Popup แสดงสถานะ Enabled on this site และ toggle ได้สำหรับ host ปัจจุบัน
- [ ] การเปลี่ยนสถานะมีผลกับ content ปัจจุบันและ content ที่เพิ่มภายหลังโดยไม่ทำ Unicode text เสีย
- [ ] ค่า per-site คงอยู่หลังปิด/เปิด Chrome และ sync กับ content scripts ทุก tab ที่เกี่ยวข้อง
- [ ] Popup แสดงจำนวน Emoji nodes ที่แก้บนหน้าปัจจุบันและอัปเดตหลัง dynamic processing
- [ ] Counter และ messaging ไม่เก็บหรือส่งเนื้อหาข้อความ, Emoji sequence หรือข้อมูลบัญชีผู้ใช้
- [ ] Popup ใช้งานด้วย keyboard, มี accessible names และรองรับ Light/Dark ของ browser อย่างอ่านได้
- [ ] Automated tests ครอบคลุม toggle, persistence, tab messaging และ unavailable/restricted page states
