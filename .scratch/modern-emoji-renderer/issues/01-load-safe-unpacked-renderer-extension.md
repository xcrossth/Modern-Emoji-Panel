# 01: โหลด Renderer Extension แบบ Unpacked ได้อย่างปลอดภัย

**What to build:** สร้างรากฐาน Renderer Extension แบบ Manifest V3 ที่ผู้ใช้ build และโหลดแบบ unpacked ใน Chrome ได้ โดยทำงานแยกจาก Picker เปิดสิทธิ์เริ่มต้นเฉพาะ Instagram กับ TikTok และไม่มี runtime dependency หรือ remote code

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Build จาก clean checkout แล้วได้ unpacked extension ที่ Chrome รับได้โดยไม่มี manifest, service worker หรือ content-script error
- [ ] Manifest ใช้ V3 และขอเฉพาะ permissions ขั้นต่ำที่จำเป็นสำหรับ settings กับ Instagram/TikTok
- [ ] Content script ไม่ทำงานนอกเว็บไซต์ที่อนุญาตตามค่าเริ่มต้น
- [ ] Extension ไม่ใช้ remote JavaScript, `eval`, external script injection หรือดาวน์โหลด executable code
- [ ] มี automated smoke test สำหรับ build output, manifest และ policy สำคัญ
- [ ] Renderer Extension build และเปิดใช้งานได้โดยไม่ต้องติดตั้งหรือรัน Picker
