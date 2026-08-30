# SPEC 02 — Chrome Emoji Renderer Fix for Windows 10

## 1. เป้าหมายของงาน

สร้างโปรเจกต์แยกจาก Emoji Picker เพื่อแก้ปัญหา:

> Windows 10 สามารถรับ Unicode Emoji ใหม่ได้ แต่ Chrome/เว็บไซต์บางเว็บ render Emoji ใหม่เป็นสี่เหลี่ยม, tofu หรือแสดง sequence ไม่สมบูรณ์ เพราะ fallback ไปใช้ Emoji font ของ Windows 10 ที่เก่า

Target หลัก:

- หน้า DM ของ Instagram Web
- หน้าแชทของ TikTok Web
- Chrome บน Windows 10

เป้าหมายคือ:

```text
Unicode Emoji ใหม่
        ↓
Chrome page
        ↓
render ด้วย Noto Emoji
แทนการพึ่ง Segoe UI Emoji ของ Windows 10
```

โปรเจกต์นี้ต้องทำงานแยกจาก Classic Emoji Picker fork

Picker มีหน้าที่ "ส่ง Unicode"

โปรเจกต์นี้มีหน้าที่ "ทำให้ Chrome render Unicode ใหม่ได้ถูก"

## 1.1 ส่วนขยายหลัง MVP

ตั้งแต่ source รุ่น `0.0.3` ขอบเขตเว็บไซต์หลักเพิ่ม Facebook Messages และ Messenger.com โดยต้องรองรับกรณีที่ Meta แปลง Unicode Emoji เป็น `<img>` จาก CDN ของตนเอง ทั้ง Emoji เดี่ยว, Emoji ในข้อความ bubble และ reaction ตัว Renderer ต้องอ่าน Emoji จาก `alt`, แสดงด้วย Noto, รักษาขนาดเดิม และไม่แก้รูปโปรไฟล์ รูปทั่วไป หรือ Editable Content

ขอบเขต Instagram/TikTok ด้านล่างยังเป็น baseline ของ MVP เดิม ส่วน Facebook/Messenger เป็นข้อกำหนดเพิ่มเติมหลัง MVP

---

# 2. Recommended Solution

ทำเป็น **Chrome Extension (Manifest V3)**

เหตุผล:

- ไม่ต้อง patch Windows system font
- ไม่ต้อง replace `Segoe UI Emoji`
- ไม่ต้องแก้ Chrome binary
- เปิด/ปิดได้
- จำกัดเฉพาะ domain ได้
- update Emoji renderer แยกจากตัว Picker
- deploy ได้ง่าย
- rollback ง่าย
- เหมาะกับ Instagram DM และ TikTok Web Chat โดยตรง

ชื่อชั่วคราว:

```text
Modern Emoji Renderer
```

---

# 3. Non-Goals

v1 ไม่ต้อง:

- แก้ Emoji ทุกโปรแกรมใน Windows
- replace Windows system font
- patch registry font substitution
- inject DLL เข้า Chrome
- ใช้ Apple Emoji
- เปลี่ยน Emoji ใน screenshot/image/video
- render Emoji ภายใน native Windows app
- รองรับ Firefox/Edge ถ้ายังไม่จำเป็น

Chrome/Chromium เป็น scope หลัก

---

# 4. Core Rendering Approach

แนะนำใช้:

**Google Noto Color Emoji**

Repository:

- https://github.com/googlefonts/noto-emoji

ใช้ Noto เป็น embedded extension asset

หลักการ:

```text
Text Node
"Hello 🫩 world"
      ↓
Emoji detector
      ↓
"Hello " + <span class=modern-emoji>🫩</span> + " world"
                         ↓
              Noto Color Emoji font
```

ผลคือ:

- surrounding text ใช้ font เดิมของเว็บ
- Emoji เฉพาะ grapheme cluster ใช้ Noto
- copy text ยังได้ Unicode เดิม
- search text ยังเห็น Unicode
- accessibility ทำได้ดีกว่าการแทนด้วย `<img>`

---

# 5. Why Not Global CSS Only

ห้ามใช้วิธี:

```css
* {
  font-family: "Noto Color Emoji";
}
```

เพราะจะทำลาย typography ของเว็บ

และการทำ:

```css
font-family: site-font, "Noto Color Emoji";
```

กับทุก element อาจยังไม่ deterministic เพราะ:

- site มี font chain ซับซ้อน
- CSS specificity
- shadow DOM
- inline styles
- emoji font fallback order
- Segoe UI Emoji อาจถูกเลือกก่อนในบาง case

ดังนั้น v1 ให้ใช้ targeted wrapping ของ Emoji sequence

---

# 6. Emoji Detection

ต้อง detect เป็น **grapheme cluster** ไม่ใช่ทีละ code point

ต้องรองรับ:

- single code point emoji
- variation selector
- skin tone modifier
- ZWJ sequence
- gender sequence
- family sequence
- keycap
- flag / regional indicator
- tag sequence
- newer Unicode emoji

แนะนำใช้:

```javascript
Intl.Segmenter
```

ด้วย granularity:

```text
grapheme
```

แล้วตรวจแต่ละ grapheme ว่าเป็น Emoji หรือไม่

สามารถใช้:

```javascript
/\p{Extended_Pictographic}/u
```

ร่วมกับ logic สำหรับ:

- Regional Indicator flags
- keycap
- variation selector
- emoji presentation

ควรมี unit tests จำนวนมาก

---

# 7. DOM Transformation

สำหรับ text node ปกติ:

```html
<div>Hello 🫩 world</div>
```

ให้แปลงเป็น:

```html
<div>
  Hello
  <span class="modern-emoji">🫩</span>
  world
</div>
```

CSS:

```css
.modern-emoji {
    font-family: "Noto Color Emoji" !important;
    font-style: normal !important;
    font-weight: normal !important;
}
```

ห้ามเปลี่ยน Unicode content ของ Emoji

---

# 8. Font Packaging

bundle Noto Color Emoji font มากับ extension ตาม license ที่อนุญาต

ตัวอย่าง:

```text
extension/
  assets/
    fonts/
      NotoColorEmoji.ttf
```

หรือ format ที่ Chrome render ได้ดีที่สุด เช่น WOFF2 ถ้าสามารถ build ได้อย่างถูกต้อง

ใช้:

```css
@font-face {
  font-family: "ModernEmojiNoto";
  src: url("chrome-extension://.../assets/fonts/...");
}
```

ควรตั้ง alias ของเราเอง เช่น:

```text
ModernEmojiNoto
```

ไม่ใช้ชื่อ generic ที่อาจชนกับ local installed font

---

# 9. Alternative Renderer

ถ้า Color Font compatibility มีปัญหา ให้มี fallback:

```text
SVG/PNG renderer
```

แต่ไม่ใช่ทางเลือกแรก

Fallback image mode สามารถ:

```text
Unicode sequence
    ↓
asset key
    ↓
Noto SVG/PNG
```

ต้อง preserve:

- selectable/copyable Unicode
- accessibility label
- layout baseline

ตัวอย่าง:

```html
<span class="modern-emoji-image"
      data-emoji="🫩"
      aria-label="face with bags under eyes">
   ...
</span>
```

แต่ v1 ให้พยายามใช้ font renderer ก่อน

---

# 10. Dynamic Pages

Instagram และ TikTok เป็น SPA และ DOM ของหน้าแชทเปลี่ยนตลอด

ต้องใช้:

```javascript
MutationObserver
```

เฝ้า:

- added nodes
- updated text nodes

แต่ต้องไม่ scan ทั้ง document ทุกครั้ง

ใช้ queue/batching:

```text
MutationObserver
    ↓
collect changed roots
    ↓
requestIdleCallback / microtask
    ↓
process only changed subtree
```

---

# 11. Performance Requirements

ห้าม:

```text
Mutation ทุกครั้ง → document.body full scan
```

เพราะหน้าแชทที่มีประวัติข้อความยาวหรือมีข้อความเข้าใหม่ต่อเนื่องจะ lag

ควรใช้:

- TreeWalker
- text node filtering
- batch mutations
- WeakSet / marker
- skip already processed subtree
- skip hidden/script/style nodes
- incremental processing

---

# 12. Nodes to Skip

ห้าม rewrite text ภายใน:

```text
SCRIPT
STYLE
NOSCRIPT
TEXTAREA
INPUT
CODE
PRE
```

`contenteditable` ต้องระวังเป็นพิเศษ

v1 แนะนำ:

```text
อย่า wrap text node ที่อยู่ภายใน contenteditable
```

เพื่อไม่ให้ cursor/selection/composition ของ editor พัง

---

# 13. Editable Fields

เป้าหมายหลักคือ display rendering หลัง post แล้ว

สำหรับ:

```text
input
textarea
contenteditable
```

v1 ไม่จำเป็นต้อง rewrite text

ถ้าต้องแก้ appearance ใน composer ภายหลัง ให้ทำเป็น Phase 2

เหตุผล:

- DOM mutation ใน contenteditable ทำให้ cursor กระโดด
- controlled editor ของ Instagram/TikTok อาจพัง
- IME อาจพัง
- selection state ซับซ้อน

---

# 14. Primary Chat Target Behavior

ต้องทดสอบ:

```text
Instagram DM: ข้อความในห้องสนทนาและข้อความที่โหลดเพิ่ม
Instagram DM: รายการสนทนาและ preview ข้อความ
TikTok Web Chat: ข้อความในห้องสนทนาและข้อความที่โหลดเพิ่ม
TikTok Web Chat: รายการสนทนาและ preview ข้อความ
ข้อความที่ส่งเองและข้อความที่ได้รับใหม่แบบ dynamic
ข้อความไทย/อังกฤษที่มี Emoji ปนกัน
```

scope priority:

```text
P0: Instagram DM + TikTok Web Chat
P1: รายการสนทนา, message preview และข้อความที่โหลดเพิ่มย้อนหลัง
P2: Instagram feed/comments และหน้า display content อื่น
```

---

# 15. General Site Mode

Extension ควรมี 2 mode:

```text
1. Primary chat sites
2. All sites
```

Default:

```text
Instagram + TikTok
```

เพื่อ minimize compatibility risk

User สามารถ enable per-site ได้จาก options

---

# 16. Extension Settings

แนะนำ:

```json
{
  "enabled": true,
  "mode": "allowlist",
  "sites": [
    "instagram.com",
    "tiktok.com"
  ],
  "emojiStyle": "noto",
  "processDynamicContent": true
}
```

ภายหลังเพิ่ม:

```text
facebook.com
threads.net
discord.com
web.telegram.org
```

---

# 17. Popup UI

Browser action popup แบบเล็ก:

```text
Modern Emoji Renderer

[✓] Enabled on this site

Style:
Noto / Android

Status:
127 emoji nodes fixed on this page
```

ไม่ต้องใหญ่

---

# 18. Options Page

ให้มี:

- enable/disable
- allowlist
- denylist
- reset settings
- debug mode
- renderer mode
- version info
- Unicode/Noto data version

---

# 19. Architecture

แนะนำโครงสร้าง:

```text
chrome-extension/
  manifest.json

  src/
    background/
      service-worker.js

    content/
      index.js
      observer.js
      walker.js
      emoji-detector.js
      emoji-wrapper.js

    popup/
      popup.html
      popup.js
      popup.css

    options/
      options.html
      options.js
      options.css

  assets/
    fonts/
      NotoColorEmoji...
    icons/

  data/
    emoji-data.json

  tests/
    detector.test.js
    wrapper.test.js
```

ใช้ TypeScript ได้ถ้าทำให้ maintain ง่ายขึ้น

แนะนำ:

```text
TypeScript + Vite
```

หรือ build system เบา ๆ

---

# 20. Manifest V3

ต้องใช้ Manifest V3

permissions ให้น้อยที่สุด

ตัวอย่างแนวคิด:

```json
{
  "manifest_version": 3,
  "name": "Modern Emoji Renderer",
  "permissions": [
    "storage"
  ],
  "host_permissions": [
    "https://www.instagram.com/*",
    "https://www.tiktok.com/*"
  ]
}
```

ถ้ารองรับ all-sites ให้ขอ host permission แบบ optional ถ้าเป็นไปได้

---

# 21. Security

ห้าม:

- remote JavaScript
- eval
- download executable code
- external script injection
- arbitrary HTML injection

assets ทั้งหมด bundle มากับ extension

DOM replacement ต้องสร้าง node ด้วย DOM API ไม่ใช้ unsafe `innerHTML` ถ้าไม่จำเป็น

---

# 22. Content Processing Pipeline

```text
Page loads
   ↓
Read extension settings
   ↓
Is site enabled?
   ↓ yes
Initial tree scan
   ↓
Find text nodes
   ↓
Segment grapheme
   ↓
Contains emoji?
   ↓ yes
Wrap Emoji cluster
   ↓
Noto renderer

MutationObserver
   ↓
new/changed nodes
   ↓
incremental scan
```

---

# 23. Avoid Double Processing

ทุก generated wrapper ให้ mark:

```html
<span data-modern-emoji="1">
```

Walker ต้อง skip node ที่อยู่ใต้:

```text
[data-modern-emoji]
```

เพื่อไม่ wrap ซ้ำ

---

# 24. Text Integrity

ก่อน:

```text
ABC🫩DEF
```

หลัง DOM transform:

```text
ABC<span>🫩</span>DEF
```

`textContent` ของ parent ต้องยังเท่ากับ:

```text
ABC🫩DEF
```

เมื่อ copy selection แล้วต้องได้ Unicode เดิม

---

# 25. Layout

Emoji wrapper ต้อง:

- inline
- baseline align
- ไม่เพิ่ม line height แบบผิดปกติ
- size ตาม surrounding text
- รองรับ zoom
- รองรับ HiDPI

แนะนำ:

```css
.modern-emoji {
  display: inline;
  font-family: "ModernEmojiNoto" !important;
  font-size: 1em;
  line-height: inherit;
  font-weight: normal !important;
  font-style: normal !important;
  font-variant: normal !important;
}
```

ต้อง test จริงกับ Instagram DM และ TikTok Web Chat

---

# 26. Skin Tone / ZWJ

ต้อง wrap ทั้ง grapheme cluster เป็น span เดียว

ถูก:

```html
<span>👩🏽‍💻</span>
```

ผิด:

```html
<span>👩</span>
<span>🏽</span>
<span>‍</span>
<span>💻</span>
```

---

# 27. Copy/Paste

copy จากหน้าเว็บต้องได้:

```text
Unicode เดิม
```

ไม่ใช่ URL ของ image หรือ alt text

นี่เป็นเหตุผลที่ font-based renderer เป็น primary solution

---

# 28. Search / Accessibility

Wrapper ควรไม่ทำลาย:

- browser find
- screen reader
- DOM text extraction
- copy/paste
- selection

ถ้าใช้ span text ตรง ๆ ส่วนใหญ่จะ preserve ได้

---

# 29. SPA Navigation

Instagram และ TikTok เปลี่ยน route หรือห้องสนทนาโดยไม่ reload

ต้อง detect:

```text
history.pushState
popstate
```

หรือ rely on MutationObserver + root persistence

เมื่อ navigate:

- ไม่สร้าง duplicate observer
- ไม่ full reload extension state
- process new page content

---

# 30. Shadow DOM

v1 ไม่จำเป็นต้องรองรับ closed shadow root

ถ้ามี open shadow root และพบว่าเว็บ target ใช้จริง สามารถเพิ่ม recursive observer ภายหลัง

---

# 31. Unicode Data

แนะนำใช้ Emoji data generator ร่วมแนวคิดกับ SPEC 01

แต่โปรเจกต์ Chrome ควร self-contained

สามารถ:

```text
shared tooling
```

ได้ภายหลัง

v1 อาจ copy generated `emoji-data.json` จาก repo tools เดียวกัน

อย่า hardcode รายชื่อ Emoji ใน regex ยาว ๆ ถ้าไม่จำเป็น

---

# 32. Update Strategy

ควรมี script:

```text
scripts/
  update-noto.ps1
  update-unicode-data.ps1
  build-extension.ps1
```

ทุก release ระบุ:

```text
Unicode version
Noto version
Extension version
```

---

# 33. License

ต้อง include:

- Noto license
- attribution
- extension source license

ห้าม bundle Apple Color Emoji

---

# 34. Debug Mode

เพิ่ม optional debug:

```text
processed text nodes
wrapped emoji count
mutation batches
processing time
skipped editable nodes
```

แสดงใน console เมื่อเปิด debug เท่านั้น

Default ปิด

---

# 35. Performance Budget

เป้าหมายคร่าว ๆ:

Initial scan ของห้องแชทที่มีประวัติยาวต้องไม่ freeze UI อย่างเห็นได้ชัด

Mutation processing ต่อ batch ควรสั้น

ถ้าหน้าใหญ่มากให้ split work ด้วย:

```javascript
requestIdleCallback
```

fallback:

```javascript
setTimeout(..., 0)
```

---

# 36. Test Cases

Detector test:

```text
😀
🫩
❤️
👩🏽‍💻
👨‍👩‍👧
1️⃣
🇹🇭
🏳️‍🌈
```

Mixed text:

```text
Hello 😀 world
ภาษาไทย 🫩 ทดสอบ
abc👩🏽‍💻def
```

No emoji:

```text
ภาษาไทยธรรมดา
English text
12345
```

---

# 37. Browser Test Matrix

Minimum:

```text
Chrome stable / Windows 10
Chrome stable / Windows 11
```

Target websites:

```text
Instagram Web DM
TikTok Web Chat
```

Regression websites:

```text
Instagram feed/comments
Google
GitHub
Reddit
Facebook
Discord Web
```

เมื่อ all-sites mode เปิด

---

# 38. Primary Chat Test Matrix

ต้องทดสอบ:

- [ ] Instagram DM: ข้อความที่ส่งเองและข้อความที่ได้รับ
- [ ] Instagram DM: เปลี่ยนห้อง, โหลดประวัติย้อนหลัง และรับข้อความใหม่
- [ ] TikTok Web Chat: ข้อความที่ส่งเองและข้อความที่ได้รับ
- [ ] TikTok Web Chat: เปลี่ยนห้อง, โหลดประวัติย้อนหลัง และรับข้อความใหม่
- [ ] ข้อความที่มีเฉพาะ Emoji
- [ ] Thai + Emoji
- [ ] English + Emoji
- [ ] new Emoji not supported by Win10
- [ ] ZWJ Emoji
- [ ] skin tone
- [ ] เลื่อนอ่านข้อความย้อนหลัง
- [ ] route/conversation navigation
- [ ] conversation list/message preview
- [ ] scrollingเร็ว
- [ ] เปิด/ปิด Extension ระหว่างอยู่ในห้องสนทนา
- [ ] composer และ Thai IME ไม่พังแม้ v1 จะไม่แก้ Emoji ขณะพิมพ์

---

# 39. Interaction with Picker

Scenario สำคัญ:

```text
Modern Emoji Picker
    ↓
click new Emoji
    ↓
Unicode inserted into Instagram DM หรือ TikTok Web Chat composer
    ↓
user sends message
    ↓
เว็บไซต์แสดงข้อความใน conversation transcript
    ↓
Chrome extension wraps Emoji
    ↓
Noto glyph shown
```

สองโปรเจกต์ไม่ควร depend กันโดยตรง

ถ้าไม่มี Picker:

```text
paste Unicode Emoji จากที่อื่น
```

extension ก็ต้อง render ได้

---

# 40. Limitations

ต้องระบุใน README:

1. Extension แก้เฉพาะการ render ใน browser page
2. Server ของเว็บไซต์อาจ normalize/filter Unicode บาง sequence
3. input/editor บางชนิดอาจยังแสดง tofu ขณะพิมพ์ใน v1
4. หลัง submit แล้ว display content จะถูกแก้
5. canvas-rendered text ไม่สามารถแก้ด้วย DOM wrapper
6. image/video/screenshot ไม่เกี่ยว
7. closed shadow DOM อาจไม่รองรับ

---

# 41. Phase 2 — Optional Editable Rendering

ถ้าต้องการให้ Instagram DM หรือ TikTok Web Chat composer แสดง Emoji ใหม่ถูกขณะพิมพ์:

ศึกษาแยกเป็น phase 2

แนวทาง:

```text
contenteditable-specific CSS/font strategy
```

หลีกเลี่ยง DOM wrapping ระหว่าง edit

อาจใช้:

```text
font-family override เฉพาะ editor
```

ด้วย font chain ที่ preserve normal text

ต้อง test IME ภาษาไทยด้วย

---

# 42. Phase 2 — Other Chromium Browsers

ภายหลังรองรับ:

```text
Edge
Brave
Vivaldi
Chromium
```

ถ้า Manifest V3 compatible ส่วนมากใช้ code เดียวกันได้

---

# 43. Acceptance Criteria

งาน v1 ถือว่าผ่านเมื่อ:

- [ ] Chrome extension load แบบ unpacked ได้
- [ ] Instagram DM render Emoji ใหม่ด้วย Noto ในข้อความที่ส่งและได้รับ
- [ ] TikTok Web Chat render Emoji ใหม่ด้วย Noto ในข้อความที่ส่งและได้รับ
- [ ] Emoji ที่ Win10 เดิมขึ้น tofu แสดงถูกใน conversation transcript
- [ ] surrounding Thai/English font ไม่เปลี่ยน
- [ ] copy text ยังได้ Unicode เดิม
- [ ] เลื่อนประวัติแชทและเปลี่ยนห้องไม่ทำให้ lag รุนแรง
- [ ] dynamic messages และประวัติที่โหลดทีหลังถูก render
- [ ] ไม่มี duplicate wrapping
- [ ] contenteditable ไม่พัง
- [ ] extension เปิด/ปิดต่อ site ได้
- [ ] popup แสดงสถานะได้
- [ ] Manifest V3
- [ ] ไม่มี remote code
- [ ] license ครบ

---

# 44. Deliverables

Codex ต้องส่ง:

1. source code
2. Manifest V3 extension
3. Noto font/asset integration
4. Emoji grapheme detector
5. DOM processor
6. MutationObserver pipeline
7. site enable/disable setting
8. popup
9. options page
10. tests
11. build script
12. README
13. license/attribution
14. packaged ZIP สำหรับ load/install manual
15. known limitations

---

# 45. Recommended Implementation Order

```text
Phase 1
สร้าง bare MV3 extension

Phase 2
bundle Noto font + test static HTML

Phase 3
ทำ grapheme emoji detector

Phase 4
ทำ text-node wrapper

Phase 5
ทำ initial DOM scan

Phase 6
ทำ MutationObserver แบบ incremental

Phase 7
test Instagram DM + TikTok Web Chat

Phase 8
ทำ popup + site toggle

Phase 9
performance optimization

Phase 10
packaging + README + release ZIP
```

---

# 46. Definition of Done

บน Windows 10:

```text
Chrome เปิด Instagram DM หรือ TikTok Web Chat
        ↓
มีข้อความแชทที่ใช้ Emoji ใหม่
        ↓
Windows 10 เดิมจะขึ้น □ / tofu
        ↓
เปิด Extension
        ↓
ข้อความหลังส่งแสดง Emoji เดียวกันด้วย Noto/Android style
        ↓
ข้อความภาษาไทย/อังกฤษรอบ ๆ ยังใช้ font เดิม
        ↓
copy ข้อความยังได้ Unicode เดิม
```

นั่นคือเป้าหมายหลักของโปรเจกต์นี้
