# สถานะเวอร์ชัน Modern Emoji Picker

## Public MVP

- Product metadata: `0.1.10`
- Target framework: `.NET 10` (`net10.0-windows`)
- Runtime identifier: `win-x64`
- รูปแบบ build: self-contained local publish
- สถานะ release: เผยแพร่แล้วใน GitHub Release `v0.1.10`

หมายเลขผลิตภัณฑ์ `0.1.10` ตรงกับ assembly metadata, installer, portable package และ Release ปัจจุบัน

## ความเข้ากันได้ที่ยืนยันแล้ว

- Automated qualification และ workflow หลักผ่านบน Windows 10 Enterprise N 22H2 build 19045 x64
- Release build และ self-contained publish ด้วย .NET SDK 10 ผ่านโดยไม่มี warning/error
- Runtime ใช้ Emoji 17 baseline และ Noto assets ที่ bundle อยู่ใน package โดยไม่ใช้ network

ผล manual ครอบคลุม Notepad, Chrome, VS Code, Narrator, input sequence และ Clipboard หลายรูปแบบ รายการที่ยังไม่ทดสอบหรือทำไม่ได้ เช่น Windows 11, Windows Terminal, mixed-DPI, NVDA, RDP และ Citrix บันทึกตามจริงใน [`docs/qualification`](../../docs/qualification/) จึงไม่ควรตีความ Public MVP ว่าเป็นการรับรองทุก environment

## Package ของ MVP

มีเพียง self-contained win-x64 Inno per-user installer และ portable ZIP ไม่มี framework-dependent, lite หรือ MSI package

- Product: Modern Emoji Picker
- Executable/assembly: `ModernEmojiPicker`
- User data: `%APPDATA%\ModernEmojiPicker`
- Release tag: `v0.1.10`

รายละเอียด local artifacts อยู่ใน [Ticket 14](../../.scratch/modern-emoji-picker/issues/14-package-and-release-locally.md) และประวัติการเผยแพร่อยู่ใน [Ticket 15](../../.scratch/modern-emoji-picker/issues/15-publish-picker-release.md)
