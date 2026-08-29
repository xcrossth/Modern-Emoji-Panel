# บริบท Modern Emoji Picker

โครงการนี้ช่วยให้ผู้ใช้ Windows ค้นหาและแทรก Emoji รุ่นใหม่ได้อย่างถูกต้อง โดยมีส่วนเสริมแยกต่างหากสำหรับแก้การแสดง Emoji บนเว็บ

## ผลิตภัณฑ์

**Picker**:
ผลิตภัณฑ์หลักสำหรับค้นหาและแทรกลำดับ Unicode Emoji ลงในแอปเป้าหมาย
_หลีกเลี่ยง_: Emoji keyboard, Chrome picker

**Renderer Extension**:
ผลิตภัณฑ์เสริมสำหรับแก้การแสดง Emoji ที่เว็บเบราว์เซอร์หรือระบบปฏิบัติการไม่มี glyph รองรับ โดยไม่ต้องพึ่ง Picker
_หลีกเลี่ยง_: Picker extension, Chrome picker

**Emoji Baseline**:
สัญญาแบบระบุเวอร์ชันซึ่งกำหนดชุด Unicode Emoji, metadata และ artwork ที่ผลิตภัณฑ์ทั้งสองต้องรองรับให้สอดคล้องกัน
_หลีกเลี่ยง_: latest Emoji, current Noto

## Emoji และการค้นหา

**Emoji Entry**:
ระเบียนของ Emoji หนึ่งความหมายซึ่งเชื่อม sequence, ชื่อ, keyword, หมวดหมู่, version และ artwork ที่ตรงกัน
_หลีกเลี่ยง_: Glyph, image file

**ชุด Emoji ที่รองรับ**:
fully-qualified sequence ทั้งหมดใน Emoji Baseline รวม flags, keycaps, ZWJ sequences และ variants
_หลีกเลี่ยง_: Popular Emoji subset

**การค้นหาสองภาษา**:
การค้นหาด้วยชื่อและ keyword ภาษาไทยหรืออังกฤษจาก metadata รุ่นเดียวกับ Emoji Baseline
_หลีกเลี่ยง_: English-only search, custom Thai keywords

**Learned Ranking**:
คะแนนความชอบระดับ Emoji Entry ที่เรียนรู้จากการเลือกของผู้ใช้ โดยคุณภาพของการ match มีลำดับสูงกว่าคะแนนความชอบเสมอ
_หลีกเลี่ยง_: Global popularity, telemetry ranking

**สีผิวเริ่มต้น**:
ค่ากำหนดระดับ global ที่ใช้กับ Emoji Entry ซึ่งรองรับ skin-tone modifier
_หลีกเลี่ยง_: สีผิวล่าสุดต่อ Emoji

**Variant Override**:
การเลือก sequence หลายคนซึ่งใช้สีผิวต่างกันและไม่สามารถแทนด้วยสีผิวเริ่มต้นค่าเดียว โดยไม่มีผลต่อค่าระดับ global
_หลีกเลี่ยง_: การเปลี่ยนสีผิวเริ่มต้น

**หมวดหมู่ Emoji**:
กลุ่มมาตรฐานของ Unicode ที่ใช้จัด Emoji Entry โดยมี Recent เป็นทางเข้าพิเศษนำหน้า
_หลีกเลี่ยง_: หมวดที่สร้างเฉพาะโครงการ

## การโต้ตอบกับ Picker

**Picker Session**:
ช่วงเวลาตั้งแต่เปิด Picker จนผู้ใช้ dismiss โดยสถานะการค้นหา หมวดหมู่ ตำแหน่งเลื่อน และ selection ยังคงอยู่ระหว่างการเลือกต่อเนื่อง
_หลีกเลี่ยง_: Insert operation, app session

**แอปเป้าหมาย**:
แอปและตำแหน่งรับข้อความที่ active อยู่ก่อนเปิด Picker ซึ่งเป็นปลายทางของ Emoji ที่ผู้ใช้เลือก
_หลีกเลี่ยง_: แอปล่าสุด, หน้าต่างที่คลิกล่าสุด

**Browse Mode**:
โหมดเริ่มต้นแบบ pointer-first สำหรับเลื่อนและคลิก Emoji โดยทุก physical key ที่ไม่ใช่ modifier (ยกเว้น Esc) หมายถึงผู้ใช้ต้องการกลับไปยังแอปเป้าหมาย ไม่ใช้ Space, Enter หรือลูกศรเลือก Emoji ในโหมดนี้
_หลีกเลี่ยง_: Search focus เริ่มต้น

**Search Mode**:
โหมดที่ keyboard input ใช้ค้นหา Emoji แทนการส่งต่อไปยังแอปเป้าหมาย
_หลีกเลี่ยง_: Typing Handoff, implicit search

**Commit Gesture**:
คำสั่งที่ยืนยันการเลือก Emoji พร้อมระบุว่า Picker Session จะดำเนินต่อหรือ dismiss ได้แก่ pointer click และคำสั่ง keyboard ภายใน Search Mode
_หลีกเลี่ยง_: Selection movement

**Hover Preview**:
ภาพขยายพร้อมชื่อของ Emoji Entry ที่ปรากฏเมื่อผู้ใช้ชี้ค้าง โดยไม่แย่ง focus หรือเริ่มการเลือก
_หลีกเลี่ยง_: Selection popup, persistent detail panel

**Typing Handoff**:
การ dismiss Picker เมื่อผู้ใช้กด physical key ต่อใน Browse Mode พร้อมส่ง virtual key และ modifiers กลับไปให้ keyboard layout ของแอปเป้าหมายตีความ โดยมี committed-text fallback สำหรับ IME/dead key ที่ไม่มี physical key ให้ replay
_หลีกเลี่ยง_: Swallowed key, search input

**Recent**:
ประวัติแบบ MRU ของ sequence ที่ผู้ใช้เลือก โดยไม่ใช่หลักฐานว่าปลายทางนำข้อความไปแสดงแล้ว
_หลีกเลี่ยง_: ประวัติการส่งสำเร็จ, รายการยอดนิยม

**Activity Data**:
Recent และ Learned Ranking ซึ่งเป็นข้อมูลกิจกรรมส่วนตัวของผู้ใช้
_หลีกเลี่ยง_: Telemetry, global popularity data

## การส่ง Emoji

**Immediate Insert**:
การเริ่มส่ง Emoji ที่เลือกไปยังแอปเป้าหมายทันทีโดยไม่รอการยืนยันเป็นชุด
_หลีกเลี่ยง_: Batch insert

**Insertion Mode**:
นโยบายที่เลือกวิธีส่ง Unicode sequence เข้าแอปเป้าหมายตามความซับซ้อนและข้อจำกัดของปลายทาง
_หลีกเลี่ยง_: Renderer mode, Emoji format

**Insertion Queue**:
ลำดับงานที่รักษาให้ลำดับการส่งตรงกับลำดับการเลือกเมื่อผู้ใช้เลือกเร็วเกินกว่าจะส่งเสร็จทีละรายการ
_หลีกเลี่ยง_: Parallel insertion

**Queue Cancellation**:
การหยุดงานที่ยังไม่เริ่มเมื่อผู้ใช้ยุติ Picker Session โดยไม่พยายามย้อนงานที่เริ่มส่งแล้ว
_หลีกเลี่ยง_: Drain after dismiss, interrupt active injection

**Target Validation**:
การยืนยันว่าแอปเป้าหมายเดิมยังเป็นปลายทางที่ปลอดภัยก่อนส่งข้อมูล
_หลีกเลี่ยง_: Focus stealing, silent retargeting

**Injection Accepted**:
ผลลัพธ์ที่ยืนยันว่าระบบปฏิบัติการรับคำสั่งส่งข้อมูลครบ แต่ไม่ยืนยันว่าแอปเป้าหมายนำข้อความไปแสดงแล้ว
_หลีกเลี่ยง_: Verified insert, guaranteed delivery

**Insertion Failure**:
ผลลัพธ์เมื่อไม่สามารถส่ง Emoji ไปยังแอปเป้าหมายได้อย่างปลอดภัย
_หลีกเลี่ยง_: Automatic retry, silent retargeting

**Temporary Paste**:
การใช้ clipboard ภายในชั่วคราวเพื่อส่ง Unicode sequence ซับซ้อนเป็นก้อนเดียว โดยไม่ใช่การ Copy ที่ผู้ใช้ร้องขอ
_หลีกเลี่ยง_: Explicit Copy, Recent storage

**Explicit Copy**:
การคัดลอก Emoji ไปยัง clipboard ตามคำสั่งโดยตรงของผู้ใช้
_หลีกเลี่ยง_: Temporary Paste, auto-copy

## ขอบเขตเว็บ

**Display Content**:
ข้อความสำหรับอ่านบนหน้าเว็บซึ่ง Renderer Extension มีสิทธิ์ตรวจจับและแก้การแสดงผล
_หลีกเลี่ยง_: Editable Content

**Editable Content**:
ข้อความที่ผู้ใช้กำลังแก้ไขและ Renderer Extension รุ่นแรกต้องไม่เปลี่ยนแปลง
_หลีกเลี่ยง_: Display Content

**Tofu**:
สัญลักษณ์กล่องที่ปรากฏเมื่อ renderer ไม่มี glyph สำหรับ Unicode sequence นั้น
_หลีกเลี่ยง_: Missing Emoji Entry, missing text
