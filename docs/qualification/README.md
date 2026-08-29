# การรับรองคุณภาพ Picker MVP

โฟลเดอร์นี้แยกหลักฐานที่รันอัตโนมัติได้ออกจากการรับรองด้วยผู้ทดสอบจริงอย่างชัดเจน เพื่อไม่ให้ผล smoke แบบ headless ถูกตีความว่า Notepad, screen reader, จอหลายตัว หรือ Windows 11 ผ่านแล้ว

## หลักฐานอัตโนมัติ

รันจาก root ของ repository:

```powershell
pwsh scripts/verify-qualification.ps1 -OutputPath docs/qualification/results/automated-win10-19045.json
```

สคริปต์จะ:

- รัน regression gates สำหรับ generator, source lock, search tiers, Learned Ranking, variants, Recent, persistence recovery, queue, target validation, insertion modes, clipboard rules และ Settings/privacy
- build และ publish แบบ Release, self-contained, win-x64
- วัด warm open-to-render **proxy** 20 ครั้ง, search 1,000 ครั้ง, virtualized scroll 100 ตำแหน่ง, working set หลัง trim และ decode/cache 128 ภาพ
- ตรวจ accessible name, live state, focus indicator, virtualization, DPI decode calculation และ High Contrast theme ที่อ้าง `SystemColors`
- scan runtime source เพื่อหา network/telemetry/upload/sync API และเฝ้าดู TCP connection/UDP endpoint ทุก 50 ms ระหว่าง qualification workload แยก process เพื่อไม่ให้ CIM socket monitor รบกวน performance sample
- บันทึก machine, SDK, commit, ขนาด Noto assets และ self-contained publish ลง JSON

การวัด warm global hotkey-to-visible จริงต้องรันแบบตั้งใจ เพราะสคริปต์จะเปิด Notepad ชั่วคราว, นำหน้าต่างนั้นขึ้น foreground และส่ง `Win + .` ด้วย `SendInput` 20 ครั้ง:

```powershell
pwsh scripts/measure-global-hotkey.ps1 -OutputPath docs/qualification/results/global-hotkey-win10-19045.json
```

สคริปต์ใช้ low-level hook, target/focus/caret capture, WPF show/activation และ Render dispatcher จริง แต่ไม่เลือกหรือส่ง Emoji, ไม่แตะ Clipboard, ไม่สร้าง tray และไม่อ่าน Activity Data ของผู้ใช้ จากนั้นสามารถรวมผลกับ qualification report ได้ด้วย `-GlobalHotkeyReportPath`

เมื่อต้องการหลักฐาน installer/portable และ release preconditions ให้รันจาก clean commit:

```powershell
pwsh scripts/release.ps1 -Version 0.1.9
```

คำสั่งนี้สร้าง artifact เฉพาะใน `artifacts/release/`, ตรวจ checksum/contents/identity/architecture และรัน qualification ซ้ำพร้อม package metrics โดยไม่สร้าง tag, upload หรือ GitHub Release หลักฐาน Ticket 14A ที่ commit ได้อยู่ใน `results/local-artifacts-v0.1.9-win10-19045.json`

qualification smoke ปกติไม่ติดตั้ง global hook, ไม่สร้าง tray, ไม่อ่านหรือเขียนข้อมูลผู้ใช้, ไม่ inject input และไม่แตะ Clipboard ส่วน `measure-global-hotkey.ps1` เป็นข้อยกเว้นที่ส่งเฉพาะ shortcut ไปยัง Notepad ทดสอบตามคำสั่งโดยเจตนา การไม่พบ socket ในช่วง smoke เป็นหลักฐานเชิงสังเกตที่ทำซ้ำได้ แต่ไม่ใช่ packet-capture certification

## สิ่งที่ผลอัตโนมัติพิสูจน์ไม่ได้

- การรับ/แสดง Emoji ของ Notepad, Chrome, VS Code, Windows Terminal และ Explorer address bar
- Thai IME/dead keys, Clipboard format จริง, elevated target, focus race และ rapid clicks บนเดสก์ท็อปจริง
- คุณภาพการอ่านด้วย Narrator/NVDA, focus indicator ด้วยสายตา และ High Contrast palette จริง
- DPI 100–250% กับจอหลายตัวต่าง DPI
- Windows 11, RDP, Citrix และ Tier B apps
- การเผยแพร่ Draft/public release ซึ่งแยกเป็น Ticket 15 (14B) และยังรอ manual qualification กับคำสั่งโดยเจตนาจาก maintainer

ให้ใช้ [manual matrices](./manual-matrices.md) บันทึกแต่ละกรณี ห้ามเปลี่ยนแถวเป็น “ผ่าน” โดยไม่มีวันที่ ผู้ทดสอบ environment และหลักฐานที่ตรวจย้อนกลับได้

## สถานะปัจจุบัน

ผลอัตโนมัติของเครื่อง Windows 10 build 19045 อยู่ที่ [`results/automated-win10-19045.json`](./results/automated-win10-19045.json) และผล hook-to-visible จริงอยู่ที่ [`results/global-hotkey-win10-19045.json`](./results/global-hotkey-win10-19045.json) ส่วน manual matrices ยังเป็น “ยังไม่ทดสอบ” ทั้งหมด จึงยังไม่ถือว่า Ticket 13 หรือ Picker MVP ผ่านการรับรองปล่อยจริง
