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
- scan runtime source เพื่อหา network/telemetry/upload/sync API และเฝ้าดู TCP connection/UDP endpoint ของ process ทุก 50 ms ระหว่าง qualification smoke
- บันทึก machine, SDK, commit, ขนาด Noto assets และ self-contained publish ลง JSON

Smoke mode ไม่ติดตั้ง global hook, ไม่สร้าง tray, ไม่อ่านหรือเขียนข้อมูลผู้ใช้, ไม่ inject input และไม่แตะ Clipboard การไม่พบ socket ในช่วง smoke เป็นหลักฐานเชิงสังเกตที่ทำซ้ำได้ แต่ไม่ใช่ packet-capture certification

## สิ่งที่ผลอัตโนมัติพิสูจน์ไม่ได้

- เวลา warm hotkey-to-visible จริง เพราะ proxy ข้าม keyboard hook, target capture และ foreground activation
- การรับ/แสดง Emoji ของ Notepad, Chrome, VS Code, Windows Terminal และ Explorer address bar
- Thai IME/dead keys, Clipboard format จริง, elevated target, focus race และ rapid clicks บนเดสก์ท็อปจริง
- คุณภาพการอ่านด้วย Narrator/NVDA, focus indicator ด้วยสายตา และ High Contrast palette จริง
- DPI 100–250% กับจอหลายตัวต่าง DPI
- Windows 11, RDP, Citrix และ Tier B apps
- installer/portable ZIP size และ release preconditions ซึ่งเป็นงาน Ticket 14

ให้ใช้ [manual matrices](./manual-matrices.md) บันทึกแต่ละกรณี ห้ามเปลี่ยนแถวเป็น “ผ่าน” โดยไม่มีวันที่ ผู้ทดสอบ environment และหลักฐานที่ตรวจย้อนกลับได้

## สถานะปัจจุบัน

ผลอัตโนมัติของเครื่อง Windows 10 build 19045 เก็บใน `results/` หลังรันคำสั่งข้างต้น ส่วน manual matrices ยังเป็น “ยังไม่ทดสอบ” ทั้งหมด จึงยังไม่ถือว่า Ticket 13 หรือ Picker MVP ผ่านการรับรองปล่อยจริง
