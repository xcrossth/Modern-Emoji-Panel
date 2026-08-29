# แหล่งข้อมูล Emoji Baseline

Emoji Baseline ของ Picker MVP ตรึงข้อมูลและ artwork ต่อไปนี้เพื่อให้ build และ runtime ทำงานแบบ deterministic และ offline:

| Source | Version | เนื้อหาที่เก็บ | License |
| --- | --- | --- | --- |
| Unicode Character Database | 17.0.0 | Emoji properties, variation sequences และ grapheme break data/tests | Unicode-3.0 |
| Unicode Emoji | 17.0 | emoji-test, sequences และ ZWJ sequences | Unicode-3.0 |
| CLDR | 48.2 | annotations และ annotationsDerived ภาษาไทย/อังกฤษ | Unicode-3.0 |
| Noto Emoji | v2.051 / `8998f5dd683424a73e2314a8c1f1e359c19e8742` | PNG canonical 128 และ 512 px | Apache-2.0 สำหรับ artwork |
| region-flags | `743e1f4a92b7d2dac49d7e6af509af63a71f0b45` | ภาพธงและ provenance ที่ Noto ใช้ | Public Domain ตามประกาศต้นทาง |

source lock ที่ `vendor/emoji-baseline/sources.lock.json` บันทึก immutable URL, version, commit/tree เมื่อมี, SHA-256, byte length, license class และปลายทางของทุก source ส่วน asset จำนวนมากใช้ inventory ที่บันทึก hash และขนาดของแต่ละไฟล์

## การตรวจสอบปกติ

ใช้คำสั่งต่อไปนี้โดยไม่ต้องเชื่อมต่อเครือข่าย:

    .\scripts\verify-emoji-baseline.ps1

verifier จะตรวจ source lock และไฟล์ที่ vendor ไว้ทั้งหมด รวมถึงยืนยันว่าไม่มี URL แบบ `latest`, `draft`, `beta`, `main` หรือ `master` และภาพไม่ได้ผ่าน Git LFS

## การอัปเดต

การเปลี่ยน baseline ต้องเป็นการกระทำโดยตั้งใจเท่านั้น:

    .\scripts\update-emoji-baseline.ps1

ก่อนเปลี่ยนเวอร์ชัน ต้องแก้ source lock จากแหล่งต้นทางที่เชื่อถือได้ ตรวจทาน license/provenance และสร้าง inventory ใหม่อย่างตรวจสอบได้ สคริปต์ดาวน์โหลดลง temporary staging, ตรวจ checksum และ Git commit/tree ก่อนคัดลอกเข้าที่จริง จากนั้นเรียก offline verifier ซ้ำ

ห้ามเรียก update command จาก ordinary build, test, runtime หรือ release build และห้ามเปลี่ยน source lock ไปใช้ moving reference
