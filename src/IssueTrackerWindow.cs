using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RoBIM.Wpf;

using WpfGrid = System.Windows.Controls.Grid;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfVisibility = System.Windows.Visibility;

namespace RoBIM.Feature;

public sealed class IssueTrackerWindow : Window
{
    public static IssueTrackerWindow? Instance { get; private set; }

    private readonly Document doc;
    private readonly IssueStorage storage;
    private readonly ExternalEvent revitEvent;
    private readonly IssueExternalEventHandler eventHandler;

    private IssueProjectConfig config;
    private readonly ObservableCollection<IssueItem> issues = [];
    private IssueItem? currentEditingIssue;

    // UI Controls - Header
    private readonly TextBlock folderPathText = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 450 };
    private readonly Button openFolderBtn = new() { Content = "폴더 열기", Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button newIssueBtn = new() { Content = "+ 새 이슈 등록", MinWidth = 110, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button configFieldsBtn = new() { Content = "커스텀 필드 설정", Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button refreshBtn = new() { Content = "새로고침", Margin = new Thickness(6, 0, 0, 0) };

    // UI Controls - Filter & List
    private readonly WpfTextBox searchBox = new() { MinWidth = 160 };
    private readonly WpfComboBox statusFilterCombo = new() { MinWidth = 110 };
    private readonly DataGrid issueGrid = new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.All
    };

    // UI Controls - Form
    private readonly TextBlock formHeader = new() { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
    private readonly WpfTextBox titleBox = new();
    private readonly WpfComboBox statusCombo = new();
    private readonly WpfComboBox typeCombo = new();
    private readonly WpfTextBox assignedToBox = new();
    private readonly DatePicker dueDatePicker = new();
    private readonly WpfTextBox locationBox = new();
    private readonly WpfComboBox rootCauseCombo = new();
    private readonly WpfTextBox descBox = new() { AcceptsReturn = true, MinHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    // Element Link Controls
    private readonly TextBlock linkedElementsText = new() { Text = "(선택된 객체 없음)", VerticalAlignment = VerticalAlignment.Center };
    private readonly Button pickElementsBtn = new() { Content = "뷰에서 선택", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button getSelectedElementsBtn = new() { Content = "현재 선택 객체", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button highlightElementsBtn = new() { Content = "객체로 이동/줌", Margin = new Thickness(4, 0, 0, 0), IsEnabled = false };
    private readonly Button clearElementsBtn = new() { Content = "해제", Margin = new Thickness(4, 0, 0, 0) };

    // Snapshot Controls
    private readonly System.Windows.Controls.Image snapshotPreview = new() { MaxHeight = 160, Stretch = Stretch.Uniform, Margin = new Thickness(0, 4, 0, 4) };
    private readonly Button captureViewBtn = new() { Content = "현재 화면 캡처", Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button clearSnapshotBtn = new() { Content = "캡처 삭제" };
    private string? currentSnapshotFileName;

    // Dynamic Custom Fields Container
    private readonly StackPanel customFieldsPanel = new();
    private readonly Dictionary<string, System.Windows.UIElement> customFieldControls = [];

    // Form Action Buttons
    private readonly Button saveIssueBtn = new() { Content = "저장", MinWidth = 90, FontWeight = FontWeights.SemiBold };
    private readonly Button deleteIssueBtn = new() { Content = "삭제", MinWidth = 70, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button cancelEditBtn = new() { Content = "취소", MinWidth = 70, Margin = new Thickness(6, 0, 0, 0) };

    // Status bar
    private readonly TextBlock statusBarText = new() { Text = "준비 완료" };

    public IssueTrackerWindow(Document doc, ExternalEvent revitEvent, IssueExternalEventHandler eventHandler, string version)
    {
        this.doc = doc;
        this.revitEvent = revitEvent;
        this.eventHandler = eventHandler;
        this.storage = new IssueStorage(doc);

        Instance = this;
        Closed += (s, e) => { Instance = null; };

        RoBimTheme.Apply(this);
        Title = $"BIM 이슈 검토 (ACC 스타일) · {version} · {doc.Title}";
        Width = 1080;
        Height = 720;
        MinWidth = 880;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        try
        {
            string asmDir = Path.GetDirectoryName(typeof(IssueTrackerWindow).Assembly.Location) ?? "";
            string iconPath = Path.Combine(asmDir, "Icons", "issue.png");
            if (File.Exists(iconPath))
            {
                Icon = new BitmapImage(new Uri(iconPath));
            }
        }
        catch
        {
            // Ignore icon loading error
        }

        config = storage.LoadConfig();

        SetupEventHandlerCallbacks();
        InitializeUI();
        LoadIssues();
        PrepareNewIssue();
    }

    private void SetupEventHandlerCallbacks()
    {
        eventHandler.OnElementsPicked = (elemIds, uniqueIds) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (currentEditingIssue == null) currentEditingIssue = new IssueItem();
                currentEditingIssue.ElementIds = elemIds;
                currentEditingIssue.UniqueIds = uniqueIds;
                UpdateLinkedElementsUI();
                SetStatus($"객체 {elemIds.Count}개가 연결되었습니다.");
            });
        };

        eventHandler.OnViewCaptured = (fileName) =>
        {
            Dispatcher.Invoke(() =>
            {
                currentSnapshotFileName = fileName;
                UpdateSnapshotPreview();
                SetStatus($"현재 뷰가 캡처되었습니다: {fileName}");
            });
        };

        eventHandler.OnError = (errMsg) =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(this, errMsg, "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatus(errMsg);
            });
        };
    }

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        // 1. Status Bar
        var statusBar = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(4, 6, 4, 0),
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(statusBar, Dock.Bottom);
        statusBar.Child = statusBarText;
        root.Children.Add(statusBar);

        // 2. Top Bar
        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(topBar, Dock.Top);

        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(topButtons, Dock.Right);
        newIssueBtn.Click += (s, e) => PrepareNewIssue();
        configFieldsBtn.Click += OpenConfigDialog;
        refreshBtn.Click += (s, e) => { LoadIssues(); SetStatus("이슈 목록을 새로고침했습니다."); };
        openFolderBtn.Click += (s, e) => OpenIssueFolder();

        topButtons.Children.Add(openFolderBtn);
        topButtons.Children.Add(configFieldsBtn);
        topButtons.Children.Add(refreshBtn);
        topButtons.Children.Add(newIssueBtn);
        topBar.Children.Add(topButtons);

        var folderInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        folderInfo.Children.Add(new TextBlock { Text = "저장 위치: ", FontWeight = FontWeights.SemiBold });
        folderPathText.Text = storage.IssueDirectory;
        folderPathText.ToolTip = storage.IssueDirectory;
        folderInfo.Children.Add(folderPathText);
        topBar.Children.Add(folderInfo);

        root.Children.Add(topBar);

        // 3. Main Split View: Left List (45%), Right Form (55%)
        var mainGrid = new WpfGrid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Splitter
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55, GridUnitType.Star) });

        var leftPanel = BuildLeftListPanel();
        WpfGrid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent };
        WpfGrid.SetColumn(splitter, 1);
        mainGrid.Children.Add(splitter);

        var rightPanel = BuildRightFormPanel();
        WpfGrid.SetColumn(rightPanel, 2);
        mainGrid.Children.Add(rightPanel);

        root.Children.Add(mainGrid);
    }

    private System.Windows.UIElement BuildLeftListPanel()
    {
        var panel = new DockPanel();

        // Search & Filter
        var filterBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(filterBar, Dock.Top);

        filterBar.Children.Add(new TextBlock { Text = "검색: ", VerticalAlignment = VerticalAlignment.Center });
        searchBox.TextChanged += (s, e) => ApplyFilter();
        filterBar.Children.Add(searchBox);

        filterBar.Children.Add(new TextBlock { Text = " 상태: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        statusFilterCombo.ItemsSource = new[] { "전체", "Open", "In Review", "Pending", "Closed", "Void" };
        statusFilterCombo.SelectedIndex = 0;
        statusFilterCombo.SelectionChanged += (s, e) => ApplyFilter();
        filterBar.Children.Add(statusFilterCombo);

        panel.Children.Add(filterBar);

        // Grid
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "번호", Binding = new System.Windows.Data.Binding("DisplayNumber"), Width = 55 });
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "상태", Binding = new System.Windows.Data.Binding("Status"), Width = 75 });
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "유형", Binding = new System.Windows.Data.Binding("Type"), Width = 80 });
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "제목", Binding = new System.Windows.Data.Binding("Title"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "담당자", Binding = new System.Windows.Data.Binding("AssignedTo"), Width = 80 });
        issueGrid.Columns.Add(new DataGridTextColumn { Header = "마감일", Binding = new System.Windows.Data.Binding("DueDate"), Width = 85 });

        issueGrid.SelectionChanged += (s, e) =>
        {
            if (issueGrid.SelectedItem is IssueItem selected)
            {
                EditIssue(selected);
            }
        };

        panel.Children.Add(issueGrid);
        return panel;
    }

    private System.Windows.UIElement BuildRightFormPanel()
    {
        var border = new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12)
        };

        var root = new DockPanel();
        border.Child = root;

        // Bottom Actions
        var bottomActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottomActions, Dock.Bottom);

        saveIssueBtn.Click += (s, e) => SaveCurrentIssue();
        deleteIssueBtn.Click += (s, e) => DeleteCurrentIssue();
        cancelEditBtn.Click += (s, e) => PrepareNewIssue();

        bottomActions.Children.Add(saveIssueBtn);
        bottomActions.Children.Add(deleteIssueBtn);
        bottomActions.Children.Add(cancelEditBtn);
        root.Children.Add(bottomActions);

        // Scrollable Form
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var form = new StackPanel();
        scroll.Content = form;
        root.Children.Add(scroll);

        form.Children.Add(formHeader);

        // Standard Fields Grid
        var g = new WpfGrid { Margin = new Thickness(0, 0, 0, 10) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < 4; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Row 0: Title (Span 3)
        AddGridRow(g, 0, "제목 *:", titleBox, 1, 3);

        // Row 1: Status & Type
        statusCombo.ItemsSource = new[] { "Open", "In Review", "Pending", "Closed", "Void" };
        AddGridRow(g, 1, "상태:", statusCombo, 1, 1);
        typeCombo.ItemsSource = new[] { "설계검토", "품질", "간섭(Clash)", "안전", "시공성", "기타" };
        AddGridRow(g, 1, "유형:", typeCombo, 3, 1);

        // Row 2: AssignedTo & DueDate
        AddGridRow(g, 2, "담당자:", assignedToBox, 1, 1);
        AddGridRow(g, 2, "마감일:", dueDatePicker, 3, 1);

        // Row 3: Location & RootCause
        AddGridRow(g, 3, "위치/층:", locationBox, 1, 1);
        rootCauseCombo.ItemsSource = new[] { "미지정", "설계 누락", "모델링 오류", "공종 간섭", "기준 미달", "발주처 요청", "기타" };
        AddGridRow(g, 3, "근본 원인:", rootCauseCombo, 3, 1);

        form.Children.Add(g);

        // Description
        form.Children.Add(new TextBlock { Text = "상세 설명:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) });
        form.Children.Add(descBox);

        // Linked Elements Section
        var elemGroup = new GroupBox { Header = "연계 객체 (ElementId)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var elemStack = new StackPanel();
        var elemBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        pickElementsBtn.Click += PickElementsFromRevit;
        getSelectedElementsBtn.Click += GetSelectedFromRevit;
        highlightElementsBtn.Click += HighlightElementsInRevit;
        clearElementsBtn.Click += (s, e) =>
        {
            if (currentEditingIssue != null)
            {
                currentEditingIssue.ElementIds.Clear();
                currentEditingIssue.UniqueIds.Clear();
                UpdateLinkedElementsUI();
            }
        };

        elemBar.Children.Add(pickElementsBtn);
        elemBar.Children.Add(getSelectedElementsBtn);
        elemBar.Children.Add(highlightElementsBtn);
        elemBar.Children.Add(clearElementsBtn);
        elemStack.Children.Add(elemBar);
        elemStack.Children.Add(linkedElementsText);
        elemGroup.Content = elemStack;
        form.Children.Add(elemGroup);

        // Snapshot Section
        var snapGroup = new GroupBox { Header = "화면 캡처 (스냅샷)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var snapStack = new StackPanel();
        var snapBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        captureViewBtn.Click += CaptureActiveViewFromRevit;
        clearSnapshotBtn.Click += (s, e) =>
        {
            currentSnapshotFileName = null;
            UpdateSnapshotPreview();
        };

        snapBar.Children.Add(captureViewBtn);
        snapBar.Children.Add(clearSnapshotBtn);
        snapStack.Children.Add(snapBar);
        snapStack.Children.Add(snapshotPreview);
        snapGroup.Content = snapStack;
        form.Children.Add(snapGroup);

        // Dynamic Custom Fields Group
        var customGroup = new GroupBox { Header = "사용자 정의 필드", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        customGroup.Content = customFieldsPanel;
        form.Children.Add(customGroup);

        return border;
    }

    private void AddGridRow(WpfGrid grid, int row, string label, System.Windows.UIElement element, int col, int colSpan)
    {
        var lbl = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(col > 1 ? 8 : 0, 4, 4, 4),
            FontWeight = FontWeights.SemiBold
        };
        WpfGrid.SetRow(lbl, row);
        WpfGrid.SetColumn(lbl, col - 1);
        grid.Children.Add(lbl);

        element.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 4));
        WpfGrid.SetRow(element, row);
        WpfGrid.SetColumn(element, col);
        if (colSpan > 1) WpfGrid.SetColumnSpan(element, colSpan);
        grid.Children.Add(element);
    }

    private void LoadIssues()
    {
        issues.Clear();
        var list = storage.LoadAllIssues();
        foreach (var item in list)
        {
            issues.Add(item);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = searchBox.Text.Trim().ToLowerInvariant();
        string statusFilter = statusFilterCombo.SelectedItem?.ToString() ?? "전체";

        var filtered = issues.Where(i =>
        {
            bool matchStatus = statusFilter == "전체" || i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase);
            bool matchQuery = string.IsNullOrWhiteSpace(query) ||
                              i.Title.ToLowerInvariant().Contains(query) ||
                              i.DisplayNumber.ToLowerInvariant().Contains(query) ||
                              i.AssignedTo.ToLowerInvariant().Contains(query) ||
                              i.Location.ToLowerInvariant().Contains(query);
            return matchStatus && matchQuery;
        }).ToList();

        issueGrid.ItemsSource = filtered;
    }

    private void PrepareNewIssue()
    {
        currentEditingIssue = new IssueItem
        {
            Status = "Open",
            Type = "설계검토",
            RootCause = "미지정",
            CreatedBy = Environment.UserName
        };

        formHeader.Text = "새 이슈 작성";
        deleteIssueBtn.Visibility = WpfVisibility.Collapsed;
        PopulateForm(currentEditingIssue);
    }

    private void EditIssue(IssueItem item)
    {
        currentEditingIssue = item;
        formHeader.Text = $"이슈 수정 {item.DisplayNumber}";
        deleteIssueBtn.Visibility = WpfVisibility.Visible;
        PopulateForm(item);
    }

    private void PopulateForm(IssueItem item)
    {
        titleBox.Text = item.Title;
        statusCombo.SelectedItem = item.Status;
        typeCombo.SelectedItem = item.Type;
        assignedToBox.Text = item.AssignedTo;

        if (DateTime.TryParse(item.DueDate, out var dt))
            dueDatePicker.SelectedDate = dt;
        else
            dueDatePicker.SelectedDate = null;

        locationBox.Text = item.Location;
        rootCauseCombo.SelectedItem = item.RootCause;
        descBox.Text = item.Description;

        currentSnapshotFileName = item.SnapshotFileName;
        UpdateSnapshotPreview();
        UpdateLinkedElementsUI();

        RebuildCustomFieldsUI(item.CustomFields);
    }

    private void RebuildCustomFieldsUI(Dictionary<string, string> values)
    {
        customFieldsPanel.Children.Clear();
        customFieldControls.Clear();

        if (config.CustomFields.Count == 0)
        {
            customFieldsPanel.Children.Add(new TextBlock
            {
                Text = "(설정된 커스텀 필드가 없습니다. 상단 '커스텀 필드 설정'에서 추가하세요.)",
                Foreground = Brushes.Gray,
                Margin = new Thickness(4)
            });
            return;
        }

        var grid = new WpfGrid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int r = 0;
        foreach (var def in config.CustomFields)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = def.Name + ":",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 6, 4)
            };
            WpfGrid.SetRow(lbl, r);
            WpfGrid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            string currentVal = values.TryGetValue(def.Id, out var v) ? v : def.DefaultValue;

            System.Windows.UIElement inputElement;
            switch (def.FieldType)
            {
                case "Dropdown":
                    var combo = new WpfComboBox { ItemsSource = def.Options, SelectedItem = currentVal, Margin = new Thickness(0, 4, 0, 4) };
                    inputElement = combo;
                    break;

                case "Radio":
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                    string groupName = "grp_" + def.Id;
                    foreach (var opt in def.Options)
                    {
                        var rb = new RadioButton
                        {
                            Content = opt,
                            GroupName = groupName,
                            IsChecked = opt == currentVal,
                            Margin = new Thickness(0, 0, 10, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        sp.Children.Add(rb);
                    }
                    inputElement = sp;
                    break;

                case "Checkbox":
                    var chk = new CheckBox
                    {
                        IsChecked = currentVal == "true" || currentVal == "True",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    inputElement = chk;
                    break;

                case "Text":
                default:
                    var txt = new WpfTextBox { Text = currentVal, Margin = new Thickness(0, 4, 0, 4) };
                    inputElement = txt;
                    break;
            }

            WpfGrid.SetRow(inputElement, r);
            WpfGrid.SetColumn(inputElement, 1);
            grid.Children.Add(inputElement);

            customFieldControls[def.Id] = inputElement;
            r++;
        }

        customFieldsPanel.Children.Add(grid);
    }

    private Dictionary<string, string> CollectCustomFieldValues()
    {
        var dict = new Dictionary<string, string>();
        foreach (var def in config.CustomFields)
        {
            if (!customFieldControls.TryGetValue(def.Id, out var ctrl)) continue;

            if (ctrl is WpfComboBox cb)
            {
                dict[def.Id] = cb.SelectedItem?.ToString() ?? "";
            }
            else if (ctrl is WpfTextBox tb)
            {
                dict[def.Id] = tb.Text;
            }
            else if (ctrl is CheckBox chk)
            {
                dict[def.Id] = chk.IsChecked == true ? "true" : "false";
            }
            else if (ctrl is StackPanel sp)
            {
                var checkedRb = sp.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true);
                dict[def.Id] = checkedRb?.Content?.ToString() ?? "";
            }
        }
        return dict;
    }

    private void UpdateLinkedElementsUI()
    {
        if (currentEditingIssue == null || currentEditingIssue.ElementIds.Count == 0)
        {
            linkedElementsText.Text = "(선택된 객체 없음)";
            highlightElementsBtn.IsEnabled = false;
        }
        else
        {
            linkedElementsText.Text = $"{currentEditingIssue.ElementIds.Count}개 객체 선택됨 (ID: {string.Join(", ", currentEditingIssue.ElementIds)})";
            highlightElementsBtn.IsEnabled = true;
        }
    }

    private void UpdateSnapshotPreview()
    {
        if (string.IsNullOrWhiteSpace(currentSnapshotFileName))
        {
            snapshotPreview.Source = null;
            snapshotPreview.Visibility = WpfVisibility.Collapsed;
            clearSnapshotBtn.IsEnabled = false;
            return;
        }

        string fullPath = storage.GetSnapshotFullPath(currentSnapshotFileName);
        if (File.Exists(fullPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath);
                bmp.EndInit();
                snapshotPreview.Source = bmp;
                snapshotPreview.Visibility = WpfVisibility.Visible;
                clearSnapshotBtn.IsEnabled = true;
                return;
            }
            catch
            {
                // Fallthrough on image load failure
            }
        }

        snapshotPreview.Source = null;
        snapshotPreview.Visibility = WpfVisibility.Collapsed;
        clearSnapshotBtn.IsEnabled = false;
    }

    private void PickElementsFromRevit(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.PickElements;
        revitEvent.Raise();
        SetStatus("Revit 뷰에서 요소를 선택하세요...");
    }

    private void GetSelectedFromRevit(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.GetSelectedElements;
        revitEvent.Raise();
    }

    private void HighlightElementsInRevit(object sender, RoutedEventArgs e)
    {
        if (currentEditingIssue == null || currentEditingIssue.ElementIds.Count == 0) return;

        eventHandler.CurrentAction = IssueRevitAction.HighlightElements;
        eventHandler.TargetElementIds = currentEditingIssue.ElementIds;
        revitEvent.Raise();
        SetStatus($"객체 {currentEditingIssue.ElementIds.Count}개를 Revit 뷰에서 하이라이트합니다.");
    }

    private void CaptureActiveViewFromRevit(object sender, RoutedEventArgs e)
    {
        string baseName = (currentEditingIssue != null && !string.IsNullOrWhiteSpace(currentEditingIssue.Id))
            ? $"{currentEditingIssue.Id}_snapshot"
            : $"snap_{DateTime.Now:yyyyMMdd_HHmmss}";

        eventHandler.CurrentAction = IssueRevitAction.CaptureView;
        eventHandler.CaptureTargetFolder = storage.IssueDirectory;
        eventHandler.CaptureTargetBaseName = baseName;
        revitEvent.Raise();
        SetStatus("현재 활성 뷰를 캡처 중입니다...");
    }

    private void SaveCurrentIssue()
    {
        string title = titleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show(this, "이슈 제목을 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            titleBox.Focus();
            return;
        }

        if (currentEditingIssue == null) currentEditingIssue = new IssueItem();

        currentEditingIssue.Title = title;
        currentEditingIssue.Status = statusCombo.SelectedItem?.ToString() ?? "Open";
        currentEditingIssue.Type = typeCombo.SelectedItem?.ToString() ?? "설계검토";
        currentEditingIssue.AssignedTo = assignedToBox.Text.Trim();
        currentEditingIssue.DueDate = dueDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
        currentEditingIssue.Location = locationBox.Text.Trim();
        currentEditingIssue.RootCause = rootCauseCombo.SelectedItem?.ToString() ?? "미지정";
        currentEditingIssue.Description = descBox.Text;
        currentEditingIssue.SnapshotFileName = currentSnapshotFileName ?? "";
        currentEditingIssue.CustomFields = CollectCustomFieldValues();

        storage.SaveIssue(currentEditingIssue, config);

        SetStatus($"이슈 {currentEditingIssue.DisplayNumber} 저장 완료.");
        LoadIssues();
        PrepareNewIssue();
    }

    private void DeleteCurrentIssue()
    {
        if (currentEditingIssue == null || string.IsNullOrWhiteSpace(currentEditingIssue.Id)) return;

        var result = MessageBox.Show(this, $"이슈 {currentEditingIssue.DisplayNumber} ('{currentEditingIssue.Title}')를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            storage.DeleteIssue(currentEditingIssue.Id);
            SetStatus($"이슈 {currentEditingIssue.DisplayNumber} 삭제 완료.");
            LoadIssues();
            PrepareNewIssue();
        }
    }

    private void OpenConfigDialog(object sender, RoutedEventArgs e)
    {
        var dlg = new CustomFieldsDialog(config.CustomFields) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.IsSaved)
        {
            config.CustomFields = dlg.GetResultFields();
            storage.SaveConfig(config);
            RebuildCustomFieldsUI(currentEditingIssue?.CustomFields ?? []);
            SetStatus("커스텀 필드 설정이 저장되었습니다.");
        }
    }

    private void OpenIssueFolder()
    {
        if (Directory.Exists(storage.IssueDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = storage.IssueDirectory,
                UseShellExecute = true
            });
        }
    }

    private void SetStatus(string msg)
    {
        statusBarText.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}";
    }
}
