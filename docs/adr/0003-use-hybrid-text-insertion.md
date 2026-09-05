# ใช้ Hybrid insertion และ Temporary Paste แบบ best-effort

โครงการเลือก Hybrid เป็นค่าเริ่มต้น: Emoji เดี่ยวใช้ Unicode keystroke บน native target ส่วน ZWJ, flags, keycaps และ sequence ซับซ้อนใช้ Temporary Paste เพื่อให้ปลายทางรับลำดับเป็นก้อนเดียว Text edit target ใน Chrome accessibility framework ใช้ Temporary Paste เพิ่มเติมเมื่อ sequence มี supplementary scalar เพราะ Chromium editor บางรุ่นแปลง surrogate pair ที่ส่งหลัง focus round-trip เป็น `U+FFFD` ก่อนส่งต้องยืนยัน target ซ้ำและความล้มเหลวต้องหยุดอย่างปลอดภัยพร้อมเสนอ Explicit Copy

## ตัวเลือกที่พิจารณา

- Keystroke only: ไม่แตะ clipboard แต่ sequence ซับซ้อนอาจถูกปลายทางแยกหรือประกอบผิด
- Paste always: เข้ากันได้กับ sequence ซับซ้อนกว่า แต่แตะ global clipboard ทุกครั้ง
- ส่งไปยัง foreground window ล่าสุด: ลด focus failure แต่เสี่ยงส่งข้อความผิดหน้าต่าง

## ผลที่ตามมา

- Temporary Paste ต้อง snapshot และ restore clipboard แบบ best-effort พร้อม exclusion marker สำหรับ Windows Clipboard History และ Cloud Clipboard
- หาก clipboard เปลี่ยนระหว่างทางต้องไม่ restore ทับข้อมูลใหม่
- ไม่รับประกันว่า target paste สำเร็จ, private clipboard formats คืนได้ครบ หรือ clipboard manager ภายนอกจะเคารพ marker
- Explicit Copy ที่ผู้ใช้สั่งเองต้องเข้า clipboard และ Win+V ตามปกติ
