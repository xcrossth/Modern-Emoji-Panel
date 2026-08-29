# 10: รักษาลำดับการส่งและทำ Typing Handoff โดยไม่ทำ input หาย

**What to build:** รองรับการเลือก Emoji ต่อเนื่องอย่างรวดเร็วด้วย Insertion Queue และคืนการพิมพ์ให้แอปเป้าหมายเมื่อผู้ใช้เริ่มพิมพ์ต่อ โดยไม่ทำ input แรกหรือองค์ประกอบของ IME สูญหาย

**Blocked by:** 09: ควบคุม Picker Session ด้วย keyboard, pointer และ focus

**Status:** resolved

- [x] Insertion Queue รับงานรอสูงสุด 20 รายการและรักษา click order ให้ตรงกับ insertion order โดยไม่ส่งขนาน
- [x] accessibility state ประกาศ pending/busy โดยไม่ทำให้ข้อความชั่วคราวกระพริบใน UI และเมื่อ queue เต็ม UI จะหยุดรับชั่วคราวพร้อมแสดงสถานะโดยไม่ drop click แบบเงียบ
- [x] เมื่อ dismiss ระบบหยุดรับงานใหม่ ปล่อยเฉพาะ active operation ให้จบ และยกเลิกงานที่ยังไม่เริ่มก่อนปิด Picker
- [x] ทุก physical key ที่ไม่ใช่ modifier ใน Browse Mode รวม Space/Enter/ลูกศร/shortcut เริ่ม Typing Handoff แทนการควบคุม Picker
- [x] Typing Handoff เก็บ input แรกไว้อย่างปลอดภัยระหว่างรอ active operation และส่งต่อไปยังแอปเป้าหมายเดิมหลัง validation
- [x] Thai IME, dead keys และ shortcuts ที่อยู่ในขอบเขตทดสอบไม่ถูกกลืน, ทำซ้ำ หรือ replay ด้วยวิธีที่ยังไม่ผ่านการพิสูจน์
- [x] queue order, capacity, cancellation และ focus transitions มี automated tests ผ่าน abstraction ที่ไม่ขึ้นกับ timing จริงของ desktop

## Answer

เพิ่ม `InsertionQueue<T>` เป็น deep module แบบ Dispatcher-confined ซึ่งแยก pending กับ active ชัดเจน รับงานรอสูงสุด 20 รายการ และมี seam เดียวสำหรับเลื่อนงานไปเป็น active จึงรักษา FIFO และห้ามการส่งขนานโดยไม่พึ่ง delay หรือ timing จริงของ desktop

WPF shell รับ click/Commit Gesture เข้า queue ก่อนเริ่ม adapter, ประกาศสถานะ pending/sending ผ่าน accessibility state โดยไม่แสดงข้อความชั่วคราวที่กระพริบใน UI และแสดงข้อความเมื่อ queue เต็มพร้อมปิด hit testing ชั่วคราว Enter ปิดรับงานใหม่แล้ว drain งานที่รับไว้ตามลำดับ ส่วน Esc, ปุ่มปิด, click ภายนอก, Tray → Exit และ Typing Handoff ยกเลิกเฉพาะ pending แล้วรอ active จบก่อนทำ terminal transition การเปลี่ยน foreground ระหว่างส่งจะไม่ทำให้ Picker แย่ง focus กลับ

Typing Handoff จับ physical virtual key กับ modifiers ก่อน WPF แปลตาม per-app keyboard layout ของ Picker แล้ว replay ให้ target ตีความด้วย layout ของตัวเอง เก็บ payload ไว้ในหน่วยความจำของ terminal intent เท่านั้นโดยไม่ log/persist/แตะ clipboard และยังใช้ committed-text fallback สำหรับ IME/dead key ที่ไม่มี physical key ให้ replay จากนั้นส่งต่อหนึ่งครั้งผ่าน Target Validation เดิมหลัง active insertion จบ หากส่งไม่ได้จะเปิด error ให้ผู้ใช้เลือก Explicit Copy เอง

หลักฐานการตรวจสอบ:

- commit implementation `4e1b7c5`
- `scripts/verify-insertion-queue.ps1`: ผ่าน 31 deterministic checks ครอบคลุม FIFO, active + 20 pending, full/stopped, การแยกสถานะ UI/accessibility, cancellation, Enter drain ใน Search, focus transition, physical key/Space/Enter/shortcut, Thai committed-text fallback, dead-key result, surrogate pair และ IME pre-edit
- `scripts/verify-picker-session.ps1 -SkipBuild`: ผ่าน 14 checks
- `scripts/verify-safe-insertion.ps1 -SkipBuild`: ผ่าน 18 checks
- `scripts/verify-foundation.ps1 -SkipPublish`: ผ่านด้วย .NET SDK 10.0.400, build 0 warnings/errors และ WPF smoke
- `scripts/test-clean-checkout.ps1 -Revision 4e1b7c5`: ผ่านทั้ง locked restore, Release build, self-contained publish, generated baseline, Noto grid, safe insertion, search/preview, variants, Picker Session และ Insertion Queue จาก detached worktree ใหม่

ข้อจำกัดการตรวจ: automated test ใช้ state seam และ committed-text policy โดยไม่ส่ง input จริง การทดสอบ rapid clicks, Thai IME/dead-key layout จริง, Tier A apps และ screen reader จะทำใน Ticket 13 ตาม manual qualification matrix; ไม่ใช้ screenshot helper ตามข้อจำกัด Windows 10 build 19045 ของ repository
