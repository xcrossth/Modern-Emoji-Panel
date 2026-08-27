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

หลัง commit แล้ว สามารถพิสูจน์การ build จาก checkout ใหม่ที่ไม่มีไฟล์ build ค้างได้ด้วย:

    .\scripts\test-clean-checkout.ps1

สคริปต์จะสร้าง detached worktree ใต้ temporary directory ของ Windows รัน verification แล้วลบ worktree นั้นเมื่อจบ

## เตรียม Classic upstream remote

Git remote ไม่ได้ติดไปกับ clone ใหม่ ให้เพิ่มหรือตรวจ remote แบบ idempotent ด้วย:

    .\scripts\setup-classic-upstream.ps1

หากต้องการ fetch และตรวจ tree hash ของ commit แรกที่อนุมัติด้วย ให้เพิ่ม `-FetchApprovedCommit` สคริปต์นี้ไม่ merge, subtree pull หรือ push และไม่ถูกเรียกจาก build
