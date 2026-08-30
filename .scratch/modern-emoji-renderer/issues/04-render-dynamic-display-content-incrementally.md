# 04: Render Dynamic Display Content แบบ Incremental

**What to build:** ให้ Display Content ที่เพิ่มหรือเปลี่ยนหลัง page load รวมถึงการเปลี่ยน route หรือห้องสนทนาใน SPA ถูก render โดยประมวลผลเฉพาะ subtree ที่เปลี่ยน ไม่สร้าง observer/wrapper ซ้ำ และไม่ทำให้หน้าแชทสะดุดอย่างเห็นได้ชัด

**Blocked by:** 03: Render Emoji Baseline ใน Static Display Content โดยรักษาข้อความเดิม

**Status:** resolved

- [x] Initial scan ประมวลผลเฉพาะ text nodes ที่เข้าเกณฑ์และยังรักษา Editable Content safety
- [x] MutationObserver รวม added/changed roots เป็น batch และไม่ full-scan ทั้ง document ทุก mutation
- [x] งานขนาดใหญ่ถูกแบ่งช่วงผ่าน idle scheduling พร้อม fallback ที่ทำงานได้ใน Chrome
- [x] Dynamic fixture ยืนยันข้อความใหม่, ข้อความที่แก้, ประวัติที่โหลดย้อนหลัง และการเปลี่ยน route/ห้องถูก render
- [x] Navigation ซ้ำไม่สร้าง duplicate observer, duplicate wrapper หรือโหลด settings ใหม่โดยไม่จำเป็น
- [x] Debug mode ที่ปิดโดยค่าเริ่มต้นรายงานจำนวน nodes, wrappers, batches, processing time และ skipped editable nodes ได้
- [x] Automated stress test ครอบคลุม mutation burst, long transcript และ repeated navigation

## Comments

- Chrome fixture สร้าง wrappers 612 ตัวจาก initial/dynamic/edited/route/history content ใน 15 batches โดยไม่ซ้ำ และรักษา editor/code subtree
- `IncrementalRenderer.start()` เป็น idempotent, observer มีหนึ่ง instance ต่อ renderer และ settings ยังไม่อยู่ใน hot path
- Metrics เปิดรายงานผ่าน `debug: true` เท่านั้น ค่าเริ่มต้นไม่ log; หลักฐานอยู่ที่ `docs/research/renderer-dom-pipeline/results/report.json`
