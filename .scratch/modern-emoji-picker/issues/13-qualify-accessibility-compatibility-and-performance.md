# 13: รับรอง accessibility, compatibility และ performance ของ Picker MVP

**What to build:** พิสูจน์ด้วย automated tests, manual matrices และตัวเลข benchmark ว่า Picker workflow หลักทำงานบนแพลตฟอร์มเป้าหมาย เข้าถึงได้ และไม่ถดถอยด้าน performance โดยไม่มี runtime network

**Blocked by:** 06: ค้นหา Emoji ไทย–อังกฤษและดู Hover Preview; 10: รักษาลำดับการส่งและทำ Typing Handoff โดยไม่ทำ input หาย; 12: รวม Settings, Welcome, ภาษา และการควบคุมความเป็นส่วนตัว

**Status:** ready-for-agent

- [ ] automated suite ครอบคลุม generator, search tiers, ranking, variants, Recent, persistence recovery, queue, validation, insertion modes, clipboard rules และ release preconditions ตาม Test Strategy
- [ ] Manual Tier A ผ่านบน Notepad, Chrome, VS Code, Windows Terminal และ Explorer address bar บน Windows 10 22H2 x64
- [ ] Windows 11 smoke test ผ่าน และ Tier B ถูกทดสอบเมื่อ environment พร้อมโดยบันทึกข้อจำกัด RDP/Citrix แบบ best-effort
- [ ] DPI 100–250%, multi-monitor ต่าง DPI, keyboard navigation, focus indicator, High Contrast และ accessible name/state ผ่าน matrix ที่บันทึกผลได้
- [ ] Thai IME, English keyboard, single code point, variation selector, skin/mixed tone, flags, keycaps, ZWJ family และ rapid clicks ผ่าน workflow ที่เกี่ยวข้อง
- [ ] clipboard ว่าง, text, image, files และ custom formats รวมถึง target ปิด, focus เปลี่ยน และ elevated target ผ่าน safety matrix
- [ ] มีตัวเลข upstream/Modern สำหรับ warm hotkey-to-visible, search latency, scroll stalls, working set, decode/cache และ package sizes พร้อม performance budgets ที่ตรวจผ่าน
- [ ] runtime verification ยืนยันว่าไม่มี update polling, telemetry, analytics, cloud sync หรือ remote font/asset calls
