# Modern Emoji Renderer

Chrome Extension แบบ Manifest V3 สำหรับแสดง Emoji รุ่นใหม่บน Windows 10 โดยมี Instagram Web DM และ TikTok Web Chat เป็นเป้าหมายหลัก ผลิตภัณฑ์นี้ทำงานแยกจาก Modern Emoji Picker และไม่เปลี่ยน Windows system font

## Build และตรวจสอบ

```powershell
npm --prefix .\apps\renderer-extension ci
npm --prefix .\apps\renderer-extension run verify
```

unpacked extension จะอยู่ใต้ `artifacts\renderer-extension\unpacked` หลัง build สำเร็จ

## โหลดใน Chrome ระหว่างพัฒนา

1. เปิด `chrome://extensions`
2. เปิด Developer mode
3. เลือก Load unpacked
4. เลือกโฟลเดอร์ `artifacts\renderer-extension\unpacked`

Ticket 01 เป็น foundation ที่ยังไม่แก้ DOM การ render จะเริ่มหลังการทดลองเลือก renderer และมี text-integrity tests แล้ว

Automated Chrome load smoke ใช้ Chrome for Testing เพื่อไม่แตะ profile จริง และเพราะ Chrome branded รุ่นใหม่ไม่รองรับ command-line extension flags สำหรับ automation ครบชุด:

```powershell
.\scripts\install-chrome-for-testing.ps1
.\scripts\verify-renderer-chrome-load.ps1 -SkipBuild
```
