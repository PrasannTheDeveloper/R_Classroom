using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // 添加这个命名空间

namespace WpfApp3
{
    public partial class RPathSettingsWindow : Window
    {
        public string RExecutablePath { get; private set; }

        public RPathSettingsWindow(string currentPath)
        {
            InitializeComponent();
            PathTextBox.Text = currentPath;
        }

        // 添加这个方法来处理标题栏的鼠标事件
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "R Script Executable|Rscript.exe|R Executable|R.exe|All Executables|*.exe",
                Title = "Select R Executable",
                InitialDirectory = @"C:\Program Files\R\"
            };
            if (dialog.ShowDialog() == true)
            {
                PathTextBox.Text = dialog.FileName;
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            string testPath = PathTextBox.Text;
            if (File.Exists(testPath))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = testPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (Process process = Process.Start(startInfo))
                    {
                        if (process != null)
                        {
                            string output = await process.StandardOutput.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            if (process.ExitCode == 0)
                            {
                                MessageBox.Show($"R executable found and working!\n\nVersion info:\n{output.Trim()}",
                                    "Test Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("R executable found but returned an error.",
                                    "Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error testing R executable:\n{ex.Message}",
                        "Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("File not found at the specified path.",
                    "Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://cran.r-project.org/bin/windows/base/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening browser: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            RExecutablePath = PathTextBox.Text;
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