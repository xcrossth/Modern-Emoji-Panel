# Provenance และการอัปเดต Classic Emoji Picker

Modern Emoji Picker ใช้ Classic Emoji Picker เป็นรากฐานชั่วคราวของแอป WPF โดยนำเข้าแบบ Git subtree เพื่อให้ตรวจสอบที่มาและรับ upstream fix ภายหลังได้ โดยไม่ต้องใช้ submodule หรือดาวน์โหลดระหว่าง build

## Import แรก

- Source: https://github.com/platima/Classic-EmojiPicker
- Git remote ที่ใช้ใน repository สำหรับพัฒนา: `classic-upstream`
- Commit ที่ตรึง: `56c54201e0673a57710c2498db25a149b45e63ec`
- Tag ที่ชี้ commit ขณะ import: `v0.1.9`
- Tree hash ต้นฉบับ: `9944b2a441ff1dd207ceb733ab30b7b0f42b8623`
- Prefix: `apps/picker`
- วิธี import: Git subtree แบบไม่ squash เพื่อเก็บ ancestry ของ upstream ไว้ในประวัติ Git
- Commit ที่ import เข้าสู่ monorepo: `9b9df626de6d355dc63a4f9c5124b91bb0668c06`

ไฟล์ `apps/picker/LICENSE` และ `apps/picker/THIRD-PARTY-NOTICES.md` เป็น notice ที่มากับ upstream และต้องคงอยู่ การแก้ภายใต้ subtree หลัง import แรกเป็น migration ของโครงการนี้ ไม่ได้แก้ source repository ของ Platima

## เพิ่ม remote ใน clone ใหม่

Git ไม่เก็บ remote configuration ไว้ใน commit ผู้พัฒนาจึงเรียกสคริปต์ idempotent ครั้งเดียวต่อ clone:

    .\scripts\setup-classic-upstream.ps1 -FetchApprovedCommit

สคริปต์อ่านค่าจาก `classic-picker.source.json` หากมี remote อยู่แล้วจะยืนยัน URL แทนการเขียนทับ และจะ fail เมื่อ URL ไม่ตรงกับ source ที่อนุมัติ

## ขั้นตอนอัปเดตแบบ manual

การอัปเดตต้องทำบน branch แยกเท่านั้น ห้าม pull subtree บน `main` โดยตรง:

1. ตรวจ release/commit ใหม่ใน upstream และเลือก SHA แบบ immutable
2. สร้าง branch เช่น `upstream/classic-picker-<วันที่>` จาก `main`
3. ดึง object แล้วตรวจ source และ diff ก่อนรวม
4. รวมด้วยคำสั่งต่อไปนี้ โดยแทน `<sha>` ด้วย commit ที่ตรวจแล้ว

       git fetch classic-upstream <sha>
       git subtree pull --prefix=apps/picker classic-upstream <sha> -m "chore(upstream): merge Classic Emoji Picker <sha>"

5. แก้ conflict โดยรักษาการย้าย path, .NET target และ product behavior ตามสเปกปัจจุบัน
6. รัน `.\scripts\verify-foundation.ps1` และ regression tests ที่เกี่ยวข้อง
7. บันทึก SHA เดิม/ใหม่, สรุป diff และหลักฐานการทดสอบใน pull request ก่อน merge

ไม่มี scheduled task, build step หรือ active GitHub workflow ที่ fetch/pull upstream ให้เอง และ ordinary build ทำงานจากไฟล์ใน checkout เท่านั้น

## Workflows ที่มากับ upstream

ไฟล์ภายใต้ `apps/picker/.github/workflows/` เป็นประวัติจาก subtree และ **ไม่ใช่ active workflow** เพราะ GitHub อ่าน workflow เฉพาะ `.github/workflows/` ที่ repository root เท่านั้น โครงการเก็บไฟล์เหล่านี้เป็นข้อมูลอ้างอิงของ upstream โดยไม่คัดลอกหรือเปิดใช้งาน เนื่องจาก release ของ Modern ต้องเป็น local-first และแยกจาก Classic

## การแก้ migration หลัง import

Ticket 01 เปลี่ยนเฉพาะรากฐานที่จำเป็น:

- project target จาก `net8.0-windows` เป็น `net10.0-windows` และ `win-x64`
- ย้าย package versions ไปไว้ที่ `Directory.Packages.props`
- ใช้ NuGet lock file
- ให้ build/quality wrappers เดิมเรียกสคริปต์จาก monorepo root
- ปรับ path และ runtime probe ของ installer เดิมเป็น .NET 10
- เพิ่ม `--foundation-smoke` เพื่อตรวจการโหลด WPF shell, Emoji data, browse category และ English search โดยไม่แย่ง Classic mutex, global hook, tray หรือข้อมูลผู้ใช้

ชื่อ executable, mutex, registry, installer identity และข้อมูลผู้ใช้ยังเป็น Classic ตาม upstream ใน Ticket 01 การแยก identity ทั้งหมดเป็นขอบเขตของ Ticket 02
