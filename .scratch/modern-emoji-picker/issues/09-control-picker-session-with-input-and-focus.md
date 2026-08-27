# 09: ควบคุม Picker Session ด้วย keyboard, pointer และ focus

**What to build:** ทำให้ Picker Session มี Browse Mode และ Search Mode ที่คาดเดาได้ พร้อม Commit Gestures, dismissal, window placement และ focus behavior ที่พาผู้ใช้กลับไปยังแอปเป้าหมายอย่างถูกต้อง

**Blocked by:** 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview; 08: ส่ง Emoji หนึ่งรายการไปยังแอปเป้าหมายอย่างปลอดภัย

**Status:** ready-for-agent

- [ ] Picker เปิดใน Browse Mode ด้วย query ว่าง และใช้ arrow keys เพื่อเลื่อน selection ได้
- [ ] click ส่งแล้วคง Picker, Enter ส่งแล้ว dismiss และ Shift+Enter ส่งแล้วคง Picker ตาม Commit Gesture ที่กำหนด
- [ ] หลัง click หรือ Shift+Enter Picker กลับมา active โดยคง selection, query, category และ scroll เดิม
- [ ] Esc ใน Search Mode ครั้งแรกกลับ Browse Mode และ Esc ถัดไป dismiss; Esc ใน Browse Mode dismiss ได้ทันที
- [ ] close button และ click ภายนอกจริง dismiss ได้ โดย click ภายนอกเคารพ focus ของหน้าต่างที่ผู้ใช้คลิกและไม่แย่งกลับ
- [ ] Picker เปิดใกล้ text caret หรือ fallback กลางหน้าต่างเป้าหมายบน monitor เดียวกัน พร้อม clamp ใน working area
- [ ] หน้าต่างปรับขนาดและจำขนาดได้ รองรับ multi-monitor/DPI และการกด hotkey ซ้ำขณะเปิดไม่เปิด Windows panel ซ้อน
- [ ] focus, selection, busy และ error states ถูกประกาศให้ accessibility API ใช้งานได้
