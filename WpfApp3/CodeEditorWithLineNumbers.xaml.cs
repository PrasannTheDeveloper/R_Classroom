using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace WpfApp3
{
    public partial class CodeEditorWithLineNumbers : UserControl
    {
        public TextBox CodeTextBox => codeTextBox;
        private ScrollViewer _codeScrollViewer;
        private bool _isUpdatingScroll = false;

        public CodeEditorWithLineNumbers()
        {
            InitializeComponent();
            codeTextBox.TextChanged += CodeTextBox_TextChanged;
            codeTextBox.Loaded += CodeTextBox_Loaded;
            codeTextBox.SizeChanged += CodeTextBox_SizeChanged;
            UpdateLineNumbers();
        }

        private void CodeTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the ScrollViewer from the TextBox's template
            _codeScrollViewer = GetScrollViewer(codeTextBox);
            if (_codeScrollViewer != null)
            {
                _codeScrollViewer.ScrollChanged += CodeScrollViewer_ScrollChanged;
                // Initial update
                UpdateLineNumbers();
                SyncScrollPositions();
            }
        }

        private void CodeTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLineNumbers();
        }

        private ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer)
                return scrollViewer;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void CodeScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingScroll) return;

            _isUpdatingScroll = true;
            try
            {
                SyncScrollPositions();
            }
            finally
            {
                _isUpdatingScroll = false;
            }
        }

        private void SyncScrollPositions()
        {
            if (_codeScrollViewer == null) return;

            // Sync the line numbers scroll position with the code editor
            lineNumbersScrollViewer.ScrollToVerticalOffset(_codeScrollViewer.VerticalOffset);
            lineNumbersScrollViewer.ScrollToHorizontalOffset(_codeScrollViewer.HorizontalOffset);

            // Update the line numbers to show only visible lines
            UpdateVisibleLineNumbers();
        }

        private void CodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLineNumbers();
        }

        private void UpdateLineNumbers()
        {
            int lineCount = codeTextBox.LineCount;
            var lineNumbers = new StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                lineNumbers.AppendLine(i.ToString());
            }
            lineNumbersTextBlock.Text = lineNumbers.ToString();

            // Adjust width of line numbers based on the number of digits
            int maxDigits = lineCount.ToString().Length;
            double width = maxDigits * 7 + 20; // 20 for padding
            lineNumbersTextBlock.Width = width;

            // Update visible line numbers
            UpdateVisibleLineNumbers();
        }

        private void UpdateVisibleLineNumbers()
        {
            if (_codeScrollViewer == null) return;

            // Calculate the first visible line and the number of visible lines
            int firstVisibleLine = (int)(_codeScrollViewer.VerticalOffset / GetLineHeight());
            int visibleLineCount = (int)(_codeScrollViewer.ViewportHeight / GetLineHeight()) + 2; // +2 for buffer

            // Update the line numbers to show only the visible lines
            var lineNumbers = new StringBuilder();
            int totalLines = codeTextBox.LineCount;

            for (int i = firstVisibleLine; i < firstVisibleLine + visibleLineCount && i < totalLines; i++)
            {
                lineNumbers.AppendLine((i + 1).ToString());
            }

            lineNumbersTextBlock.Text = lineNumbers.ToString();

            // Adjust the line numbers container to match the viewport height
            lineNumbersScrollViewer.Height = _codeScrollViewer.ViewportHeight;
        }

        private double GetLineHeight()
        {
            // Get the height of a single line
            if (codeTextBox.LineCount > 0)
            {
                try
                {
                    var firstLineRect = codeTextBox.GetRectFromCharacterIndex(
                        codeTextBox.GetCharacterIndexFromLineIndex(0));
                    var secondLineRect = codeTextBox.GetRectFromCharacterIndex(
                        codeTextBox.GetCharacterIndexFromLineIndex(1));
                    return secondLineRect.Top - firstLineRect.Top;
                }
                catch
                {
                    // Fallback to font size
                    return codeTextBox.FontSize * 1.2;
                }
            }
            return codeTextBox.FontSize * 1.2;
        }

        public new string Text
        {
            get => codeTextBox.Text;
            set => codeTextBox.Text = value;
        }

        public int CaretIndex
        {
            get => codeTextBox.CaretIndex;
            set => codeTextBox.CaretIndex = value;
        }

        public void HighlightErrorLines(int[] errorLines)
        {
            // Reset all line colors
            lineNumbersTextBlock.Foreground = new SolidColorBrush(Colors.Black);
            if (errorLines == null || errorLines.Length == 0)
            {
                UpdateVisibleLineNumbers();
                return;
            }

            // Update the visible line numbers with error indicators
            if (_codeScrollViewer == null) return;

            int firstVisibleLine = (int)(_codeScrollViewer.VerticalOffset / GetLineHeight());
            int visibleLineCount = (int)(_codeScrollViewer.ViewportHeight / GetLineHeight()) + 2;

            var lineNumbers = new StringBuilder();
            int totalLines = codeTextBox.LineCount;

            for (int i = firstVisibleLine; i < firstVisibleLine + visibleLineCount && i < totalLines; i++)
            {
                if (errorLines.Contains(i + 1))
                {
                    lineNumbers.AppendLine($"⚠️ {i + 1}");
                }
                else
                {
                    lineNumbers.AppendLine((i + 1).ToString());
                }
            }

            lineNumbersTextBlock.Text = lineNumbers.ToString();
        }

        public void SetTransparency(double opacity)
        {
            // Set background opacity for both codeTextBox and line numbers
            var editorBrush = new SolidColorBrush(Colors.White) { Opacity = opacity };
            codeTextBox.Background = editorBrush;

            var lineNumbersBrush = new SolidColorBrush(Color.FromRgb(238, 238, 238)) { Opacity = opacity };
            ((Border)lineNumbersScrollViewer.Parent).Background = lineNumbersBrush;

            // Set foreground colors
            codeTextBox.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            lineNumbersTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128));

            this.Background = Brushes.Transparent;
        }
    }
}