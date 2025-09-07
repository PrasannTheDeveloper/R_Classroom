using System.Windows;
using System.Windows.Input;

namespace WpfApp3
{
    public partial class RenameTabDialog : Window
    {
        public string NewName { get; private set; }

        public RenameTabDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName.Replace(" *", "");
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = NameTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

    }
}