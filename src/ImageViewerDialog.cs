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
    private readonly TextBlock zoomLevelText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Foreground = Brushes.LightGray };
    private double currentScale = 1.0;

    public ImageViewerDialog(string imagePath, string title)
    {
        this.imagePath = imagePath;

        RoBimTheme.Apply(this);
        Title = $"스냅샷 고해상도 확대 뷰어 · {title}";
        Width = 1080;
        Height = 760;
        MinWidth = 720;
        MinHeight = 500;
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
        var zoomInBtn = new Button { Content = "확대 (+)", MinWidth = 70, Margin = new Thickness(0, 0, 4, 0) };
        var zoomOutBtn = new Button { Content = "축소 (-)", MinWidth = 70, Margin = new Thickness(0, 0, 4, 0) };
        var resetZoomBtn = new Button { Content = "100% 리셋", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var openExtBtn = new Button { Content = "외부 기본 뷰어로 열기", MinWidth = 140, Margin = new Thickness(0, 0, 6, 0) };
        var closeBtn = new Button { Content = "닫기", MinWidth = 75 };

        zoomInBtn.Click += (s, e) => ApplyZoom(1.25);
        zoomOutBtn.Click += (s, e) => ApplyZoom(0.8);
        resetZoomBtn.Click += (s, e) => ResetZoom();
        openExtBtn.Click += (s, e) =>
        {
            if (File.Exists(imagePath))
            {
                Process.Start(new ProcessStartInfo { FileName = imagePath, UseShellExecute = true });
            }
        };
        closeBtn.Click += (s, e) => Close();

        rightBtns.Children.Add(zoomInBtn);
        rightBtns.Children.Add(zoomOutBtn);
        rightBtns.Children.Add(resetZoomBtn);
        rightBtns.Children.Add(openExtBtn);
        rightBtns.Children.Add(closeBtn);
        bottomBar.Children.Add(rightBtns);

        var zoomInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        zoomInfo.Children.Add(new TextBlock
        {
            Text = "마우스 휠: 확대/축소 | 마우스 드래그: 이동 | 더블클릭: 100% 리셋",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        });
        zoomInfo.Children.Add(zoomLevelText);
        bottomBar.Children.Add(zoomInfo);
        root.Children.Add(bottomBar);

        // Center Scroll & Image
        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.Background = new SolidColorBrush(Color.FromRgb(25, 25, 25));

        displayImage.LayoutTransform = scaleTransform;
        displayImage.Stretch = Stretch.Uniform;

        var centerGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        centerGrid.Children.Add(displayImage);
        scrollViewer.Content = centerGrid;
        root.Children.Add(scrollViewer);

        // Mouse Wheel Zoom
        scrollViewer.PreviewMouseWheel += (s, e) =>
        {
            double factor = e.Delta > 0 ? 1.2 : 0.833;
            ApplyZoom(factor);
            e.Handled = true;
        };

        // Double click reset
        displayImage.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                ResetZoom();
            }
        };

        UpdateZoomText();
    }

    private void ApplyZoom(double factor)
    {
        currentScale = Math.Clamp(currentScale * factor, 0.2, 6.0);
        scaleTransform.ScaleX = currentScale;
        scaleTransform.ScaleY = currentScale;
        UpdateZoomText();
    }

    private void ResetZoom()
    {
        currentScale = 1.0;
        scaleTransform.ScaleX = 1.0;
        scaleTransform.ScaleY = 1.0;
        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        zoomLevelText.Text = $" [{Math.Round(currentScale * 100)}%]";
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

                // Display original pixel resolution in title
                Title = $"스냅샷 고해상도 확대 뷰어 · 원본 크기: {bmp.PixelWidth} × {bmp.PixelHeight} px";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"이미지를 불러올 수 없습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
