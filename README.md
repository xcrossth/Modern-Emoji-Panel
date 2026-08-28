# Modern Emoji Picker

Modern Emoji Picker คือโครงการสร้าง Emoji Picker สำหรับ Windows 10/11 ที่รองรับ Unicode Emoji รุ่นใหม่ แสดงภาพด้วย Noto Emoji และส่ง Unicode sequence ไปยังแอปเป้าหมายโดยไม่ผูกกับ Emoji font ของ Windows

สถานะปัจจุบัน: **Foundation และ product isolation พร้อมแล้ว** โดยนำ Classic Emoji Picker ที่ตรึง commit เข้ามาใต้ `apps/picker`, ย้าย build target ไป .NET 10 และแยก runtime/installer/data identity ของ Modern ออกจาก Classic

## เป้าหมายหลัก

- รองรับ fully-qualified Emoji 17 ทุก sequence
- ค้นหาด้วยภาษาไทยและอังกฤษ
- แสดง Noto artwork ที่คมชัดใน grid และ Hover Preview
- รองรับ multi-insert พร้อม focus และ clipboard safety
- ทำงาน offline โดยไม่มี telemetry
- สร้าง installer และ portable ZIP บนเครื่อง local

## เอกสารสำคัญ

- [SPEC 01 — Modern Emoji Picker](./docs/specs/01-modern-emoji-picker.md)
- [SPEC 02 — Chrome Emoji Renderer Extension](./docs/specs/02-chrome-emoji-renderer-extension.md)
- [Domain glossary](./CONTEXT.md)
- [Architecture Decision Records](./docs/adr/)
- [ผลทดสอบ Noto PNG 128 เทียบ 512](./docs/research/asset-visual-spike/README.md)

## โครง repository

โครงการเป็น monorepo:

    apps/picker/              WPF application
    tools/emoji-baseline/     .NET 10 data generator
    vendor/noto-emoji/        Versioned Noto PNG assets
    tests/                    Automated tests
    scripts/                  Local build and release scripts

Picker ถูกนำเข้าจาก platima/Classic-EmojiPicker ด้วย Git subtree และยังรับ upstream updates แบบ manual ได้ ดู [provenance และขั้นตอนอัปเดต upstream](./docs/upstream/classic-picker.md) โค้ดที่นำเข้าถูกแยกเป็น Modern product identity แล้ว โดยยังเก็บที่มาและเครดิต upstream ครบถ้วน

## แพลตฟอร์มและข้อมูล

- Picker: .NET 10 WPF, self-contained win-x64
- แพลตฟอร์มหลักที่โครงการทดสอบ: Windows 10 22H2 x64
- Windows 11: ต้องผ่าน smoke test ก่อน release
- Emoji Baseline: Unicode/Emoji 17.0, CLDR 48.2 และ Noto Emoji v2.051

โครงการทดสอบ Windows 10 22H2 build 19045 ด้วยตนเอง แต่ Windows 10 รุ่นทั่วไปไม่อยู่ใน supported-OS matrix ปัจจุบันของ .NET 10 จึงต้องทดสอบ smoke และ regression บนเครื่องจริงทุกครั้งที่อัปเดต runtime

## Build foundation

ต้องมี .NET SDK feature band 10.0.4xx แล้วรัน:

    .\scripts\verify-foundation.ps1

คำสั่ง build และ regression จาก clean checkout อธิบายเพิ่มเติมใน [เอกสารสคริปต์](./scripts/README.md)

## ความเป็นส่วนตัว

Picker MVP ทำงานแบบ offline ไม่มี telemetry, analytics, cloud sync หรือ automatic update ข้อมูล Recent และ Learned Ranking อยู่ในเครื่องของผู้ใช้เท่านั้น

## License

โค้ดของโครงการใช้ [MIT License](./LICENSE) และรักษา attribution ของ upstream, agent skills และข้อมูล/asset ภายนอกตาม [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md)
