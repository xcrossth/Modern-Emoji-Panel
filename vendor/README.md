# Vendor assets

โฟลเดอร์นี้เก็บ source และ artwork ที่ตรึงเวอร์ชันเพื่อให้ ordinary build ทำงาน offline ได้

- `emoji-baseline/` เก็บ Unicode 17.0.0, Emoji 17.0 และ CLDR 48.2 พร้อม source lock
- `noto-emoji/v2.051/` เก็บ PNG canonical ขนาด 128/512, inventory hashes และ region-flags provenance

ตรวจไฟล์ทั้งหมดแบบ offline ด้วย:

    .\scripts\verify-emoji-baseline.ps1

รายละเอียดที่มาของข้อมูล, license และขั้นตอนอัปเดตอยู่ที่ [แหล่งข้อมูล Emoji Baseline](../docs/emoji-baseline-sources.md)
