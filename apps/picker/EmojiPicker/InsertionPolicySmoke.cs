using System.IO;
using System.Text.Json;

namespace EmojiPicker;

internal static class InsertionPolicySmoke
{
    public static int Run(string reportPath)
    {
        var checks = new Dictionary<string, bool>
        {
            ["hybrid-single-keystroke"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "😀") == InsertionMethod.UnicodeKeystrokes,
            ["hybrid-vs16-keystroke"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "❤️") == InsertionMethod.UnicodeKeystrokes,
            ["hybrid-zwj-paste"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "👨‍👩‍👧") == InsertionMethod.TemporaryPaste,
            ["hybrid-flag-paste"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "🇹🇭") == InsertionMethod.TemporaryPaste,
            ["hybrid-keycap-paste"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "1️⃣") == InsertionMethod.TemporaryPaste,
            ["hybrid-skin-tone-keystroke"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Hybrid, "👍🏽") == InsertionMethod.UnicodeKeystrokes,
            ["hybrid-omnibox-supplementary-paste"] = InsertionPolicy.SelectMethod(
                EmojiInsertMode.Hybrid,
                "🤍",
                targetRequiresAtomicSupplementaryText: true) == InsertionMethod.TemporaryPaste,
            ["hybrid-omnibox-bmp-keystroke"] = InsertionPolicy.SelectMethod(
                EmojiInsertMode.Hybrid,
                "❤️",
                targetRequiresAtomicSupplementaryText: true) == InsertionMethod.UnicodeKeystrokes,
            ["keystroke-omnibox-override"] = InsertionPolicy.SelectMethod(
                EmojiInsertMode.Keystroke,
                "🤍",
                targetRequiresAtomicSupplementaryText: true) == InsertionMethod.UnicodeKeystrokes,
            ["paste-always"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Paste, "😀") == InsertionMethod.TemporaryPaste,
            ["keystroke-only"] = InsertionPolicy.SelectMethod(EmojiInsertMode.Keystroke, "👨‍👩‍👧") == InsertionMethod.UnicodeKeystrokes,
            ["target-valid"] = TargetValidationPolicy.Validate(1, true, 1, 0x2000, 0x2000) == TargetValidationFailure.None,
            ["target-missing"] = TargetValidationPolicy.Validate(0, false, 0, 0x2000, null) == TargetValidationFailure.MissingTarget,
            ["target-closed"] = TargetValidationPolicy.Validate(1, false, 1, 0x2000, null) == TargetValidationFailure.TargetClosed,
            ["target-changed"] = TargetValidationPolicy.Validate(1, true, 2, 0x2000, 0x2000) == TargetValidationFailure.ForegroundChanged,
            ["target-higher-integrity"] = TargetValidationPolicy.Validate(1, true, 1, 0x2000, 0x3000) == TargetValidationFailure.HigherIntegrity,
            ["target-integrity-unknown"] = TargetValidationPolicy.Validate(1, true, 1, null, 0x2000) == TargetValidationFailure.IntegrityUnknown,
            ["clipboard-unchanged-restore"] = ClipboardRestorePolicy.ShouldRestore(true, 42, 42),
            ["clipboard-user-change-skip"] = !ClipboardRestorePolicy.ShouldRestore(true, 42, 43),
            ["clipboard-no-snapshot-skip"] = !ClipboardRestorePolicy.ShouldRestore(false, 42, 42),
            ["clipboard-empty-snapshot-restore"] = ClipboardRestorePolicy.ShouldRestore(true, 42, 42),
        };

        var report = new
        {
            schemaVersion = 1,
            checks,
            passed = checks.Values.All(value => value),
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return report.passed ? 0 : 1;
        }
        catch
        {
            return 2;
        }
    }
}
