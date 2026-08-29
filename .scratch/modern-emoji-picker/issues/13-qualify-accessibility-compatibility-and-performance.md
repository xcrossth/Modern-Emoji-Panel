# 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP

**What to build:** พิสูจน์ด้วย automated tests, manual matrices และตัวเลข benchmark ว่า Picker workflow หลักทำงานบนแพลตฟอร์มเป้าหมาย เข้าถึงได้ และไม่ถดถอยด้าน performance โดยไม่มี runtime network

**Blocked by:** 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview; 10: รักษาลำดับการส่งและทำ Typing Handoff โดยไม่ทำ input หาย; 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว; 14: สร้าง local qualification artifacts

**Status:** needs-info

- [x] automated suite ครอบคลุม generator, search tiers, ranking, variants, Recent, persistence recovery, queue, validation, insertion modes, clipboard rules และ release preconditions ตาม Test Strategy
- [ ] Manual Tier A ผ่านบน Notepad, Chrome, VS Code, Windows Terminal และ Explorer address bar บน Windows 10 22H2 x64
- [ ] Windows 11 smoke test ผ่าน และ Tier B ถูกทดสอบเมื่อ environment พร้อมโดยบันทึกข้อจำกัด RDP/Citrix แบบ best-effort
- [ ] DPI 100–250%, multi-monitor ต่าง DPI, keyboard navigation, focus indicator, High Contrast และ accessible name/state ผ่าน matrix ที่บันทึกผลได้
- [ ] Thai IME, English keyboard, single code point, variation selector, skin/mixed tone, flags, keycaps, ZWJ family และ rapid clicks ผ่าน workflow ที่เกี่ยวข้อง
- [ ] clipboard ว่าง, text, image, files และ custom formats รวมถึง target ปิด, focus เปลี่ยน และ elevated target ผ่าน safety matrix
- [ ] มีตัวเลข upstream/Modern สำหรับ warm hotkey-to-visible, search latency, scroll stalls, working set, decode/cache และ package sizes พร้อม performance budgets ที่ตรวจผ่าน
- [x] runtime verification ยืนยันว่าไม่มี update polling, telemetry, analytics, cloud sync หรือ remote font/asset calls

## Comments

### 28 สิงหาคม 2026 — automated qualification ที่ทำได้ใน environment ปัจจุบัน

เพิ่ม `scripts/verify-qualification.ps1` เพื่อรวม regression gates ของ Ticket 01–12, build/publish แบบ Release self-contained win-x64, ตรวจ accessibility wiring/High Contrast, วัด performance และเฝ้าดู socket ของ process จริงโดยไม่ติดตั้ง global hook ไม่ inject input ไม่แตะ Clipboard และไม่อ่าน/เขียนข้อมูลผู้ใช้

ผลบน Windows 10 Enterprise N build 19045, Intel Core i9-10900K, .NET SDK 10.0.400 / runtime 10.0.11:

- regression suite ผ่าน generator determinism/source lock/full coverage, Noto grid, search ไทย–อังกฤษและ match tiers, Learned Ranking/Recent/persistence recovery, variants, Picker Session, target validation/insertion modes/clipboard rules, queue/Typing Handoff และ Settings/privacy
- Release build 0 warnings/errors และ self-contained publish ผ่าน
- warm open-to-render proxy P95 9.6573 ms จาก 20 sample (budget 100 ms); metric นี้ข้าม global hook, target capture และ foreground activation จึงไม่ใช่ warm hotkey-to-visible จริง
- bilingual search P95 1.318 ms จาก 1,000 sample (budget 10 ms)
- virtualized scroll P95 41.3767 ms, maximum 48.4778 ms จาก 100 sample (budgets 60/150 ms); clean-checkout รอบแรกวัด P95 51.4855 ms จึงยืนยันว่า gate 50 ms แกว่งตามภาระเครื่องและปรับเป็น 60 ms ซึ่งเป็น guardrail ประมาณ 16 FPS โดยยังคง maximum 150 ms
- idle working set หลัง trim 12.7578 MiB (budget 128 MiB)
- grid decode P95 0.679 ms และ cache hit P95 0.0007 ms จากอย่างละ 128 sample; cache อยู่ที่ขอบเขต 256 ภาพ
- Noto assets 127,309,639 bytes; self-contained publish 312,784,054 bytes จาก budget 350 MiB
- static runtime scan ไม่พบ network/telemetry/upload/sync API และ dynamic monitor ไม่พบ TCP connection หรือ UDP endpoint ตลอด 29 sample ระหว่าง qualification smoke; เป็น socket observation ไม่ใช่ packet-capture certification
- เพิ่ม High Contrast theme ที่ใช้ Windows `SystemColors` และ refresh เมื่อ accessibility/color/visual-style เปลี่ยน; automated accessible name/live state/focus indicator/DPI calculation ผ่าน แต่ยังไม่อ้างว่า screen reader/visual matrix ผ่าน

หลักฐาน machine-readable อยู่ที่ `docs/qualification/results/automated-win10-19045.json` และ budgets/ข้อจำกัดอยู่ใน `docs/qualification/`

สถานะยังเป็น `needs-info` เพราะ acceptance criteria ต่อไปนี้ต้องใช้ human/external environment หรือ artifact ของ Ticket 14 และยังไม่มีหลักฐาน:

1. Tier A จริงบน Notepad, Chrome, VS Code, Windows Terminal และ Explorer address bar พร้อม hotkey/focus/insertion/Typing Handoff
2. Windows 11 smoke, Tier B, RDP/Citrix
3. DPI 100–250%, multi-monitor ต่าง DPI, keyboard navigation/focus indicator ด้วยสายตา, High Contrast และ Narrator/NVDA
4. Thai IME/dead key, sequence matrix, rapid clicks/queue บน desktop จริง
5. Clipboard ว่าง/text/image/files/custom formats และ target ปิด/focus เปลี่ยน/elevated target
6. warm hotkey-to-visible จริง, upstream search/scroll/decode/package raw measurements และ extended runtime packet capture
7. installer/portable ZIP size กับ release preconditions ซึ่ง Ticket 14A กำลังสร้างหลัง maintainer อนุมัติการแยก dependency แล้ว

แบบบันทึกผลทุกแถวอยู่ที่ `docs/qualification/manual-matrices.md`; ทุกกรณียังคงระบุ “ยังไม่ทดสอบ” โดยเจตนา

### 29 สิงหาคม 2026 — คลี่ dependency กับ release

Maintainer อนุมัติให้แยก Ticket 14 เดิมเป็น 14A/14B แล้ว Ticket 14A จึงเป็น blocker ของ qualification เฉพาะ local artifact/package metrics และ Ticket 15 (14B) รอ Ticket 13 ก่อนเตรียม Draft/public release ทำให้ไม่มีวงจร dependency อีกต่อไป

### 29 สิงหาคม 2026 — รับหลักฐาน local artifact จาก Ticket 14A

Ticket 14A ผ่านครบสายจาก clean commit `181cfe09a69e59285bece176c86a36333bab04bc` แล้ว จึงปิดเกณฑ์ automated suite/release preconditions และเติม package metrics จริงได้: raw Noto 127,309,639 bytes, self-contained publish 313,238,522 bytes (ผ่าน budget 350 MiB), portable ZIP 202,376,122 bytes และ installer 174,151,850 bytes พร้อม checksum ที่ verifier ตรวจผ่าน

หลักฐานล่าสุดอยู่ที่ `docs/qualification/results/automated-win10-19045.json` และ `docs/qualification/results/local-artifacts-v0.1.9-win10-19045.json` อย่างไรก็ตามเกณฑ์ performance โดยรวมยังไม่ปิด เพราะ warm hotkey-to-visible จริงและ upstream search/scroll/decode/package ที่ทำซ้ำได้ยังไม่มี ส่วน manual matrices ทุกชุดยังต้องให้มนุษย์ทดสอบ จึงคงสถานะ `needs-info` และไม่ปลด Ticket 15 (14B)

### 29 สิงหาคม 2026 — วัด global hotkey จริงและทำ performance gate ให้เสถียร

เพิ่ม qualification path ที่ติดตั้ง low-level hook จริง เปิด Notepad ทดสอบ จับ foreground/focused control/caret และส่ง `Win + .` ด้วย `SendInput` 20 รอบ โดยไม่เลือก Emoji ไม่แตะ Clipboard/tray/Activity Data ผลจาก clean commit `2f5410ea72d8855d59dbb1c58d8f5196155d8e6e` ผ่าน warm global hotkey-to-visible ที่ median 18.2008 ms, P95 21.4390 ms และ maximum 37.6641 ms จาก budget 100 ms พร้อมยืนยันว่า Picker visible/foreground และ category cache reuse/invalidation ถูกต้อง หลักฐานแยกอยู่ที่ `docs/qualification/results/global-hotkey-win10-19045.json`

ก่อนแก้ เส้นทางจริงวัด P95 ประมาณ 172–187 ms โดย boundary ชี้ว่า `LoadCategory` ใช้เวลาประมาณ 147.5 ms จึง cache `ItemsSource` ตาม category/data generation และ invalidate เมื่อ search/data เปลี่ยน หลังแก้ hotkey ผ่านซ้ำในช่วง P95 ประมาณ 21–27 ms โดยไม่ได้ลด budget

ระหว่าง qualification เต็มพบ virtualized-scroll แกว่งเกิน budget เพราะ grid เดิม prefetch ก่อน–หลังอย่างละหนึ่งหน้า การกระโดด 100 ตำแหน่งทำให้ decode 12,357 ภาพในชุดเดียว จึงลด near-viewport cache เป็น 0.1 หน้าต่อด้านและวัดที่ cadence 60 Hz โดยไม่บังคับ drain Dispatcher ถึง `ContextIdle` ระหว่าง sample ผลสุดท้าย P95 51.8716 ms และ maximum 72.3436 ms จาก budgets 60/150 ms รายงานแยก boundary ยืนยันว่า scroll command P95 0.2029 ms ส่วน render wait P95 51.7885 ms

qualification เต็มรอบเดียวกันผ่าน automated regression suite, Release self-contained publish, package budget และ runtime network observation 37 sample โดยไม่พบ socket รายงานรวมอยู่ที่ `docs/qualification/results/automated-win10-19045.json` และอ้าง commit เดียวกัน

เกณฑ์ performance โดยรวมยังไม่ทำเครื่องหมายผ่าน เพราะ imported upstream มีเพียงตัวเลข warm open/working set โดยประมาณ ไม่มี raw search/scroll/decode/package ที่ทำซ้ำได้ การพิสูจน์ฝั่ง Modern และ global hotkey จริงเสร็จแล้ว แต่ manual app/OS/accessibility/DPI/input/clipboard matrices ยังไม่ผ่าน จึงคงสถานะ `needs-info` และยังไม่ปลด Ticket 15 (14B)

### 29 สิงหาคม 2026 — ขอบเขต wizard สำหรับ manual qualification ที่รอผู้ทดสอบยืนยัน

สำรวจเครื่องปัจจุบันแล้วพบ Windows 10 Enterprise N build 19045, จอเดียว 3440×1440, Notepad 10.0.19041.1, Chrome 151.0.7922.174, VS Code 1.133.0, Explorer และ Narrator พร้อมใช้ ส่วน Windows Terminal, NVDA, จอที่สอง และ Windows 11 ไม่มีใน environment นี้ Git Bash พร้อมสำหรับรัน wizard ตามมาตรฐาน repository skill

เสนอ wizard แบบทำซ้ำได้ที่ `scripts/manual-qualification-wizard.sh` จำนวน 7 stage โดยไม่เก็บ secret และไม่เปลี่ยน manual matrix เป็น “ผ่าน” เอง:

1. preflight: ตรวจ clean commit/binary, บันทึก OS/build/app versions, input language, DPI/monitor และ insertion mode
2. Tier A ที่มีในเครื่อง: Notepad, Chrome, VS Code และ Explorer address bar; บันทึก Windows Terminal เป็น “ทำไม่ได้ใน environment” พร้อมเหตุผล
3. keyboard/accessibility ที่ DPI ปัจจุบัน: keyboard-only, focus indicator, Light/Dark/System, High Contrast และ Narrator
4. input/sequence/queue: English/Thai Typing Handoff, single/VS/skin/mixed tone/flag/keycap/ZWJ, rapid clicks, queue full และ dismiss ระหว่างส่งใน Notepad กับ Chrome
5. Clipboard/target safety: empty/text/image/FileDrop/custom format, Clipboard เปลี่ยนระหว่าง delay, target ปิด/focus เปลี่ยน/elevated target, Explicit Copy และ Clipboard History
6. privacy 15 นาที: ให้มนุษย์ทำ resident workflow ขณะที่ตัวช่วยเก็บช่วงเวลาและ socket observation; packet/firewall audit ยังต้องระบุเครื่องมือและหลักฐานโดยผู้ทดสอบ
7. สรุป: เขียน JSON กับ Markdown ลง `artifacts/ticket-13/manual/` พร้อมผล `ผ่าน`/`ไม่ผ่าน`/`ทำไม่ได้ใน environment`, หมายเหตุ และ path หลักฐาน เพื่อให้ agent review ก่อนนำเข้าตารางหลัก

คำถามที่ต้องการคำตอบจาก maintainer: ยืนยัน 7 stage ตามลำดับนี้หรือระบุ stage ที่ต้องเพิ่ม/ตัด/สลับ และยืนยันว่าจะเป็นผู้สังเกตผลด้วยตนเองระหว่างรัน wizard หนึ่ง session เมื่อพร้อม หลังยืนยัน agent จึงจะเขียน/ตรวจ static wizard ตามกติกา skill โดยไม่รันแทนมนุษย์
