# การส่ง Emoji ไปยังแอปเป้าหมายอย่างปลอดภัย

Picker จับ top-level window และ focused control ก่อนเปิดหน้าต่างของตนเอง ทุกครั้งก่อนส่งจะ activate target เดิม รอให้ focus settle แล้วตรวจซ้ำทันทีว่า window ยังอยู่, foreground ยังเป็น handle เดิม และ integrity level ไม่สูงกว่า Picker หากข้อใดไม่ผ่านจะหยุดโดยไม่ retry และไม่เปลี่ยนไปส่งหน้าต่างอื่น

## Insertion Mode

- **Hybrid** เป็นค่าเริ่มต้น: Emoji เดี่ยวใช้ Unicode keystroke ส่วน ZWJ, flags, keycaps, skin-tone และ sequence หลาย code point ใช้ Temporary Paste
- **Keystroke only** ส่ง UTF-16 code units ด้วย `SendInput` และตรวจจำนวน event ที่ Windows รับ หากรับเพียงบางส่วนจะรายงาน failure โดยไม่ส่ง string ซ้ำ
- **Paste always** ใช้ Temporary Paste กับทุกรายการ

Hybrid และ Paste always ใช้ `pasteRestoreDelayMs` ค่าเดียวกัน โดย clamp อยู่ที่ 50–5,000 ms การที่ Windows รับ Ctrl+V ครบหมายถึงรับคำสั่ง input ไม่ได้ยืนยันว่าแอปปลายทางนำข้อความไปแสดงแล้ว

## Temporary Paste กับ clipboard

ระบบ snapshot format ที่อ่านได้แบบ best-effort ใส่ Unicode sequence พร้อม marker สำหรับตัดออกจาก Clipboard History, Cloud Clipboard และ monitor processing แล้วจึงส่ง Ctrl+V หลังครบ delay จะ restore เฉพาะเมื่อ clipboard sequence number ยังเท่ากับค่าหลังใส่ข้อมูลชั่วคราว หากผู้ใช้หรือโปรแกรมอื่น copy ระหว่างนั้น ระบบจะไม่เขียนทับ clipboard ใหม่

การ restore private หรือ delayed format ทุกชนิดรับประกันไม่ได้ และ clipboard manager ภายนอกอาจไม่เคารพ marker

เมื่อ insertion ล้มเหลว Picker กลับมาใน session เดิมพร้อม error ที่ไม่บัง grid และปุ่ม **Copy** ปุ่มนี้เป็น Explicit Copy จึงไม่ใส่ exclusion markerและสามารถเข้า Win+V ตามปกติ

## การตรวจอัตโนมัติ

```powershell
.\scripts\verify-safe-insertion.ps1
```

smoke path ตรวจ policy 18 กรณีสำหรับ Insertion Mode, target validation และ clipboard restore โดยไม่ส่ง input จริง จึงรันได้โดยไม่เสี่ยงพิมพ์ข้อความลงแอปอื่น
