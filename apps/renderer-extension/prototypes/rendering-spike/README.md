# Rendering spike

Prototype ชั่วคราวของ Ticket 02 สำหรับเปรียบเทียบ Noto CBDT, Windows-compatible, COLRv1 และ PNG 128/512 บน Chrome for Testing ของเครื่อง Windows 10 จริง

ไฟล์นี้อยู่ใน branch `codex/renderer-rendering-spike` เท่านั้น และไม่ใช่ production architecture ให้รันหลัง `scripts/install-chrome-for-testing.ps1` และเตรียม Noto v2.051 ทั้งสามไฟล์ไว้ใต้ `artifacts/tooling/noto-rendering-spike-v2.051`

แหล่ง font คือ tag `v2.051` ของ `googlefonts/noto-emoji` (peeled commit `8998f5dd683424a73e2314a8c1f1e359c19e8742`):

- `NotoColorEmoji.ttf` — SHA-256 `72A635CB3D2F3524C51620CDDE406B217204E8A6A06C6A096FF8ED4B5FD6E27B`
- `NotoColorEmoji_WindowsCompatible.ttf` — SHA-256 `19473341D23F8FDF90E91FFCA381D727C43F7BC05B2758DEC9687A58FBB81150`
- `Noto-COLRv1.ttf` — SHA-256 `0AE57FE58645638523BA35F388D93739D292539A9ACB84DF5700C81B1E1A28D2`

```powershell
node .\apps\renderer-extension\prototypes\rendering-spike\run-spike.mjs
```

ผลลัพธ์อยู่ที่ `artifacts/renderer-extension/rendering-spike` รวมภาพ 100%/200% และ `metrics.json`
