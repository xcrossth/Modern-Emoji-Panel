# สีผิวเริ่มต้นและ Variant Override

Picker แยกการเลือกสีผิวออกเป็นสองระดับเพื่อให้ใช้งานรายการทั่วไปได้เร็ว แต่ยังเข้าถึง fully-qualified Emoji 17 ได้ครบทุก sequence

## สีผิวเริ่มต้นระดับ global

ตัวเลือกสีผิวอยู่ข้างชื่อหมวดและมีหกค่า ได้แก่ neutral สีเหลืองกับสีผิว Unicode อีกห้าระดับ แต่ละตัวเลือกแสดงภาพตัวอย่างจาก Noto Emoji ที่ตรงกับค่านั้นโดยตรง จึงไม่พึ่งการแสดงผล emoji modifier ของ Windows 10 และไม่เกิดอักขระเสียแบบ `�` ตัว dropdown ใช้สีตาม System/Light/Dark/High Contrast theme เดียวกับหน้าต่างหลัก

ค่าเริ่มต้นของโปรไฟล์ใหม่คือ neutral เมื่อเปลี่ยนค่า Picker จะบันทึกลง `%APPDATA%\ModernEmojiPicker\settings.json` และนำไปใช้กับ Emoji Entry ที่รองรับ modifier ใน Picker Session ถัดไปด้วย

หาก Emoji Entry มีคนหลายคนและมี sequence สีเดียวครบทุกตำแหน่ง เช่น people holding hands ค่า global จะใช้สีเดียวกันกับทุกตำแหน่ง รายการ Family สองถึงสี่คนก็ใช้ค่าเดียวกันกับสมาชิกทุกคน เช่น `👨‍👩‍👦` เมื่อเลือก Light จะ resolve เป็น `👨🏻‍👩🏻‍👦🏻`

Noto v2.051 ไม่มี artwork Family แบบใส่สีผิวสำเร็จรูป Picker จึงสร้างภาพ composite ในเครื่องจากภาพสมาชิก Noto ที่ตรงกับเพศ/วัยและสีผิว แล้วจัดวางเป็นครอบครัว 2, 3 หรือ 4 คน ภาพ grid ใช้ source 128 และ Hover Preview ใช้ source 512 เหมือน artwork อื่น โดยไม่ดาวน์โหลดหรือสร้างไฟล์ถาวรเพิ่ม ค่า Neutral ใช้ภาพสมาชิกสีเหลือง จึงไม่แสดง Family silhouette ขาวดำใน grid เช่นกัน

Family ที่มีสีผิวเป็น derived sequence นอกชุด RGI Emoji Baseline เนื่องจาก Unicode ยังไม่กำหนด combination เหล่านี้เป็น fully-qualified entry ทั้งหมด Picker ส่ง code points ของสมาชิกและสีผิวจริงครบถ้วน แต่แอปปลายทางอาจแสดงเป็นภาพครอบครัวรวม หรือแยกเป็น Emoji หลายคนตาม renderer ของปลายทาง ส่วน Neutral เปลี่ยนเฉพาะภาพใน Picker และยังส่ง family sequence มาตรฐานเดิม

## Variant Override สำหรับ mixed tone

รายการที่คนแต่ละคนใช้สีผิวต่างกันไม่สามารถแทนด้วยค่า global ค่าเดียวได้ ให้คลิกขวาที่ tile แล้วเลือก sequence ในเมนู Variant Override หรือเลือก tile ด้วยแป้นลูกศรแล้วกด `Alt+ลูกศรลง` ส่วนตัวเลือกสีผิว global เปิดจาก keyboard ได้ด้วย `Alt+T`

Variant Override มีผลกับการเลือกครั้งนั้นเท่านั้นและไม่เปลี่ยนสีผิวเริ่มต้น เมนูแสดงชื่อ localized ของ sequence จริงและใช้ภาพ Noto ที่ผูกไว้ใน Emoji Baseline หลังเลือกแล้ว Recent จะรับ resolved Unicode sequence จริงตามที่ส่ง

ใน Hybrid insertion uniform skin-tone sequence เช่น `👌🏻` ใช้ grouped Unicode keystrokes จึงไม่รอรอบ Temporary Paste/Clipboard restore ต่อคลิก ส่วน mixed-tone ที่มี ZWJ และ sequence ซับซ้อนอื่นยังใช้ Temporary Paste เพื่อรักษาการประกอบเป็น sequence เดียว

## ความครบถ้วนของข้อมูล

runtime ไม่เดาชื่อไฟล์ของ Emoji Baseline โมดูล variant จัดกลุ่มจากระเบียนที่ generator ตรวจแล้วและคืน fully-qualified sequence ที่มีอยู่ใน Emoji Baseline เป็นหลัก ข้อยกเว้นคือ uniform-tone Family ซึ่งประกอบจากรายชื่อสมาชิกใน family entry และใช้ Noto member assets ที่ pin ไว้เท่านั้น

- entry ที่ไม่มี skin-tone modifier รวม flags, keycaps และ ZWJ sequence ยังคงอยู่ใน grid
- uniform-tone sequence เข้าถึงผ่านสีผิว global ทั้งห้าค่า
- Family 30 รายการมีภาพ composite ครบหกค่า; ห้าสีผิวรวม 150 derived variants และคืนค่าใน Recent ได้ด้วย stable derived ID ส่วน Neutral รักษา baseline ID/sequence เดิม
- mixed-tone sequence เข้าถึงผ่าน Variant Override
- handshake แบบเก่ากับ sequence มือสองข้างแบบใหม่ถูกผูกเป็น Emoji Entry เดียวกันโดยใช้ metadata ของ baseline

ตรวจ coverage และพฤติกรรมทั้งหมดด้วย:

```powershell
.\scripts\verify-emoji-variants.ps1
```
