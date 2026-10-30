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
    public bool IsCustomField { get; set; }
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
        new ColumnPreferenceItem { ColumnId = "DisciplinesDisplay", HeaderText = "공종(간섭)", IsVisible = true, Width = 110 },
        new ColumnPreferenceItem { ColumnId = "Title", HeaderText = "제목", IsVisible = true, Width = 200 },
        new ColumnPreferenceItem { ColumnId = "AssignedTo", HeaderText = "담당자", IsVisible = true, Width = 110 },
        new ColumnPreferenceItem { ColumnId = "CreatedBy", HeaderText = "작성자", IsVisible = true, Width = 110 },
        new ColumnPreferenceItem { ColumnId = "CommentsSummary", HeaderText = "댓글", IsVisible = true, Width = 55 },
        new ColumnPreferenceItem { ColumnId = "LinkedView", HeaderText = "연계 뷰", IsVisible = true, Width = 110 },
        new ColumnPreferenceItem { ColumnId = "Elements", HeaderText = "연계 객체", IsVisible = false, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "DueDate", HeaderText = "마감일", IsVisible = true, Width = 85 },
        new ColumnPreferenceItem { ColumnId = "Type", HeaderText = "유형", IsVisible = false, Width = 80 },
        new ColumnPreferenceItem { ColumnId = "RootCause", HeaderText = "근본 원인", IsVisible = false, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "Location", HeaderText = "위치/층", IsVisible = false, Width = 90 },
        new ColumnPreferenceItem { ColumnId = "CreatedAt", HeaderText = "작성일시", IsVisible = false, Width = 95 }
    ];

    public static List<ColumnPreferenceItem> LoadColumns(List<CustomFieldDefinition>? customFields = null)
    {
        var defaults = GetDefaultColumns();

        // Add custom fields as optional columns
        if (customFields != null)
        {
            foreach (var cf in customFields)
            {
                defaults.Add(new ColumnPreferenceItem
                {
                    ColumnId = "CF_" + cf.Id,
                    HeaderText = cf.Name,
                    IsVisible = false,
                    Width = 90,
                    IsCustomField = true
                });
            }
        }

        if (File.Exists(ColumnsFile))
        {
            try
            {
                string json = File.ReadAllText(ColumnsFile);
                var loaded = JsonSerializer.Deserialize<List<ColumnPreferenceItem>>(json);
                if (loaded != null && loaded.Count > 0)
                {
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
                // Fallback
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
            // Ignore
        }
    }
}

public sealed class ColumnConfigDialog : Window
{
    private readonly List<ColumnPreferenceItem> items;
    private readonly ListView listView = new();
    private readonly List<TextBox> headerBoxes = [];
    private readonly List<CheckBox> checkBoxes = [];

    public bool IsSaved { get; private set; }

    public ColumnConfigDialog(List<ColumnPreferenceItem> currentItems)
    {
        items = currentItems.Select(x => new ColumnPreferenceItem
        {
            ColumnId = x.ColumnId,
            HeaderText = x.HeaderText,
            IsVisible = x.IsVisible,
            Width = x.Width,
            IsCustomField = x.IsCustomField
        }).ToList();

        RoBimTheme.Apply(this);
        Title = "목록 상단 제목(열 헤더) 및 표시 설정";
        Width = 460;
        Height = 520;
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
            for (int i = 0; i < items.Count; i++)
            {
                items[i].IsVisible = checkBoxes[i].IsChecked == true;
                string customTitle = headerBoxes[i].Text.Trim();
                if (!string.IsNullOrWhiteSpace(customTitle))
                {
                    items[i].HeaderText = customTitle;
                }
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
            Text = "상단 제목(헤더 텍스트)을 원하는 이름으로 직접 변경하거나,\n보고 싶은 열을 체크하여 사용자 맞춤 테이블로 설정할 수 있습니다.",
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(info, Dock.Top);
        root.Children.Add(info);

        // List with Edit boxes
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel();
        scroll.Content = stack;

        var headerGrid = new Grid { Margin = new Thickness(4, 0, 4, 6) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.Children.Add(new TextBlock { Text = "표시", FontWeight = FontWeights.Bold });
        var lbl1 = new TextBlock { Text = "필드 식별자", FontWeight = FontWeights.Bold };
        Grid.SetColumn(lbl1, 1); headerGrid.Children.Add(lbl1);
        var lbl2 = new TextBlock { Text = "상단 제목(헤더 텍스트 수정)", FontWeight = FontWeights.Bold };
        Grid.SetColumn(lbl2, 2); headerGrid.Children.Add(lbl2);
        stack.Children.Add(headerGrid);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var rowGrid = new Grid { Margin = new Thickness(4, 3, 4, 3) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cb = new CheckBox { IsChecked = item.IsVisible, VerticalAlignment = VerticalAlignment.Center };
            checkBoxes.Add(cb);
            rowGrid.Children.Add(cb);

            var idText = new TextBlock
            {
                Text = item.ColumnId + (item.IsCustomField ? " (커스텀)" : ""),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkSlateGray
            };
            Grid.SetColumn(idText, 1);
            rowGrid.Children.Add(idText);

            var titleBox = new TextBox { Text = item.HeaderText, Margin = new Thickness(2, 0, 2, 0) };
            headerBoxes.Add(titleBox);
            Grid.SetColumn(titleBox, 2);
            rowGrid.Children.Add(titleBox);

            stack.Children.Add(rowGrid);
        }

        root.Children.Add(scroll);
    }
}
