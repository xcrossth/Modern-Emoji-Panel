# สถานะเวอร์ชัน Modern Emoji Picker

## Development snapshot

- Product metadata: `0.1.9`
- Target framework: `.NET 10` (`net10.0-windows`)
- Runtime identifier: `win-x64`
- รูปแบบ build: self-contained local publish
- สถานะ release: ยังไม่มี public MVP release

หมายเลข `0.1.9` เป็น metadata ของ checkpoint ระหว่างพัฒนา ไม่ใช่คำประกาศว่า Modern Emoji Picker v0.1.9 ถูกเผยแพร่แล้ว เวอร์ชัน release จริงต้องผ่าน Ticket 13 และ Ticket 15 (14B)

## ความเข้ากันได้ที่ยืนยันแล้ว

- Automated qualification ผ่านบน Windows 10 Enterprise N 22H2 build 19045 x64
- Release build และ self-contained publish ด้วย .NET SDK 10 ผ่านโดยไม่มี warning/error
- Runtime ใช้ Emoji 17 baseline และ Noto assets ที่ bundle อยู่ใน package โดยไม่ใช้ network

Windows 11, แอป Tier A/Tier B, screen reader, mixed-DPI และ input/clipboard matrix บน desktop จริงยังอยู่ใน Ticket 13 จึงห้ามตีความ automated result เป็น support certification ที่ครบแล้ว

## Package ของ MVP

มีเพียง self-contained win-x64 Inno per-user installer และ portable ZIP ไม่มี framework-dependent, lite หรือ MSI package

- Product: Modern Emoji Picker
- Executable/assembly: `ModernEmojiPicker`
- User data: `%APPDATA%\ModernEmojiPicker`
- Tag เมื่อได้รับอนุมัติ: `picker-v<version>`

รายละเอียด local artifacts อยู่ใน [Ticket 14](../../.scratch/modern-emoji-picker/issues/14-package-and-release-locally.md) และ release gate อยู่ใน [Ticket 15](../../.scratch/modern-emoji-picker/issues/15-publish-picker-release.md)
