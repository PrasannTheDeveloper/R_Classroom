using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfApp3
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string rExecutablePath = @"C:\Program Files\R\R-4.5.1\bin\Rscript.exe";
        private string tempScriptPath;
        private string backgroundImagePath;
        private string settingsFilePath;
        private string databasePath;
        private TabItem activeTab;
        private int newTabCounter = 1;
        private SQLiteConnection dbConnection;
        private double editorTransparency = 0.0; // Fully transparent by default
        private double outputTransparency = 0.0; // Fully transparent by default
        private double backgroundOpacity = 0.7; // Default background opacity

        // Notes functionality
        private double noteZoomLevel = 1.0;
        private string currentNotePath;
        private bool isNotePanning = false;
        private Point notePanStartPoint;

        // Add fields for XAML elements
        private ImageBrush backgroundImageBrush;
        private Rectangle imageBackground;
        public ObservableCollection<TabItem> CodeTabs { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public string BackgroundImagePath
        {
            get => backgroundImagePath;
            set
            {
                backgroundImagePath = value;
                OnPropertyChanged(nameof(BackgroundImagePath));
                UpdateBackground();
                SaveSettingsToDatabase();
            }
        }
        public double EditorTransparency
        {
            get => editorTransparency;
            set
            {
                editorTransparency = value;
                OnPropertyChanged(nameof(EditorTransparency));
                UpdateEditorTransparency();
                SaveSettingsToDatabase();
            }
        }
        public double OutputTransparency
        {
            get => outputTransparency;
            set
            {
                outputTransparency = value;
                OnPropertyChanged(nameof(OutputTransparency));
                UpdateOutputTransparency();
                SaveSettingsToDatabase();
            }
        }
        public double BackgroundOpacity
        {
            get => backgroundOpacity;
            set
            {
                backgroundOpacity = value;
                OnPropertyChanged(nameof(BackgroundOpacity));
                UpdateBackgroundOpacity();
                SaveSettingsToDatabase();
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            // Initialize XAML element references
            backgroundImageBrush = (ImageBrush)FindName("BackgroundImageBrush");
            imageBackground = (Rectangle)FindName("ImageBackground");
            // Set up data context for bindings
            DataContext = this;
            // Initialize tabs collection
            CodeTabs = new ObservableCollection<TabItem>();
            CodeTabsTabControl.ItemsSource = CodeTabs;
            // Setup event handlers
            SetupEventHandlers();
            // Initialize app
            InitializeApp();
            // Load saved settings
            LoadSettingsFromDatabase();
            // Load saved sessions and create initial tab if none exist
            LoadSavedSessions();
            if (CodeTabs.Count == 0)
            {
                AddNewTab();
            }
            // Initialize WebView2 for AI Assistant and Notes
            InitializeWebView2Controls();
        }

        private async void InitializeWebView2Controls()
        {
            try
            {
                // Ensure WebView2 runtime is available
                var env = await CoreWebView2Environment.CreateAsync(null, null);

                // Initialize AI WebView
                await AIWebView.EnsureCoreWebView2Async(env);
                LoadChatGPT();

                // Initialize Notes WebView
                await NotesWebView.EnsureCoreWebView2Async(env);
                SetupNotesWebViewEvents();
                LoadNoteFromDatabase();

                // Initialize Google Search WebView
                await GoogleSearchWebView.EnsureCoreWebView2Async(env);
                GoogleSearchWebView.Source = new Uri("https://www.google.com");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GoogleSearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformGoogleSearch();
        }

        private void GoogleSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformGoogleSearch();
            }
        }

        private void PerformGoogleSearch()
        {
            string query = GoogleSearchTextBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
            {
                // URL encode the query
                string encodedQuery = Uri.EscapeDataString(query);
                string searchUrl = $"https://www.google.com/search?q={encodedQuery}";
                GoogleSearchWebView.Source = new Uri(searchUrl);
            }
        }
        private void SetupNotesWebViewEvents()
        {
            NotesWebView.NavigationCompleted += NotesWebView_NavigationCompleted;
            // Use mouse events instead of pointer events for WPF
            NotesWebView.MouseDown += NotesWebView_MouseDown;
            NotesWebView.MouseMove += NotesWebView_MouseMove;
            NotesWebView.MouseUp += NotesWebView_MouseUp;
            NotesWebView.MouseWheel += NotesWebView_MouseWheel;
        }

        private void NotesWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Apply zoom level after navigation completes
            ApplyNoteZoom();
        }

        private void NotesWebView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isNotePanning = true;
                notePanStartPoint = e.GetPosition(NotesWebView);
                NotesWebView.CaptureMouse();
            }
        }

        private void NotesWebView_MouseMove(object sender, MouseEventArgs e)
        {
            if (isNotePanning && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(NotesWebView);
                Vector delta = new Vector(currentPoint.X - notePanStartPoint.X, currentPoint.Y - notePanStartPoint.Y);

                if (NotesWebView.CoreWebView2 != null)
                {
                    NotesWebView.CoreWebView2.ExecuteScriptAsync($"window.scrollBy({-delta.X}, {-delta.Y});");
                }

                notePanStartPoint = currentPoint;
            }
        }

        private void NotesWebView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isNotePanning = false;
            NotesWebView.ReleaseMouseCapture();
        }

        private void NotesWebView_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    ZoomInNote();
                }
                else
                {
                    ZoomOutNote();
                }
                e.Handled = true;
            }
        }

        private void ApplyNoteZoom()
        {
            if (NotesWebView.CoreWebView2 != null)
            {
                // Use JavaScript to set zoom level instead of ZoomFactor property
                string script = $"document.body.style.zoom = '{noteZoomLevel}';";
                NotesWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private void LoadChatGPT()
        {
            if (AIWebView != null && AIWebView.CoreWebView2 != null)
            {
                AIWebView.CoreWebView2.Navigate("https://chat.openai.com/");
            }
        }

        private void LoadClaude()
        {
            if (AIWebView != null && AIWebView.CoreWebView2 != null)
            {
                AIWebView.CoreWebView2.Navigate("https://claude.ai/");
            }
        }

        private void AIServiceRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ChatGptRadioButton.IsChecked == true)
            {
                LoadChatGPT();
            }
            else if (ClaudeRadioButton.IsChecked == true)
            {
                LoadClaude();
            }
        }

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CodeEditor.Text))
            {
                Clipboard.SetText(CodeEditor.Text);
                MessageBox.Show("Code copied to clipboard!", "Copy Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CopyErrorsButton_Click(object sender, RoutedEventArgs e)
        {
            string outputText = OutputDisplay.Text;
            if (!string.IsNullOrEmpty(outputText))
            {
                // Extract only the error lines
                string[] lines = outputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                StringBuilder errorLines = new StringBuilder();
                bool inErrorSection = false;
                foreach (string line in lines)
                {
                    if (line.Contains("=== ERRORS/WARNINGS ==="))
                    {
                        inErrorSection = true;
                        errorLines.AppendLine(line);
                        continue;
                    }
                    else if (line.StartsWith("===") && inErrorSection)
                    {
                        inErrorSection = false;
                        continue;
                    }
                    if (inErrorSection)
                    {
                        errorLines.AppendLine(line);
                    }
                }
                if (errorLines.Length > 0)
                {
                    Clipboard.SetText(errorLines.ToString());
                    MessageBox.Show("Errors copied to clipboard!", "Copy Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No errors found in output.", "Copy Errors", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("No output to copy.", "Copy Errors", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void InitializeCodeEditor()
        {
            // Use the CodeEditor control directly
            // Set up event handlers
            CodeEditor.CodeTextBox.TextChanged += CodeEditor_TextChanged;
            CodeEditor.CodeTextBox.SelectionChanged += CodeEditor_SelectionChanged;
            CodeEditor.CodeTextBox.KeyDown += CodeEditor_KeyDown;
            // Apply initial transparency
            CodeEditor.SetTransparency(editorTransparency);
        }

        private void SetupEventHandlers()
        {
            // Update to use the new CodeEditor
            CodeEditor.CodeTextBox.TextChanged += CodeEditor_TextChanged;
            CodeEditor.CodeTextBox.SelectionChanged += CodeEditor_SelectionChanged;
            CodeEditor.CodeTextBox.KeyDown += CodeEditor_KeyDown;
            // Auto-save timer
            var autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            autoSaveTimer.Interval = TimeSpan.FromSeconds(30);
            autoSaveTimer.Tick += (s, e) => SaveCurrentSessionToDatabase();
            autoSaveTimer.Start();
        }

        private void InitializeApp()
        {
            // Create temp directory for R scripts
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RCodeRunner");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            tempScriptPath = System.IO.Path.Combine(tempDir, "temp_script.R");
            // Set up settings file path
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDir = System.IO.Path.Combine(appDataPath, "RCodeRunner");
            if (!Directory.Exists(settingsDir))
                Directory.CreateDirectory(settingsDir);
            settingsFilePath = System.IO.Path.Combine(settingsDir, "settings.json");
            databasePath = System.IO.Path.Combine(settingsDir, "appdata.db");
            // Initialize database
            InitializeDatabase();
            // Update status bar
            UpdateStatusBar();
            CheckRInstallation();
        }

        private void InitializeDatabase()
        {
            try
            {
                string connectionString = $"Data Source={databasePath};Version=3;";
                dbConnection = new SQLiteConnection(connectionString);
                dbConnection.Open();
                // Create tables for sessions and settings
                string createSessionsTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Sessions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TabName TEXT NOT NULL,
                        Content TEXT NOT NULL,
                        FilePath TEXT,
                        IsActive INTEGER DEFAULT 0,
                        LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                string createSettingsTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Settings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingKey TEXT NOT NULL UNIQUE,
                        SettingValue TEXT,
                        LastModified DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                using (var command = new SQLiteCommand(createSessionsTableQuery, dbConnection))
                {
                    command.ExecuteNonQuery();
                }
                using (var command = new SQLiteCommand(createSettingsTableQuery, dbConnection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadSettingsFromDatabase()
        {
            try
            {
                if (dbConnection == null) return;
                // Load background image path
                string selectBackgroundQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'BackgroundImagePath'";
                using (var command = new SQLiteCommand(selectBackgroundQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        BackgroundImagePath = reader["SettingValue"].ToString();
                    }
                }
                // Load editor transparency
                string selectEditorTransparencyQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'EditorTransparency'";
                using (var command = new SQLiteCommand(selectEditorTransparencyQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && double.TryParse(reader["SettingValue"].ToString(), out double editorTransp))
                    {
                        EditorTransparency = editorTransp;
                    }
                }
                // Load output transparency
                string selectOutputTransparencyQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'OutputTransparency'";
                using (var command = new SQLiteCommand(selectOutputTransparencyQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && double.TryParse(reader["SettingValue"].ToString(), out double outputTransp))
                    {
                        OutputTransparency = outputTransp;
                    }
                }
                // Load background opacity
                string selectBackgroundOpacityQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'BackgroundOpacity'";
                using (var command = new SQLiteCommand(selectBackgroundOpacityQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && double.TryParse(reader["SettingValue"].ToString(), out double bgOpacity))
                    {
                        BackgroundOpacity = bgOpacity;
                    }
                }
                // Load R executable path
                string selectRPathQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'RExecutablePath'";
                using (var command = new SQLiteCommand(selectRPathQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        rExecutablePath = reader["SettingValue"].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings from database: {ex.Message}");
            }
        }

        private void SaveSettingsToDatabase()
        {
            try
            {
                if (dbConnection == null) return;
                // Save background image path
                string upsertBackgroundQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('BackgroundImagePath', @value)";
                using (var command = new SQLiteCommand(upsertBackgroundQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", BackgroundImagePath ?? "");
                    command.ExecuteNonQuery();
                }
                // Save editor transparency
                string upsertEditorTransparencyQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('EditorTransparency', @value)";
                using (var command = new SQLiteCommand(upsertEditorTransparencyQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", EditorTransparency.ToString());
                    command.ExecuteNonQuery();
                }
                // Save output transparency
                string upsertOutputTransparencyQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('OutputTransparency', @value)";
                using (var command = new SQLiteCommand(upsertOutputTransparencyQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", OutputTransparency.ToString());
                    command.ExecuteNonQuery();
                }
                // Save background opacity
                string upsertBackgroundOpacityQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('BackgroundOpacity', @value)";
                using (var command = new SQLiteCommand(upsertBackgroundOpacityQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", BackgroundOpacity.ToString());
                    command.ExecuteNonQuery();
                }
                // Save R executable path
                string upsertRPathQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('RExecutablePath', @value)";
                using (var command = new SQLiteCommand(upsertRPathQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", rExecutablePath);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings to database: {ex.Message}");
            }
        }

        private void LoadSavedSessions()
        {
            try
            {
                if (dbConnection == null) return;
                string selectQuery = "SELECT * FROM Sessions ORDER BY LastModified DESC";
                using (var command = new SQLiteCommand(selectQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tab = CreateTabFromDatabase(
                            reader["TabName"].ToString(),
                            reader["Content"].ToString(),
                            reader["FilePath"].ToString(),
                            Convert.ToBoolean(reader["IsActive"])
                        );
                        CodeTabs.Add(tab);
                    }
                }
                // Select the last active tab or the first one
                var activeTab = CodeTabs.FirstOrDefault(t => ((CodeDocument)t.Tag).IsActive);
                if (activeTab != null)
                {
                    CodeTabsTabControl.SelectedItem = activeTab;
                }
                else if (CodeTabs.Count > 0)
                {
                    CodeTabsTabControl.SelectedItem = CodeTabs[0];
                }
            }
            catch (Exception ex)
            {
                // If there's an error loading sessions, just start fresh
                Debug.WriteLine($"Error loading sessions: {ex.Message}");
            }
        }

        private TabItem CreateTabFromDatabase(string tabName, string content, string filePath, bool isActive)
        {
            var newTab = new TabItem { Style = (Style)FindResource("NotebookTabStyle") };
            newTab.Header = tabName;
            // Attach double-click event for renaming
            newTab.MouseDoubleClick += (s, e) =>
            {
                e.Handled = true;
                RenameTab(newTab);
            };
            // Attach close button click event
            newTab.Loaded += (s, e) =>
            {
                var tabItem = s as TabItem;
                if (tabItem == null) return;
                // Find the close button in the template
                var closeButton = tabItem.Template.FindName("CloseButton", tabItem) as Button;
                if (closeButton != null)
                {
                    closeButton.Click += (btnSender, btnE) =>
                    {
                        btnE.Handled = true;
                        CloseTab(tabItem);
                    };
                }
                // Find the modified indicator in the template
                var modifiedIndicator = tabItem.Template.FindName("ModifiedIndicator", tabItem) as TextBlock;
                if (modifiedIndicator != null)
                {
                    var document = (CodeDocument)tabItem.Tag;
                    modifiedIndicator.Visibility = document.IsModified ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            newTab.Tag = new CodeDocument
            {
                FilePath = filePath,
                Content = content,
                IsModified = false,
                IsActive = isActive
            };
            return newTab;
        }

        private void SaveCurrentSessionToDatabase()
        {
            try
            {
                if (dbConnection == null) return;

                // Clear existing sessions
                string deleteQuery = "DELETE FROM Sessions";
                using (var command = new SQLiteCommand(deleteQuery, dbConnection))
                {
                    command.ExecuteNonQuery();
                }

                // Save all current tabs
                foreach (TabItem tab in CodeTabs)
                {
                    var tabDocument = (CodeDocument)tab.Tag;
                    string tabName = tab.Header as string ?? "Untitled";
                    tabName = tabName.Replace(" *", "");

                    // Get the current content of the tab
                    string content = GetTabContent(tab);

                    string insertQuery = @"
                        INSERT INTO Sessions (TabName, Content, FilePath, IsActive, LastModified) 
                        VALUES (@tabName, @content, @filePath, @isActive, @lastModified)";
                    using (var command = new SQLiteCommand(insertQuery, dbConnection))
                    {
                        command.Parameters.AddWithValue("@tabName", tabName);
                        command.Parameters.AddWithValue("@content", content);
                        command.Parameters.AddWithValue("@filePath", tabDocument.FilePath ?? "");
                        command.Parameters.AddWithValue("@isActive", tab == activeTab);
                        command.Parameters.AddWithValue("@lastModified", DateTime.Now);
                        command.ExecuteNonQuery();
                    }
                }
                UpdateSessionStatus("SESSION AUTO-SAVED");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        private string GetTabContent(TabItem tab)
        {
            if (tab == activeTab && CodeEditor != null)
            {
                return CodeEditor.Text;
            }
            else
            {
                var document = (CodeDocument)tab.Tag;
                return document.Content;
            }
        }

        private async void CheckRInstallation()
        {
            // Check common R installation paths
            string[] commonPaths = {
                @"C:\Program Files\R\R-4.5.1\bin\Rscript.exe",
                @"C:\Program Files\R\R-4.5.0\bin\Rscript.exe",
                @"C:\Program Files\R\R-4.4.1\bin\Rscript.exe",
                @"C:\Program Files\R\R-4.3.3\bin\Rscript.exe"
            };
            bool foundR = false;
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    rExecutablePath = path;
                    foundR = true;
                    break;
                }
            }
            // Try to find R in registry
            if (!foundR)
            {
                try
                {
                    string regPath = @"SOFTWARE\R-core\R";
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath))
                    {
                        if (key != null)
                        {
                            string installPath = key.GetValue("InstallPath")?.ToString();
                            if (!string.IsNullOrEmpty(installPath))
                            {
                                string scriptPath = System.IO.Path.Combine(installPath, "bin", "Rscript.exe");
                                if (File.Exists(scriptPath))
                                {
                                    rExecutablePath = scriptPath;
                                    foundR = true;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            if (!foundR)
            {
                await ShowRInstallationDialog();
            }
        }

        private async Task ShowRInstallationDialog()
        {
            var result = MessageBox.Show(
                "R is not found on your system.\n\n" +
                "Would you like to:\n" +
                "• YES: Download R from the official website\n" +
                "• NO: Manually specify R installation path\n" +
                "• CANCEL: Continue without R (limited functionality)",
                "R Installation Required",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://cran.r-project.org/bin/windows/base/",
                            UseShellExecute = true
                        });
                        MessageBox.Show(
                            "R download page opened in your browser.\n\n" +
                            "After installing R, restart this application or use 'R Path Settings' to configure the path.",
                            "R Installation",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case MessageBoxResult.No:
                    SettingsButton_Click(null, null);
                    break;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateBackground()
        {
            if (string.IsNullOrEmpty(BackgroundImagePath) || !File.Exists(BackgroundImagePath))
            {
                // Set default white background if no image is selected
                var whiteBrush = new SolidColorBrush(Colors.White);
                if (backgroundImageBrush != null)
                {
                    backgroundImageBrush.ImageSource = null;
                }
                if (imageBackground != null)
                {
                    imageBackground.Fill = whiteBrush;
                }
                return;
            }
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(BackgroundImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                if (backgroundImageBrush != null)
                {
                    backgroundImageBrush.ImageSource = bitmap;
                }
                UpdateBackgroundOpacity();
            }
            catch
            {
                // If there's an error loading the image, use default white background
                var whiteBrush = new SolidColorBrush(Colors.White);
                if (backgroundImageBrush != null)
                {
                    backgroundImageBrush.ImageSource = null;
                }
                if (imageBackground != null)
                {
                    imageBackground.Fill = whiteBrush;
                }
            }
        }

        private void UpdateBackgroundOpacity()
        {
            if (backgroundImageBrush != null)
            {
                backgroundImageBrush.Opacity = backgroundOpacity;
            }
        }

        private void UpdateEditorTransparency()
        {
            var editorBrush = new SolidColorBrush
            {
                Color = Color.FromArgb((byte)(255 * editorTransparency), 255, 255, 255)
            };

            if (EditorBorder != null)
            {
                EditorBorder.Background = editorBrush;
            }
            if (CodeEditor != null)
            {
                CodeEditor.SetTransparency(editorTransparency);
            }
        }

        private void UpdateOutputTransparency()
        {
            var outputBrush = new SolidColorBrush
            {
                Color = Color.FromArgb((byte)(255 * outputTransparency), 255, 255, 255)
            };
            if (OutputBorder != null)
            {
                OutputBorder.Background = outputBrush;
            }
            if (OutputDisplay != null)
            {
                OutputDisplay.Background = Brushes.Transparent;
                OutputDisplay.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            }
        }

        private void AddNewTab()
        {
            var newTab = new TabItem { Style = (Style)FindResource("NotebookTabStyle") };
            string tabName = $"UNTITLED {newTabCounter++}";
            newTab.Header = tabName;
            // Attach double-click event for renaming
            newTab.MouseDoubleClick += (s, e) =>
            {
                e.Handled = true;
                RenameTab(newTab);
            };
            // Attach close button click event
            newTab.Loaded += (s, e) =>
            {
                var tabItem = s as TabItem;
                if (tabItem == null) return;
                // Find the close button in the template
                var closeButton = tabItem.Template.FindName("CloseButton", tabItem) as Button;
                if (closeButton != null)
                {
                    closeButton.Click += (btnSender, btnE) =>
                    {
                        btnE.Handled = true;
                        CloseTab(tabItem);
                    };
                }
                // Find the modified indicator in the template
                var modifiedIndicator = tabItem.Template.FindName("ModifiedIndicator", tabItem) as TextBlock;
                if (modifiedIndicator != null)
                {
                    var document = (CodeDocument)tabItem.Tag;
                    modifiedIndicator.Visibility = document.IsModified ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            newTab.Tag = new CodeDocument
            {
                FilePath = null,
                Content = "",
                IsModified = false
            };
            CodeTabs.Add(newTab);
            CodeTabsTabControl.SelectedItem = newTab;
            activeTab = newTab;
            LoadTabContent(newTab);
        }

        private void RenameTab(TabItem tab)
        {
            string currentName = tab.Header as string;
            if (currentName == null) return;
            // Remove the * if present
            string baseName = currentName.Replace(" *", "");
            var inputDialog = new RenameTabDialog(baseName);
            if (inputDialog.ShowDialog() == true)
            {
                tab.Header = inputDialog.NewName;
                var document = (CodeDocument)tab.Tag;
                if (document.IsModified && !inputDialog.NewName.EndsWith(" *"))
                {
                    tab.Header = inputDialog.NewName + " *";
                }
            }
        }

        private void CloseTab(TabItem tab)
        {
            var document = (CodeDocument)tab.Tag;
            if (document.IsModified)
            {
                string tabName = tab.Header as string ?? "Untitled";
                tabName = tabName.Replace(" *", "");
                var result = MessageBox.Show(
                    $"Do you want to save changes to {tabName}?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    SaveTabContent(tab);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }
            CodeTabs.Remove(tab);
            if (CodeTabs.Count == 0)
            {
                AddNewTab();
            }
            else if (activeTab == tab)
            {
                activeTab = CodeTabs.LastOrDefault();
                if (activeTab != null)
                {
                    CodeTabsTabControl.SelectedItem = activeTab;
                }
            }
        }

        private void LoadTabContent(TabItem tab)
        {
            var document = (CodeDocument)tab.Tag;
            if (!string.IsNullOrEmpty(document.Content))
            {
                CodeEditor.Text = document.Content;
            }
            else
            {
                CodeEditor.Text = "# Welcome to R Classroom!\n# Enter your R code below and press F5 or click Run to execute\n\n# Example with user input:\nif (exists(\"user_inputs\") && length(user_inputs) >= 1) {\n  num1 <- as.numeric(user_inputs[1])\n} else {\n  num1 <- as.numeric(readline(prompt = \"Enter first number: \"))\n}\n\nif (exists(\"user_inputs\") && length(user_inputs) >= 2) {\n  num2 <- as.numeric(user_inputs[2])\n} else {\n  num2 <- as.numeric(readline(prompt = \"Enter second number: \"))\n}\n\n# Addition\nsum_result <- num1 + num2\ncat(\"The sum is:\", sum_result, \"\\n\")\n\n# Multiplication\nmul_result <- num1 * num2\ncat(\"The multiplication is:\", mul_result, \"\\n\")";
            }
            document.IsModified = false;
            // Update the modified indicator in the tab header
            UpdateTabHeader(tab, false);
            // Clear any previous error highlighting
            CodeEditor.HighlightErrorLines(null);
        }

        private void UpdateTabHeader(TabItem tab, bool isModified)
        {
            string currentName = tab.Header as string;
            if (currentName == null) return;
            string baseName = currentName.Replace(" *", "");
            // Update the header text
            if (isModified)
            {
                tab.Header = baseName + " *";
            }
            else
            {
                tab.Header = baseName;
            }
            // Update the modified indicator in the template
            if (tab.Template != null)
            {
                var modifiedIndicator = tab.Template.FindName("ModifiedIndicator", tab) as TextBlock;
                if (modifiedIndicator != null)
                {
                    modifiedIndicator.Visibility = isModified ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void CodeTabsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CodeTabsTabControl.SelectedItem is TabItem selectedTab)
            {
                // Save current tab content before switching
                if (activeTab != null)
                {
                    var currentDocument = (CodeDocument)activeTab.Tag;
                    currentDocument.Content = CodeEditor.Text;
                }
                activeTab = selectedTab;
                LoadTabContent(selectedTab);
            }
        }

        private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (activeTab != null)
            {
                var document = (CodeDocument)activeTab.Tag;
                document.Content = CodeEditor.Text;
                if (!document.IsModified)
                {
                    document.IsModified = true;
                    UpdateTabHeader(activeTab, true);
                }
            }
        }

        private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // Update to use the new CodeEditor
            int line = CodeEditor.CodeTextBox.GetLineIndexFromCharacterIndex(CodeEditor.CodeTextBox.CaretIndex) + 1;
            int column = CodeEditor.CodeTextBox.CaretIndex - CodeEditor.CodeTextBox.GetCharacterIndexFromLineIndex(line - 1) + 1;
            LineColumnIndicator.Text = $"LINE: {line}, COLUMN: {column}";
        }

        private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                _ = ExecuteCurrentCode();
            }
        }

        private async Task ExecuteCurrentCode()
        {
            if (activeTab != null)
            {
                await ExecuteRCode(CodeEditor.Text);
            }
        }

        private double plotZoom = 1.0;
        private void ZoomInPlotButton_Click(object sender, RoutedEventArgs e)
        {
            plotZoom += 0.2;
            UpdatePlotZoom();
        }

        private void ZoomOutPlotButton_Click(object sender, RoutedEventArgs e)
        {
            plotZoom = Math.Max(0.2, plotZoom - 0.2);
            UpdatePlotZoom();
        }

        private void UpdatePlotZoom()
        {
            if (PlotDisplay != null)
            {
                PlotDisplay.LayoutTransform = new ScaleTransform(plotZoom, plotZoom);
            }
        }

        private void DisplayPlot(string plotPath)
        {
            try
            {
                currentPlotPath = plotPath;
                // Always clear previous image source to force reload
                PlotDisplay.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (!File.Exists(plotPath))
                {
                    OutputDisplay.Text += "\nError: Plot file does not exist.\n";
                    OutputTabControl.SelectedIndex = 0;
                    return;
                }
                var fileInfo = new FileInfo(plotPath);
                if (fileInfo.Length == 0)
                {
                    OutputDisplay.Text += "\nError: Plot file is empty.\n";
                    OutputTabControl.SelectedIndex = 0;
                    return;
                }
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(plotPath);
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();
                if (bitmap.Width > 0 && bitmap.Height > 0)
                {
                    PlotDisplay.Source = bitmap;
                    UpdatePlotZoom();
                    OutputTabControl.SelectedIndex = 1;
                }
                else
                {
                    OutputDisplay.Text += "\nError: Plot image is invalid or corrupted.\n";
                    OutputTabControl.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                OutputDisplay.Text += $"\nError displaying plot: {ex.Message}\n";
                OutputTabControl.SelectedIndex = 0;
            }
        }

        private int[] ParseErrorLines(string errorOutput)
        {
            List<int> errorLines = new List<int>();
            if (string.IsNullOrEmpty(errorOutput))
                return errorLines.ToArray();
            // Common R error patterns
            // Pattern 1: Error in <function> (line <number>)
            var regex1 = new Regex(@"Error in.*?\(line (\d+)\)");
            var matches1 = regex1.Matches(errorOutput);
            foreach (Match match in matches1)
            {
                if (int.TryParse(match.Groups[1].Value, out int line))
                {
                    errorLines.Add(line);
                }
            }
            // Pattern 2: Error: <message> at line <number>
            var regex2 = new Regex(@"Error:.*?at line (\d+)");
            var matches2 = regex2.Matches(errorOutput);
            foreach (Match match in matches2)
            {
                if (int.TryParse(match.Groups[1].Value, out int line))
                {
                    errorLines.Add(line);
                }
            }
            // Pattern 3: <filename>:<line>:<column>: Error:
            var regex3 = new Regex(@":(\d+):\d+:\s*Error:");
            var matches3 = regex3.Matches(errorOutput);
            foreach (Match match in matches3)
            {
                if (int.TryParse(match.Groups[1].Value, out int line))
                {
                    errorLines.Add(line);
                }
            }
            return errorLines.Distinct().ToArray();
        }

        private async Task ExecuteRCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                OutputDisplay.Text = "No code to execute.\n";
                UpdateExecutionStatus("NO CODE", "#FFFFC107");
                OutputTabControl.SelectedIndex = 0; // Show console tab
                return;
            }
            // Clear previous error highlighting
            CodeEditor.HighlightErrorLines(null);
            UpdateExecutionStatus("EXECUTING...", "#FF0078D4");
            OutputDisplay.Text = "Executing R code...\n\n";
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RCodeRunner");
            if (!Directory.Exists(tempDir))
                Directory.CreateDirectory(tempDir);
            string plotPath = System.IO.Path.Combine(tempDir, "plot.png");
            if (File.Exists(plotPath))
            {
                try { File.Delete(plotPath); } catch { }
            }
            // Check if the code contains readline calls
            bool hasReadline = code.Contains("readline(");
            List<string> userInputs = new List<string>();
            List<string> prompts = new List<string>();
            if (hasReadline)
            {
                // Extract prompts from the code
                var regex = new Regex(@"readline\s*\(\s*prompt\s*=\s*""([^""]*)""");
                var matches = regex.Matches(code);
                foreach (Match match in matches)
                {
                    prompts.Add(match.Groups[1].Value);
                }
                // If we found prompts, get input from user
                if (prompts.Count > 0)
                {
                    OutputDisplay.Text += "This script requires user input:\n";
                    foreach (var prompt in prompts)
                    {
                        // Show input dialog on UI thread
                        string input = await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            InputDialog dialog = new InputDialog(prompt);
                            return dialog.ShowDialog() == true ? dialog.InputValue : null;
                        });
                        if (input == null)
                        {
                            OutputDisplay.Text += "\nExecution cancelled by user.\n";
                            UpdateExecutionStatus("CANCELLED", "#FFDC3545");
                            return;
                        }
                        userInputs.Add(input);
                        OutputDisplay.Text += $"{prompt} {input}\n";
                    }
                    OutputDisplay.Text += "\n";
                }
            }
            // Prepare the R code with user inputs
            string wrappedCode;
            if (userInputs.Count > 0)
            {
                // Convert user inputs to R format (escape quotes)
                string inputsR = string.Join(", ", userInputs.Select(i => $"\"{i.Replace("\"", "\\\"")}\""));
                wrappedCode = $@"
                    user_inputs <- c({inputsR})
                    input_index <- 1
                    readline <- function(prompt = '') {{
                        if (input_index <= length(user_inputs)) {{
                            value <- user_inputs[input_index]
                            input_index <<- input_index + 1
                            cat(prompt, value, '\n', sep='')
                            value
                        }} else {{
                            stop('Not enough inputs provided')
                        }}
                    }}
                    png('{plotPath.Replace("\\", "/")}', width = 800, height = 600)
                    {code}
                    dev.off()
                ";
            }
            else
            {
                wrappedCode = $@"
                    png('{plotPath.Replace("\\", "/")}', width = 800, height = 600)
                    {code}
                    dev.off()
                ";
            }
            try
            {
                await File.WriteAllTextAsync(tempScriptPath, wrappedCode);
                if (!File.Exists(rExecutablePath))
                {
                    OutputDisplay.Text += $"Error: R executable not found at: {rExecutablePath}\n";
                    OutputDisplay.Text += "Please check the R installation path in Settings.\n";
                    UpdateExecutionStatus("R NOT FOUND", "#FFDC3545");
                    OutputTabControl.SelectedIndex = 0; // Show console tab
                    return;
                }
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = rExecutablePath,
                    Arguments = $"--vanilla --slave \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                DateTime startTime = DateTime.Now;
                using (Process process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                        Task<string> errorTask = process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();
                        string output = await outputTask;
                        string error = await errorTask;
                        DateTime endTime = DateTime.Now;
                        TimeSpan executionTime = endTime - startTime;
                        OutputDisplay.Text = "";
                        // Parse error lines from R output
                        int[] errorLines = ParseErrorLines(error);
                        if (errorLines.Length > 0)
                        {
                            CodeEditor.HighlightErrorLines(errorLines);
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            OutputDisplay.Text += "=== OUTPUT ===\n" + output + "\n";
                        }
                        if (!string.IsNullOrEmpty(error))
                        {
                            OutputDisplay.Text += "=== ERRORS/WARNINGS ===\n" + error + "\n";
                            // Add error line information
                            if (errorLines.Length > 0)
                            {
                                OutputDisplay.Text += "\n=== ERROR LOCATIONS ===\n";
                                foreach (int line in errorLines)
                                {
                                    OutputDisplay.Text += $"Error at line: {line}\n";
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error))
                        {
                            OutputDisplay.Text += "Code executed successfully with no output.\n";
                        }
                        OutputDisplay.Text += $"\n=== EXECUTION COMPLETED ===\n";
                        OutputDisplay.Text += $"Exit Code: {process.ExitCode}\n";
                        OutputDisplay.Text += $"Execution Time: {executionTime.TotalMilliseconds:F0} ms\n";
                        OutputDisplay.Text += $"Completed at: {endTime:HH:mm:ss}\n";
                        // Detect if code contains plot commands
                        bool containsPlot = code.IndexOf("plot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            code.IndexOf("hist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            code.IndexOf("barplot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            code.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (containsPlot && File.Exists(plotPath) && new FileInfo(plotPath).Length > 0)
                        {
                            DisplayPlot(plotPath);
                        }
                        else
                        {
                            OutputTabControl.SelectedIndex = 0; // Show console tab only
                        }
                        if (process.ExitCode == 0)
                        {
                            UpdateExecutionStatus("SUCCESS", "#FF68D391");
                        }
                        else
                        {
                            UpdateExecutionStatus($"ERRORS (EXIT: {process.ExitCode})", "#FFDC3545");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OutputDisplay.Text += $"Error executing R code: {ex.Message}\n";
                UpdateExecutionStatus("EXECUTION FAILED", "#FFDC3545");
                OutputTabControl.SelectedIndex = 0; // Show console tab
            }
        }

        private void CreateMessagePlot(string plotPath, string message)
        {
            try
            {
                // Create a simple R script to generate a message plot
                string messageScript = $@"
                    png('{plotPath.Replace("\\", "/")}', width = 800, height = 600, res = 100)
                    plot.new()
                    text(0.5, 0.5, '{message}', cex = 2, col = 'blue')
                    dev.off()
                ";
                string messageScriptPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(plotPath), "message_script.R");
                File.WriteAllText(messageScriptPath, messageScript);
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = rExecutablePath,
                    Arguments = $"--vanilla --slave \"{messageScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating message plot: {ex.Message}");
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteCurrentCode();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            OutputDisplay.Clear();
            OutputDisplay.Text = "Output cleared.\n";
            UpdateExecutionStatus("READY", "#FF68D391");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            RPathSettingsWindow settingsWindow = new RPathSettingsWindow(rExecutablePath);
            if (settingsWindow.ShowDialog() == true)
            {
                rExecutablePath = settingsWindow.RExecutablePath;
                UpdateStatusBar();
                SaveSettingsToDatabase();
            }
        }

        private void BackgroundSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            BackgroundSettingsWindow settingsWindow = new BackgroundSettingsWindow(BackgroundImagePath);
            if (settingsWindow.ShowDialog() == true)
            {
                BackgroundImagePath = settingsWindow.BackgroundImagePath;
            }
        }

        private void NewTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentTab();
        }

        private void SaveCurrentTab()
        {
            if (activeTab != null)
            {
                SaveTabContent(activeTab);
            }
        }

        private void SaveTabContent(TabItem tab)
        {
            var document = (CodeDocument)tab.Tag;
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "R Script Files (*.R)|*.R|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".R",
                Title = "Save R Script"
            };
            if (!string.IsNullOrEmpty(document.FilePath))
            {
                dialog.FileName = document.FilePath;
            }
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string content = tab == activeTab ? CodeEditor.Text : document.Content;
                    File.WriteAllText(dialog.FileName, content);
                    document.FilePath = dialog.FileName;
                    document.IsModified = false;
                    string fileName = System.IO.Path.GetFileName(dialog.FileName);
                    tab.Header = fileName;
                    UpdateExecutionStatus("FILE SAVED", "#FF68D391");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "R Script Files (*.R)|*.R|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Load R Script"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    AddNewTab();
                    var document = (CodeDocument)activeTab.Tag;
                    string content = File.ReadAllText(dialog.FileName);
                    CodeEditor.Text = content;
                    document.Content = content;
                    document.FilePath = dialog.FileName;
                    document.IsModified = false;
                    activeTab.Header = System.IO.Path.GetFileName(dialog.FileName);
                    UpdateExecutionStatus("FILE LOADED", "#FF68D391");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateStatusBar()
        {
            RPathStatus.Text = $"R PATH: {rExecutablePath}";
            bool rExists = File.Exists(rExecutablePath);
            StatusText.Text = rExists ? "READY" : "R NOT FOUND";
            // Fixed: Use SolidColorBrush instead of hex color directly
            StatusText.Foreground = new SolidColorBrush(rExists ? Color.FromRgb(25, 135, 84) : Color.FromRgb(220, 53, 69));
        }

        private void UpdateExecutionStatus(string status, string color)
        {
            ExecutionStatus.Text = status;
            // Fixed: Convert hex color to SolidColorBrush
            ExecutionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private void UpdateSessionStatus(string status)
        {
            SessionStatus.Text = status;
            // Fixed: Use SolidColorBrush instead of hex color directly
            SessionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveCurrentSessionToDatabase();
            try
            {
                dbConnection?.Close();
                dbConnection?.Dispose();
                if (File.Exists(tempScriptPath))
                    File.Delete(tempScriptPath);
            }
            catch { }
            base.OnClosed(e);
        }

        private string currentPlotPath;
        private void SavePlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPlotPath) || !File.Exists(currentPlotPath))
            {
                MessageBox.Show("No plot to save.", "Save Plot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "PNG Files (*.png)|*.png|JPEG Files (*.jpg)|*.jpg|All Files (*.*)|*.*",
                DefaultExt = ".png",
                Title = "Save Plot"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(currentPlotPath, dialog.FileName, true);
                    MessageBox.Show("Plot saved successfully.", "Save Plot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving plot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(OutputDisplay.Text))
            {
                MessageBox.Show("No output to save.", "Save Output", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = ".txt",
                Title = "Save Output"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, OutputDisplay.Text);
                    MessageBox.Show("Output saved successfully.", "Save Output", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving output: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearPlotButton_Click(object sender, RoutedEventArgs e)
        {
            PlotDisplay.Source = null;
            currentPlotPath = null;
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var tab = (TabItem)((Button)sender).Tag;
            CloseTab(tab);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        // Notes functionality
        private void LoadNoteFromDatabase()
        {
            try
            {
                if (dbConnection == null) return;

                // Load note path
                string selectNotePathQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'CurrentNotePath'";
                using (var command = new SQLiteCommand(selectNotePathQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentNotePath = reader["SettingValue"].ToString();
                    }
                }

                // Load zoom level
                string selectZoomLevelQuery = "SELECT SettingValue FROM Settings WHERE SettingKey = 'NoteZoomLevel'";
                using (var command = new SQLiteCommand(selectZoomLevelQuery, dbConnection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read() && double.TryParse(reader["SettingValue"].ToString(), out double zoom))
                    {
                        noteZoomLevel = zoom;
                    }
                }

                // If there's a note path, load it
                if (!string.IsNullOrEmpty(currentNotePath) && File.Exists(currentNotePath))
                {
                    NotesWebView.Source = new Uri(currentNotePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading note from database: {ex.Message}");
            }
        }

        private void SaveNoteSettingsToDatabase()
        {
            try
            {
                if (dbConnection == null) return;

                // Save note path
                string upsertNotePathQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('CurrentNotePath', @value)";
                using (var command = new SQLiteCommand(upsertNotePathQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", currentNotePath ?? "");
                    command.ExecuteNonQuery();
                }

                // Save zoom level
                string upsertZoomLevelQuery = @"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue) 
                    VALUES ('NoteZoomLevel', @value)";
                using (var command = new SQLiteCommand(upsertZoomLevelQuery, dbConnection))
                {
                    command.Parameters.AddWithValue("@value", noteZoomLevel.ToString());
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving note settings to database: {ex.Message}");
            }
        }

        private void OpenNoteButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*",
                Title = "Open Note"
            };

            if (dialog.ShowDialog() == true)
            {
                currentNotePath = dialog.FileName;
                // Convert local file path to URI for WebView2
                var uri = new Uri(currentNotePath);
                NotesWebView.Source = uri;
                // Reset zoom to default
                noteZoomLevel = 1.0;
                ApplyNoteZoom();
                // Save to database
                SaveNoteSettingsToDatabase();
            }
        }

        private void ZoomInNoteButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomInNote();
        }

        private void ZoomInNote()
        {
            noteZoomLevel += 0.1;
            ApplyNoteZoom();
            SaveNoteSettingsToDatabase();
        }

        private void ZoomOutNoteButton_Click(object sender, RoutedEventArgs e)
        {
            ZoomOutNote();
        }

        private void ZoomOutNote()
        {
            noteZoomLevel = Math.Max(0.1, noteZoomLevel - 0.1);
            ApplyNoteZoom();
            SaveNoteSettingsToDatabase();
        }

        private void ResetZoomNoteButton_Click(object sender, RoutedEventArgs e)
        {
            noteZoomLevel = 1.0;
            ApplyNoteZoom();
            SaveNoteSettingsToDatabase();
        }
    }

    public class CodeDocument
    {
        public string FilePath { get; set; }
        public string Content { get; set; }
        public bool IsModified { get; set; }
        public bool IsActive { get; set; }
    }
}