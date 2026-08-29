# Performance budgets

ตัวเลขต่อไปนี้เป็น gate ของ Modern Emoji Picker ที่รันแบบ Release, self-contained, win-x64 บนเครื่อง qualification หลัง prewarm ค่า P95 ใช้ nearest-rank percentile จาก sample ใน JSON report

| Metric | Budget | จำนวน sample | ความหมาย |
|---|---:|---:|---|
| warm global hotkey-to-visible P95 | ≤ 100 ms | 20 | ส่ง Win + . ผ่าน `SendInput` ไปยัง low-level hook จริง, จับ foreground/focused control/caret, เปิดและ activate Picker แล้วรอ Dispatcher priority Render |
| warm open-to-render proxy P95 | ≤ 100 ms | 20 | reset view, show WPF shell และรอ Dispatcher priority Render; ไม่รวม global hook/foreground activation |
| bilingual search P95 | ≤ 10 ms | 1,000 | query ไทย/อังกฤษสลับกันบน index ที่ prewarm แล้ว |
| virtualized scroll P95 | ≤ 60 ms | 100 | เลื่อนหมวดที่ใหญ่ที่สุดไปยังตำแหน่งกระจายแล้วรอ Render priority; เทียบเท่า guardrail ประมาณ 16 FPS สำหรับการกระโดดข้าม viewport |
| virtualized scroll maximum | ≤ 150 ms | 100 | guardrail สำหรับ stall เดี่ยวในชุดเดียวกัน |
| idle working set หลัง trim | ≤ 128 MiB | 1 | process sample หลังซ่อน window, trim และ ContextIdle |
| grid PNG decode P95 | ≤ 15 ms | 128 | Noto 128 role ที่ decode width 47 px เพื่อหลีก cache จาก grid ปกติ |
| image-cache hit P95 | ≤ 2 ms | 128 | โหลด key เดิมรอบสอง |
| bounded image cache | ≤ 256 ภาพ | หลัง benchmark | จำนวน entry ใน LRU cache |
| self-contained publish | ≤ 350 MiB | 1 directory | ไฟล์ทั้งหมดใน `artifacts/foundation/picker-win-x64` |

global-hotkey metric ใช้ Notepad ที่สคริปต์เปิดเองเป็น foreground target และไม่เลือก/ส่ง Emoji จึงพิสูจน์เส้นทาง hook-to-visible แต่ไม่แทน manual app matrix ส่วน budgets ทั้งหมดตั้งใจจับ regression ที่มีผลต่อการใช้งาน ไม่ใช่คำรับรองว่า GPU/จอทุกแบบจะให้ frame time เดียวกัน ถ้าเปลี่ยนเครื่องมือวัด จำนวน sample หรือเส้นทาง code ต้องเพิ่ม schema version และอธิบายใน report ห้ามเลื่อน budget เพื่อให้ผลที่ล้มผ่านโดยไม่มีเหตุผลและ review

## Upstream comparison ที่มีอยู่

ประวัติ Classic ที่สรุปไว้ใน `docs/upstream/classic-picker.md` รายงาน open speed โดยประมาณ 35–40 ms และ idle working set ประมาณ 20 MiB แต่ไม่มี raw samples, machine metadata หรือตัวเลข search, scroll, decode/cache และ package size ที่ทำซ้ำได้ ดังนั้น JSON เก็บตัวเลขเหล่านี้เป็น **reported approximate baseline** เท่านั้น ไม่ใช้เป็น gate เชิงสถิติ และบันทึก metric ที่ขาดเป็น `null`

การเทียบ 128/512 รายภาพที่ทำก่อนหน้านี้อยู่ใน `docs/research/asset-visual-spike/` และไม่ถูกอ้างว่าเป็น virtualized-scroll benchmark
