using System.Windows;

namespace EmojiPicker;

public partial class SettingsWindow : Window
{
    private readonly SettingsControlModel model;
    private readonly Action clearRecent;
    private readonly Action resetRanking;
    private readonly Action clearAll;

    internal SettingsWindow(
        SettingsControlModel model,
        Action clearRecent,
        Action resetRanking,
        Action clearAll)
    {
        InitializeComponent();
        this.model = model;
        this.clearRecent = clearRecent;
        this.resetRanking = resetRanking;
        this.clearAll = clearAll;
        PopulateControls();
        ApplyLanguage();
    }

    internal SettingsControlModel Result => model;

    private void PopulateControls()
    {
        var thai = Localizer.IsThai;
        HotkeyEnabledCheck.IsChecked = model.HotkeyEnabled;
        var hotkeys = HotkeyBinding.Supported
            .Select(item => new Choice<HotkeyBinding>(item, item.GetDisplayName(thai))).ToList();
        HotkeyCombo.DisplayMemberPath = nameof(Choice<HotkeyBinding>.DisplayName);
        HotkeyCombo.ItemsSource = hotkeys;
        HotkeyCombo.SelectedItem = hotkeys.Single(item => item.Value.SettingValue == model.Hotkey.SettingValue);
        StartupCheck.IsChecked = model.StartWithWindows;
        StartupCheck.IsEnabled = !model.StartupManagedByInstaller;
        if (model.StartupManagedByInstaller)
        {
            StartupCheck.ToolTip = "Autostart is managed by the installer.";
        }

        SetChoices(LanguageCombo, model.Language,
            (UiLanguagePreference.System, thai ? "ตามภาษา Windows" : "System"),
            (UiLanguagePreference.English, "English"),
            (UiLanguagePreference.Thai, "ไทย"));
        SetChoices(ThemeCombo, model.Theme,
            (AppThemePreference.System, thai ? "ตามธีม Windows" : "System"),
            (AppThemePreference.Light, thai ? "สว่าง" : "Light"),
            (AppThemePreference.Dark, thai ? "มืด" : "Dark"));
        SetChoices(ToneCombo, model.SkinTone,
            (SkinTonePreference.Neutral, thai ? "กลาง (สีเหลือง)" : "Neutral (yellow)"),
            (SkinTonePreference.Light, thai ? "สีผิวอ่อน" : "Light"),
            (SkinTonePreference.MediumLight, thai ? "สีผิวขาวเหลือง" : "Medium-light"),
            (SkinTonePreference.Medium, thai ? "สีผิวปานกลาง" : "Medium"),
            (SkinTonePreference.MediumDark, thai ? "สีผิวเข้มปานกลาง" : "Medium-dark"),
            (SkinTonePreference.Dark, thai ? "สีผิวเข้ม" : "Dark"));
        SetChoices(InsertionCombo, model.InsertionMode,
            (EmojiInsertMode.Hybrid, "Hybrid"),
            (EmojiInsertMode.Keystroke, thai ? "Keystroke เท่านั้น" : "Keystroke only"),
            (EmojiInsertMode.Paste, thai ? "Paste ทุกครั้ง" : "Paste always"));
        DelayText.Text = model.PasteRestoreDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DiagnosticCheck.IsChecked = model.DiagnosticLoggingEnabled;
    }

    private void ApplyLanguage()
    {
        if (!Localizer.IsThai)
        {
            return;
        }

        Title = TitleText.Text = "การตั้งค่า";
        GeneralGroup.Header = "ทั่วไป";
        HotkeyEnabledCheck.Content = "เปิดใช้ปุ่มลัดส่วนกลาง";
        HotkeyLabel.Text = "ปุ่มลัด";
        StartupCheck.Content = "เปิดพร้อม Windows";
        LanguageLabel.Text = "ภาษา UI";
        ThemeLabel.Text = "ธีม";
        EmojiGroup.Header = "Emoji และการส่ง";
        ToneLabel.Text = "สีผิวเริ่มต้น";
        InsertionLabel.Text = "โหมดการส่ง";
        AdvancedGroup.Header = "ขั้นสูง";
        DelayLabel.Text = "เวลารอก่อนคืน Clipboard (ms)";
        PasteExplanation.Text = "Temporary Paste ไม่รับประกันว่าแอปจะวางสำเร็จ คืน private clipboard format ได้ครบ หรือ clipboard manager อื่นจะเคารพ exclusion marker และจะไม่คืนทับข้อมูลใหม่";
        DiagnosticCheck.Content = "เปิด diagnostic logging ที่คุ้มครองความเป็นส่วนตัว";
        DiagnosticExplanation.Text = "Log เก็บเฉพาะ metadata ทางเทคนิค ไม่เก็บคำค้น Emoji ที่เลือก clipboard/ข้อความ หรือชื่อหน้าต่างเป้าหมาย และไม่ upload อัตโนมัติ";
        ResetAdvancedButton.Content = "คืนค่าขั้นสูงเริ่มต้น";
        ActivityGroup.Header = "ข้อมูลกิจกรรม (เฉพาะเครื่องนี้)";
        ClearRecentButton.Content = "ล้าง Recent";
        ResetRankingButton.Content = "รีเซ็ต Learned Ranking";
        ClearAllButton.Content = "ล้างกิจกรรมทั้งหมด";
        ActivityExplanation.Text = "Settings และ Activity Data อยู่ในพื้นที่ข้อมูลของ Modern Emoji Picker บนเครื่องเท่านั้น v1 ไม่มี account, sync, telemetry, provider หรือ upload code";
        CancelButton.Content = "ยกเลิก";
        SaveButton.Content = "บันทึก";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DelayText.Text, out var delay) ||
            delay is < Settings.MinimumPasteRestoreDelayMs or > Settings.MaximumPasteRestoreDelayMs)
        {
            ResultText.Text = Localizer.Text(
                $"Enter a delay from {Settings.MinimumPasteRestoreDelayMs} to {Settings.MaximumPasteRestoreDelayMs} ms.",
                $"กรอกเวลาระหว่าง {Settings.MinimumPasteRestoreDelayMs}–{Settings.MaximumPasteRestoreDelayMs} ms");
            return;
        }

        model.HotkeyEnabled = HotkeyEnabledCheck.IsChecked == true;
        model.Hotkey = ((Choice<HotkeyBinding>)HotkeyCombo.SelectedItem).Value;
        model.StartWithWindows = StartupCheck.IsChecked == true;
        model.Language = ((Choice<UiLanguagePreference>)LanguageCombo.SelectedItem).Value;
        model.Theme = ((Choice<AppThemePreference>)ThemeCombo.SelectedItem).Value;
        model.SkinTone = ((Choice<SkinTonePreference>)ToneCombo.SelectedItem).Value;
        model.InsertionMode = ((Choice<EmojiInsertMode>)InsertionCombo.SelectedItem).Value;
        model.PasteRestoreDelayMs = delay;
        model.DiagnosticLoggingEnabled = DiagnosticCheck.IsChecked == true;
        DialogResult = true;
    }

    private void ResetAdvanced_Click(object sender, RoutedEventArgs e)
    {
        model.ResetAdvancedDefaults();
        DelayText.Text = model.PasteRestoreDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DiagnosticCheck.IsChecked = model.DiagnosticLoggingEnabled;
        ResultText.Text = Localizer.Text("Advanced defaults restored. Save to apply.", "คืนค่าขั้นสูงแล้ว กดบันทึกเพื่อใช้ค่า");
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e) => RunActivity(clearRecent, "Recent cleared.", "ล้าง Recent แล้ว");
    private void ResetRanking_Click(object sender, RoutedEventArgs e) => RunActivity(resetRanking, "Learned ranking reset.", "รีเซ็ต Learned Ranking แล้ว");
    private void ClearAll_Click(object sender, RoutedEventArgs e) => RunActivity(clearAll, "All activity cleared.", "ล้างกิจกรรมทั้งหมดแล้ว");

    private void RunActivity(Action command, string english, string thai)
    {
        command();
        ResultText.Text = Localizer.Text(english, thai);
    }

    private static void SetChoices<T>(System.Windows.Controls.ComboBox combo, T selected, params (T Value, string Name)[] values)
        where T : struct, Enum
    {
        var choices = values.Select(item => new Choice<T>(item.Value, item.Name)).ToList();
        combo.DisplayMemberPath = nameof(Choice<T>.DisplayName);
        combo.ItemsSource = choices;
        combo.SelectedItem = choices.Single(item => EqualityComparer<T>.Default.Equals(item.Value, selected));
    }

    private sealed record Choice<T>(T Value, string DisplayName);
}
