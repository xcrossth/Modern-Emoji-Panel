# 11: เรียนรู้ Recent และลำดับความชอบบนเครื่อง

**What to build:** ให้ Picker จำ Emoji ที่ผู้ใช้เลือกและปรับลำดับผลลัพธ์ภายใน match tier ตามพฤติกรรมบนเครื่อง โดยข้อมูลเสียไม่ทำให้แอปเปิดไม่ได้และไม่ปะปนกับ Classic

**Blocked by:** 07: เลือกสีผิวและ Variant Override ได้ครบทุก sequence; 09: ควบคุม Picker Session ด้วย keyboard, pointer และ focus

**Status:** resolved

- [x] Recent เป็น MRU สูงสุด 50 รายการ เพิ่มทันทีเมื่อเลือกแม้ insertion ล้มเหลว และรายการซ้ำย้ายขึ้นหน้า
- [x] Recent เก็บ resolved Unicode sequence จริงรวม skin tone หรือ Variant Override และคงอยู่ข้าม session
- [x] เมื่อมี Recent Picker เปิดหมวด Recent เป็น Initial View; เมื่อไม่มีให้เปิด Smileys & Emotion
- [x] Learned Ranking เพิ่มคะแนนระดับ Emoji Entry ฐานเมื่อเลือก ใช้ frequency กับ half-life 90 วัน และไม่แยกคะแนนตาม skin tone
- [x] Learned Ranking เปลี่ยนลำดับได้เฉพาะภายใน match tier เดียวกัน และ tie สุดท้ายยัง deterministic ตาม CLDR
- [x] Activity Data ใช้ stable identifiers, versioned schemas และ atomic writes โดยไม่ import ข้อมูลจาก Classic
- [x] ไฟล์ที่อ่านไม่ได้ถูกสำรองเป็น `.corrupt-<เวลา>` แล้ว reset เฉพาะข้อมูลส่วนนั้นพร้อมแจ้งผู้ใช้ โดยแอปยังเปิดได้
- [x] Clear Recent, Reset learned ranking และ Clear all activity ทำงานแยกกันได้และมี automated tests ครบ

## หลักฐานการตรวจรับ

- commit งาน `5bd19a2` และ merge ที่ผ่านร่วมกับ queue/settings บน branch MVP
- `scripts/verify-activity-data.ps1` ผ่าน: Recent MRU 50, resolved sequence, half-life 90 วัน, versioned atomic persistence, recovery แยกส่วน และคำสั่งล้างทั้งสามแบบ
- domain tests ตรวจ migration จากข้อมูล Modern เดิม, สำรอง `.corrupt-<เวลา>`, stable base identifiers และ ranking ที่ไม่ข้าม match tier
- clean-checkout ล่าสุดผ่าน Activity Data พร้อม search, variants, Picker Session, Insertion Queue และ Settings/privacy regressions
- รายละเอียด schema และ recovery อยู่ที่ `docs/local-activity-data.md`
