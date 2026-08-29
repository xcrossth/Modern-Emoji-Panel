# 02: พิสูจน์ Renderer ที่แสดง Noto บน Chrome และ Windows 10 ได้จริง

**What to build:** ให้ผู้พัฒนามี static rendering fixture ที่พิสูจน์บน Chrome/Windows 10 ว่า Noto แบบใดแสดง Emoji ใหม่ได้ถูก พร้อมเปรียบเทียบ font renderer กับ PNG/SVG fallback และบันทึกหลักฐานเพื่อเลือก primary/fallback renderer ก่อนสร้าง production DOM pipeline

**Blocked by:** 01: โหลด Renderer Extension แบบ Unpacked ได้อย่างปลอดภัย

**Status:** resolved

- [x] Fixture ครอบคลุม single code point, VS16, skin tone, ZWJ, family, keycap, regional flag, tag sequence และ Emoji ใหม่ที่ Windows 10 เดิมแสดงเป็น Tofu
- [x] ทดสอบ font formats ที่มีเหตุผลกับ Chrome/Windows 10 และเปรียบเทียบกับ image fallback ในด้านความถูกต้อง, baseline, line height, zoom, HiDPI และเวลา render
- [x] ตรวจว่า surrounding Thai/English typography และ Unicode text ไม่ถูกเปลี่ยนจากการทดลอง renderer
- [x] บันทึกผลและเหตุผลเลือก primary renderer กับ fallback renderer เป็นเอกสารภาษาไทย พร้อมระบุข้อจำกัดที่พบจริง
- [x] Renderer ที่เลือกใช้ assets แบบ bundled/offline และมี automated fixture checks ที่ agent รันซ้ำได้
- [x] ผลการทดลองไม่แก้ Windows system font, Chrome binary หรือ registry font substitution

## Comments

- เลือก bundled Noto COLRv1 v2.051 เป็น primary และ PNG เป็น fallback เมื่อ font โหลดไม่ได้ รายละเอียดและภาพอยู่ที่ `docs/research/renderer-rendering-spike/README.md`
- การทดลองที่ intentionally throwaway เก็บไว้ที่ branch `codex/renderer-rendering-spike`, commit `a68f6d7`; production branch รับเฉพาะ font, license, fixture, verifier และข้อสรุป
- `scripts/verify-renderer-static-fixture.ps1` ผ่านบน Chrome for Testing 152.0.7977.64 ที่ 100%/200% พร้อมยืนยัน font load และ Unicode text integrity
