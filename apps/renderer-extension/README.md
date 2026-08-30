# Modern Emoji Renderer

ส่วนขยาย Chrome แบบ Manifest V3 สำหรับแสดง Emoji รุ่นใหม่ด้วยฟอนต์ Noto Color Emoji บน Windows 10 โดยไม่เปลี่ยนข้อความต้นฉบับ ไม่เปลี่ยนฟอนต์ของระบบ และทำงานแยกจาก Modern Emoji Picker

เว็บไซต์หลักของรุ่นแรกคือ Instagram Web DM และ TikTok Web Chat ส่วนเว็บไซต์อื่นเปิดเพิ่มได้จาก Options

## ติดตั้งจาก ZIP

Chrome ไม่รับ ZIP ที่ไม่ได้เผยแพร่ผ่าน Chrome Web Store โดยตรง จึงต้องแตกไฟล์ก่อนโหลดแบบ manual

1. ตรวจ SHA-256 ของ ZIP ให้ตรงกับไฟล์ `.sha256` ที่มาคู่กัน
2. แตก ZIP ไปยังโฟลเดอร์ถาวร ห้ามลบโฟลเดอร์นี้ขณะที่ยังใช้งานส่วนขยาย
3. เปิด `chrome://extensions`
4. เปิด **Developer mode**
5. กด **Load unpacked** แล้วเลือกโฟลเดอร์ที่แตก ZIP

แพ็กเกจที่สถานะเป็น `release-candidate` ผ่านชุดทดสอบอัตโนมัติแล้ว แต่ยังรอทดสอบ Instagram DM และ TikTok Chat บนบัญชีจริง ดูสถานะได้ใน `release-metadata.json`

## อัปเดต

1. แตก ZIP รุ่นใหม่ลงในโฟลเดอร์ใหม่
2. เปิด `chrome://extensions`
3. ลบรุ่นเดิม แล้ว Load unpacked จากโฟลเดอร์ใหม่ หรือแทนที่ไฟล์เดิมแล้วกด **Reload**
4. ตรวจเลข Version ในรายละเอียดส่วนขยายให้ตรงกับ `release-metadata.json`

การตั้งค่าถูกเก็บด้วย Chrome Extension Storage และควรคงอยู่เมื่อกด Reload หากลบส่วนขยายก่อนติดตั้งใหม่ Chrome อาจลบการตั้งค่าเดิม

## เปิดหรือปิดต่อเว็บไซต์

- กดไอคอนส่วนขยายขณะอยู่บนหน้าเว็บ แล้วใช้สวิตช์ **เปิดใช้บนเว็บไซต์นี้**
- Instagram และ TikTok เปิดเป็นค่าเริ่มต้น
- หน้า `chrome://`, Chrome Web Store และหน้าภายในของเบราว์เซอร์ไม่อนุญาตให้ส่วนขยายแก้เนื้อหา

## Options

หน้า Options ใช้กำหนดนโยบายเว็บไซต์ได้สามแบบ

- **Allowlist:** ทำงานเฉพาะเว็บไซต์ในรายการ
- **Denylist:** ทำงานทุกเว็บไซต์ยกเว้นรายการที่ระบุ
- **ทุกเว็บไซต์:** ทำงานทุกเว็บไซต์ที่ Chrome อนุญาต

การเลือก Denylist หรือทุกเว็บไซต์จะทำให้ Chrome ขอสิทธิ์เข้าถึงเว็บไซต์ทั้งหมด (`<all_urls>`) เพิ่มเติม เมื่อกลับมาใช้ Allowlist สิทธิ์กว้างนี้จะถูกนำออกอีกครั้ง ตัวเลือก **Diagnostic logging** ปิดเป็นค่าเริ่มต้นและควรเปิดเฉพาะตอนวิเคราะห์ปัญหา

## Privacy

- Renderer ประมวลผล DOM ในเครื่องเท่านั้น
- ไม่มี analytics, telemetry, account backend หรือการส่งเนื้อหาข้อความออกจากเครื่อง
- ไม่มีการโหลด code, font หรือ assets จาก network ขณะทำงาน ไฟล์ Noto ถูก bundle อยู่ในส่วนขยาย
- เก็บเฉพาะการตั้งค่า site policy และตัวเลือก Renderer ใน Chrome Extension Storage
- Popup อ่านเฉพาะ hostname ของแท็บปัจจุบันและตัวนับเชิงตัวเลข ไม่ส่งเนื้อหาข้อความกลับไปยัง Popup หรือ service worker

สิทธิ์ที่ใช้คือ `storage`, `activeTab`, `scripting`, Instagram/TikTok และสิทธิ์เว็บไซต์เพิ่มเติมที่ผู้ใช้อนุมัติเอง

## แก้ปัญหาเบื้องต้น

- **Emoji ยังเป็นขาวดำหรือเป็นช่อง:** เปิด `chrome://extensions` แล้วกด Reload จากนั้น refresh หน้าเว็บ
- **ไม่ทำงานบนเว็บไซต์:** ตรวจสวิตช์ของเว็บไซต์และรายการใน Options หากใช้ทุกเว็บไซต์ให้ตรวจว่ายอมรับ permission แล้ว
- **ข้อความที่กำลังพิมพ์ไม่เปลี่ยนรูป:** เป็นพฤติกรรมที่ตั้งใจไว้ Renderer จะข้ามช่องพิมพ์เพื่อรักษา caret, selection และ IME; ข้อความที่ส่งและกลายเป็น display content แล้วจึงถูก render
- **หน้าเว็บเพี้ยน:** ปิดเว็บไซต์นั้นจาก Popup แล้ว refresh หน้า การปิด Renderer จะคืน wrapper เป็น text เดิม
- **ต้องส่งข้อมูลวิเคราะห์:** เปิด Diagnostic logging ชั่วคราว เปิด DevTools Console เพื่อดูตัวนับ แล้วปิดอีกครั้งเมื่อเสร็จ ห้ามส่งข้อความส่วนตัวหรือข้อมูลบัญชีไปกับรายงาน

## ถอนการติดตั้ง

1. เปิด `chrome://extensions`
2. เลือก Modern Emoji Renderer แล้วกด **Remove**
3. ลบโฟลเดอร์ที่แตก ZIP ได้หลัง Chrome ถอดส่วนขยายแล้ว

การถอนการติดตั้งไม่เปลี่ยนข้อความบนเว็บไซต์ เพราะ Renderer ไม่บันทึก DOM ที่แก้ไว้กลับไปยังเซิร์ฟเวอร์

## ข้อจำกัดของรุ่นแรก

- **Editable Content:** ไม่ render ภายใน `input`, `textarea`, `contenteditable` และ composer เพื่อไม่รบกวน caret, selection, keyboard layout และ IME
- **Server normalization:** เว็บไซต์อาจแปลง ลบ หรือ normalize code points ก่อนแสดงผล Renderer ไม่สามารถคืนข้อมูลที่เซิร์ฟเวอร์เปลี่ยนไปแล้ว
- **Canvas, image และ video:** Renderer ไม่แก้ Emoji ที่วาดบน canvas หรือฝังอยู่ในรูป/วิดีโอทั่วไป แต่รองรับกรณี Instagram แปลง Unicode Emoji เป็นรูปจาก `/images/emoji.php/` โดยอ่าน sequence จาก `alt` และแสดงด้วย Noto แทน
- **Closed Shadow DOM:** Chrome ไม่เปิดให้เข้าถึง text node ภายใน closed shadow root
- เว็บไซต์เปลี่ยน DOM ได้ตลอด จึงยังต้องทดสอบบัญชีจริงหลังเว็บไซต์อัปเดต

## Build และตรวจสอบในเครื่อง

ต้องมี Node.js 24+ และ npm 11+ จาก repository root ให้รัน:

```powershell
.\scripts\build-renderer-release.ps1
```

คำสั่งนี้ติดตั้ง dependency จาก lockfile, รัน qualification อัตโนมัติ, สร้าง production build, สร้าง deterministic ZIP, SHA-256 และ verification report ทั้งหมดใน `artifacts\renderer-extension\release` โดยไม่ใช้ GitHub Actions minutes

ระหว่างพัฒนาใช้คำสั่งต่อไปนี้เพื่อสร้าง unpacked build ที่มี test fixtures:

```powershell
npm --prefix .\apps\renderer-extension ci
npm --prefix .\apps\renderer-extension run verify
```

## ลิขสิทธิ์

โค้ดโครงการใช้ MIT License ฟอนต์ Noto COLRv1 ใช้ SIL Open Font License 1.1 และข้อมูลอนุพันธ์จาก Unicode ใช้ Unicode License V3 ดูรายละเอียดใน `THIRD-PARTY-NOTICES.md` และโฟลเดอร์ `licenses` แพ็กเกจนี้ไม่มี Apple Emoji artwork หรือ Apple fonts
