# Product icon ของ Modern Emoji Picker

ไฟล์ `modern-emoji-picker-master.png` เป็น artwork ต้นฉบับขนาดสี่เหลี่ยมโปร่งใสที่สร้างใหม่สำหรับ Modern Emoji Picker โดยไม่ใช้ภาพหรือ icon ของ Classic Emoji Picker

แนวคิดคือแผงเลือกแบบ 2×2 ที่มีช่องหนึ่งเป็นใบหน้ายิ้มและมีประกายเล็กด้านขวาบน รูปทรงหลักตั้งใจให้ยังอ่านออกเมื่อย่อเป็น tray/application icon และใช้สี indigo, cyan, gold กับ coral เพื่อแยกจาก platform emoji artwork

## การสร้างไฟล์ใช้งาน

รันจาก root ของ repository:

```powershell
.\scripts\build-product-icon.ps1
```

สคริปต์จะตรวจ SHA-256 ของ master จาก `icon-generation.json` แล้วสร้าง:

- `apps/picker/EmojiPicker/Resources/modern-emoji-picker.ico` หลาย frame ตั้งแต่ 16–256 px
- `apps/picker/EmojiPicker/Resources/modern-emoji-picker-512.png` สำหรับ preview/balloon ขนาดใหญ่ในอนาคต

ใช้ `-VerifyOnly` เพื่อตรวจ master, ICO frames, alpha และภาพ 512 px โดยไม่เขียนไฟล์ใหม่

## แหล่งที่มา

สร้างด้วย built-in `image_gen` ใน Codex โหมด generate เมื่อ 29 สิงหาคม 2026 Prompt ฉบับเต็มและ hash อยู่ใน `icon-generation.json` ซึ่งเป็น metadata สำหรับเครื่องมือ/เอเจนต์ ส่วน conversion จาก master ไปเป็น ICO/PNG ทำซ้ำได้ด้วยสคริปต์ใน repository
