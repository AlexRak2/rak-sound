using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SonnissBrowser
{
    public partial class VoiceCreatorWindow : Window
    {
        private readonly VoiceCreatorViewModel _vm;

        public VoiceCreatorWindow()
        {
            InitializeComponent();
            _vm = new VoiceCreatorViewModel();
            DataContext = _vm;

            Closed += (_, _) => _vm.Dispose();
        }

        public void ApplyTheme(bool isDarkMode)
        {
            if (isDarkMode)
            {
                Resources["Bg0"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                Resources["Bg1"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                Resources["Panel"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                Resources["Border"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x37));

                Resources["Text0"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                Resources["Text1"] = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB));
                Resources["Text2"] = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
                Resources["Text3"] = new SolidColorBrush(Color.FromRgb(0x6A, 0x6A, 0x6A));

                Resources["InputBg"] = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));
                Resources["InputBorder"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                Resources["InputFg"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                Resources["Accent"] = new SolidColorBrush(Color.FromRgb(0x0E, 0x63, 0x9C));
                Resources["AccentBright"] = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));

                Resources["TitleBar"] = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
                Resources["TitleBarBorder"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

                Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
                Resources["BtnBorder"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                Resources["BtnBgHover"] = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x55));
                Resources["BtnBgPressed"] = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5F));

                Resources["CaptionBtnFg"] = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
                Resources["CaptionBtnHoverBg"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
                Resources["CaptionBtnPressedBg"] = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
                Resources["CaptionBtnCloseHoverBg"] = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
                Resources["CaptionBtnClosePressedBg"] = new SolidColorBrush(Color.FromRgb(0xA5, 0x23, 0x17));

                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
            else
            {
                Resources["Bg0"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
                Resources["Bg1"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["Panel"] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
                Resources["Border"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));

                Resources["Text0"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                Resources["Text1"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                Resources["Text2"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                Resources["Text3"] = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

                Resources["InputBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                Resources["InputBorder"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                Resources["InputFg"] = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));

                Resources["Accent"] = new SolidColorBrush(Color.FromRgb(0x00, 0x5A, 0x9E));
                Resources["AccentBright"] = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));

                Resources["TitleBar"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                Resources["TitleBarBorder"] = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));

                Resources["BtnBg"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                Resources["BtnBorder"] = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
                Resources["BtnFg"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                Resources["BtnBgHover"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                Resources["BtnBgPressed"] = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));

                Resources["CaptionBtnFg"] = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                Resources["CaptionBtnHoverBg"] = new SolidColorBrush(Color.FromRgb(0xE9, 0xE9, 0xE9));
                Resources["CaptionBtnPressedBg"] = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
                Resources["CaptionBtnCloseHoverBg"] = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
                Resources["CaptionBtnClosePressedBg"] = new SolidColorBrush(Color.FromRgb(0xA5, 0x23, 0x17));

                Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                return; // No maximize for this window
            }
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
