# สีผิวเริ่มต้นและ Variant Override

Picker แยกการเลือกสีผิวออกเป็นสองระดับเพื่อให้ใช้งานรายการทั่วไปได้เร็ว แต่ยังเข้าถึง fully-qualified Emoji 17 ได้ครบทุก sequence

## สีผิวเริ่มต้นระดับ global

ตัวเลือกสีผิวอยู่ข้างชื่อหมวดและมีหกค่า ได้แก่ neutral สีเหลืองกับสีผิว Unicode อีกห้าระดับ แต่ละตัวเลือกแสดงภาพตัวอย่างจาก Noto Emoji ที่ตรงกับค่านั้นโดยตรง จึงไม่พึ่งการแสดงผล emoji modifier ของ Windows 10 และไม่เกิดอักขระเสียแบบ `�` ตัว dropdown ใช้สีตาม System/Light/Dark/High Contrast theme เดียวกับหน้าต่างหลัก

ค่าเริ่มต้นของโปรไฟล์ใหม่คือ neutral เมื่อเปลี่ยนค่า Picker จะบันทึกลง `%APPDATA%\ModernEmojiPicker\settings.json` และนำไปใช้กับ Emoji Entry ที่รองรับ modifier ใน Picker Session ถัดไปด้วย

หาก Emoji Entry มีคนหลายคนและมี sequence สีเดียวครบทุกตำแหน่ง เช่น people holding hands ค่า global จะใช้สีเดียวกันกับทุกตำแหน่ง หากรายการไม่รองรับ skin tone ค่า global ไม่มีผล

## Variant Override สำหรับ mixed tone

รายการที่คนแต่ละคนใช้สีผิวต่างกันไม่สามารถแทนด้วยค่า global ค่าเดียวได้ ให้คลิกขวาที่ tile แล้วเลือก sequence ในเมนู Variant Override หรือเลือก tile ด้วยแป้นลูกศรแล้วกด `Alt+ลูกศรลง` ส่วนตัวเลือกสีผิว global เปิดจาก keyboard ได้ด้วย `Alt+T`

Variant Override มีผลกับการเลือกครั้งนั้นเท่านั้นและไม่เปลี่ยนสีผิวเริ่มต้น เมนูแสดงชื่อ localized ของ sequence จริงและใช้ภาพ Noto ที่ผูกไว้ใน Emoji Baseline หลังเลือกแล้ว Recent จะรับ resolved Unicode sequence จริงตามที่ส่ง

ใน Hybrid insertion uniform skin-tone sequence เช่น `👌🏻` ใช้ grouped Unicode keystrokes จึงไม่รอรอบ Temporary Paste/Clipboard restore ต่อคลิก ส่วน mixed-tone ที่มี ZWJ และ sequence ซับซ้อนอื่นยังใช้ Temporary Paste เพื่อรักษาการประกอบเป็น sequence เดียว

## ความครบถ้วนของข้อมูล

runtime ไม่ประกอบ Unicode sequence หรือเดาชื่อไฟล์ภาพเอง โมดูล variant จัดกลุ่มจากระเบียนที่ generator ตรวจแล้วและคืนเฉพาะ fully-qualified sequence ที่มีอยู่ใน Emoji Baseline เท่านั้น

- entry ที่ไม่มี skin-tone modifier รวม flags, keycaps และ ZWJ sequence ยังคงอยู่ใน grid
- uniform-tone sequence เข้าถึงผ่านสีผิว global ทั้งห้าค่า
- mixed-tone sequence เข้าถึงผ่าน Variant Override
- handshake แบบเก่ากับ sequence มือสองข้างแบบใหม่ถูกผูกเป็น Emoji Entry เดียวกันโดยใช้ metadata ของ baseline

ตรวจ coverage และพฤติกรรมทั้งหมดด้วย:

```powershell
.\scripts\verify-emoji-variants.ps1
```
