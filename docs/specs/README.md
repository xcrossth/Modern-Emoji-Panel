# สเปกผลิตภัณฑ์

- [SPEC 01 — Modern Emoji Picker](./01-modern-emoji-picker.md): สเปกหลักที่ยืนยัน shared understanding แล้ว และเป็นขอบเขตของ Picker MVP
- [SPEC 02 — Chrome Emoji Renderer Fix](./02-chrome-emoji-renderer-extension.md): ผลิตภัณฑ์เสริมที่แยกจาก Picker และเลื่อนไปทำหลัง Picker MVP

หากสเปกทั้งสองกล่าวถึงข้อมูล Emoji ร่วมกัน ให้ยึด Emoji Baseline และการตัดสินใจใน `docs/adr/` เป็นข้อกำหนดร่วม แต่ห้ามสร้าง runtime dependency ระหว่างผลิตภัณฑ์
