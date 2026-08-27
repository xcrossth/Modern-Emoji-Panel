# 01: นำ Classic Picker เข้าสู่ Modern monorepo บน .NET 10

**What to build:** นำ upstream ที่ตรึงไว้เข้ามาเป็นรากฐานของ Picker ใน monorepo และทำให้พฤติกรรมเดิม build และเปิดใช้งานได้ด้วย .NET 10 โดยยังรักษาประวัติและ provenance ที่ตรวจสอบได้

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] นำ Classic Emoji Picker จาก commit `56c54201e0673a57710c2498db25a149b45e63ec` เข้าด้วย Git subtree และบันทึก source, commit และวิธีอัปเดต upstream แบบ manual
- [ ] โครง monorepo รองรับ Picker, Emoji Baseline tooling, shared tests, scripts และ vendor assets โดยไม่ทำให้เอกสารหรือ research เดิมเสียหาย
- [ ] Picker และ tooling ใช้ .NET 10 ตาม target ที่สเปกระบุ พร้อมล็อก SDK feature band, central package versions และ NuGet dependencies
- [ ] Picker build และเปิดใช้งานบน Windows 10 22H2 x64 ได้ โดย smoke test พฤติกรรม upstream หลักผ่าน
- [ ] build scripts ที่รับมาจาก upstream ใช้ตำแหน่งใหม่ใน monorepo ได้และไม่มี workflow ใด sync upstream เข้า `main` อัตโนมัติ
- [ ] มี regression check ที่พิสูจน์ว่ารากฐานใหม่ build ซ้ำได้จาก clean checkout

## Comments

- 28 สิงหาคม 2026: เริ่มดำเนินการบน branch `feature/modern-emoji-picker-mvp` ภายใต้ Goal ของ Picker MVP
