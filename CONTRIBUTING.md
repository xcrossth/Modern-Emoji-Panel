# การมีส่วนร่วมกับ Modern Emoji Panel

ขอบคุณที่ช่วยรายงานปัญหาหรือปรับปรุงโครงการ ก่อนส่งข้อมูลใด ๆ โปรดลบชื่อบัญชี ข้อความสนทนา path ส่วนตัว token และ credential ออกทั้งหมด ปัญหาด้านความปลอดภัยให้ใช้ขั้นตอนใน [`SECURITY.md`](./SECURITY.md) แทน GitHub Issue สาธารณะ

## รายงานปัญหา

เปิด GitHub Issue พร้อมข้อมูลเท่าที่จำเป็น:

- Picker หรือ Renderer และเลขรุ่น
- Windows/Chrome รุ่นที่ใช้
- ขั้นตอนทำซ้ำและผลที่คาดหวัง
- ผลจริง โดยใช้ข้อความหรือบัญชีทดสอบแทนข้อมูลส่วนตัว
- log หรือภาพที่ตรวจและปกปิดข้อมูลแล้ว

Maintainer อาจนำ issue ที่รับมาทำต่อเป็น local ticket ใต้ `.scratch/` ซึ่งเป็น issue tracker ภายใน repository

## พัฒนาในเครื่อง

ต้องมี Git, PowerShell 7, .NET 10 SDK และ Node.js 24+/npm 11+ เมื่อต้องแก้ Renderer จาก root ของ repository ให้รันชุดตรวจที่เกี่ยวข้อง:

```powershell
./scripts/verify-foundation.ps1
npm --prefix ./apps/renderer-extension ci
npm --prefix ./apps/renderer-extension run verify
```

ก่อนส่งการเปลี่ยนแปลงขนาดใหญ่ ให้รัน clean-checkout test ตามคำแนะนำใน `AGENTS.md` และเอกสาร qualification ที่เกี่ยวข้อง

## หลักการส่งการเปลี่ยนแปลง

- แยกหนึ่งประเด็นต่อ branch/PR และอธิบายพฤติกรรมที่เปลี่ยน
- เพิ่ม regression test เมื่อแก้ bug ที่ทำซ้ำได้
- ไม่ commit `bin`, `obj`, `node_modules`, `artifacts`, installer, executable, log หรือไฟล์ editor ส่วนตัว
- ไม่เพิ่ม network call, telemetry, analytics หรือ remote asset โดยไม่อธิบายผลด้าน privacy และได้รับการอนุมัติ
- รักษา Unicode sequence เดิม ห้ามใช้ภาพที่เห็นแทนการตรวจ code points
- รักษา license และ attribution ของ Classic Emoji Picker, Unicode, Noto และ dependency อื่น
- เอกสารสำหรับผู้ใช้อ่านเขียนเป็นภาษาไทย ส่วนไฟล์สำหรับ agent โดยเฉพาะเขียนภาษาอังกฤษได้

Release artifact สร้างในเครื่องและเก็บนอก Git ตาม [`docs/release/README.md`](./docs/release/README.md) โครงการไม่มี GitHub-hosted build workflow เป็นค่าเริ่มต้น
