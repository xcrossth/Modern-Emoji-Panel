# บันทึกการเปลี่ยนแปลง Modern Emoji Picker

ไฟล์นี้บันทึกผลิตภัณฑ์ Modern เท่านั้น ประวัติ Classic Emoji Picker ตรวจได้จาก import history และ [`docs/upstream/classic-picker.md`](../../docs/upstream/classic-picker.md)

## 0.1.9 — 30 สิงหาคม 2026

### Foundation และข้อมูล

- แยก identity, process, registry และพื้นที่ข้อมูลของ Modern ออกจาก Classic
- ย้าย solution เป็น .NET 10 พร้อม central/locked dependencies
- pin Unicode Emoji 17, CLDR ไทย/อังกฤษ และ Noto Emoji v2.051 พร้อม checksum/license metadata
- เพิ่ม deterministic generator และ baseline 3,944 fully-qualified sequences

### Picker workflow

- ใช้ภาพ Noto 128 px ใน grid และ 512 px ใน preview พร้อม bounded lazy cache
- เพิ่ม search ไทย–อังกฤษ, Hover Preview, skin/mixed-tone variants และ Learned Ranking ภายใน match tier
- เพิ่ม Picker Session, captured-target validation, insertion modes, bounded FIFO queue และ Typing Handoff
- รองรับ Recent MRU 50, atomic persistence/recovery และ prune เฉพาะ stable identifier ที่หายจาก baseline
- เพิ่ม Settings/Welcome, language, theme/High Contrast, hotkey, autostart และ privacy controls

### Qualification และ package

- เพิ่ม regression suite, performance budgets, accessibility wiring checks และ runtime socket observation
- เพิ่ม product icon ใหม่พร้อม ICO หลายขนาดและภาพ 512 px
- เพิ่ม local-only package pipeline สำหรับ self-contained per-user installer และ portable ZIP
- ถอด lite/framework-dependent, MSI และ GitHub-hosted release workflow ออกจาก Modern MVP
- เผยแพร่ Public MVP ผ่าน GitHub Release พร้อมตัวติดตั้ง, portable ZIP, checksum, license และ notices
- เก็บผล manual/automated qualification และกรณีที่ยังไม่ครอบคลุมไว้ใน `docs/qualification`
