# คู่มือ artifact ของ Modern Emoji Picker MVP

ชุดไฟล์นี้สร้างจากเครื่อง local ด้วย `scripts/release.ps1` และยังไม่ใช่ public release

## รูปแบบ

- `Modern-Emoji-Picker-v<version>-setup-win-x64.exe` — Inno per-user installer แบบ self-contained
- `Modern-Emoji-Picker-v<version>-portable-win-x64.zip` — portable แบบ self-contained
- `SHA256SUMS.txt` และ `release-manifest.json` — hash, commit, architecture และขนาด

MVP ไม่มี framework-dependent, lite หรือ MSI package

## Windows และ .NET 10

โครงการตรวจ automated qualification บน Windows 10 Enterprise N 22H2 build 19045 x64 จริง แต่ Windows 10 22H2 รุ่นทั่วไปไม่อยู่ใน supported-OS matrix ปัจจุบันของ .NET 10 จึงต้องอ่านผล matrix ของ Ticket 13 ก่อนตีความว่า environment ใดได้รับการรับรอง

Artifact เป็น self-contained จึงไม่ต้องติดตั้ง .NET Runtime แยก อย่างไรก็ตามเมื่อ .NET 10 มี security/servicing update โครงการต้อง rebuild artifact และทำ smoke test ก่อนออก patch release

## SmartScreen และลายเซ็น

MVP ยังไม่มี code-signing certificate Windows SmartScreen จึงอาจเตือนว่าไม่รู้จักผู้เผยแพร่ ตรวจชื่อไฟล์และ SHA-256 กับ `SHA256SUMS.txt` ที่มาจาก repository/Draft Release ทางการก่อนเปิด ห้ามใช้คำเตือนนี้เป็นเหตุผลให้ปิดระบบป้องกันทั้งเครื่อง

## Installer

Installer ติดตั้งเฉพาะผู้ใช้ปัจจุบันโดยไม่ขอสิทธิ์ administrator และเปิด Start with Windows เป็นค่าเริ่มต้น Uninstaller เก็บ `%APPDATA%\ModernEmojiPicker` ไว้ตามค่าเริ่มต้น; เลือก checkbox ลบ Settings และ Activity Data เฉพาะเมื่อต้องการล้างข้อมูล Modern จริง

## Portable

แตก ZIP ไปยังโฟลเดอร์ที่เขียนได้แล้วเปิด `ModernEmojiPicker.exe` Portable ไม่เปิด autostart เอง ใช้ Settings เมื่อต้องการเปิดพร้อม Windows และใช้ `Clear all activity` เมื่อต้องการล้าง Activity Data

Modern ไม่อ่าน แก้ หรือลบข้อมูลของ Classic Emoji Picker
