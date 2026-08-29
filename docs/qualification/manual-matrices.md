# Manual qualification matrices

ทุกแถวเริ่มที่ **ยังไม่ทดสอบ** ผู้ทดสอบต้องบันทึกวันเวลา, Windows build, app version, architecture, input language, DPI/monitor, insertion mode และหลักฐานที่ตรวจย้อนกลับได้ ห้ามใช้ automated smoke หรือ source inspection แทนผล manual

ค่า Result ที่ใช้ได้: `ผ่าน`, `ไม่ผ่าน`, `ยังไม่ทดสอบ`, `ทำไม่ได้ใน environment` หากไม่ผ่านให้เปิด issue พร้อมขั้นตอนทำซ้ำและลิงก์ในช่องหลักฐาน

## Matrix A — แอปเป้าหมายและระบบปฏิบัติการ

Workflow ต่อแถว: เปิดด้วย hotkey จาก text control จริง, ใน Browse ตรวจ physical-key handoff/keyboard layout, คลิก single/complex sequence และ rapid multi-insert โดย Picker ไม่กระพริบ จากนั้นคลิก Search เพื่อทดสอบไทย/อังกฤษ, Enter, Shift+Enter, Esc และตรวจว่า focus กลับถูก app/control กับ input แรกไม่หาย

| Tier | OS / session | Target | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|---|---|
| A | Windows 10 22H2 x64 | Notepad | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | workflow หลักผ่าน |
| A | Windows 10 22H2 x64 | Chrome | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | retest หลังแก้ caret placement ผ่าน |
| A | Windows 10 22H2 x64 | VS Code | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | retest หลังแก้ caret placement ผ่าน |
| A | Windows 10 22H2 x64 | Windows Terminal | ยังไม่ทดสอบ | — | — | — |
| A | Windows 10 22H2 x64 | Explorer address bar | ไม่ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) / Ticket 13 | lose focus แล้ว control กลับเป็น breadcrumb จึงพิมพ์ต่อไม่ได้ |
| smoke | Windows 11 x64 | Notepad + Chrome | ยังไม่ทดสอบ | — | — | — |
| B | เมื่อพร้อม | Discord | ยังไม่ทดสอบ | — | — | best-effort |
| B | เมื่อพร้อม | Slack | ยังไม่ทดสอบ | — | — | best-effort |
| B | เมื่อพร้อม | Instagram Web | ยังไม่ทดสอบ | — | — | best-effort |
| B | เมื่อพร้อม | PowerShell | ยังไม่ทดสอบ | — | — | best-effort |
| B | RDP | Notepad/target ที่มี | ยังไม่ทดสอบ | — | — | บันทึก clipboard delay |
| B | Citrix | target ที่มี | ยังไม่ทดสอบ | — | — | บันทึกข้อจำกัด policy |

## Matrix B — DPI, monitor และ accessibility

ทดสอบว่า Browse ส่ง Tab/arrow/Enter กลับ target และทดสอบ keyboard navigation ภายใน Search ด้วย arrow/Enter/Shift+Enter/Esc ตรวจ focus indicator ด้วยสายตา และใช้ Narrator หรือ NVDA อ่านชื่อ tile, selection, busy, queue full และ error state

| Environment / case | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|
| DPI 100% จอเดียว | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | 3440×1440, 96 DPI |
| DPI 125% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 150% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 175% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 200% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 225% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 250% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| สองจอต่าง DPI: เปิด/ย้าย/เปิดซ้ำทั้งสองทิศ | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | มีจอเดียว |
| keyboard-only workflow | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | Tier A ผ่าน; Codex Shift+Enter ยังมี compatibility issue |
| focus indicator ทุก interactive control | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ตรวจ Light/Dark/System |
| Windows High Contrast | ไม่ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) / Ticket 13 | Search Enter/Shift+Enter ใช้ไม่ได้; System theme เลือก Light ผิด |
| Narrator: name/selection/busy/error | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | อ่านสถานะปกติได้; rapid-click corruption แยกใน Matrix C |
| NVDA: name/selection/busy/error | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ไม่มี NVDA |

## Matrix C — input, sequence และ queue

ทดสอบอย่างน้อยใน Notepad และอีกหนึ่ง Chromium/Electron target ใช้ทั้ง Hybrid, Keystroke only และ Paste always เมื่อกรณีนั้นเกี่ยวข้อง ตรวจ Unicode sequence ที่รับจริง ไม่ใช้เพียงภาพ glyph เป็นเกณฑ์

| Input / sequence | ตัวอย่าง | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|---|
| English keyboard | ค้น `heart` แล้วพิมพ์ต่อทันที | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | — |
| Thai IME | ค้น `หัวใจ` และ Typing Handoff ตัวแรก | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | per-app Thai layout ผ่าน |
| dead key | layout ที่มี dead key แล้ว commit ตัวแรก | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | — |
| single code point | 😀 | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ตรวจ code points |
| variation selector | ❤️ | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | `U+2764 U+FE0F` |
| skin tone | 🙋🏿 | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | global tone |
| mixed tone | 🫱🏻‍🫲🏿 | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | one-shot override; Win10 อาจแสดง tofu แต่ code points ถูกต้อง |
| flag | 🇹🇭 | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | `U+1F1F9 U+1F1ED` |
| keycap | 1️⃣ | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | `U+0031 U+FE0F U+20E3` |
| ZWJ family | 👨‍👩‍👧 | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | sequence ครบ |
| rapid clicks ≤ 20 pending | เลือกลำดับต่างกัน ≥ 10 ตัว | ไม่ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) / Ticket 13 | พบ isolated `U+D83D`/`�`, แถบแดงกะพริบ และบางครั้ง UI ค้างจนต้องปิด Picker |
| queue full | active + 20 pending แล้วคลิกเพิ่ม | ยังไม่ทดสอบ | — | — | ต้องไม่ drop เงียบ |
| dismiss ระหว่างส่ง | active + pending แล้ว Esc | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | active จบ, pending cancel |

## Matrix D — Clipboard และ target safety

ก่อนแต่ละ Temporary Paste ให้เตรียม Clipboard ตามชนิด ระหว่าง delay ให้ทดสอบทั้งไม่เปลี่ยนและเปลี่ยน Clipboard จาก process อื่น ตรวจด้วย format enumeration/แอปต้นทาง ไม่ใช่เฉพาะ Ctrl+V

| Clipboard / target case | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|
| Clipboard ว่าง | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | restore กลับว่าง |
| Unicode text | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | content เดิมกลับครบ |
| image | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | image format/content เดิมกลับ |
| files (`FileDrop`) | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | FileDrop เดิมกลับ |
| custom/private format | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | Chromium internal source formats กลับ |
| Clipboard เปลี่ยนระหว่าง delay | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ไม่ restore ทับข้อมูลใหม่ |
| target ปิดก่อน validation | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | abort โดยไม่ retarget |
| focus เปลี่ยนไป window อื่น | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | การลองครั้งนี้ปิด Picker/Notepad จึงไม่ใช่ repro ที่ใช้ตัดสินได้ |
| elevated target จาก non-elevated Picker | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | target เปิด Windows 10 Picker เดิมแทน Modern |
| Explicit Copy | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | เข้า failure UI ของ Modern ไม่ได้ |
| Windows Clipboard History เปิด | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | เปิด History แล้ว แต่ไม่มีหลักฐานสรุปผล Temporary Paste/Explicit Copy ครบ |
| Clipboard manager ภายนอก | ทำไม่ได้ใน environment | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ไม่มี Clipboard manager ภายนอกติดตั้ง |

## Matrix E — privacy/runtime network แบบ manual

รัน resident process อย่างน้อย 15 นาที เปิด/ค้น/preview/Settings/insert หลายครั้ง พร้อม packet capture หรือ firewall audit ที่บันทึก filter และเวลา ตรวจ Task Scheduler/autoruns เฉพาะ identity ของ Modern และตรวจ log เมื่อปิด/เปิด diagnostic logging

| Case | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|
| ไม่มี outbound DNS/TCP/UDP ระหว่าง resident workflow | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | 15 นาที, 1,157 samples, 0 sockets; ไม่ใช่ packet capture |
| ไม่มี update polling/telemetry/analytics/cloud sync | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | socket observation ร่วมกับ automated source gate |
| ไม่มี remote font/asset call | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | browse/preview ใช้ asset ในเครื่องและไม่พบ socket |
| diagnostic logging ปิด: ไม่มี log content | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ไม่มีรายการใหม่ก่อนเปิด logging เวลา 19:04:45 |
| diagnostic logging เปิด: ไม่มี query/emoji/clipboard/target title | ผ่าน | 2026-08-29 / June | [ผล manual รอบ 2026-08-29](./results/manual-win10-19045-20260829.md) | ตรวจ sanitized log แล้วพบเฉพาะ metadata |
