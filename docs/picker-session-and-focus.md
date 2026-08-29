# Picker Session, focus และการวางหน้าต่าง

Picker เปิดทุกครั้งใน Browse Mode โดย query ว่างและใช้ pointer เลื่อน/คลิก Emoji ทุก physical key ที่ไม่ใช่ modifier รวม Space, Enter, Tab, ลูกศร และ shortcut chord จะเริ่ม Typing Handoff กลับไปยังแอปเป้าหมาย ส่วน Esc dismiss และการคลิกช่องค้นหาจะเข้าสู่ Search Mode

## Commit Gesture

- คลิก Emoji ใน Browse/Search: ส่งแล้วเปิด Picker Session เดิมต่อโดยไม่ซ่อนหน้าต่าง
- Enter ใน Search: ส่งแล้ว dismiss และคง focus ที่แอปเป้าหมาย
- Shift+Enter ใน Search: ส่งแล้วเปิด Picker Session เดิมต่อโดยไม่ซ่อนหน้าต่าง

ระหว่าง pointer/Shift+Enter insertion Picker ยังคง visible ขณะที่ focus สลับไปยังแอปเป้าหมายชั่วคราว จึงไม่เกิดภาพดับ–ติด เมื่อคิวจบ Picker กลับมา active พร้อม selection, query, หมวดหมู่ และตำแหน่ง scroll เดิม หากส่งไม่สำเร็จ Picker จะแสดง session เดิมพร้อมข้อความผิดพลาดและปุ่ม Explicit Copy

ขณะที่ Insertion Queue กำลังส่ง Picker ยังรับ pointer click เพื่อเพิ่มงานต่อได้ แต่ใช้หน้าต่างแบบไม่ activate ชั่วคราวเพื่อไม่แย่ง foreground กลับจากแอปเป้าหมายระหว่างการตรวจ target และส่งงานในคิว เมื่อคิวจบจึงคืน activation style และ Picker Session ตามเดิม regression อัตโนมัติยืนยันว่าแนวทางนี้ส่ง Unicode sequence ครบโดยไม่ขึ้น error และ UI ยังตอบสนอง ส่วนผลกับ Chrome จริงต้องยืนยันด้วยการทดสอบบนเครื่อง

ก่อนเปิด Picker ระบบจับทั้ง native focus HWND และ UI Automation focused element หาก target ใช้ editor แบบไม่มี child HWND เช่น Chrome omnibox, Chrome New Tab search หรือ Explorer address bar การส่งและ explicit dismiss จะคืน accessibility focus element เดิมก่อน inject ส่วน classic edit control เช่น Notepad ยังคงใช้ native HWND path ที่เร็วกว่า

## Esc และการ dismiss

Esc ใน Search Mode ครั้งแรกจะล้าง query และกลับ Browse Mode ส่วน Esc ใน Browse Mode จะ dismiss ทันที การกด Esc หรือปุ่มปิดจะคืน focus ไปยังแอปเป้าหมายที่ capture ไว้

เมื่อผู้ใช้คลิกหน้าต่างอื่นจริง WPF ส่งเหตุการณ์ deactivation และ Picker จะ dismiss โดยไม่ activate แอปเป้าหมายเดิมซ้ำ หน้าต่างที่ผู้ใช้คลิกจึงคง focus ตามเจตนา

## ตำแหน่งและขนาด

Picker ใช้พิกัด text caret ที่ capture ตอนกด hotkey เป็นจุดวางหลัก หากแอปไม่เปิดเผย caret จะจัด Picker กึ่งกลางหน้าต่างเป้าหมายบน monitor เดียวกัน จากนั้น clamp ขอบทั้งหมดไว้ใน working area ของ monitor นั้นโดยคำนวณตาม DPI

หน้าต่างปรับขนาดได้ตั้งแต่ 320×360 ถึง 900×900 DIP และบันทึกขนาดไว้ใน `settings.json` เมื่อ dismiss โดยไม่จำตำแหน่ง, query หรือหมวดหมู่จาก session ก่อน

## Accessibility

Emoji tile มี accessible name จากชื่อที่แปลตาม locale และมีเส้น focus ที่มองเห็นได้ สถานะ Browse/Search, selection, busy, ผลการส่ง และ error ประกาศผ่าน UI Automation live region และ ItemStatus

## การตรวจสอบ

รันตัวตรวจ session policy แบบไม่ส่ง input จริงด้วย:

```powershell
.\scripts\verify-picker-session.ps1
```

และรัน queue/focus regression ที่ใช้ WPF target จริงและส่ง Unicode ทดสอบภายใน process แยกด้วย:

```powershell
.\scripts\verify-insertion-queue.ps1
```

ตัวตรวจครอบคลุม transition ของ Browse/Search, Esc สองจังหวะ, Commit Gesture, นโยบายคง visibility ระหว่าง multi-insert, accessibility/native focus restoration, rapid pointer insertion 15 รายการ, placement ทั้ง monitor พิกัดบวกและลบ, การบันทึกขนาด และ wiring ของ WPF/accessibility
