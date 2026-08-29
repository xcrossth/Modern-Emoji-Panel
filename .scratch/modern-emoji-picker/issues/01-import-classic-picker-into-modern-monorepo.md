# 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**What to build:** นำ upstream ที่ตรึงไว้เข้ามาเป็นรากฐานของ Picker ใน monorepo และทำให้พฤติกรรมเดิม build และเปิดใช้งานได้ด้วย .NET 10 โดยยังรักษาประวัติและ provenance ที่ตรวจสอบได้

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] นำ Classic Emoji Picker จาก commit `56c54201e0673a57710c2498db25a149b45e63ec` เข้าด้วย Git subtree และบันทึก source, commit และวิธีอัปเดต upstream แบบ manual
- [x] โครง monorepo รองรับ Picker, Emoji Baseline tooling, shared tests, scripts และ vendor assets โดยไม่ทำให้เอกสารหรือ research เดิมเสียหาย
- [x] Picker และ tooling ใช้ .NET 10 ตาม target ที่สเปกระบุ พร้อมล็อก SDK feature band, central package versions และ NuGet dependencies
- [x] Picker build และเปิดใช้งานบน Windows 10 22H2 x64 ได้ โดย smoke test พฤติกรรม upstream หลักผ่าน
- [x] build scripts ที่รับมาจาก upstream ใช้ตำแหน่งใหม่ใน monorepo ได้และไม่มี workflow ใด sync upstream เข้า `main` อัตโนมัติ
- [x] มี regression check ที่พิสูจน์ว่ารากฐานใหม่ build ซ้ำได้จาก clean checkout

## Comments

- 28 สิงหาคม 2026: เริ่มดำเนินการบน branch `feature/modern-emoji-picker-mvp` ภายใต้ Goal ของ Picker MVP
- 28 สิงหาคม 2026: import แบบ full-history subtree สำเร็จที่ commit `9b9df626de6d355dc63a4f9c5124b91bb0668c06` โดย upstream commit เป็น parent/ancestor และ tree ก่อน migration เท่ากับ source tree `9944b2a441ff1dd207ceb733ab30b7b0f42b8623` รายละเอียดและ update flow อยู่ที่ `docs/upstream/classic-picker.md` และ manifest อยู่ที่ `docs/upstream/classic-picker.source.json`
- 28 สิงหาคม 2026: เพิ่ม `global.json` สำหรับ SDK 10.0.400 แบบ `latestPatch`, root solution, central package versions และ `packages.lock.json` จากนั้น `scripts/verify-foundation.ps1` ผ่านบน Windows 10 Enterprise N 22H2 build 19045 ด้วย SDK 10.0.400: locked restore ผ่าน, Release build 0 warnings/0 errors, self-contained `win-x64` publish ผ่าน และ `dotnet format --verify-no-changes` ผ่าน
- 28 สิงหาคม 2026: runtime smoke `EmojiPicker.exe --foundation-smoke` ผ่าน โดยโหลด Emoji data และ WPF shell จริง, prewarm visual tree, ตรวจ 7 upstream categories, grid ที่ไม่ว่าง และค้นหา `smile` แล้วได้ผลลัพธ์ โดยไม่ใช้ Classic mutex, global hook, tray หรือ Activity Data การทดสอบ hotkey/insertion แบบ interactive เต็มชุดยังอยู่ใน Ticket 13 หลัง Ticket 02 แยก product identity แล้ว
- 28 สิงหาคม 2026: `apps/picker/build.bat` และ `apps/picker/code-quality-simple.ps1` ทำงานจาก current directory นอก subtree ได้ด้วย exit code 0 ส่วน workflows ที่มากับ upstream คงอยู่ใต้ `apps/picker/.github/workflows/` ในสถานะ inert และไม่มี `.github/workflows/` ที่ repository root
- 28 สิงหาคม 2026: `scripts/test-clean-checkout.ps1 -Revision HEAD` ผ่านบน detached temporary worktree ของ commit `301a9fc742418686a1504e8c4acf5d05b2b75a8a` โดย restore จาก tracked lock, build, publish, runtime smoke และ format verification สำเร็จทั้งหมด แล้วลบ temporary worktree สำเร็จ
- 28 สิงหาคม 2026: ระหว่างตรวจ merge พบว่า wrapper เดิมล้มเหลวเมื่อเรียกผ่าน Windows PowerShell 5.1 เพราะอ่านข้อความภาษาไทยในไฟล์ UTF-8 แบบไม่มี BOM ผิด encoding จึงเปลี่ยนข้อความ runtime ใน PowerShell scripts เป็น ASCII แล้วตรวจ `apps/picker/code-quality-simple.ps1` ผ่านทั้ง Windows PowerShell 5.1 และ PowerShell 7 ก่อนรัน clean-checkout verification ของ merge commit ซ้ำ
