# Modern Emoji Picker

แอป WPF แบบ resident tray utility สำหรับค้นหาและส่ง Unicode Emoji ไปยังแอปเป้าหมายบน Windows โค้ดตั้งต้นนำเข้าจาก Classic Emoji Picker ตาม provenance ที่บันทึกไว้ใน [`docs/upstream/classic-picker.md`](../../docs/upstream/classic-picker.md) แต่ตัวผลิตภัณฑ์ Modern ใช้ identity ของตนเองทั้งหมด

## สถานะรุ่น 0.1.9

- target `net10.0-windows` และ runtime `win-x64`
- executable และ assembly ชื่อ `ModernEmojiPicker`
- ข้อมูลผู้ใช้อยู่ที่ `%APPDATA%\ModernEmojiPicker`
- เป็น single-instance; การเปิด executable ซ้ำส่งสัญญาณเปิด instance เดิม
- ปิดหน้าต่างเป็นการ dismiss ส่วน `Exit Modern Emoji Picker` ใน tray จึงหยุด process
- ถ้าพบ Classic กำลังทำงาน Modern จะไม่ติดตั้ง Win + . hook และจะไม่ปิด process อื่น
- ใช้ product icon ของ Modern แยกจาก Classic ทั้งใน executable, tray และตัวติดตั้ง
- เผยแพร่เป็น Public MVP แล้ว ดาวน์โหลดและดูวิธีติดตั้งได้จาก [README หลัก](../../README.md)

## Build และตรวจสอบ

จาก root ของ repository:

```powershell
.\scripts\build.ps1
.\scripts\verify-product-identity.ps1
.\scripts\verify-foundation.ps1 -SkipPublish
```

การตรวจ identity และ foundation smoke ไม่เปิด tray, ไม่ติดตั้ง global keyboard hook และไม่อ่านหรือเขียน Settings/Activity Data จึงไม่รบกวน Classic ที่ผู้ใช้อาจกำลังใช้งาน

## Classic Conflict

Modern ตรวจ Classic ผ่าน named mutex แบบ read-only เท่านั้น หากพบ conflict จะปล่อย Win + . ให้ Classic และแสดงคำแนะนำให้ผู้ใช้เลือก `Exit` จาก tray ของ Classic เอง จากนั้นเลือก `Check for Classic again` ใน tray ของ Modern ระบบไม่ kill process, ไม่ส่ง signal ไปยัง Classic และไม่อ่านโฟลเดอร์ข้อมูลของ Classic

## ขอบเขตเอกสาร

พฤติกรรมผลิตภัณฑ์ฉบับเต็มอยู่ใน [`docs/specs/01-modern-emoji-picker.md`](../../docs/specs/01-modern-emoji-picker.md) ส่วนประวัติและ license ของ upstream ยังคงเก็บอยู่ใน subtree และ `THIRD-PARTY-NOTICES.md`
