using System.Windows;

namespace WpfApp3
{
    public partial class ThemeSettingsWindow : Window
    {
        public double EditorTransparency { get; private set; }
        public double OutputTransparency { get; private set; }
        public double BackgroundOpacity { get; private set; }

        public ThemeSettingsWindow(double editorTransparency, double outputTransparency, double backgroundOpacity)
        {
            InitializeComponent();
            EditorTransparency = editorTransparency;
            OutputTransparency = outputTransparency;
            BackgroundOpacity = backgroundOpacity;

            EditorTransparencySlider.Value = editorTransparency;
            OutputTransparencySlider.Value = outputTransparency;

            // Add mouse drag functionality for borderless window
            this.MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            EditorTransparency = EditorTransparencySlider.Value;
            OutputTransparency = OutputTransparencySlider.Value;
            BackgroundOpacity = BackgroundOpacitySlider.Value;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}