# 07: ควบคุม Renderer รายเว็บไซต์จาก Popup

**What to build:** ให้ผู้ใช้เปิด popup ขนาดเล็กเพื่อดูว่าเว็บไซต์ปัจจุบันได้รับการแก้ Emoji อยู่หรือไม่ เปิด/ปิดต่อ site ได้ และเห็นจำนวน Emoji nodes ที่ Renderer แก้บนหน้าปัจจุบันโดยไม่เปิดเผยข้อมูลข้อความ

**Blocked by:** 04: Render Dynamic Display Content แบบ Incremental

**Status:** resolved

- [x] Popup แสดงสถานะ Enabled on this site และ toggle ได้สำหรับ host ปัจจุบัน
- [x] การเปลี่ยนสถานะมีผลกับ content ปัจจุบันและ content ที่เพิ่มภายหลังโดยไม่ทำ Unicode text เสีย
- [x] ค่า per-site คงอยู่หลังปิด/เปิด Chrome และ sync กับ content scripts ทุก tab ที่เกี่ยวข้อง
- [x] Popup แสดงจำนวน Emoji nodes ที่แก้บนหน้าปัจจุบันและอัปเดตหลัง dynamic processing
- [x] Counter และ messaging ไม่เก็บหรือส่งเนื้อหาข้อความ, Emoji sequence หรือข้อมูลบัญชีผู้ใช้
- [x] Popup ใช้งานด้วย keyboard, มี accessible names และรองรับ Light/Dark ของ browser อย่างอ่านได้
- [x] Automated tests ครอบคลุม toggle, persistence, tab messaging และ unavailable/restricted page states

## Comments

- Popup poll สถานะจาก content script ทุก 750 ms เฉพาะ count/metrics และใช้ `chrome.storage.local` เป็น source of truth สำหรับทุก tab
- toggle off หยุด observer และ unwrap เป็น text node เดิม; toggle on inject หน้าปัจจุบันได้เมื่อมี activeTab/host permission
- Vitest และ Chrome UI fixture ผ่าน หลักฐาน Light/Dark/accessibility อยู่ที่ `docs/research/renderer-settings-ui/README.md`
