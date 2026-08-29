# SPEC 01 — Modern Emoji Picker

สถานะ: ยืนยัน shared understanding แล้วเมื่อ 28 สิงหาคม 2026

เอกสารนี้เป็นแหล่งความจริงสำหรับ Picker MVP และแทนข้อกำหนดเดิมของ Classic Emoji Picker fork เมื่อข้อความเดิมขัดกับเอกสารนี้ ให้ใช้เอกสารนี้

## 1. เป้าหมายผลิตภัณฑ์

Modern Emoji Picker เป็นแอป Windows สำหรับค้นหาและแทรก Unicode Emoji รุ่นใหม่ แม้ Windows 10 จะไม่มี glyph สำหรับ Emoji นั้น

ผลิตภัณฑ์แบ่งเป็นสองส่วน:

1. Picker เป็นผลิตภัณฑ์หลักและเป็นงานที่ต้องทำให้เสร็จก่อน
2. Renderer Extension เป็น Chrome Extension แยกต่างหากสำหรับแก้การแสดง Emoji บนเว็บตาม [SPEC 02](./02-chrome-emoji-renderer-extension.md) และไม่อยู่ใน Picker MVP

ทั้งสองส่วนต้อง build, ติดตั้ง และทำงานได้อย่างอิสระ ไม่มี runtime dependency ต่อกัน แต่ใช้ Emoji Baseline เดียวกัน

## 2. ขอบเขต MVP

MVP ต้องทำให้ workflow นี้สำเร็จบน Windows 10 22H2 x64:

    เปิด Picker ด้วย Win + .
    ค้นหาหรือเลือก Emoji 17 ได้ทุก fully-qualified sequence
    เห็นภาพ Noto ชัดเจนโดยไม่พึ่ง Segoe UI Emoji
    ส่ง Unicode sequence ไปยังแอปเป้าหมายอย่างปลอดภัย
    คลิกเพื่อเลือกหลายตัวต่อเนื่องโดย Picker ไม่กระพริบ
    ใช้ Enter/Shift+Enter เลือกจาก Search Mode หรือกดปุ่มใดต่อใน Browse Mode เพื่อกลับไปยังแอปเป้าหมาย
    ปิด Picker ด้วย Esc, การเริ่มพิมพ์ต่อ หรือการคลิกภายนอก

สิ่งต่อไปนี้ไม่อยู่ใน Picker MVP:

- Renderer Extension
- cloud sync
- Favorites
- Emoji theme อื่นนอกจาก Noto
- user-supplied themes
- Emoji 18 ก่อนข้อมูลและ asset ทุกแหล่งออก stable
- code signing แบบ trusted certificate
- automatic runtime update
- telemetry หรือ automatic crash upload

## 3. Repository และ upstream

ใช้ monorepo โดยมีโครงสร้างเป้าหมาย:

    apps/
      picker/
    tools/
      emoji-baseline/
    vendor/
      noto-emoji/
        v2.051/
          png/
            128/
            512/
    tests/
    scripts/
    docs/
      adr/

นำ platima/Classic-EmojiPicker เข้า apps/picker ด้วย Git subtree โดยตรึง import แรกที่ commit:

    56c54201e0673a57710c2498db25a149b45e63ec

เก็บ upstream remote ไว้ รับ commit ใหม่ด้วย subtree pull แบบ manual เข้า branch แยก บันทึก SHA, review diff และรัน regression tests ก่อน merge ห้าม sync เข้า main อัตโนมัติหรือ pull ระหว่าง build

เมื่อย้ายเข้า monorepo ต้องปรับ path ของ build scripts, installer และ workflow ซึ่งเดิมสมมติว่า Picker อยู่ repository root

ต้องสร้าง product identity ใหม่ทั้งหมดและห้าม reuse identity ของ Classic:

- executable และ assembly name
- mutex และ named event
- registry Run value
- Inno AppId
- MSI identity เดิมต้องไม่ถูกใช้
- install directory และ uninstall identity
- artifact names, URLs, publisher และ icon

Modern ต้องไม่ import, แชร์, แก้ไข หรือลบข้อมูลของ Classic Emoji Picker

## 4. Toolchain และแพลตฟอร์ม

ใช้:

- Picker: .NET 10, target net10.0-windows
- Generator: .NET 10, target net10.0
- Architecture: win-x64
- UI: WPF
- Tray integration: Windows Forms เท่าที่ upstream ใช้อยู่

เพิ่ม global.json ล็อก SDK feature band 10.0.400 และอนุญาต patch servicing ภายใน band ใช้ central package versions และ NuGet lock files

Windows 10 22H2 x64 เป็นแพลตฟอร์มหลักที่โครงการทดสอบและรับรองเอง Windows 11 ต้องผ่าน smoke test แต่ UX บางจุดอาจต่างกัน

เอกสารผู้ใช้ต้องระบุอย่างตรงไปตรงมาว่า Windows 10 22H2 รุ่นทั่วไปไม่อยู่ใน supported-OS matrix ปัจจุบันของ .NET 10 แม้โครงการจะทดสอบบน Windows 10 build 19045 จริง

Self-contained release ต้อง rebuild และออก patch release เมื่อ .NET 10 มี security หรือ servicing update หลังผ่าน smoke tests

## 5. Emoji Baseline

MVP ล็อก baseline ไว้ที่:

- Unicode 17.0.0
- Unicode Emoji 17.0
- CLDR 48.2
- Noto Emoji v2.051
- Noto commit 8998f5dd683424a73e2314a8c1f1e359c19e8742

ห้ามใช้ URL latest, draft, GitHub main หรือข้อมูล beta ใน production build

Source lock manifest ต้องเก็บอย่างน้อย:

- source name
- version
- immutable URL
- commit เมื่อมี
- SHA-256 หรือ checksum ทางการ
- byte length
- license class

ไฟล์ Unicode ที่ generator ต้องรองรับอย่างน้อย:

- emoji-test.txt
- emoji-sequences.txt
- emoji-zwj-sequences.txt
- emoji-data.txt
- emoji-variation-sequences.txt
- GraphemeBreakProperty.txt และ GraphemeBreakTest.txt เมื่อใช้ segmentation

CLDR ต้องอ่านทั้ง annotations และ annotationsDerived ของภาษาไทยและอังกฤษ

Picker แสดง fully-qualified sequences ทั้งหมด รวม flags, keycaps, ZWJ และ variants ส่วน component ใช้เพื่อสร้าง variant UI ตามความเหมาะสม

เมื่อ baseline เปลี่ยน ให้ update generator และ artifact ของทุกผลิตภัณฑ์ใน commit เดียวกัน แต่ Picker กับ Renderer Extension release คนละเวลาได้ Activity Data ที่ยังอ้างถึง entry เดิมต้องคงอยู่ และตัดเฉพาะรายการที่ไม่มีใน baseline ใหม่

## 6. Emoji Baseline Generator

Generator เป็น .NET 10 console tool ภายใต้ tools/emoji-baseline และ output เป็น JSON/manifest ที่ไม่ผูกกับ .NET เพื่อให้ Renderer Extension ใช้ภายหลังได้

หน้าที่:

1. อ่าน source ที่ pin และตรวจ checksum
2. สร้าง canonical Unicode sequence โดยยังรักษา sequence ดั้งเดิมสำหรับ copy/insert
3. map Unicode groups และ subgroups
4. รวม CLDR short names และ keywords ภาษาไทย/อังกฤษ
5. map Noto asset key และ aliases โดยไม่อนุมานจาก sequence ตรง ๆ
6. สร้าง versioned source manifest
7. สร้าง Emoji data แบบ deterministic
8. ตรวจ duplicate sequence และ stable identifier
9. ตรวจ coverage ของ PNG 128 และ 512
10. fail หาก fully-qualified sequence ใดไม่มีข้อมูลหรือ asset ที่กำหนด
11. สร้างรายงานเพิ่ม ลบ เปลี่ยน และ asset ที่ผิดปกติ

Ordinary build และ release build ต้องทำงาน offline จากไฟล์ที่ commit แล้ว การดาวน์โหลด source ใหม่เกิดเฉพาะในคำสั่ง update baseline ที่ผู้ใช้เรียกโดยตั้งใจ

## 7. Asset และ renderer

เลิกใช้ Emoji.Wpf และ Segoe UI Emoji เป็นฐานข้อมูลหรือ primary renderer หลังระบบใหม่ทำงานครบ

ใช้ Noto PNG canonical สองขนาด:

- 128 px สำหรับ grid
- 512 px สำหรับ Hover Preview

Commit PNG โดยตรงใน Git และไม่ใช้ Git LFS เพื่อให้ build offline และไม่พึ่ง LFS quota ขนาด artwork ดิบรวมประมาณ 110 MiB

ใช้ Emoji Asset Provider เป็น seam ภายใน แต่ v1 มีเฉพาะ Noto และไม่มี theme selector

Grid:

- tile ขนาด 32 DIP
- ใช้ 128 source
- DecodePixelWidth ตาม physical pixels ของ DPI
- รองรับ DPI 100–250%
- lazy load เฉพาะ visible และ near-viewport
- bounded cache
- Freeze image หลัง decode
- ใช้ virtualization
- คง VirtualizingWrapPanel จาก upstream แล้วเปลี่ยนเฉพาะเมื่อ benchmark หรือ accessibility test ไม่ผ่าน

Hover Preview:

- แสดงทันทีเมื่อ pointer เข้า tile
- เมื่อ pointer ย้ายไป tile อื่น ให้คงการ์ดเดิมแล้วเปลี่ยนตำแหน่งและเนื้อหาแทนการปิด–เปิดใหม่
- เมื่อ pointer ออกจาก tile ให้หน่วง 150 ms ก่อนปิด และยกเลิกการปิดหากเข้า tile อื่นในช่วงดังกล่าว
- ไม่แย่ง focus
- ภาพประมาณ 160 DIP จาก 512 source
- แสดงชื่อตาม UI locale
- แสดงชื่ออังกฤษบรรทัดรองเมื่อไม่ซ้ำ
- แสดง Emoji version
- หายเมื่อ pointer ออก, กด Esc หรือเริ่ม insert
- tile ที่ focus ใช้ F1 เปิดข้อมูลเดียวกันได้

หาก coverage ไม่ครบ ให้ generator และ release fail หาก runtime อ่านภาพเฉพาะรายการไม่ได้ ให้แสดง placeholder พร้อมชื่อและยังส่ง Unicode ได้ หาก asset assembly หายทั้งชุด ให้แสดงคำแนะนำ Repair/Reinstall

Visual spike ที่ใช้ตัดสินใจอยู่ที่:

    docs/research/asset-visual-spike/

ผลหลักคือ 128 กับ 512 ให้คุณภาพ grid ใกล้กันมาก แต่ 512 decode ช้ากว่าประมาณห้าเท่า จึงใช้ 128 สำหรับ grid และ 512 สำหรับ preview

## 8. หมวดหมู่และ variants

ใช้หมวดมาตรฐาน:

- Recent
- Smileys & Emotion
- People & Body
- Animals & Nature
- Food & Drink
- Travel & Places
- Activities
- Objects
- Symbols
- Flags

สีผิวเริ่มต้น:

- เป็น global setting
- ค่าเริ่มต้นคือ neutral สีเหลือง
- ใช้กับ entry ที่รองรับ skin-tone modifier

Mixed-tone sequences:

- มี Variant Override เฉพาะรายการที่ global setting แทนไม่ได้
- override มีผลหนึ่งครั้ง
- ไม่เปลี่ยน global setting
- sequence ที่ resolve แล้วถูกบันทึกใน Recent

ทุก fully-qualified sequence ต้องเข้าถึงได้ ห้ามตัด mixed-tone, flags หรือ sequence ซับซ้อนออกจาก v1

## 9. Search และ Learned Ranking

Search ค้นด้วยชื่อและ keyword ภาษาไทยหรืออังกฤษได้ตลอด ไม่ขึ้นกับ UI locale

ลำดับ match:

1. exact short name
2. word หรือ term prefix
3. keyword match
4. substring

Learned Ranking ใช้จัดลำดับเฉพาะภายใน match tier เดียวกัน ห้ามทำให้รายการที่ match แย่กว่าแซง exact name

กติกาคะแนน:

- เพิ่มเมื่อผู้ใช้เลือกด้วย click, Enter หรือ Shift+Enter แม้ insertion ล้มเหลว
- ให้คะแนนระดับ Emoji Entry ฐาน
- skin tone และ mixed-tone override ไม่สร้างคะแนนแยก
- Emoji คนละ entry เช่น หัวใจคนละสีมีคะแนนแยก
- ใช้ frequency ร่วมกับ time decay
- half-life 90 วัน
- tie ที่เหลือใช้ลำดับ CLDR แบบ deterministic
- เก็บเฉพาะในเครื่อง

ไม่มี global popularity ranking, telemetry ranking หรือ Favorites ใน v1

## 10. Recent และ Activity Data

Recent เป็น MRU สูงสุด 50 รายการ:

- เพิ่มทันทีเมื่อผู้ใช้เลือก แม้ insertion ล้มเหลว
- เก็บ resolved Unicode sequence จริง รวม skin tone หรือ Variant Override
- รายการซ้ำย้ายขึ้นหน้า
- ไม่แสดงสถานะส่งสำเร็จหรือล้มเหลว
- เก็บข้าม session

คำสั่งข้อมูล:

- Clear Recent
- Reset learned ranking
- Clear all activity

ข้อมูลอยู่ภายใต้:

    %APPDATA%\ModernEmojiPicker

ใช้ stable identifier, versioned schema และ atomic write เพื่อรองรับ migration และ sync ในอนาคต แต่ v1 ไม่มี account, provider, network หรือ sync code

หากไฟล์อ่านไม่ได้:

1. เก็บสำเนาเป็นชื่อ .corrupt-<เวลา>
2. reset เฉพาะไฟล์นั้น
3. แจ้งผู้ใช้
4. ห้ามทำให้แอปเปิดไม่ได้

ห้าม import ข้อมูลจาก Classic Emoji Picker

Uninstaller เก็บข้อมูลผู้ใช้เป็นค่าเริ่มต้นและมี checkbox ให้ลบ Settings/Activity Data ส่วน portable มีคำสั่งล้างข้อมูลใน Settings

## 11. Picker lifecycle และการเปิด

แอปเป็น single-instance resident tray utility:

- per-user installer เปิด Start with Windows เป็นค่าเริ่มต้น
- portable ไม่เปิด autostart จนผู้ใช้สั่งจาก Settings
- การปิดหน้าต่าง Picker เป็นการ dismiss ไม่ใช่ exit
- Tray → Exit จึงหยุด process
- เปิด executable ซ้ำให้ส่งสัญญาณเปิด Picker จาก instance เดิม

ตอนเริ่ม process ให้ prewarm:

- Emoji metadata
- search index
- window shell

ห้าม decode PNG ทั้งชุดล่วงหน้า

First-run Welcome แสดงครั้งเดียวและอธิบาย:

- Win + .
- Classic Conflict
- Temporary Paste
- autostart
- ทางเข้า Settings

ไม่มี account หรือ network onboarding

หากพบ Classic Emoji Picker กำลังทำงาน Modern ต้องไม่แย่ง hotkeyหรือปิด process อื่น ให้แจ้งวิธี Exit Classic ก่อน

## 12. Hotkey, focus และ window

Global hotkey:

- ค่าเริ่มต้น Win + .
- เปลี่ยนหรือปิดได้
- เมื่อ Modern ไม่ทำงาน Windows Emoji Panel เดิมต้องทำงานตามปกติ
- เมื่อ Picker เปิดอยู่ การกด hotkey ซ้ำไม่มีผลและต้องไม่เปิด Windows panel ซ้อน

เปิด Picker ใกล้ text caret ของแอปเป้าหมาย หากหาไม่ได้ให้จัดกลางหน้าต่างเป้าหมายบน monitor เดียวกัน และ clamp ภายใน working area

หน้าต่าง:

- มีขนาดเริ่มต้นที่เหมาะสม
- ปรับขนาดได้
- จำขนาดข้าม session
- clamp ตาม monitor และ DPI
- ตามธีม Windows โดยค่าเริ่มต้น
- มี Light และ Dark override

Initial View:

- เปิด Recent เมื่อมีข้อมูล
- หาก Recent ว่าง เปิด Smileys & Emotion
- query ว่างเสมอ
- ไม่ restore query หรือ category จาก session ก่อน

## 13. Browse Mode, Search Mode และ dismissal

เปิด Picker ใน Browse Mode

Browse Mode:

- pointer ใช้เลื่อนและ click เลือก Emoji
- ทุก physical key ที่ไม่ใช่ modifier รวม Space, Enter, Tab, ลูกศร และ shortcut chord เริ่ม Typing Handoff
- Esc dismiss Picker
- คลิกช่องค้นหาเข้าสู่ Search Mode
- ห้ามใช้ keyboard selection/commit ใน Browse Mode

Search Mode:

- keyboard input ใช้ค้นหา
- arrow keys ใช้ navigation
- Enter ใช้ส่งแล้ว dismiss
- Shift+Enter ใช้ส่งและคง Picker
- Esc ครั้งแรกกลับ Browse Mode
- Esc ครั้งที่สอง dismiss Picker

Commit Gesture:

- click ใน Browse/Search: insert และคง Picker แบบ visible
- Enter ใน Search: insert แล้ว dismiss กลับแอปเป้าหมาย
- Shift+Enter ใน Search: insert และคง Picker แบบ visible

หลัง click หรือ Shift+Enter:

- คง selection ที่ Emoji เดิม
- คง query, category และ scroll
- Picker visible ตลอดและกลับมา active โดยไม่มีภาพดับ–ติด

Dismissal:

- Esc
- close button
- การเริ่ม Typing Handoff
- click ภายนอกจริง
- Tray → Exit

เมื่อ click หน้าต่างอื่น ให้เคารพ focus ของหน้าต่างที่คลิกและห้ามแย่งกลับ เมื่อ Typing Handoff หรือ Esc ให้คืน focus ไปยังแอปเป้าหมายเดิม

Typing Handoff ต้องจับ physical key ก่อน Picker แปล per-app keyboard layout แล้วให้แอปเป้าหมายตีความด้วย layout ของตัวเอง รวม Space, Enter, Thai layout และ shortcuts พร้อม committed-text fallback สำหรับ IME/dead keys ที่ไม่มี physical key ให้ replay

## 14. แอปเป้าหมายและผลการส่ง

แอปเป้าหมายคือ app/window/control ที่ active ก่อนเปิด Picker

ก่อนส่งทุกครั้ง:

1. activate target ที่ capture ไว้
2. ตรวจ foreground target ซ้ำทันทีก่อน inject
3. หาก target ไม่ตรง ปิดไปแล้ว หรือมี integrity level สูงกว่า ให้ abort
4. ห้ามเปลี่ยนไปส่ง foreground window อื่น
5. ห้าม retry อัตโนมัติ

Injection Accepted หมายถึง target validation ผ่านและ Windows API รับ input ครบ ไม่ได้ยืนยันว่าแอปเป้าหมายแสดงข้อความแล้ว

Insertion Failure:

- คง Picker ไว้
- แสดง error แบบไม่บัง UI
- ไม่ retry
- ไม่ retarget
- มีปุ่ม Explicit Copy

## 15. Insertion Mode และ clipboard

มีสามโหมด:

- Hybrid เป็นค่าเริ่มต้น
- Keystroke only
- Paste always

Hybrid:

- Emoji เดี่ยวใช้ SendInput KEYEVENTF_UNICODE
- ZWJ, flags, keycaps, skin-tone และ multi-codepoint sequence ใช้ Temporary Paste

SendInput ต้อง:

- ส่ง UTF-16 units ตามลำดับ
- ตรวจจำนวน INPUT ที่ API รับ
- ไม่ retry ทั้ง string เมื่อส่งได้เพียงบางส่วน
- ทดสอบร่วมกับ Thai และ IME อื่น

Temporary Paste:

1. snapshot clipboard formats แบบ best-effort
2. ใส่ Unicode sequence
3. ใส่ ExcludeClipboardContentFromMonitorProcessing
4. กัน Windows Clipboard History และ Cloud Clipboard
5. ส่ง Ctrl+V
6. รอตาม configurable delay
7. ตรวจ clipboard sequence number
8. restore เฉพาะเมื่อ clipboard ไม่ถูกเปลี่ยนระหว่างทาง

ข้อจำกัดที่ต้องสื่อสาร:

- ไม่รับประกันว่า target paste สำเร็จ
- ไม่รับประกัน restore ทุก private/delayed clipboard format
- ไม่รับประกัน clipboard manager ภายนอกจะเคารพ exclusion marker
- ห้าม restore ทับข้อมูลใหม่ที่ผู้ใช้หรือโปรแกรมอื่น copy ระหว่างทาง

Explicit Copy เป็นคำสั่งโดยเจตนาของผู้ใช้ จึงต้องเข้า clipboard และ Win+V ตามปกติ

pasteRestoreDelayMs อยู่ใน Advanced Settings พร้อมคำอธิบายและ Reset to default

## 16. Insertion Queue

ใช้ bounded queue:

- รับงานรอสูงสุด 20 รายการ
- click order ต้องเท่ากับ insertion order
- ประกาศ pending/sending state ผ่าน accessibility โดยไม่แสดงข้อความชั่วคราวที่กระพริบใน UI
- เมื่อเต็มให้หยุดรับชั่วคราวและแสดง `Queue full` ใน UI จนกลับมารับงานได้
- ห้าม drop click แบบเงียบ
- ห้ามส่งขนาน

เมื่อผู้ใช้ dismiss หรือเริ่ม Typing Handoff:

1. หยุดรับงานใหม่
2. ปล่อยงานที่เริ่มส่งแล้วให้จบ
3. ยกเลิกงานที่ยังไม่เริ่ม
4. จากนั้น dismiss หรือ handoff

input แรกของ Typing Handoff ต้องถูกเก็บไว้อย่างปลอดภัยระหว่างรอ active operation

## 17. ภาษาและ accessibility

UI:

- ตาม Windows display language ระหว่างไทยกับอังกฤษ
- เปลี่ยนภาษาได้ใน Settings
- ภาษาอื่น fallback อังกฤษ
- Search ยังค้นไทยและอังกฤษได้ตลอด

Accessibility Baseline:

- workflow หลักใช้ keyboard ได้ครบ
- accessible name ของ tile มาจาก localized short name
- screen reader อ่าน selection, busy และ error state ได้
- focus indicator มองเห็นชัด
- ใช้ system colors ที่รองรับ High Contrast
- รองรับ DPI 100–250%

ขอบเขตนี้ไม่ใช่ certification และไม่รับประกัน screen reader ทุกชนิด

## 18. Settings

Settings หลัก:

- global hotkey และ disable
- Start with Windows
- UI language
- theme: System, Light, Dark
- global skin tone
- insertion mode
- Clear Recent
- Reset learned ranking
- Clear all activity

Advanced Settings:

- pasteRestoreDelayMs
- diagnostic logging
- Reset advanced defaults

Diagnostic log:

- ปิดเป็นค่าเริ่มต้น
- เก็บเฉพาะ metadata ทางเทคนิค
- ห้ามเก็บ query
- ห้ามเก็บ Emoji ที่เลือก
- ห้ามเก็บ clipboard หรือข้อความ
- ห้ามเก็บชื่อหน้าต่างเป้าหมาย
- ไม่มี automatic upload

## 19. Offline runtime และ privacy

Picker v1 ไม่มี runtime network call:

- data และ PNG bundle มากับ release
- ไม่มี update polling
- ไม่มี telemetry
- ไม่มี analytics
- ไม่มี cloud sync
- ไม่มี remote font หรือ asset

RDP/Citrix รองรับแบบ best-effort ผ่าน Advanced clipboard delay และ manual smoke test แต่ไม่ block MVP

## 20. Build และ release

Official artifacts:

- self-contained Inno per-user installer
- self-contained portable ZIP

ไม่สร้าง framework-dependent package หรือ MSI ใน MVP

Local-first release:

    scripts/release.ps1 -Version <version>

ต้อง:

1. ตรวจ clean commit
2. ตรวจ product-scoped version
3. ตรวจ Emoji Baseline lock
4. รัน generator verification
5. รัน automated tests
6. build Release
7. publish win-x64 self-contained
8. สร้าง installer และ portable ZIP
9. รวม LICENSE และ THIRD-PARTY-NOTICES
10. สร้าง SHA-256
11. รายงาน raw assets, installer และ ZIP size
12. ไม่ upload อัตโนมัติ

ใช้ Semantic Versioning และ tag:

    picker-v0.1.0

Renderer Extension ใช้ tag แยก เช่น renderer-v0.1.0

Publish แยกจาก build:

- ใช้สคริปต์ผ่าน gh
- รับเฉพาะ artifact ที่ build local และผ่าน verification
- สร้าง Draft GitHub Release
- ตรวจ release notes, licenses และ checksums
- ผู้ใช้สั่ง Publish เอง

ห้ามพึ่ง GitHub-hosted CI/CD เป็นค่าเริ่มต้น หากเพิ่ม automation ภายหลังให้พิจารณาวิธีที่ไม่ใช้ Actions minutes ก่อน

MVP ยัง unsigned ได้ แต่:

- ต้องเผยแพร่ SHA-256
- ต้องอธิบาย SmartScreen
- ห้ามใช้ self-signed certificate เพื่ออ้างความน่าเชื่อถือ
- ต้องสร้าง icon ใหม่ก่อน public MVP release

## 21. License และ notices

ใช้ MIT สำหรับโค้ดของ Modern Emoji Picker

ต้องรักษา:

- MIT notice และ copyright ของ Platima
- Unicode License สำหรับ Unicode และ CLDR data
- Apache 2.0 สำหรับ Noto image assets ที่เกี่ยวข้อง
- OFL 1.1 เมื่อแจก Noto font ใน Renderer Extension ภายหลัง
- provenance ของ region flags
- notices ของ dependencies ทั้ง direct และ transitive

Release payload ต้องมี LICENSE และ THIRD-PARTY-NOTICES ห้ามทำซ้ำปัญหา upstream ที่ artifact ไม่มี notices ครบ

## 22. Test Strategy

Automated tests:

- generator determinism
- source checksum และ lock
- duplicate sequence
- Noto alias และ asset mapping
- full coverage
- category mapping
- Thai/English search
- match tiers
- Learned Ranking และ half-life
- skin tone และ Variant Override
- Recent MRU
- schema migration และ corrupted-file recovery
- queue order, bound และ cancellation
- target validation abstraction
- insertion mode selection
- clipboard sequence-number rules
- release script verification

Manual Tier A ซึ่งต้องผ่านทุก release:

- Notepad
- Chrome
- VS Code
- Windows Terminal
- Explorer address bar

Manual Tier B เมื่อ environment พร้อม:

- Discord
- Slack
- Instagram Web
- PowerShell
- RDP/Citrix

Matrix เพิ่มเติม:

- Windows 10 22H2 x64
- Windows 11 smoke test
- DPI 100, 125, 150, 175, 200, 225 และ 250%
- multi-monitor ต่าง DPI
- Thai IME
- English keyboard
- single code point
- variation selector
- skin tone
- mixed tone
- flags
- keycaps
- ZWJ family
- rapid clicks
- clipboard ว่าง, text, image, files และ custom formats
- target ปิด, focus เปลี่ยน และ elevated target

## 23. Performance Baseline

หลัง import upstream ให้ทำ migration/performance spike ก่อนสร้างระบบเต็ม:

- build net10.0-windows
- self-contained publish
- installer
- clipboard round-trip
- Win10/Win11 smoke

วัด upstream และ Modern บนเครื่อง Windows 10 22H2 เครื่องเดียวกัน แล้วล็อกตัวเลขสำหรับ:

- warm hotkey-to-visible
- search latency
- virtualized scroll/frame stalls
- idle working set
- decode/cache behavior
- installer size
- portable ZIP size

ห้ามใช้คำว่าเร็วพอหรือไม่ช้าอย่างมีนัยสำคัญโดยไม่มีตัวเลข

## 24. Implementation Phases

### Phase 0 — Foundation

- import upstream subtree
- บันทึก provenance
- rebrand identity
- migrate .NET 8 → .NET 10
- build upstream behavior ให้ผ่าน
- เพิ่ม global.json และ dependency locks
- ทำ Performance Baseline

### Phase 1 — Emoji Baseline

- vendor Unicode, CLDR และ Noto ที่ pin
- สร้าง generator
- สร้าง JSON/manifest
- coverage และ license reports

### Phase 2 — Domain model และ search

- Emoji Entry
- categories
- Thai/English search
- global skin tone
- Variant Override
- Recent
- Learned Ranking

### Phase 3 — Noto renderer

- asset assembly
- 128 grid renderer
- 512 Hover Preview
- lazy decode, cache และ virtualization
- ถอด Emoji.Wpf

### Phase 4 — Picker interaction

- Browse/Search modes
- Commit Gestures
- placement, resizing และ DPI
- theme และ accessibility
- hotkey customization
- Classic Conflict

### Phase 5 — Insertion

- target capture/validation
- Hybrid, Keystroke only และ Paste always
- safe Temporary Paste
- bounded queue
- dismissal และ Typing Handoff
- failure และ Explicit Copy

### Phase 6 — Persistence และ settings

- versioned schema
- atomic writes
- corruption recovery
- privacy controls
- first-run welcome

### Phase 7 — Quality และ release

- automated tests
- manual matrix
- performance verification
- installer และ portable ZIP
- local release scripts
- notices, checksum และ SmartScreen docs
- icon ใหม่

## 25. Definition of Done

Picker MVP เสร็จเมื่อ:

- build และ release ด้วย .NET 10 ผ่าน
- Windows 10 22H2 Tier A ผ่านทั้งหมด
- Windows 11 smoke test ผ่าน
- fully-qualified Emoji 17 ทุก sequence เข้าถึงได้
- ไทยและอังกฤษค้นหาได้
- grid และ Hover Preview ใช้ Noto ตาม baseline
- ไม่มี asset coverage gap
- click ทำ multi-insert ได้โดย Picker ไม่กระพริบ
- Enter/Shift+Enter ทำ commit เฉพาะใน Search Mode; Enter ใน Browse Mode handoff กลับ target
- queue รักษาลำดับและ cancel ถูก
- Typing Handoff ไม่ทำ input แรกหรือ Thai IME เสีย
- target validation ไม่ส่งไปผิดหน้าต่าง
- Temporary Paste ไม่ทับ clipboard ใหม่
- Recent และ Learned Ranking ทำงานตามสเปก
- privacy controls และ corruption recovery ผ่าน
- accessibility baseline และ DPI matrix ผ่าน
- performance budgets ที่วัดแล้วผ่าน
- installer และ portable ZIP มี license notices กับ SHA-256
- public release ใช้ icon ใหม่

Renderer Extension ไม่ใช่เงื่อนไขของ Picker MVP และเริ่มหลัง Picker MVP ผ่าน Definition of Done
