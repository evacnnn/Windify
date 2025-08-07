using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Windify
{
    public partial class MainWindow : Window
    {
        private IntPtr selectedWindow = IntPtr.Zero;
        private List<WindowInfo> openWindows = new();

        public MainWindow()
        {
            InitializeComponent();
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

        #endregion

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
                    openWindows.Add(new WindowInfo { Handle = hWnd, Title = title });
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
                selectedWindow = openWindows.Find(w => w.Title == selectedTitle)?.Handle ?? IntPtr.Zero;
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
            // Se maneja automáticamente con StaysOpen=False
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

            byte alpha = (byte)(e.NewValue * 2.55); // 0-100 -> 0-255
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

        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public required string Title { get; set; }
        }


    }
}