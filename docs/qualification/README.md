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
- การเผยแพร่ Release ไม่ใช่หลักฐานว่า environment ที่ไม่ได้ทดสอบผ่าน การตัดสินใจเผยแพร่และผล qualification ต้องบันทึกแยกกัน

ให้ใช้ [manual matrices](./manual-matrices.md) บันทึกแต่ละกรณี ห้ามเปลี่ยนแถวเป็น “ผ่าน” โดยไม่มีวันที่ ผู้ทดสอบ environment และหลักฐานที่ตรวจย้อนกลับได้

## ตัวช่วย Manual Qualification

เมื่อผู้ทดสอบพร้อมอยู่หน้าเครื่อง ให้ build commit ที่ต้องการตรวจ แล้วเปิด Git Bash จาก root ของ repository:

```bash
bash scripts/manual-qualification-wizard.sh
```

wizard มี 7 stage ครอบคลุม preflight, Tier A apps, keyboard/accessibility, input/sequence/queue, Clipboard/target safety, privacy 15 นาที และสรุปผล สามารถกด `Ctrl+C` แล้วรันซ้ำภายในวันเดียวกันเพื่อเก็บผลเดิมหรือแก้เฉพาะกรณีได้ ผลดิบและรายงาน JSON/Markdown อยู่ใต้ `artifacts/ticket-13/manual/` ซึ่ง Git ignore ไว้

ผลจาก wizard เป็นเพียงหลักฐานที่มนุษย์บันทึกและมี `acceptedAutomatically: false` เสมอ Agent/maintainer ต้อง review ก่อนคัดลอกเข้าสู่ `manual-matrices.md` ตรวจโครงสร้างตัวช่วยได้โดยไม่เริ่ม session ด้วย:

```powershell
pwsh scripts/verify-manual-qualification-wizard.ps1
```

## สถานะปัจจุบัน

ผลอัตโนมัติของเครื่อง Windows 10 build 19045 อยู่ที่ [`results/automated-win10-19045.json`](./results/automated-win10-19045.json) และผล hook-to-visible จริงอยู่ที่ [`results/global-hotkey-win10-19045.json`](./results/global-hotkey-win10-19045.json)

ผู้ทดสอบรัน manual wizard เมื่อ 29 สิงหาคม 2026 และ Agent review เทียบกับข้อความ/ภาพต้นทางแล้ว ผลรอบแรกคือผ่าน 31, ไม่ผ่าน 3, ทำไม่ได้ใน environment 7 และยังไม่ทดสอบ 2 รายการ ดูรายละเอียดที่ [`results/manual-win10-19045-20260829.md`](./results/manual-win10-19045-20260829.md) หลังจากนั้นมี regression และการแก้ rapid-click/focus, Chrome omnibox และ High Contrast เพิ่มเติม แต่ matrix เดิมยังคงผลตามเวลาที่ทดสอบเพื่อไม่แก้หลักฐานย้อนหลัง

Maintainer อนุมัติและเผยแพร่ Public MVP `v0.1.9` เมื่อ 30 สิงหาคม 2026 โดยยอมรับขอบเขตที่ยังไม่ครอบคลุม การเผยแพร่จึงไม่ใช่คำรับรอง Windows 11, mixed-DPI, NVDA, RDP, Citrix หรือทุกแอปเป้าหมาย

ผลปิด MVP หลังแก้ regression และรับรองขอบเขตสุดท้ายอยู่ที่ [`results/picker-mvp-closure-win10-20260830.md`](./results/picker-mvp-closure-win10-20260830.md) Ticket 13 ปิดเป็น `resolved` โดยรักษา matrix รอบแรกไว้เป็นหลักฐานประวัติ และจัดรายการที่เครื่องนี้ทดสอบไม่ได้หรือผู้ดูแลไม่ต้องการขยายผลเป็น known limitations ที่ไม่บล็อก release
