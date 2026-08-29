# สคริปต์สำหรับพัฒนาและตรวจสอบ

สคริปต์ในโฟลเดอร์นี้ทำงานจาก repository root ได้โดยไม่ขึ้นกับ current working directory และไม่ดาวน์โหลดหรือรวม upstream โดยอัตโนมัติ

## Build รากฐาน Picker

    .\scripts\build.ps1

คำสั่งนี้ restore dependencies ด้วย NuGet lock file แล้ว build `ModernEmojiPanel.sln` แบบ Release หากต้องการทดสอบ self-contained publish สำหรับ `win-x64` ด้วย ให้ใช้:

    .\scripts\build.ps1 -PublishSelfContained

ผลลัพธ์ที่สร้างชั่วคราวอยู่ใต้ `artifacts/foundation/` และไม่ถูก commit

## ตรวจรากฐานทั้งหมด

    .\scripts\verify-foundation.ps1

คำสั่งนี้ตรวจ subtree ancestry/tree hash, SDK feature band, target framework, architecture, central package versions, lock file, active workflow, build, self-contained publish, WPF browse/search smoke และรูปแบบโค้ด smoke path ไม่ใช้ mutex, global hook, tray หรือ Activity Data จึงรันร่วมกับ Classic Emoji Picker ที่ติดตั้งอยู่ได้

## ตรวจ product identity และ lifecycle

```powershell
.\scripts\verify-product-identity.ps1
```

คำสั่งนี้ตรวจ executable/assembly, mutex, named event, Run value, data directory, Inno AppId, artifact names และ product icon ใหม่ที่ไม่ reuse Classic พร้อมรัน smoke สำหรับ secondary-launch signal กับ Classic Conflict seam โดยไม่ติดตั้ง global hook ไม่เปิด tray และไม่อ่านหรือเขียนข้อมูลผู้ใช้

หลัง commit แล้ว สามารถพิสูจน์การ build จาก checkout ใหม่ที่ไม่มีไฟล์ build ค้างได้ด้วย:

    .\scripts\test-clean-checkout.ps1

สคริปต์จะสร้าง detached worktree ใต้ temporary directory ของ Windows รัน verification แล้วลบ worktree นั้นเมื่อจบ

## เตรียม Classic upstream remote

Git remote ไม่ได้ติดไปกับ clone ใหม่ ให้เพิ่มหรือตรวจ remote แบบ idempotent ด้วย:

    .\scripts\setup-classic-upstream.ps1

หากต้องการ fetch และตรวจ tree hash ของ commit แรกที่อนุมัติด้วย ให้เพิ่ม `-FetchApprovedCommit` สคริปต์นี้ไม่ merge, subtree pull หรือ push และไม่ถูกเรียกจาก build

## ตรวจ Emoji Baseline แบบ offline

    .\scripts\verify-emoji-baseline.ps1

คำสั่งนี้อ่านเฉพาะไฟล์ที่ commit แล้ว ตรวจเวอร์ชัน, immutable URL, SHA-256, byte length, inventory ของ PNG ทั้งชุด, license notices และยืนยันว่า asset ไม่ใช้ Git LFS โดยไม่มี network call

ก่อนสร้าง release ให้เพิ่ม release gate ซึ่งตรวจขนาดไฟล์สูงสุดด้วย:

    .\scripts\verify-emoji-baseline.ps1 -VerificationMode Release

## อัปเดต Emoji Baseline โดยตั้งใจ

    .\scripts\update-emoji-baseline.ps1

คำสั่งนี้เป็นทางเดียวในโครงการที่ดาวน์โหลด source ของ Emoji Baseline โดย fetch เฉพาะ URL และ Git commit ที่ตรึงใน source lock ลง staging directory ชั่วคราว ตรวจ checksum กับ inventory ก่อนแทนที่ไฟล์ใน repository และรัน offline verifier ซ้ำ การ build, test และ release ตามปกติไม่เรียกคำสั่งนี้

## สร้างและตรวจ generated Emoji Baseline

หลัง restore แบบ locked แล้ว สร้าง JSON กลางจาก source ที่ commit ไว้โดยไม่ใช้ network:

```powershell
.\scripts\generate-emoji-baseline.ps1
```

ตรวจ source checksum, generator build, full metadata/asset coverage และ determinism แบบ byte-for-byte ด้วย:

```powershell
.\scripts\verify-generated-emoji-baseline.ps1
```

รายละเอียด schema, stable ID, asset aliases และรายงาน update อยู่ที่ [การสร้าง Emoji Baseline](../docs/emoji-baseline-generator.md)

## ตรวจ Noto grid ของ Picker

```powershell
.\scripts\verify-noto-grid.ps1
```

คำสั่งนี้ build Picker แล้วตรวจว่า Emoji 17 ครบ 3,944 รายการในหมวดมาตรฐาน, PNG 128 ทุกภาพถูก bundle ตาม manifest, ธงทั้ง 270 รายการถอดรหัสด้วย WPF ได้จริง, runtime ไม่อ้าง `Emoji.Wpf` และ WPF smoke ผ่านทั้ง lazy decode, frozen image, cache bound, DPI 100–250%, missing-image fallback และคำแนะนำ Repair/Reinstall เมื่อชุด asset หาย รายละเอียดอยู่ที่ [การแสดง Emoji 17 ด้วย Noto grid](../docs/noto-grid-runtime.md)

หากต้องการตรวจเฉพาะภาพธงโดยไม่รัน smoke อื่น ใช้:

```powershell
.\scripts\verify-flag-assets.ps1 -SkipBuild
```

## ตรวจนโยบายการส่งอย่างปลอดภัย

```powershell
.\scripts\verify-safe-insertion.ps1
```

คำสั่งนี้ตรวจ Hybrid/Keystroke/Paste, target validation และ clipboard restore rules ผ่าน smoke seam โดยไม่ส่ง input จริง รายละเอียดและข้อจำกัดอยู่ที่ [การส่ง Emoji ไปยังแอปเป้าหมายอย่างปลอดภัย](../docs/safe-insertion.md)

หากเครื่องมี Chrome เปิดอยู่ ให้ตรวจเส้นทางจริงของ address bar ซึ่งไม่มี child HWND ด้วย:

```powershell
.\scripts\verify-chrome-omnibox.ps1 -SkipBuild
```

คำสั่งนี้ focus address bar ชั่วคราว เปิด Picker test process ส่งหัวใจขาว `🤍` 10 ครั้งผ่าน MainWindow, Insertion Queue, target validation และ Hybrid insertion จริง จากนั้นตรวจ UTF-16 ว่าไม่มี `U+FFFD` ก่อนคืนค่า address bar เดิม คำสั่งไม่กด Enter จึงไม่เปิด URL ที่ใช้ทดสอบ

## ตรวจการค้นหาสองภาษาและ Hover Preview

```powershell
.\scripts\verify-search-preview.ps1
```

คำสั่งนี้ตรวจชื่อและ keyword ไทย–อังกฤษ, match tiers สี่ระดับ, CLDR tie-break, accessible name, การคง keyboard focus, การเปิด preview ทันที, close grace 150 ms, การ reuse popup เมื่อย้าย tile, รายละเอียด preview และ coverage ของ Noto PNG 512 ทั้ง 3,944 รายการ พร้อมวัด guardrail ของการค้นหา 100 ครั้งโดยไม่ใช้เครือข่าย รายละเอียดอยู่ที่ [การค้นหาสองภาษาและ Hover Preview](../docs/bilingual-search-hover-preview.md)

## ตรวจสีผิวและ Variant Override

```powershell
.\scripts\verify-emoji-variants.ps1
```

คำสั่งนี้ตรวจค่าเริ่มต้นและการคงอยู่ของสีผิวระดับ global, การ resolve สีผิวเดียวทุกตำแหน่ง, mixed-tone Variant Override แบบหนึ่งครั้ง และยืนยันว่า fully-qualified sequence ทุกตัวเข้าถึงได้ผ่าน Emoji Entry ฐาน, global setting หรือ Variant Override

## ตรวจ Picker Session และ focus

```powershell
.\scripts\verify-picker-session.ps1
```

คำสั่งนี้ตรวจ Browse/Search Mode, Esc, click/Enter/Shift+Enter, การคง visibility ระหว่าง pointer multi-insert, นโยบายคืน focus, placement บน working area หลาย monitor, การจำขนาด และ accessibility state โดยไม่ส่ง input จริง รายละเอียดอยู่ที่ [Picker Session, focus และการวางหน้าต่าง](../docs/picker-session-and-focus.md)

## ตรวจ Recent และ Learned Ranking

```powershell
.\scripts\verify-activity-data.ps1
```

คำสั่งนี้ตรวจ Recent แบบ MRU 50 รายการและ resolved sequence, Learned Ranking ที่มี half-life 90 วันโดยไม่ข้าม match tier, schema แบบระบุเวอร์ชัน, atomic write, migration, การสำรองและรีเซ็ตไฟล์เสียแบบแยกส่วน ตลอดจน Clear Recent, Reset learned ranking และ Clear all activity รายละเอียดอยู่ที่ [Recent และ Learned Ranking บนเครื่อง](../docs/local-activity-data.md)

## ตรวจ Insertion Queue และ Typing Handoff

```powershell
.\scripts\verify-insertion-queue.ps1
```

คำสั่งนี้ตรวจลำดับ FIFO, ขอบเขตงานรอ 20 รายการ, การไม่เริ่มงานขนาน, การยกเลิกเฉพาะงานที่ยังไม่เริ่ม, การรอ active operation ก่อน dismiss/handoff, physical-key handoff ที่รักษา per-app keyboard layout และ committed-text fallback ผ่าน state seam ที่ไม่ขึ้นกับ timing จริงของเดสก์ท็อป รายละเอียดอยู่ที่ [Insertion Queue และ Typing Handoff](../docs/insertion-queue-and-typing-handoff.md)

## ตรวจ Settings, Welcome และความเป็นส่วนตัว

```powershell
pwsh scripts/verify-settings-privacy.ps1
```

ตรวจ Settings model เดียว, hotkey/autostart/ภาษา/ธีม/สีผิว/Insertion Mode, Advanced Paste delay และ reset, Welcome ครั้งแรก, คำสั่งล้าง Activity Data, diagnostic logging แบบ opt-in ที่ไม่เก็บเนื้อหาผู้ใช้ และการไม่มี runtime network, telemetry, sync หรือ upload code รายละเอียดอยู่ที่ [Settings, Welcome และความเป็นส่วนตัว](../docs/settings-welcome-and-privacy.md)

## รับรอง automated qualification

```powershell
pwsh scripts/verify-qualification.ps1 -OutputPath docs/qualification/results/automated-win10-19045.json
```

คำสั่งนี้รัน regression suite ที่สร้างไว้ใน Ticket 01–12 แล้ววัด warm open-to-render proxy, search, virtualized scroll, working set และ decode/cache จาก self-contained Release process ตรวจ performance budgets, accessibility/High Contrast wiring, ขนาด publish และเฝ้าดู TCP/UDP socket ของ process ระหว่าง smoke จริง ผลที่ได้ไม่แทน manual matrix, packet capture หรือผลบน Windows 11 ดูขอบเขตและแบบบันทึกผลที่ [การรับรองคุณภาพ](../docs/qualification/README.md)

วัด warm global hotkey-to-visible จริงกับ Notepad ที่สคริปต์เปิดเองได้ด้วย:

```powershell
pwsh scripts/measure-global-hotkey.ps1 -OutputPath artifacts/ticket-13/global-hotkey-win10-19045.json
```

คำสั่งนี้ต้องไม่มี Modern Emoji Picker instance อื่นทำงานอยู่ จากนั้นจะติดตั้ง low-level hook แบบ isolated, ส่ง `Win + .` ด้วย `SendInput` 20 ครั้ง, ตรวจ target/focused control/caret, การ activate Picker, category-cache semantics และ P95 ≤ 100 ms โดยไม่เลือก Emoji หรือแตะ Clipboard หากต้องการผูกหลักฐานนี้กับ qualification report ให้ส่ง path ผ่าน `verify-qualification.ps1 -GlobalHotkeyReportPath <path>`

เมื่อมีผู้ทดสอบอยู่หน้าเครื่อง ให้เริ่ม manual qualification แบบ 7 stage ผ่าน Git Bash:

```bash
bash scripts/manual-qualification-wizard.sh
```

ให้รันจาก PowerShell หรือ Windows Terminal แบบปกติเท่านั้น ห้ามใช้หน้าต่างที่มีคำว่า `Administrator` เพราะแอปเป้าหมายที่ wizard เปิดจะมีสิทธิ์สูงกว่า Modern Emoji Picker และทำให้ `Win + .` ดูเหมือนเสียทั้งที่แอปทำงานปกติ ตัว wizard จะตรวจและหยุดก่อนเริ่มทดสอบหากพบสิทธิ์ Administrator

ตัวช่วยบันทึกผลที่มนุษย์เลือกพร้อม environment/evidence เป็น JSON และ Markdown ใต้ `artifacts/ticket-13/manual/` โดยไม่แก้ manual matrix หรือรับรองผลอัตโนมัติ ตรวจโครงสร้างและ report writer แยกต่างหากได้ด้วย:

```powershell
pwsh scripts/verify-manual-qualification-wizard.ps1
```

## ตรวจ Renderer Extension

```powershell
.\scripts\verify-renderer-qualification.ps1
```

คำสั่งนี้ตรวจ generated Emoji data, TypeScript, unit/integration tests, DOM/text integrity, Popup/Options, performance, Chrome load และเส้นทางโหลดฟอนต์ของ extension จริงบน URL fixture ที่ตรงกับ Instagram โดยใช้ Chrome for Testing กับโปรไฟล์ชั่วคราว การตรวจฟอนต์ถาม Chrome ว่า glyph มาจาก bundled `Noto Color Emoji` จริง ไม่ได้สรุปจากชื่อ `font-family` หรือการมีไฟล์เพียงอย่างเดียว

สร้าง deterministic release candidate ในเครื่องพร้อม ZIP, SHA-256, licenses และ verification report ด้วย:

```powershell
.\scripts\build-renderer-release.ps1
```

## สร้าง product icon

```powershell
.\scripts\build-product-icon.ps1
.\scripts\build-product-icon.ps1 -VerifyOnly
```

คำสั่งนี้ตรวจ hash ของ artwork master แล้วสร้าง/ตรวจ ICO 16–256 px และภาพ preview 512 px รายละเอียดอยู่ที่ [`design/brand/README.md`](../design/brand/README.md)

## สร้าง local qualification artifacts

```powershell
.\scripts\release.ps1 -Version 0.1.9
```

ต้องรันจาก clean commit และมี Inno Setup 6 สคริปต์จะตรวจ icon, baseline/generator, regression/qualification และ product version ก่อนสร้าง self-contained per-user installer กับ portable ZIP พร้อม notices, SHA-256 และ manifest ใต้ `artifacts/release/picker-v<version>/` โดยไม่มี tag, upload หรือ GitHub Release

ตรวจ artifact ที่สร้างแล้วแยกต่างหากได้ด้วย:

```powershell
.\scripts\verify-release-artifacts.ps1 -Version 0.1.9
```

MVP ไม่มี framework-dependent, lite หรือ MSI package การเตรียม Draft/public releaseอยู่ใน Ticket 15 และไม่ใช่หน้าที่ของ `release.ps1`
