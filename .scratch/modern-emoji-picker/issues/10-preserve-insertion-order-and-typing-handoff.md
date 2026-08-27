# 10: รักษาลำดับการส่งและทำ Typing Handoff โดยไม่ทำ input หาย

**What to build:** รองรับการเลือก Emoji ต่อเนื่องอย่างรวดเร็วด้วย Insertion Queue และคืนการพิมพ์ให้แอปเป้าหมายเมื่อผู้ใช้เริ่มพิมพ์ต่อ โดยไม่ทำ input แรกหรือองค์ประกอบของ IME สูญหาย

**Blocked by:** 09: ควบคุม Picker Session ด้วย keyboard, pointer และ focus

**Status:** ready-for-agent

- [ ] Insertion Queue รับงานรอสูงสุด 20 รายการและรักษา click order ให้ตรงกับ insertion order โดยไม่ส่งขนาน
- [ ] UI และ accessibility state แสดง pending/busy และเมื่อ queue เต็มจะหยุดรับชั่วคราวโดยไม่ drop click แบบเงียบ
- [ ] เมื่อ dismiss ระบบหยุดรับงานใหม่ ปล่อยเฉพาะ active operation ให้จบ และยกเลิกงานที่ยังไม่เริ่มก่อนปิด Picker
- [ ] printable input ใน Browse Mode เริ่ม Typing Handoff แทนการเข้าสู่ Search Mode
- [ ] Typing Handoff เก็บ input แรกไว้อย่างปลอดภัยระหว่างรอ active operation และส่งต่อไปยังแอปเป้าหมายเดิมหลัง validation
- [ ] Thai IME, dead keys และ shortcuts ที่อยู่ในขอบเขตทดสอบไม่ถูกกลืน, ทำซ้ำ หรือ replay ด้วยวิธีที่ยังไม่ผ่านการพิสูจน์
- [ ] queue order, capacity, cancellation และ focus transitions มี automated tests ผ่าน abstraction ที่ไม่ขึ้นกับ timing จริงของ desktop
