# การสร้าง Emoji Baseline

Emoji Baseline ที่ `data/emoji-baseline/17.0/` เป็น artifact กลางจาก source ที่ตรึงเวอร์ชันไว้ ไม่ใช่ข้อมูลที่เขียนแก้ด้วยมือ

## ไฟล์ผลลัพธ์

| ไฟล์ | หน้าที่ |
| --- | --- |
| `emoji.json` | Emoji Entry fully-qualified 3,944 รายการ พร้อม sequence, ลำดับ, group/subgroup, Emoji version, metadata ไทย/อังกฤษ และ asset mapping |
| `source-manifest.json` | เวอร์ชันและ provenance ของ source lock พร้อม SHA-256/ขนาดของ artifact ที่สร้าง |
| `review-report.json` | รายการเพิ่ม ลบ เปลี่ยน และ asset anomaly สำหรับ review การอัปเดต baseline |

Stable ID ใช้ canonical code points เช่น `emoji-1f600` และ `emoji-1f469-200d-1f4bb` จึงไม่ขึ้นกับชื่อภาษา ชื่อไฟล์ภาพ หรือลำดับการค้นหา ค่า `text` และ `canonicalSequence` ยังคง fully-qualified sequence เดิมจาก Unicode สำหรับ insert/copy

## Asset mapping

Generator อ่านรายการไฟล์จาก inventory ที่ตรวจ checksum แล้ว จากนั้นสร้าง key และ aliases ลงในแต่ละ Emoji Entry ตัว Picker ต้องอ่าน path จาก mapping นี้และห้ามประกอบชื่อไฟล์ Noto จาก sequence เอง

- Emoji ทั่วไปใช้ canonical PNG 128 สำหรับ grid และ 512 สำหรับ Hover Preview
- Noto เก็บธงประเทศและธงเขตย่อยไว้ใน `third_party/region-flags` แยกจาก canonical folders ธงทั้ง 262 รายการจึง map ไปยัง source PNG ที่มีความละเอียดสูงไฟล์เดียวสำหรับสองบทบาท โดยระบุ `sourceKind: noto-region-flag` และ `sharedSourceForSizes: true` อย่างชัดเจน
- ไฟล์ legacy 128 ที่ซ้ำกับ canonical name, component assets ที่ไม่ใช่ fully-qualified entry และ region flags นอก Emoji 17 จะไม่ถูกเลือกใช้ แต่ยังปรากฏใน `assetAnomalies` เพื่อให้ review ได้

## การสร้างและตรวจสอบ

หลัง restore แบบ locked แล้ว ใช้:

```powershell
.\scripts\generate-emoji-baseline.ps1
.\scripts\verify-generated-emoji-baseline.ps1
```

Verifier build generator โดยไม่ restore, รันสองครั้งใน temporary directories, เปรียบเทียบผลลัพธ์ byte-for-byte กับกันเองและไฟล์ที่ commit แล้ว จากนั้นตรวจ coverage ของ flags, keycaps, ZWJ, variation selectors, skin-tone variants, metadata, asset paths และ generated-file hashes

เมื่อต้องการรายงาน delta เทียบ baseline ก่อนหน้า ให้เก็บไฟล์เก่าไว้นอก output ปัจจุบันแล้วใช้:

```powershell
.\scripts\generate-emoji-baseline.ps1 -PreviousEmojiData <path-to-old-emoji.json>
```

การสร้างตามปกติเทียบกับ baseline ว่างอย่างคงที่ เพื่อให้ artifact เริ่มต้นสร้างซ้ำได้ byte-for-byte ส่วนการอัปเดตเวอร์ชันต้องแนบรายงานที่สร้างด้วย `-PreviousEmojiData` สำหรับ review ก่อน commit
