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

คำสั่งนี้ตรวจ executable/assembly, mutex, named event, Run value, data directory, Inno AppId, WiX UpgradeCode, artifact names และการไม่ reuse icon ของ Classic พร้อมรัน smoke สำหรับ secondary-launch signal กับ Classic Conflict seam โดยไม่ติดตั้ง global hook ไม่เปิด tray และไม่อ่านหรือเขียนข้อมูลผู้ใช้

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

คำสั่งนี้ build Picker แล้วตรวจว่า Emoji 17 ครบ 3,944 รายการในหมวดมาตรฐาน, PNG 128 ทุกภาพถูก bundle ตาม manifest, runtime ไม่อ้าง `Emoji.Wpf` และ WPF smoke ผ่านทั้ง lazy decode, frozen image, cache bound, DPI 100–250%, missing-image fallback และคำแนะนำ Repair/Reinstall เมื่อชุด asset หาย รายละเอียดอยู่ที่ [การแสดง Emoji 17 ด้วย Noto grid](../docs/noto-grid-runtime.md)

## ตรวจนโยบายการส่งอย่างปลอดภัย

```powershell
.\scripts\verify-safe-insertion.ps1
```

คำสั่งนี้ตรวจ Hybrid/Keystroke/Paste, target validation และ clipboard restore rules ผ่าน smoke seam โดยไม่ส่ง input จริง รายละเอียดและข้อจำกัดอยู่ที่ [การส่ง Emoji ไปยังแอปเป้าหมายอย่างปลอดภัย](../docs/safe-insertion.md)
