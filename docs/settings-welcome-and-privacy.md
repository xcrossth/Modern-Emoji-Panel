# Settings, Welcome และความเป็นส่วนตัว

Modern Emoji Picker รวมค่าที่ผู้ใช้ควบคุมไว้ในหน้าต่าง **Settings** เดียว เปิดได้จากไอคอน tray การเปลี่ยนค่าถูกตรวจความถูกต้องและบันทึกแบบ atomic ใน `%APPDATA%\ModernEmojiPicker\settings.json` ซึ่งเป็นพื้นที่ข้อมูลของ Modern โดยเฉพาะและไม่อ่านข้อมูลของ Classic Emoji Picker

## ค่าทั่วไป

- เปิด ปิด หรือเปลี่ยน global hotkey จากค่าเริ่มต้น `Win + .` เป็นชุดที่แอปรองรับ
- เปิดพร้อม Windows ได้เมื่อผู้ใช้สั่ง สำหรับ portable ค่าเริ่มต้นคือปิด ส่วน installer สามารถสร้างค่า Modern ตอนติดตั้งและแอปจะแสดงเป็นค่าที่ installer จัดการ
- ภาษา UI เลือก System, English หรือไทย เมื่อเลือก System จะใช้ไทยเฉพาะเมื่อ Windows display language เป็นไทย ภาษาอื่นใช้ English โดยการค้นหายังค้นชื่อและ keyword ไทยกับอังกฤษพร้อมกันเสมอ
- ธีมเลือก System, Light หรือ Dark
- สีผิวเริ่มต้นเป็นค่าระดับ global และไม่เปลี่ยนพฤติกรรม Variant Override แบบครั้งเดียว

## การส่ง Emoji และค่าขั้นสูง

เลือก Insertion Mode ได้สามแบบ:

- Hybrid ใช้ keystroke กับ Emoji เดี่ยวและ Temporary Paste กับ sequence ซับซ้อน
- Keystroke only ไม่ใช้ Clipboard แต่ปลายทางบางแอปอาจประกอบ sequence ซับซ้อนไม่ถูก
- Paste always ใช้ Temporary Paste ทุกครั้ง

`pasteRestoreDelayMs` ปรับได้ระหว่าง 50–5,000 ms และกด Reset advanced defaults เพื่อกลับ 250 ms ได้ ค่านี้ช่วยแอปช้าหรือ remote session แต่ Temporary Paste ยังไม่รับประกันว่าปลายทางจะวางสำเร็จ คืน private/delayed clipboard format ได้ครบ หรือ clipboard manager ภายนอกจะเคารพ exclusion marker หาก Clipboard เปลี่ยนระหว่างรอ แอปจะไม่คืนทับข้อมูลใหม่

## Activity Data

Settings เรียกคำสั่งจาก Activity Data store โดยตรงและรายงานผลหลังทำคำสั่ง:

- Clear Recent ล้างเฉพาะ Recent
- Reset learned ranking ล้างเฉพาะ Learned Ranking
- Clear all activity ล้างทั้งสองส่วน

ข้อมูลทั้งหมดอยู่ในเครื่อง ไม่มี account, provider, cloud sync หรือการ upload ใน v1

## Welcome ครั้งแรก

Welcome แสดงครั้งเดียวต่อโปรไฟล์และอธิบาย `Win + .`, Classic Conflict, Temporary Paste, autostart และทางเข้า Settings โดยไม่มีขั้นตอนสมัครบัญชีหรือเชื่อมเครือข่าย การแสดงหน้าต่างครั้งแรกจะบันทึก `welcomeShown` ทันทีเพื่อไม่ให้หน้าต่างวนกลับมาหากผู้ใช้ปิดด้วยวิธีอื่น

## Diagnostic logging

Diagnostic logging ปิดเป็นค่าเริ่มต้นและเปิดได้จาก Advanced Settings เท่านั้น Log เก็บเฉพาะ metadata ทางเทคนิค เช่นชนิดเหตุการณ์ เวลา จำนวนรายการ สถานะ และชนิดข้อผิดพลาด โดยห้ามบันทึก:

- คำค้น
- Emoji หรือ Unicode sequence ที่เลือก
- Clipboard หรือข้อความของผู้ใช้
- handle หรือชื่อหน้าต่างเป้าหมาย

การปิด logging มีผลกับทุกเหตุการณ์รวมถึงข้อผิดพลาดร้ายแรง และไม่มี automatic crash upload, telemetry หรือ runtime network call ระบบหมุนไฟล์ในเครื่องเมื่อมีขนาดใหญ่เพื่อไม่ให้โตไม่จำกัด

## การตรวจอัตโนมัติ

รัน:

```powershell
pwsh scripts/verify-settings-privacy.ps1
```

คำสั่งนี้ตรวจ default ที่ปลอดภัย การ persist และ normalize, ภาษา fallback, theme, hotkey, insertion mode, advanced reset, Welcome, Activity Data wiring, ขอบเขต autostart, logging opt-in และการไม่มี network/telemetry/sync/upload code ใน runtime
