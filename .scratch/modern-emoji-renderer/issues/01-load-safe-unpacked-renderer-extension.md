# 01: โหลด Renderer Extension แบบ Unpacked ได้อย่างปลอดภัย

**What to build:** สร้างรากฐาน Renderer Extension แบบ Manifest V3 ที่ผู้ใช้ build และโหลดแบบ unpacked ใน Chrome ได้ โดยทำงานแยกจาก Picker เปิดสิทธิ์เริ่มต้นเฉพาะ Instagram กับ TikTok และไม่มี runtime dependency หรือ remote code

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Build จาก clean checkout แล้วได้ unpacked extension ที่ Chrome รับได้โดยไม่มี manifest, service worker หรือ content-script error
- [x] Manifest ใช้ V3 และขอเฉพาะ permissions ขั้นต่ำที่จำเป็นสำหรับ settings กับ Instagram/TikTok
- [x] Content script ไม่ทำงานนอกเว็บไซต์ที่อนุญาตตามค่าเริ่มต้น
- [x] Extension ไม่ใช้ remote JavaScript, `eval`, external script injection หรือดาวน์โหลด executable code
- [x] มี automated smoke test สำหรับ build output, manifest และ policy สำคัญ
- [x] Renderer Extension build และเปิดใช้งานได้โดยไม่ต้องติดตั้งหรือรัน Picker

## Comments

- `scripts/verify-renderer-foundation.ps1` ผ่านจาก locked `npm ci`: TypeScript typecheck, production build และ policy tests 3 รายการ
- `scripts/verify-renderer-chrome-load.ps1` ผ่านบน Chrome for Testing 152.0.7977.64 โดยพบ service worker ของ unpacked MV3 extension จริงใน temporary profile
- Chrome branded ตั้งแต่รุ่น 139 ถอด command-line extension flags บางตัว จึงใช้ Chrome for Testing สำหรับ automated load smoke และคง Developer mode/Load unpacked เป็น workflow ผู้ใช้ตามปกติ
