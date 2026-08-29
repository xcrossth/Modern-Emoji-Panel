# ประกาศซอฟต์แวร์และข้อมูลจากบุคคลที่สาม

เอกสารนี้บันทึก attribution ของทรัพยากรที่มีอยู่ใน repository ปัจจุบัน

## Classic Emoji Picker

โค้ดรากฐานใต้ `apps/picker` นำเข้าจาก Classic Emoji Picker ที่ commit `56c54201e0673a57710c2498db25a149b45e63ec`

- Source: https://github.com/platima/Classic-EmojiPicker
- License: MIT
- Copyright (c) 2025 Platima
- License และ notices ต้นฉบับ: `apps/picker/LICENSE` และ `apps/picker/THIRD-PARTY-NOTICES.md`

รายละเอียด provenance และ manual update flow อยู่ที่ `docs/upstream/classic-picker.md`

## Unicode 17.0.0 และ CLDR 48.2

ข้อมูล Unicode Emoji, Unicode Character Database และ CLDR ภาษาไทย/อังกฤษที่ตรึงไว้ใน Emoji Baseline มาจาก Unicode Consortium

- Unicode version: 17.0.0
- Unicode Emoji version: 17.0
- CLDR version: 48.2
- License: Unicode-3.0
- Source lock: `vendor/emoji-baseline/sources.lock.json`
- License text: `vendor/emoji-baseline/licenses/UNICODE-LICENSE-V3.txt`

Unicode, Inc. สงวนลิขสิทธิ์ตามข้อความต้นฉบับ การแจกและใช้งานข้อมูลอยู่ภายใต้ Unicode License V3

## Noto Emoji v2.051

ภาพ PNG canonical ขนาด 128 และ 512 px มาจาก Noto Emoji ที่ commit `8998f5dd683424a73e2314a8c1f1e359c19e8742`

- Source: https://github.com/googlefonts/noto-emoji
- Version: v2.051
- License class: Apache-2.0 สำหรับ artwork ที่นำมาใช้
- Artwork notice: `vendor/noto-emoji/v2.051/licenses/ARTWORK-LICENSE.txt`
- Apache License 2.0: `vendor/noto-emoji/v2.051/licenses/APACHE-2.0.txt`

Copyright 2013 Google, Inc. All Rights Reserved.

## Noto region-flags

ภาพธงที่มากับ Noto Emoji มี provenance จากโครงการ `googlei18n/region-flags` ที่ commit `743e1f4a92b7d2dac49d7e6af509af63a71f0b45` และประกาศเป็น Public Domain หรือได้รับการยกเว้นลิขสิทธิ์ตามรายละเอียดของแต่ละธง

- Provenance: `vendor/noto-emoji/v2.051/third_party/region-flags/README.third_party`
- Authors: `vendor/noto-emoji/v2.051/third_party/region-flags/AUTHORS`
- Public Domain notice: `vendor/noto-emoji/v2.051/third_party/region-flags/LICENSE`

## Matt Pocock Skills

ไฟล์ภายใต้ .agents/skills และข้อมูลที่เกี่ยวข้องใน skills-lock.json มาจากโครงการ mattpocock/skills

- Source: https://github.com/mattpocock/skills
- License: MIT
- Copyright (c) 2026 Matt Pocock

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
