# Insertion Queue และ Typing Handoff

Picker ใช้ Insertion Queue แบบ FIFO เพื่อรักษาให้ลำดับการส่งตรงกับลำดับที่ผู้ใช้เลือก Emoji อย่างรวดเร็ว คิวรับงานที่ยังไม่เริ่มได้สูงสุด 20 รายการ และมี active operation ได้ครั้งละหนึ่งรายการเท่านั้น การส่งแต่ละรายการยังตรวจแอปเป้าหมายเดิมซ้ำตามนโยบาย Target Validation และไม่เปลี่ยนไปส่งหน้าต่างอื่น

## สถานะคิว

- รายการที่ผู้ใช้เลือกจะเข้า pending ตามลำดับ click หรือ Commit Gesture
- เมื่อ adapter เริ่มส่ง รายการเดียวจะย้ายจาก pending เป็น active
- Picker แสดงสถานะ `Sending`, จำนวน pending และ `Queue full` ที่หัว grid พร้อมประกาศผ่าน accessibility live region
- เมื่อมีงานรอครบ 20 รายการ Picker จะหยุดรับชั่วคราวและแจ้งสถานะอย่างชัดเจน งานจะไม่ถูกทิ้งแบบเงียบ
- หลัง active operation และ pending ทั้งหมดจบ Picker จึงกลับมาแสดงโดยคง selection, query, category และ scroll จากการเลือกล่าสุด

Enter เป็น Commit Gesture ที่ต้องส่ง Emoji ของตัวเอง จึงปิดรับงานใหม่แล้วปล่อยรายการที่รับไว้ก่อนหน้าและรายการ Enter ให้จบตาม FIFO จากนั้น dismiss ส่วน Esc, ปุ่มปิด, click ภายนอก, Tray → Exit และ Typing Handoff ใช้ Queue Cancellation: หยุดรับงานใหม่, ยกเลิกงานที่ยังไม่ข้าม seam ไปเป็น active และปล่อยเฉพาะ active operation ให้จบ

Tray → Exit จะรอ active operation จบก่อน shutdown เพื่อไม่ตัดการส่งกลางคัน

## Typing Handoff

ใน Browse Mode ระบบฟังเฉพาะ WPF `TextInput` ที่ commit แล้ว ไม่ดัก raw key เพื่อ replay ดังนั้น shortcut chord, dead-key prefix และช่วง pre-edit ของ IME จะไม่ถูกนำไปส่งซ้ำ

เมื่อได้รับ committed printable text:

1. เก็บ string ที่ commit แล้วไว้ในหน่วยความจำเท่านั้น โดยไม่ log, persist หรือแตะ clipboard
2. หยุดรับ Emoji ใหม่และยกเลิก pending insertion
3. รอ active insertion ที่เริ่มแล้วให้จบ
4. activate และ validate แอปเป้าหมายเดิม แล้วส่ง committed text หนึ่งครั้งผ่าน insertion path เดียวกับ Emoji
5. หาก validation หรือการส่งล้มเหลว จะเปิด Picker เดิมพร้อม error และให้ผู้ใช้เลือก Copy เอง โดยไม่ retarget หรือ retry

Thai IME อาจ commit อักษรฐานและวรรณยุกต์รวมมาเป็น string เดียว ระบบเก็บ string นั้นทั้งก้อน ส่วน dead key จะถูกส่งต่อเฉพาะผลที่ compose เสร็จแล้ว เช่น `é` ไม่ส่ง prefix แยก Shortcuts ที่ให้ control character และ IME pre-edit ที่ยังไม่มี committed text จะไม่เริ่ม Typing Handoff

## การตรวจสอบ

รัน:

```powershell
.\scripts\verify-insertion-queue.ps1
```

ตัวตรวจครอบคลุม FIFO 21 insertion (หนึ่ง active และ 20 pending), capacity/full feedback, single-active invariant, cancellation, Enter drain, focus policy และการรักษา Thai/dead-key/surrogate text ผ่าน state seam แบบ deterministic พร้อมตรวจ wiring ของ WPF โดยไม่ส่ง input จริง

การตรวจด้วย Thai IME จริง, dead-key layout จริง, rapid clicks ใน Notepad/Chrome/VS Code/Windows Terminal/Explorer และ screen reader อยู่ใน manual qualification ของ Ticket 13 เนื่องจาก automated verifier นี้ตั้งใจไม่พึ่ง desktop timing
