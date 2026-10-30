using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class ColumnPreferenceItem
{
    public string ColumnId { get; set; } = string.Empty;
    public string HeaderText { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public double Width { get; set; } = 80;
}

public static class UserPreferences
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RoBIM",
        "IssueTracker");

    private static readonly string ColumnsFile = Path.Combine(SettingsDir, "user_columns.json");

    public static List<ColumnPreferenceItem> GetDefaultColumns() =>
    [
        new ColumnPreferenceItem { ColumnId = "Number", HeaderText = "번호", IsVisible = true, Width = 55 },
        new ColumnPreferenceItem { ColumnId = "Status", HeaderText = "상태", IsVisible = true, Width = 75 },
        new ColumnPreferenceItem { ColumnId = "Priority", HeaderText = "우선순위", IsVisible = true, Width = 70 },
        new ColumnPreferenceItem { ColumnId = "Discipline1", HeaderText = "공종1", IsVisible = true, Width = 65 },
        new ColumnPreferenceItem { ColumnId = "Discipline2", HeaderText = "공종2", IsVisible = true, Width = 75 },
        new ColumnPreferenceItem { ColumnId = "Title", HeaderText = "제목", IsVisible = true, Width = 200 },
        new ColumnPreferenceItem { ColumnId = "AssignedTo", HeaderText = "담당자", IsVisible = true, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "CreatedBy", HeaderText = "작성자", IsVisible = true, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "LinkedView", HeaderText = "연계 뷰", IsVisible = true, Width = 110 },
        new ColumnPreferenceItem { ColumnId = "Elements", HeaderText = "연계 객체", IsVisible = false, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "DueDate", HeaderText = "마감일", IsVisible = true, Width = 85 },
        new ColumnPreferenceItem { ColumnId = "Location", HeaderText = "위치/층", IsVisible = false, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "CreatedAt", HeaderText = "작성일시", IsVisible = false, Width = 110 }
    ];

    public static List<ColumnPreferenceItem> LoadColumns()
    {
        var defaults = GetDefaultColumns();
        if (File.Exists(ColumnsFile))
        {
            try
            {
                string json = File.ReadAllText(ColumnsFile);
                var loaded = JsonSerializer.Deserialize<List<ColumnPreferenceItem>>(json);
                if (loaded != null && loaded.Count > 0)
                {
                    // Merge any newly introduced columns that might be missing in saved config
                    var map = loaded.ToDictionary(x => x.ColumnId);
                    foreach (var def in defaults)
                    {
                        if (!map.ContainsKey(def.ColumnId))
                        {
                            loaded.Add(def);
                        }
                    }
                    return loaded;
                }
            }
            catch
            {
                // Fallback to defaults
            }
        }
        return defaults;
    }

    public static void SaveColumns(List<ColumnPreferenceItem> items)
    {
        try
        {
            if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ColumnsFile, json);
        }
        catch
        {
            // Ignore settings save failure
        }
    }
}

public sealed class ColumnConfigDialog : Window
{
    private readonly List<ColumnPreferenceItem> items;
    private readonly StackPanel checkStack = new();
    private readonly List<CheckBox> checkBoxes = [];

    public bool IsSaved { get; private set; }

    public ColumnConfigDialog(List<ColumnPreferenceItem> currentItems)
    {
        items = currentItems.Select(x => new ColumnPreferenceItem
        {
            ColumnId = x.ColumnId,
            HeaderText = x.HeaderText,
            IsVisible = x.IsVisible,
            Width = x.Width
        }).ToList();

        RoBimTheme.Apply(this);
        Title = "목록 열 설정 (사용자별 개별 저장)";
        Width = 360;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI();
    }

    public List<ColumnPreferenceItem> GetResult() => items;

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(14) };
        Content = root;

        // Bottom Actions
        var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        var saveBtn = new Button { Content = "저장 및 적용", MinWidth = 90, Margin = new Thickness(0, 0, 6, 0) };
        var cancelBtn = new Button { Content = "취소", MinWidth = 70 };

        saveBtn.Click += (s, e) =>
        {
            for (int i = 0; i < checkBoxes.Count; i++)
            {
                items[i].IsVisible = checkBoxes[i].IsChecked == true;
            }
            IsSaved = true;
            DialogResult = true;
            Close();
        };

        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(saveBtn);
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Header Guide
        var info = new TextBlock
        {
            Text = "이슈 목록 테이블에 표시할 열을 선택하세요.\n(현재 PC 로그인 사용자 전용으로 저장됩니다.)",
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(info, Dock.Top);
        root.Children.Add(info);

        // Checkbox List inside ScrollViewer
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Content = checkStack;

        foreach (var col in items)
        {
            var cb = new CheckBox
            {
                Content = col.HeaderText,
                IsChecked = col.IsVisible,
                Margin = new Thickness(4, 5, 4, 5),
                FontWeight = FontWeights.Normal
            };
            checkBoxes.Add(cb);
            checkStack.Children.Add(cb);
        }

        root.Children.Add(scroll);
    }
}
