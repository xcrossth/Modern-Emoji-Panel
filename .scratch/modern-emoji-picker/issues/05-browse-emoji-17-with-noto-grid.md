# 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**What to build:** ให้ผู้ใช้เปิด Picker และเรียกดู Emoji Entry จาก Emoji Baseline จริงตามหมวดมาตรฐาน โดยแสดง Noto artwork ที่ชัดเจนบน Windows 10 โดยไม่พึ่ง Segoe UI Emoji เป็น primary renderer

**Blocked by:** 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์; 04: สร้าง Emoji Baseline ที่สมบูรณ์และตรวจสอบซ้ำได้

**Status:** ready-for-agent

- [ ] Picker โหลด Emoji Baseline ที่ bundle มาและแสดงหมวดมาตรฐานครบ โดยเปิด Smileys & Emotion เมื่อ Recent ยังว่าง
- [ ] grid ใช้ PNG 128, tile 32 DIP และ decode ตาม physical pixels ของ DPI
- [ ] เฉพาะรายการ visible และ near-viewport ถูก lazy decode ผ่าน bounded cache และภาพที่ decode แล้วไม่ถูกแก้ไข
- [ ] virtualization ทำให้เลื่อน catalog เต็มชุดได้โดยไม่สร้าง tile หรือ decode PNG ทั้งหมดล่วงหน้า
- [ ] grid ใช้งานได้ที่ DPI 100–250% รวมการย้ายข้าม monitor ที่ DPI ต่างกันโดยไม่เกิดภาพผิดขนาดรุนแรง
- [ ] หากภาพของรายการเดียวเสีย แสดง placeholder พร้อมชื่อและยังเลือก Unicode sequence นั้นได้
- [ ] หาก asset ทั้งชุดหาย Picker แสดงคำแนะนำ Repair/Reinstall แทนการ crash หรือเปิดไม่ได้
