# คู่มือพัฒนา Modern Emoji Picker

เอกสารนี้อธิบาย workflow ปัจจุบันใน monorepo โค้ดตั้งต้นจาก Classic Emoji Picker ถูกเก็บเพื่อ provenance แต่คำสั่ง build, installer และ release เดิมไม่ใช่ workflow ของ Modern

## สิ่งที่ต้องมี

- Windows 10 22H2 x64 หรือใหม่กว่า
- .NET SDK 10 ตาม `global.json`
- Git for Windows และ PowerShell 7
- Inno Setup 6 เฉพาะเมื่อสร้าง local installer

## Build และรัน

รันจาก root ของ repository:

```powershell
dotnet restore .\ModernEmojiPanel.sln --locked-mode
dotnet build .\ModernEmojiPanel.sln --configuration Release --no-restore
dotnet run --project .\apps\picker\EmojiPicker\EmojiPicker.csproj
```

แอปทำงานแบบ resident tray utility กด `Win + .` หรือเปิด executable ซ้ำเพื่อเรียก Picker และเลือก `Exit Modern Emoji Picker` จาก tray เมื่อต้องการหยุด process

## Verification

ใช้ verifier ที่ตรงกับส่วนซึ่งแก้ และปิดท้ายด้วย qualification เต็มชุดเมื่อ behavior ข้ามโมดูล:

```powershell
.\scripts\verify-product-identity.ps1
.\scripts\verify-generated-emoji-baseline.ps1
.\scripts\verify-noto-grid.ps1 -SkipBuild
.\scripts\verify-search-preview.ps1 -SkipBuild
.\scripts\verify-emoji-variants.ps1 -SkipBuild
.\scripts\verify-safe-insertion.ps1 -SkipBuild
.\scripts\verify-picker-session.ps1 -SkipBuild
.\scripts\verify-activity-data.ps1 -SkipBuild
.\scripts\verify-insertion-queue.ps1 -SkipBuild
.\scripts\verify-settings-privacy.ps1 -SkipBuild
.\scripts\verify-qualification.ps1
```

ก่อน merge checkpoint สำคัญ ให้ยืนยันจาก commit ที่ checkout ใหม่:

```powershell
.\scripts\test-clean-checkout.ps1 -Revision HEAD
```

ผล manual test และข้อจำกัด environment บันทึกตาม [`docs/qualification/manual-matrices.md`](../../docs/qualification/manual-matrices.md)

## หลักการสำคัญ

- Runtime ใช้ Emoji Baseline และ Noto assets ที่ pin อยู่ใน repositoryโดยไม่เรียก network
- Settings และ Activity Data อยู่ใต้ `%APPDATA%\ModernEmojiPicker` และไม่อ่านข้อมูลของ Classic
- ส่ง input เฉพาะ captured target ที่ผ่าน foreground/integrity validation โดยไม่ retry หรือ retarget
- Temporary Paste รักษา clipboard เดิม ส่วน Explicit Copy เป็นคำสั่งที่ผู้ใช้เลือกโดยตรง
- รักษา bounded queue, UI virtualization และ memory/cache budgets ที่ qualification ตรวจอยู่
- ความเห็นในโค้ดใช้ Australian English เอกสารที่ผู้ใช้อ่านใช้ภาษาไทย

## Local package

```powershell
.\scripts\release.ps1 -Version 0.1.9
```

คำสั่งนี้ต้องรันจาก clean commit ตรวจ qualification ก่อนสร้าง self-contained Inno per-user installer กับ portable ZIP และไม่ tag/upload/สร้าง GitHub Release ผลลัพธ์อยู่ใต้ `artifacts/release/picker-v<version>/`

MVP ไม่มี framework-dependent, lite หรือ MSI package การเตรียม Draft/public release เป็น Ticket 15 (14B) และยังต้องรอ Ticket 13

## แหล่งความจริง

- พฤติกรรม: [`docs/specs/01-modern-emoji-picker.md`](../../docs/specs/01-modern-emoji-picker.md)
- สถาปัตยกรรม: [`docs/adr/`](../../docs/adr/)
- งานและ blocker: [`.scratch/modern-emoji-picker/issues/`](../../.scratch/modern-emoji-picker/issues/)
- qualification: [`docs/qualification/README.md`](../../docs/qualification/README.md)
- provenance: [`docs/upstream/classic-picker.md`](../../docs/upstream/classic-picker.md)
