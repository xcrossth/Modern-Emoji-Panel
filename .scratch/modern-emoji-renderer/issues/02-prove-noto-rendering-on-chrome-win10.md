# 02: พิสูจน์ Renderer ที่แสดง Noto บน Chrome และ Windows 10 ได้จริง

**What to build:** ให้ผู้พัฒนามี static rendering fixture ที่พิสูจน์บน Chrome/Windows 10 ว่า Noto แบบใดแสดง Emoji ใหม่ได้ถูก พร้อมเปรียบเทียบ font renderer กับ PNG/SVG fallback และบันทึกหลักฐานเพื่อเลือก primary/fallback renderer ก่อนสร้าง production DOM pipeline

**Blocked by:** 01: โหลด Renderer Extension แบบ Unpacked ได้อย่างปลอดภัย

**Status:** ready-for-agent

- [ ] Fixture ครอบคลุม single code point, VS16, skin tone, ZWJ, family, keycap, regional flag, tag sequence และ Emoji ใหม่ที่ Windows 10 เดิมแสดงเป็น Tofu
- [ ] ทดสอบ font formats ที่มีเหตุผลกับ Chrome/Windows 10 และเปรียบเทียบกับ image fallback ในด้านความถูกต้อง, baseline, line height, zoom, HiDPI และเวลา render
- [ ] ตรวจว่า surrounding Thai/English typography และ Unicode text ไม่ถูกเปลี่ยนจากการทดลอง renderer
- [ ] บันทึกผลและเหตุผลเลือก primary renderer กับ fallback renderer เป็นเอกสารภาษาไทย พร้อมระบุข้อจำกัดที่พบจริง
- [ ] Renderer ที่เลือกใช้ assets แบบ bundled/offline และมี automated fixture checks ที่ agent รันซ้ำได้
- [ ] ผลการทดลองไม่แก้ Windows system font, Chrome binary หรือ registry font substitution
