# 08: ส่ง Emoji หนึ่งรายการไปยังแอปเป้าหมายอย่างปลอดภัย

**What to build:** ให้ผู้ใช้เลือก Emoji แล้วส่ง resolved Unicode sequence หนึ่งรายการไปยังแอปเป้าหมายเดิมด้วยวิธีที่เหมาะกับ sequence โดยไม่ retarget ไปยังหน้าต่างอื่นและไม่ทำลาย clipboard ใหม่

**Blocked by:** 02: แยก Modern Picker ออกจาก Classic อย่างสมบูรณ์; 04: สร้าง Emoji Baseline ที่สมบูรณ์และตรวจสอบซ้ำได้; 05: เปิดดู Emoji 17 ทั้งชุดด้วย Noto grid

**Status:** resolved

- [x] Picker capture app/window/control ที่ active ก่อนเปิดเป็นแอปเป้าหมาย และตรวจ foreground target ซ้ำทันทีก่อนส่งทุกครั้ง
- [x] target ที่ปิดไปแล้ว, เปลี่ยนไป หรือมี integrity level สูงกว่าทำให้ abort โดยไม่ retry และไม่ส่งไป foreground window อื่น
- [x] Hybrid ใช้ Unicode keystroke สำหรับ Emoji เดี่ยวและ Temporary Paste สำหรับ ZWJ, flags, keycaps, skin-tone และ multi-codepoint sequence
- [x] Keystroke only ส่ง UTF-16 units ตามลำดับ ตรวจจำนวน input ที่ Windows รับ และไม่ retry ทั้ง string หลัง partial acceptance
- [x] Temporary Paste ใส่ exclusion marker, ตรวจ clipboard sequence number และ restore แบบ best-effort เฉพาะเมื่อ clipboard ไม่ถูกเปลี่ยนระหว่างทาง
- [x] Paste always และ Hybrid ใช้ configurable restore delay เดียวกันโดยไม่กล่าวอ้างว่าตรวจผลในแอปเป้าหมายสำเร็จได้
- [x] Insertion Failure คง Picker ไว้ แสดงข้อผิดพลาดที่ไม่บัง UI และมี Explicit Copy ซึ่งเข้า clipboard/history ตามปกติ
- [x] automated tests ครอบคลุมการเลือก Insertion Mode, target validation และ clipboard restore rules โดยไม่ต้องส่ง input จริงใน unit tests

## หลักฐานการตรวจรับ

- commit งาน: `886539c` (`feat(ticket-08): validate targets and insert emoji safely`)
- `scripts/verify-safe-insertion.ps1` ผ่าน policy checks 18 กรณีโดยไม่ส่ง input จริง ครอบคลุม Hybrid/Keystroke/Paste, target หาย/ปิด/เปลี่ยน/higher integrity และ clipboard restore/skip
- production path ตรวจ top-level window, focused control, foreground handle และ integrity RID ก่อน `SendInput` โดยไม่ retry หรือ retarget
- Unicode keystroke และ Ctrl+V ตรวจจำนวน input ที่ Windows รับ; Temporary Paste ใช้ exclusion marker และ clipboard sequence number
- WPF smoke ยืนยัน Insertion Failure เปิด Picker session เดิมพร้อม non-blocking error และ Explicit Copy
- `scripts/test-clean-checkout.ps1 -Revision HEAD` ผ่านทั้ง build, self-contained publish, WPF smoke, generator, Noto grid และ safe-insertion verification
