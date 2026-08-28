using System.Windows;

namespace EmojiPicker;

public partial class WelcomeWindow : Window
{
    internal WelcomeWindow()
    {
        InitializeComponent();
        if (Localizer.IsThai)
        {
            Title = Heading.Text = "ยินดีต้อนรับสู่ Modern Emoji Picker";
            Intro.Text = "Picker ทำงานในเครื่องโดยไม่ต้องมีบัญชีหรือเชื่อมต่อเครือข่าย";
            HotkeyText.Text = "• กด Win + . เพื่อเปิด Picker และเปลี่ยนหรือปิดปุ่มลัดได้ใน Settings";
            ConflictText.Text = "• หาก Classic Emoji Picker ทำงานอยู่ Modern จะไม่แย่งปุ่มลัดหรือปิด Classic ให้ Exit Classic ก่อน";
            PasteText.Text = "• Emoji ซับซ้อนใช้ Temporary Paste การคืน Clipboard เป็นแบบ best-effort และไม่คืนทับข้อมูลใหม่";
            StartupText.Text = "• แบบ portable จะไม่เปิดพร้อม Windows จนกว่าคุณสั่ง ส่วน installer อาจเตรียม autostart ตอนติดตั้ง";
            SettingsText.Text = "• เปิด Settings จากไอคอน tray เพื่อควบคุมภาษา ธีม สีผิว การส่ง ความเป็นส่วนตัว และ Activity Data บนเครื่อง";
            PrivacyText.Text = "v1 ไม่มี account, cloud sync, telemetry หรือ automatic upload";
            ContinueButton.Content = "เริ่มใช้งาน";
        }
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
