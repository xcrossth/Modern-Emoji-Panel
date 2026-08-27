# ใช้ monorepo และนำ Picker เข้าเป็น Git subtree

โครงการเลือกเก็บ Picker, Renderer Extension และ Emoji Baseline tooling ใน monorepo เดียว แต่ให้ผลิตภัณฑ์ build, ติดตั้งและ release อย่างอิสระ โดยนำ platima/Classic-EmojiPicker เข้า apps/picker ด้วย Git subtree ที่ตรึง commit SHA วิธีนี้ทำให้ baseline เปลี่ยนพร้อมกันได้และยังรับ bugfix จาก upstream แบบ manual โดยไม่ต้องพึ่ง repository ซ้อน

## ตัวเลือกที่พิจารณา

- แยก repository ของแต่ละผลิตภัณฑ์: ขอบเขตชัด แต่เพิ่มโอกาสที่ Emoji Baseline drift
- Git submodule: รักษา upstream ง่าย แต่ clone, build และพัฒนา fork ซับซ้อนขึ้น
- Copy source snapshot: เริ่มง่าย แต่สูญประวัติและรับ upstream update ยาก

## ผลที่ตามมา

- Upstream update ต้องเข้า branch แยก บันทึก SHA, review และทดสอบก่อน merge
- Workflow และ installer paths ต้องปรับจาก repository root ไปยัง apps/picker
- Modern ต้องมี process, installer, registry และ data identity ใหม่ทั้งหมดเพื่อไม่กระทบ Classic
