# Manual qualification matrices

ทุกแถวเริ่มที่ **ยังไม่ทดสอบ** ผู้ทดสอบต้องบันทึกวันเวลา, Windows build, app version, architecture, input language, DPI/monitor, insertion mode และหลักฐานที่ตรวจย้อนกลับได้ ห้ามใช้ automated smoke หรือ source inspection แทนผล manual

ค่า Result ที่ใช้ได้: `ผ่าน`, `ไม่ผ่าน`, `ยังไม่ทดสอบ`, `ทำไม่ได้ใน environment` หากไม่ผ่านให้เปิด issue พร้อมขั้นตอนทำซ้ำและลิงก์ในช่องหลักฐาน

## Matrix A — แอปเป้าหมายและระบบปฏิบัติการ

Workflow ต่อแถว: เปิดด้วย hotkey จาก text control จริง, ใน Browse ตรวจ physical-key handoff/keyboard layout, คลิก single/complex sequence และ rapid multi-insert โดย Picker ไม่กระพริบ จากนั้นคลิก Search เพื่อทดสอบไทย/อังกฤษ, Enter, Shift+Enter, Esc และตรวจว่า focus กลับถูก app/control กับ input แรกไม่หาย

| Tier | OS / session | Target | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|---|---|
| A | Windows 10 22H2 x64 | Notepad | ยังไม่ทดสอบ | — | — | — |
| A | Windows 10 22H2 x64 | Chrome | ยังไม่ทดสอบ | — | — | — |
| A | Windows 10 22H2 x64 | VS Code | ยังไม่ทดสอบ | — | — | — |
| A | Windows 10 22H2 x64 | Windows Terminal | ยังไม่ทดสอบ | — | — | — |
| A | Windows 10 22H2 x64 | Explorer address bar | ยังไม่ทดสอบ | — | — | — |
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
| DPI 100% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 125% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 150% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 175% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 200% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 225% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| DPI 250% จอเดียว | ยังไม่ทดสอบ | — | — | — |
| สองจอต่าง DPI: เปิด/ย้าย/เปิดซ้ำทั้งสองทิศ | ยังไม่ทดสอบ | — | — | บันทึก DPI ของแต่ละจอ |
| keyboard-only workflow | ยังไม่ทดสอบ | — | — | mouse ไม่ใช้งานระหว่างกรณี |
| focus indicator ทุก interactive control | ยังไม่ทดสอบ | — | — | ตรวจ Light/Dark/System |
| Windows High Contrast | ยังไม่ทดสอบ | — | — | บันทึก scheme ที่ใช้ |
| Narrator: name/selection/busy/error | ยังไม่ทดสอบ | — | — | ระบุ screen reader version |
| NVDA: name/selection/busy/error | ยังไม่ทดสอบ | — | — | เมื่อ environment พร้อม |

## Matrix C — input, sequence และ queue

ทดสอบอย่างน้อยใน Notepad และอีกหนึ่ง Chromium/Electron target ใช้ทั้ง Hybrid, Keystroke only และ Paste always เมื่อกรณีนั้นเกี่ยวข้อง ตรวจ Unicode sequence ที่รับจริง ไม่ใช้เพียงภาพ glyph เป็นเกณฑ์

| Input / sequence | ตัวอย่าง | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|---|
| English keyboard | ค้น `heart` แล้วพิมพ์ต่อทันที | ยังไม่ทดสอบ | — | — | — |
| Thai IME | ค้น `หัวใจ` และ Typing Handoff ตัวแรก | ยังไม่ทดสอบ | — | — | บันทึก IME/layout |
| dead key | layout ที่มี dead key แล้ว commit ตัวแรก | ยังไม่ทดสอบ | — | — | ห้าม raw replay |
| single code point | 😀 | ยังไม่ทดสอบ | — | — | — |
| variation selector | ❤️ | ยังไม่ทดสอบ | — | — | ตรวจ code units |
| skin tone | 🙋🏿 | ยังไม่ทดสอบ | — | — | global tone |
| mixed tone | 🫱🏻‍🫲🏿 | ยังไม่ทดสอบ | — | — | one-shot override |
| flag | 🇹🇭 | ยังไม่ทดสอบ | — | — | — |
| keycap | 1️⃣ | ยังไม่ทดสอบ | — | — | — |
| ZWJ family | 👨‍👩‍👧 | ยังไม่ทดสอบ | — | — | — |
| rapid clicks ≤ 20 pending | เลือกลำดับต่างกัน ≥ 10 ตัว | ยังไม่ทดสอบ | — | — | ตรวจลำดับปลายทาง |
| queue full | active + 20 pending แล้วคลิกเพิ่ม | ยังไม่ทดสอบ | — | — | ต้องไม่ drop เงียบ |
| dismiss ระหว่างส่ง | active + pending แล้ว Esc | ยังไม่ทดสอบ | — | — | active จบ, pending cancel |

## Matrix D — Clipboard และ target safety

ก่อนแต่ละ Temporary Paste ให้เตรียม Clipboard ตามชนิด ระหว่าง delay ให้ทดสอบทั้งไม่เปลี่ยนและเปลี่ยน Clipboard จาก process อื่น ตรวจด้วย format enumeration/แอปต้นทาง ไม่ใช่เฉพาะ Ctrl+V

| Clipboard / target case | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|
| Clipboard ว่าง | ยังไม่ทดสอบ | — | — | restore กลับว่างเมื่อทำได้ |
| Unicode text | ยังไม่ทดสอบ | — | — | content เดิมต้องกลับ |
| image | ยังไม่ทดสอบ | — | — | บันทึก format list/hash |
| files (`FileDrop`) | ยังไม่ทดสอบ | — | — | บันทึก path ชุดทดสอบ |
| custom/private format | ยังไม่ทดสอบ | — | — | best-effort; ระบุ format |
| Clipboard เปลี่ยนระหว่าง delay | ยังไม่ทดสอบ | — | — | ห้าม restore ทับของใหม่ |
| target ปิดก่อน validation | ยังไม่ทดสอบ | — | — | ต้อง abort/reopen Picker |
| focus เปลี่ยนไป window อื่น | ยังไม่ทดสอบ | — | — | ห้าม retarget |
| elevated target จาก non-elevated Picker | ยังไม่ทดสอบ | — | — | ต้อง abort |
| Explicit Copy | ยังไม่ทดสอบ | — | — | ต้องเข้า history ตามปกติ |
| Windows Clipboard History เปิด | ยังไม่ทดสอบ | — | — | Temporary Paste ต้องใส่ exclusion marker |
| Clipboard manager ภายนอก | ยังไม่ทดสอบ | — | — | best-effort; ระบุชื่อ/version |

## Matrix E — privacy/runtime network แบบ manual

รัน resident process อย่างน้อย 15 นาที เปิด/ค้น/preview/Settings/insert หลายครั้ง พร้อม packet capture หรือ firewall audit ที่บันทึก filter และเวลา ตรวจ Task Scheduler/autoruns เฉพาะ identity ของ Modern และตรวจ log เมื่อปิด/เปิด diagnostic logging

| Case | Result | วันที่/ผู้ทดสอบ | หลักฐาน/issue | หมายเหตุ |
|---|---|---|---|---|
| ไม่มี outbound DNS/TCP/UDP ระหว่าง resident workflow | ยังไม่ทดสอบ | — | — | ระบุเครื่องมือ/filter |
| ไม่มี update polling/telemetry/analytics/cloud sync | ยังไม่ทดสอบ | — | — | ตรวจร่วมกับ source gate |
| ไม่มี remote font/asset call | ยังไม่ทดสอบ | — | — | ปิด network แล้วยัง browse/preview ได้ |
| diagnostic logging ปิด: ไม่มี log content | ยังไม่ทดสอบ | — | — | — |
| diagnostic logging เปิด: ไม่มี query/emoji/clipboard/target title | ยังไม่ทดสอบ | — | — | แนบ sanitized log |
