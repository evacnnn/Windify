using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Windify
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private IntPtr selectedWindow = IntPtr.Zero;
        private List<WindowInfo> openWindows = new();

        public List<WindowInfo> OpenWindows => openWindows;

        private WindowInfo selectedApp;
        public WindowInfo SelectedApp
        {
            get => selectedApp;
            set
            {
                selectedApp = value;
                OnPropertyChanged(nameof(SelectedApp));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            RefreshWindowList();
        }

        #region Win32 API

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxLength);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint LWA_ALPHA = 0x2;

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL2 = 2;
        private const int GCL_HICON = -14;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr", SetLastError = true)]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        #endregion

        private static ImageSource? GetIconFromWindow(IntPtr hWnd)
        {
            IntPtr hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, 0);

            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetClassLongPtr(hWnd, GCL_HICON);
            }

            if (hIcon == IntPtr.Zero)
            {
                return null;
            }

            return Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        private void RefreshWindowList()
        {
            openWindows.Clear();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                var builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);

                string title = builder.ToString();

                if (!string.IsNullOrWhiteSpace(title))
                {
                    var icon = GetIconFromWindow(hWnd);

                    openWindows.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        Icon = icon
                    });
                }

                return true;
            }, IntPtr.Zero);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower();
            var matches = openWindows.FindAll(w => w.Title.ToLower().Contains(filter));

            SuggestionsListBox.Items.Clear();
            foreach (var match in matches)
            {
                SuggestionsListBox.Items.Add(match.Title);
            }

            SuggestionsPopup.IsOpen = matches.Count > 0;
        }

        private void SuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionsListBox.SelectedItem is string selectedTitle)
            {
                SearchBox.Text = selectedTitle;
                SelectedApp = openWindows.Find(w => w.Title == selectedTitle);
                selectedWindow = SelectedApp?.Handle ?? IntPtr.Zero;
            }

            SuggestionsPopup.IsOpen = false;
            SearchBox.CaretIndex = SearchBox.Text.Length;
            SearchBox.Focus();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                SuggestionsPopup.IsOpen = true;
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (SuggestionsPopup.IsOpen && e.Key == Key.Down)
            {
                SuggestionsListBox.Focus();
                SuggestionsListBox.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selectedWindow == IntPtr.Zero) return;

            int exStyle = GetWindowLong(selectedWindow, GWL_EXSTYLE);
            SetWindowLong(selectedWindow, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

            byte alpha = (byte)(e.NewValue * 2.55);
            SetLayeredWindowAttributes(selectedWindow, 0, alpha, LWA_ALPHA);
        }

        private void TopOnView_On_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWindow == IntPtr.Zero) return;

            SetWindowPos(selectedWindow, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private void TopOnView_Off_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWindow == IntPtr.Zero) return;

            SetWindowPos(selectedWindow, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public required string Title { get; set; }
            public ImageSource? Icon { get; set; }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowList();
            SearchBox.Clear();
            SuggestionsPopup.IsOpen = false;
            selectedWindow = IntPtr.Zero;
            SelectedApp = new WindowInfo
            {
                Handle = IntPtr.Zero,
                Title = string.Empty,
                Icon = null
            };
            selectedWindow = IntPtr.Zero;
        }
    }
}
