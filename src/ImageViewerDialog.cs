using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class ImageViewerDialog : Window
{
    private readonly string imagePath;
    private readonly Image displayImage = new();
    private readonly ScrollViewer scrollViewer = new();
    private readonly ScaleTransform scaleTransform = new(1.0, 1.0);

    public ImageViewerDialog(string imagePath, string title)
    {
        this.imagePath = imagePath;

        RoBimTheme.Apply(this);
        Title = $"스냅샷 크게 보기 · {title}";
        Width = 960;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI();
        LoadImage();
    }

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(8) };
        Content = root;

        // Bottom Bar
        var bottomBar = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(bottomBar, Dock.Bottom);

        var rightBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var openExtBtn = new Button { Content = "외부 뷰어로 열기", MinWidth = 120, Margin = new Thickness(0, 0, 6, 0) };
        var closeBtn = new Button { Content = "닫기", MinWidth = 80 };

        openExtBtn.Click += (s, e) =>
        {
            if (File.Exists(imagePath))
            {
                Process.Start(new ProcessStartInfo { FileName = imagePath, UseShellExecute = true });
            }
        };
        closeBtn.Click += (s, e) => Close();

        rightBtns.Children.Add(openExtBtn);
        rightBtns.Children.Add(closeBtn);
        bottomBar.Children.Add(rightBtns);

        var zoomInfo = new TextBlock
        {
            Text = "마우스 휠: 확대/축소 | 더블클릭: 원본 크기 리셋",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        };
        bottomBar.Children.Add(zoomInfo);
        root.Children.Add(bottomBar);

        // Center Scroll & Image
        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

        displayImage.LayoutTransform = scaleTransform;
        displayImage.Stretch = Stretch.None;

        scrollViewer.Content = displayImage;
        root.Children.Add(scrollViewer);

        // Zoom Events
        scrollViewer.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control || true)
            {
                double zoom = e.Delta > 0 ? 1.15 : 0.85;
                double newX = Math.Clamp(scaleTransform.ScaleX * zoom, 0.2, 5.0);
                double newY = Math.Clamp(scaleTransform.ScaleY * zoom, 0.2, 5.0);
                scaleTransform.ScaleX = newX;
                scaleTransform.ScaleY = newY;
                e.Handled = true;
            }
        };

        displayImage.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
            }
        };
    }

    private void LoadImage()
    {
        if (File.Exists(imagePath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(imagePath);
                bmp.EndInit();
                displayImage.Source = bmp;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"이미지를 불러올 수 없습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
