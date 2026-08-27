# Throwaway spike: Noto Emoji 128 px เทียบ 512 px ใน WPF grid

> **THROWAWAY — ห้ามย้ายโค้ดนี้เข้า production โดยตรง**

คำถามของ spike นี้คือ เมื่อแสดง Noto Emoji v2.051 ในช่องขนาด 32 DIP บน DPI 100–250% การใช้ PNG ต้นทาง 128 px กับ 512 px ผ่านเส้นทาง WPF ที่ตั้งใจใช้จริง ให้ความคม รายละเอียด contrast/รอยหยัก และต้นทุน decode/render ต่างกันอย่างไร

## ขอบเขตและแหล่งข้อมูล

- Pin tag `v2.051` ซึ่งชี้ผ่าน annotated tag `6202fe7c20dd5e1727a4c3c01604edc176c576da` ไป commit `8998f5dd683424a73e2314a8c1f1e359c19e8742`
- ใช้ PNG จาก `png/128/` และ `png/512/` ของ commit เดียวกัน
- เลือก 9 ภาพ: DNA, gear, mirror ball, chequered flag, woman technologist ผิวระดับกลาง, family ZWJ, butterfly, bicycle และ eye in speech bubble
- ครอบคลุม fine detail, hard contrast, curves, flag, ล้อ/ซี่บาง, ZWJ และ skin tone
- country flag ไม่มี PNG 128/512 ที่ path เดียวกับ emoji ทั่วไปใน source tree นี้ จึงใช้ chequered flag ซึ่งเป็น PNG upstream โดยตรง เพื่อไม่ผสมขั้นตอน rasterize SVG เข้าในการเทียบ

## เส้นทาง WPF ที่ทดสอบ

1. เปิดไฟล์ด้วย `BitmapImage`
2. ตั้ง `BitmapCacheOption.OnLoad`
3. ตั้ง `DecodePixelWidth` เท่ากับจำนวน physical pixels ของ 32 DIP: 32, 40, 48, 56, 64, 72 และ 80 px
4. ใส่ลง `Image` ขนาด 32×32 DIP, `Stretch=Uniform`, `SnapsToDevicePixels=true`, `UseLayoutRounding=true`
5. ตั้ง `RenderOptions.BitmapScalingMode=HighQuality`
6. render ด้วย `RenderTargetBitmap` ที่ DPI 96–240 ตาม scale จริง

เส้นทางนี้ตั้งใจจำลอง candidate ของ grid จริง ไม่ใช่ย่อภาพด้วยไลบรารีภาพคนละตัว

## วิธีรันซ้ำ

ต้องมี .NET 10 SDK และรันบน Windows ที่มี WPF:

```powershell
./run.ps1
```

script จะดาวน์โหลดเฉพาะ 18 ไฟล์ที่ pin ไว้ ตรวจ SHA-256 ลง `asset-manifest.json`, build/run แบบ Release แล้วเขียนผลใต้ `results/`

## ไฟล์ผลลัพธ์

- `results/comparison-native.png` — ทุก DPI ในแผ่นเดียว โดย 1 pixel ของภาพ render เท่ากับ 1 pixel ในแผ่น
- `results/comparison-zoom4x-*.png` — ขยาย nearest-neighbor 4 เท่าเพื่อดูโครง pixel โดยไม่ใส่ interpolation รอบสอง
- `results/rendered-grid-icons/` — PNG ผล render จริงราย emoji/source/DPI
- `results/performance-metrics.csv` — median/P95 ของ warm decode และ WPF render, managed allocation proxy และ decoded-pixel memory proxy
- `results/visual-metrics.csv` — ความต่างราย pixel และ edge-energy proxy บนพื้นขาว
- `results/metrics.json` — ข้อมูลชุดเดียวกันในรูป JSON

## ข้อจำกัดของการวัด

- เวลาเป็น warm local-file benchmark บนเครื่องเดียว ไม่ใช่ cold disk และไม่ใช่ scroll benchmark ของ virtualized grid ทั้งหน้า
- `managedDecodeAllocationMedianBytes` นับเฉพาะ managed allocation บน thread ปัจจุบัน จึงไม่ใช่ขนาด native WIC cache
- `decodedPixelBytesProxyNineTiles` คือ `จำนวนภาพ × กว้าง × สูง × 4` เป็น proxy แบบ deterministic ไม่ใช่ process working set
- edge energy บอกระดับความเปลี่ยนแปลงของ luminance ระหว่าง pixel ข้างเคียง ไม่สามารถตัดสินว่า “สวยกว่า” หรือ “หยักกว่า” ได้เอง ต้องอ่านร่วมกับ comparison sheets
- spike นี้ไม่มีผลเปลี่ยน production, spec หรือ `CONTEXT.md`

## ผลและข้อสรุป

รันบน Windows 10 build 19045, Intel Core i9-10900K, .NET SDK 10.0.400 / Windows Desktop runtime 10.0.11 แบบ Release โดยแต่ละ source/DPI มีตัวอย่างเวลา 216 ครั้ง

### Fact จากการรัน

- ที่ขนาดจริง 32–80 physical pixels ภาพคู่ 128/512 ใกล้กันมาก แต่ไม่เหมือนกันทั้งหมด ค่า mean absolute difference เฉลี่ยต่อช่องสีอยู่ที่ 1.25–1.81 จากช่วง 0–255 หรือประมาณ 0.49–0.71% ของ full scale
- edge-energy เฉลี่ยของ 512 เทียบกับ 128 แกว่งตั้งแต่ -0.62% ถึง +0.56% ตาม DPI และเมื่อดูรายภาพอยู่ในช่วง -3.71% ถึง +1.88% จึงไม่พบแนวโน้มว่า 512 ทำให้ contrast ของขอบสูงขึ้นทุกกรณี
- ความต่างเห็นง่ายสุดในกลุ่มเส้นเล็ก/รายละเอียดถี่ เช่น bicycle; mirror ball จาก 512 กลับมี edge energy ต่ำกว่า 128 ในหลาย DPI จึงเป็นตัวอย่างว่า source ใหญ่ไม่ได้แปลว่าผลย่อจะคม/contrast สูงกว่าเสมอ
- warm decode median ของ 128 อยู่ที่ 0.309–0.320 ms ต่อภาพ ส่วน 512 อยู่ที่ 1.524–1.634 ms ต่อภาพ หรือช้าประมาณ 4.87–5.25 เท่าในเครื่องนี้
- หลัง decode แล้ว WPF render median ใกล้กัน: 128 อยู่ที่ 0.155–0.226 ms และ 512 อยู่ที่ 0.157–0.222 ms เพราะ output มีขนาด pixel เท่ากัน
- managed allocation proxy ของการ decode เท่ากันที่ median 5,072 bytes ในการวัดนี้ ส่วน decoded-pixel memory proxy ของ 9 tile เท่ากันทั้งสอง source และเพิ่มจาก 36,864 bytes ที่ 100% เป็น 230,400 bytes ที่ 250%
- ไฟล์ตัวอย่าง 9 ภาพรวม 51,289 bytes สำหรับ 128 และ 230,371 bytes สำหรับ 512 หรือ 512 ใหญ่ประมาณ 4.49 เท่า ตัวเลขนี้ไม่ใช่ขนาด bundle เต็มชุด

### Inference / คำแนะนำจาก spike

- สำหรับ **คุณภาพใน grid อย่างเดียว** ยังไม่มีหลักฐานว่า 512 ชนะอย่างสม่ำเสมอ และไม่มีหลักฐานว่ามันทำให้รอยหยัก/contrast สูงเกินไปอย่างเป็นระบบ ภาพต่างกันตาม artwork และ target pixel size มากกว่า
- ถ้ายอมเพิ่ม asset 128 อีกชุดได้ แนวทาง `128 สำหรับ grid + 512 สำหรับ hover preview` มีเหตุผลที่สุดในด้าน latency เพราะได้ภาพ grid ที่อย่างน้อยสูสีกัน พร้อมลด decode time ต่อ tile ราว 5 เท่า
- ถ้าต้องลดขนาด package และใช้ 512 ชุดเดียว ภาพ grid จากเส้นทางที่ทดสอบยังดูดีและไม่แสดงปัญหารอยหยักเป็นระบบ แต่ควรยืนยันต่อด้วย scroll/virtualization benchmark ทั้ง viewport เพราะ spike นี้วัดราย tile เท่านั้น
- อย่าเลือกจาก edge-energy เพียงตัวเดียว การตัดสินด้านความสวยควรเปิด `comparison-native.png` ที่ zoom 100% และใช้ `comparison-zoom4x-*.png` ตรวจ pixel structure ประกอบ
