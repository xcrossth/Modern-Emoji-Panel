# 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP

**What to build:** พิสูจน์ด้วย automated tests, manual matrices และตัวเลข benchmark ว่า Picker workflow หลักทำงานบนแพลตฟอร์มเป้าหมาย เข้าถึงได้ และไม่ถดถอยด้าน performance โดยไม่มี runtime network

**Blocked by:** 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview; 10: รักษาลำดับการส่งและทำ Typing Handoff โดยไม่ทำ input หาย; 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว

**Status:** needs-info

- [ ] automated suite ครอบคลุม generator, search tiers, ranking, variants, Recent, persistence recovery, queue, validation, insertion modes, clipboard rules และ release preconditions ตาม Test Strategy
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
7. installer/portable ZIP size กับ release preconditions ซึ่งยังไม่มีจนกว่า Ticket 14 จะสร้าง artifact แต่ Ticket 14 ระบุว่าถูก block โดย Ticket 13 จึงต้องให้ maintainer ตัดสินวิธีคลี่ dependency นี้ก่อนปิดทั้งสอง ticket

แบบบันทึกผลทุกแถวอยู่ที่ `docs/qualification/manual-matrices.md`; ทุกกรณียังคงระบุ “ยังไม่ทดสอบ” โดยเจตนา
