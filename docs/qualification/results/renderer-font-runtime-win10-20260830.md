# ผลตรวจฟอนต์ของ Renderer Extension บน Chrome

สถานะ: **ผ่านหลังแก้ไข**

## อาการที่พบ

Extension สร้าง wrapper รอบ Emoji และ computed style แสดงชื่อ `ModernEmojiNoto` แต่ glyph จริงยังมาจาก `Segoe UI Emoji` ทำให้ Emoji ใหม่บน Windows 10 เป็น tofu หรือใช้หน้าตาของฟอนต์ระบบ

## สาเหตุ

ไฟล์ CSS ที่ inject ผ่าน `manifest.content_scripts.css` อ้างฟอนต์ด้วย URL แบบ relative Chrome จึงพยายามโหลดจาก URL ของเอกสาร เช่น `https://www.instagram.com/direct/fonts/Noto-COLRv1.ttf` แทนไฟล์ในส่วนขยาย FontFace จึงอยู่ในสถานะ `error`

## การแก้ไข

Content controller ใช้ `chrome.runtime.getURL("assets/fonts/Noto-COLRv1.ttf")` สร้าง URL แบบ absolute แล้ว inject `@font-face` และ wrapper styles จาก code path เดียว CSS injection แบบเดิมถูกนำออกจาก manifest, dynamic registration และ Popup

## หลักฐานหลังแก้

- Text wrapper ถูกสร้างครบ 8 ตัวในชุด Emoji ใหม่, VS16, skin tone, ZWJ, family, keycap และ regional flag
- Instagram image-Emoji wrapper ถูกสร้างเพิ่ม 1 ตัวจาก `/images/emoji.php/`, source image ถูกซ่อน และรูปทั่วไปไม่ถูกแตะ
- FontFace `ModernEmojiNoto` มีสถานะ `loaded`
- Chrome DevTools Protocol รายงาน glyph font เป็น `Noto Color Emoji` / `NotoColorEmoji`
- `isCustomFont` เป็น `true`
- Font request ใช้ `chrome-extension://…/assets/fonts/Noto-COLRv1.ttf`
- หน้า Instagram fixture ถูกตอบผ่าน CDP ภายในเครื่อง จึงไม่มีการเข้าถึงบัญชีหรือส่ง request ไป Instagram จริง

รันซ้ำได้ด้วย:

```powershell
npm --prefix .\apps\renderer-extension run build
npm --prefix .\apps\renderer-extension run verify:extension-font
```
