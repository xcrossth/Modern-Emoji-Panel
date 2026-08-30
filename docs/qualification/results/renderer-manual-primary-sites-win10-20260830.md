# ผล Manual Qualification ของ Renderer บนเว็บไซต์หลัก

สถานะ: **ผ่าน**

ทดสอบวันที่ 30 สิงหาคม 2026 บน Windows 10 Enterprise N 22H2 build 19045, Chrome Stable 151.0.7922.174 และ Modern Emoji Renderer 0.0.1

## Instagram Web DM

- Emoji ในข้อความแสดงด้วย Noto ถูกต้องหลังแก้เส้นทางโหลด bundled font
- ข้อความใหม่และการสลับห้องไปมายัง render ถูกต้อง
- Copy ข้อความเดิมและ Paste Emoji ยังคง Unicode และใช้งานได้
- ช่องพิมพ์คงการแสดงผลเดิมตามสเปก v1 และไม่ถูกรบกวนโดย Renderer

## TikTok Web Chat

- Emoji ในข้อความแสดงด้วย Noto ถูกต้องหลัง reload หน้าให้ใช้ content script รุ่นล่าสุด
- ข้อความใหม่และการสลับห้องไปมายัง render ถูกต้อง
- Copy ข้อความเดิมและ Paste Emoji ยังคง Unicode และใช้งานได้
- ช่องพิมพ์คงการแสดงผลเดิมตามสเปก v1 และไม่ถูกรบกวนโดย Renderer

## การตีความช่องพิมพ์

การที่ Emoji ใน composer ยังแสดงแบบเดิมเป็นผลที่คาดหวัง ไม่ใช่ failure รุ่นแรกจงใจข้าม `input`, `textarea` และ `contenteditable` เพื่อรักษา caret, selection, keyboard layout และ IME เมื่อส่งข้อความแล้วและข้อความกลายเป็น Display Content จึงใช้ Noto Renderer

หลักฐานมาจากภาพหน้าจอ Instagram/TikTok ที่แสดง wrapper ใน Elements panel และคำยืนยันผลทดสอบของผู้ใช้ใน Codex session เดียวกัน
