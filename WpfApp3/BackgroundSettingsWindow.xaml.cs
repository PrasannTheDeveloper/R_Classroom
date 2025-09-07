using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp3
{
    public partial class BackgroundSettingsWindow : Window
    {
        public string BackgroundImagePath { get; set; }

        public BackgroundSettingsWindow(string backgroundImagePath)
        {
            InitializeComponent();

            BackgroundImagePath = backgroundImagePath;

            UpdateUI();

            // Add mouse drag functionality for borderless window
            this.MouseLeftButtonDown += (s, e) => { DragMove(); };
        }

        private void UpdateUI()
        {
            if (!string.IsNullOrEmpty(BackgroundImagePath) && System.IO.File.Exists(BackgroundImagePath))
            {
                CurrentBackgroundPath.Text = BackgroundImagePath;

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(BackgroundImagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    BackgroundPreview.Source = bitmap;
                }
                catch
                {
                    BackgroundPreview.Source = null;
                }
            }
            else
            {
                CurrentBackgroundPath.Text = "NO BACKGROUND IMAGE SELECTED";
                BackgroundPreview.Source = null;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*",
                Title = "Select Background Image"
            };

            if (dialog.ShowDialog() == true)
            {
                BackgroundImagePath = dialog.FileName;
                UpdateUI();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundImagePath = null;
            UpdateUI();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        public void SetTransparency(double transparency)
        {
            var lineNumbersBrush = new SolidColorBrush
            {
                Color = Color.FromRgb(238, 238, 238),
                Opacity = transparency
            };

            //lineNumbersTextBox.Background = lineNumbersBrush;
            //codeTextBox.Background = Brushes.Transparent;
            this.Background = Brushes.Transparent;
        }
    }
}