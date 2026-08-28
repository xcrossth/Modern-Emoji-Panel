# 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**What to build:** ให้ผู้ใช้เปิด Picker และเรียกดู Emoji Entry จาก Emoji Baseline จริงตามหมวดมาตรฐาน โดยแสดง Noto artwork ที่ชัดเจนบน Windows 10 โดยไม่พึ่ง Segoe UI Emoji เป็น primary renderer

**Blocked by:** 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์; 04: สร้าง Emoji Baseline ที่สมบูรณ์และตรวจสอบซ้ำได้

**Status:** resolved

- [x] Picker โหลด Emoji Baseline ที่ bundle มาและแสดงหมวดมาตรฐานครบ โดยเปิด Smileys & Emotion เมื่อ Recent ยังว่าง
- [x] grid ใช้ PNG 128, tile 32 DIP และ decode ตาม physical pixels ของ DPI
- [x] เฉพาะรายการ visible และ near-viewport ถูก lazy decode ผ่าน bounded cache และภาพที่ decode แล้วไม่ถูกแก้ไข
- [x] virtualization ทำให้เลื่อน catalog เต็มชุดได้โดยไม่สร้าง tile หรือ decode PNG ทั้งหมดล่วงหน้า
- [x] grid ใช้งานได้ที่ DPI 100–250% รวมการย้ายข้าม monitor ที่ DPI ต่างกันโดยไม่เกิดภาพผิดขนาดรุนแรง
- [x] หากภาพของรายการเดียวเสีย แสดง placeholder พร้อมชื่อและยังเลือก Unicode sequence นั้นได้
- [x] หาก asset ทั้งชุดหาย Picker แสดงคำแนะนำ Repair/Reinstall แทนการ crash หรือเปิดไม่ได้

## หลักฐานการตรวจรับ

- commit งาน: `d8d0d90` (`feat(ticket-05): browse emoji 17 with virtualized Noto grid`)
- `scripts/verify-noto-grid.ps1` ผ่าน: 3,944 รายการ, 9 หมวดมาตรฐาน, PNG 128 bundle ครบ และไม่มี runtime dependency ต่อ `Emoji.Wpf`
- WPF smoke ตรวจ decode ภาพจริงแบบ background, `Freeze()`, LRU cache ไม่เกิน 256 ภาพ, virtualization ไม่ realize ทั้ง catalog, missing-image placeholder และ Repair/Reinstall state
- ตรวจการคำนวณ `DecodePixelWidth` ที่ DPI 100, 125, 150, 175, 200, 225 และ 250% พร้อม reload เมื่อ `DpiChanged`
- `scripts/verify-foundation.ps1` ผ่านทั้ง build, self-contained publish, WPF browse/search smoke และ format gate
- `scripts/test-clean-checkout.ps1 -Revision HEAD` ผ่านจาก detached clean checkout รวม generator determinism และ Noto grid verification
