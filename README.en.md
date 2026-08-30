# Modern Emoji Picker and Renderer

[ภาษาไทย](./README.md)

Modern Emoji Panel brings current Unicode Emoji support to Windows 10 and Windows 11 for both selecting Emoji and displaying them on the web. It consists of two products that can be installed together or used independently.

- **Modern Emoji Picker 0.1.9** — A resident WPF tray application invoked with `Win + .`. It supports Thai and English search, displays Noto artwork, and inserts Unicode sequences into the target application.
- **Modern Emoji Renderer 0.0.3** — A Chrome extension that renders Emoji with Noto Color Emoji in Instagram Web DMs, TikTok Web Chat, Facebook Messages/Inbox, and Messenger.com Inbox, including message bubbles, story and note replies, and reactions.

Both products run locally, contain no telemetry, and require no project account or backend service.

## Screenshot

![Modern Emoji Picker showing Recent Emoji and Hover Preview on Windows](./docs/screenshot/screenshot.png)

Modern Emoji Picker displaying Noto artwork, skin tones, and Hover Preview while used with Notepad.

## Downloads

Download the Picker from [GitHub Release v0.1.9](https://github.com/xcrossth/Modern-Emoji-Panel/releases/tag/v0.1.9) and the Renderer from [GitHub Release renderer-v0.0.3](https://github.com/xcrossth/Modern-Emoji-Panel/releases/tag/renderer-v0.0.3).

| Product | File | Recommended use |
|---|---|---|
| Picker — Installer | [Modern-Emoji-Picker-v0.1.9-setup-win-x64.exe](https://github.com/xcrossth/Modern-Emoji-Panel/releases/download/v0.1.9/Modern-Emoji-Picker-v0.1.9-setup-win-x64.exe) | Recommended for most users. Installs for the current account and can start with Windows. |
| Picker — Portable | [Modern-Emoji-Picker-v0.1.9-portable-win-x64.zip](https://github.com/xcrossth/Modern-Emoji-Panel/releases/download/v0.1.9/Modern-Emoji-Picker-v0.1.9-portable-win-x64.zip) | Extract and run without installation. |
| Chrome Renderer | [modern-emoji-renderer-0.0.3.zip](https://github.com/xcrossth/Modern-Emoji-Panel/releases/download/renderer-v0.0.3/modern-emoji-renderer-0.0.3.zip) | Load as an unpacked extension in Chrome. |

Verification files: [SHA256SUMS.txt](https://github.com/xcrossth/Modern-Emoji-Panel/releases/download/v0.1.9/SHA256SUMS.txt) for the Picker and [modern-emoji-renderer-0.0.3.zip.sha256](https://github.com/xcrossth/Modern-Emoji-Panel/releases/download/renderer-v0.0.3/modern-emoji-renderer-0.0.3.zip.sha256) for the Renderer.

## Install Modern Emoji Picker

### Installer — recommended

1. Download `Modern-Emoji-Picker-v0.1.9-setup-win-x64.exe`.
2. Verify its SHA-256 value against `SHA256SUMS.txt`.
3. Exit Classic Emoji Picker from the system tray if it is still running.
4. Run the installer and follow the prompts. Administrator privileges are not required.
5. Start Modern Emoji Picker, place the caret in a text field, and press `Win + .`.

The installer is not code-signed yet, so Windows SmartScreen may report an unknown publisher. Verify the file name and SHA-256 value against the official Release before proceeding. You do not need to disable Windows security features.

### Portable

1. Download `Modern-Emoji-Picker-v0.1.9-portable-win-x64.zip`.
2. Extract it to a writable folder where you intend to keep it.
3. Run `ModernEmojiPicker.exe`.
4. To start it with Windows, enable the option in the application's Settings.

Both the Installer and Portable packages are self-contained `win-x64` builds. A separate .NET Runtime installation is not required.

## Install Modern Emoji Renderer in Chrome

Chrome cannot install a ZIP file from outside the Chrome Web Store directly, so the extension must be extracted and loaded as unpacked.

1. Download `modern-emoji-renderer-0.0.3.zip` and its `.sha256` file.
2. Verify the SHA-256 value, then extract the ZIP to a permanent folder.
3. Open `chrome://extensions`.
4. Enable **Developer mode**.
5. Select **Load unpacked**, then choose the extracted folder containing `manifest.json`.
6. Refresh any open Instagram, TikTok, Facebook Messages, or Messenger.com pages.

When updating to a newer version, replace the files in the existing folder and select **Reload** on the Extensions page, or extract the new version to a separate folder and load it again.

The Renderer intentionally leaves Emoji inside editable fields unchanged to preserve the caret, selection, and IME state. It applies Noto rendering after a message becomes display content.

## Verify SHA-256 in PowerShell

```powershell
(Get-FileHash .\Modern-Emoji-Picker-v0.1.9-setup-win-x64.exe -Algorithm SHA256).Hash.ToLower()
(Get-FileHash .\modern-emoji-renderer-0.0.3.zip -Algorithm SHA256).Hash.ToLower()
```

SHA-256 values for the current release:

| File | SHA-256 |
|---|---|
| Picker Installer | `f62e881d9a143bbe74486f4b82c75a902ee53b7083eb998893fdf76b43146582` |
| Picker Portable | `1fe2a0226bea343b3817c40c3c28d48fea1c401af47e73a153a3986ecbaba110` |
| Renderer ZIP | `fec74ace1470992228b887b1a8cbbabc9f9b4c16089d8e0ba453acde666f9eed` |

## Key features

- Supports every fully-qualified Emoji 17 sequence.
- Searches names and keywords in Thai and English.
- Displays Noto artwork in the Picker with Hover Preview.
- Supports skin tones, multi-person variants, Recent, and locally learned ranking.
- Inserts Emoji through an ordered queue with focus and clipboard safety.
- Renderer supports new messages and room switching on Instagram, TikTok, Facebook Messages, and Messenger.com, including Meta image-Emoji and reactions. Facebook qualification covers Messages/Inbox only, not posts or comments.
- Operates offline with no analytics or telemetry.

## Platform and data

- Picker: .NET 10 WPF, self-contained `win-x64`.
- Renderer: Chrome Manifest V3 with bundled Noto Color Emoji.
- Primarily tested on Windows 10 Enterprise N 22H2 build 19045 x64 and Chrome Stable.
- Emoji Baseline: Unicode/Emoji 17.0, CLDR 48.2, and Noto Emoji v2.051.

## Documentation

- [Release guide and caveats](./docs/release/README.md)
- [Complete Renderer guide](./apps/renderer-extension/README.md)
- [SPEC 01 — Modern Emoji Picker](./docs/specs/01-modern-emoji-picker.md)
- [SPEC 02 — Chrome Emoji Renderer Extension](./docs/specs/02-chrome-emoji-renderer-extension.md)
- [Qualification results](./docs/qualification/README.md)
- [Security policy](./SECURITY.md)
- [Contributing guide](./CONTRIBUTING.md)
- [Domain glossary](./CONTEXT.md)
- [Architecture Decision Records](./docs/adr/)

## Build locally

```powershell
.\scripts\verify-foundation.ps1
.\scripts\release.ps1 -Version 0.1.9
.\scripts\build-renderer-release.ps1
```

The primary release workflow runs locally and does not require GitHub Actions minutes. See [scripts/README.md](./scripts/README.md) for details.

## License

Project code is available under the [MIT License](./LICENSE). Attribution for upstream projects, agent skills, Unicode, and Noto is preserved in [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md).
