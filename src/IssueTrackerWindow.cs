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
using WpfBinding = System.Windows.Data.Binding;
using MediaColor = System.Windows.Media.Color;

namespace RoBIM.Feature;

public sealed class IssueTrackerWindow : Window
{
    public static IssueTrackerWindow? Instance { get; private set; }

    private readonly Document doc;
    private readonly IssueStorage storage;
    private readonly ExternalEvent revitEvent;
    private readonly IssueExternalEventHandler eventHandler;
    private readonly string currentModelName;

    private IssueProjectConfig config;
    private List<ColumnPreferenceItem> columnPrefs;
    private readonly ObservableCollection<IssueItem> issues = [];
    private IssueItem? currentEditingIssue;
    private IssueItem? originalSnapshot;

    // UI Controls - Header
    private readonly TextBlock folderPathText = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 280 };
    private readonly Button openFolderBtn = new() { Content = "폴더 열기", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button newIssueBtn = new() { Content = "+ 새 이슈 등록", MinWidth = 95, Margin = new Thickness(4, 0, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly Button manageUsersBtn = new() { Content = "참여자 관리", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button configColumnsBtn = new() { Content = "상단 제목/열 설정", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button configFieldsBtn = new() { Content = "분류/필드 설정", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button refreshBtn = new() { Content = "새로고침", Margin = new Thickness(4, 0, 0, 0) };

    // UI Controls - Filter & List
    private readonly WpfTextBox searchBox = new() { MinWidth = 110 };
    private readonly WpfComboBox modelFilterCombo = new() { MinWidth = 165 };
    private readonly WpfComboBox statusFilterCombo = new() { MinWidth = 85 };
    private readonly WpfComboBox priorityFilterCombo = new() { MinWidth = 80 };
    private readonly CheckBox archiveFilterCheck = new() { Content = "보관함(아카이브)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly DataGrid issueGrid = new()
    {
        AutoGenerateColumns = false,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        CanUserSortColumns = true, // 헤더 클릭 시 오름차순/내림차순 정렬 지원
        IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.All
    };

    // UI Controls - Form
    private readonly StackPanel formHeaderPanel = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly TextBlock formHeaderTitle = new() { FontSize = 16, FontWeight = FontWeights.Bold };
    private readonly TextBlock formHeaderSubtitle = new() { FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) };

    private readonly WpfTextBox titleBox = new();
    private readonly WpfComboBox statusCombo = new();
    private readonly WpfComboBox priorityCombo = new() { IsEditable = true };
    private readonly WpfComboBox discipline1Combo = new() { IsEditable = true };
    private readonly WpfComboBox discipline2Combo = new() { IsEditable = true };
    private readonly WpfComboBox typeCombo = new() { IsEditable = true };
    private readonly WpfComboBox assignedToCombo = new() { IsEditable = true };
    private readonly WpfTextBox createdByBox = new() { IsReadOnly = true, Background = Brushes.WhiteSmoke }; // 최초 등록 작성자 (고정)
    private readonly TextBlock currentUserTag = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), FontWeight = FontWeights.SemiBold };
    private readonly DatePicker dueDatePicker = new();
    private readonly WpfTextBox locationBox = new();
    private readonly WpfComboBox rootCauseCombo = new() { IsEditable = true };
    private readonly WpfTextBox sourceModelBox = new() { IsReadOnly = true, Background = Brushes.WhiteSmoke };
    private readonly WpfTextBox descBox = new() { AcceptsReturn = true, MinHeight = 60, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

    // Linked View Controls
    private readonly TextBlock linkedViewText = new() { Text = "(연계된 뷰 없음)", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
    private readonly Button linkActiveViewBtn = new() { Content = "현재 뷰 연계", Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button openViewBtn = new() { Content = "뷰 열기/이동", Margin = new Thickness(4, 0, 0, 0), IsEnabled = false };
    private readonly Button clearViewBtn = new() { Content = "해제", Margin = new Thickness(4, 0, 0, 0) };

    // Element Link Controls
    private readonly TextBlock linkedElementsText = new() { Text = "(연결된 객체 없음)", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
    private readonly Button getSelectedElementsBtn = new() { Content = "현재 Revit 선택 가져오기", Margin = new Thickness(0, 0, 6, 4), ToolTip = "Revit 화면에서 이미 선택되어 있는 객체들을 가져와 이슈에 연결합니다." };
    private readonly Button pickRectElementsBtn = new() { Content = "영역 드래그(박스) 선택", Margin = new Thickness(0, 0, 6, 4), ToolTip = "마우스로 사각형을 드래그하여 영역 내 객체들을 한 번에 선택하여 연결합니다." };
    private readonly Button pickElementsBtn = new() { Content = "다중 클릭 선택", Margin = new Thickness(0, 0, 6, 4), ToolTip = "Ctrl 키를 누른 채 객체들을 하나씩 클릭하여 연결합니다." };
    private readonly Button clearElementsBtn = new() { Content = "연결 해제", Margin = new Thickness(0, 0, 6, 4), ToolTip = "이 이슈에 연결된 객체 목록을 모두 비웁니다." };

    private readonly Button selectOnlyElementsBtn = new() { Content = "전체 선택 (선택만)", Margin = new Thickness(8, 0, 4, 0), IsEnabled = false, ToolTip = "연결된 모든 객체를 Revit에서 선택 상태로 만듭니다. (카메라 이동 없음)" };
    private readonly Button zoomElementsBtn = new() { Content = "전체 선택 및 줌 이동", Margin = new Thickness(4, 0, 0, 0), IsEnabled = false, ToolTip = "연결된 모든 객체를 선택하고 화면 중앙으로 줌 이동합니다." };
    private readonly WrapPanel linkedElementsChipsPanel = new();

    // Snapshot Multi Gallery Controls
    private readonly WrapPanel snapshotGalleryPanel = new();
    private readonly Button captureViewBtn = new() { Content = "현재 화면 캡처 추가", Margin = new Thickness(0, 0, 6, 0) };
    private readonly Button addLocalImageBtn = new() { Content = "외부 이미지 추가", Margin = new Thickness(0, 0, 6, 0) };
    private readonly TextBlock snapshotCountText = new() { VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray };

    // Comments & History Controls
    private readonly StackPanel commentsListPanel = new();
    private readonly WpfTextBox newCommentBox = new() { AcceptsReturn = true, MinHeight = 45, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button addCommentBtn = new() { Content = "댓글 등록", MinWidth = 80, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };

    // Dynamic Custom Fields Container
    private readonly StackPanel customFieldsPanel = new();
    private readonly Dictionary<string, System.Windows.UIElement> customFieldControls = [];

    // Form Action Buttons
    private readonly Button saveIssueBtn = new() { Content = "저장", MinWidth = 85, FontWeight = FontWeights.SemiBold };
    private readonly Button archiveIssueBtn = new() { Content = "보관함 이동(삭제)", MinWidth = 110, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button restoreIssueBtn = new() { Content = "보관함에서 복원", MinWidth = 110, Margin = new Thickness(6, 0, 0, 0), Visibility = WpfVisibility.Collapsed };
    private readonly Button cancelEditBtn = new() { Content = "취소", MinWidth = 65, Margin = new Thickness(6, 0, 0, 0) };

    // Status bar
    private readonly TextBlock statusBarText = new() { Text = "준비 완료" };

    public IssueTrackerWindow(Document doc, ExternalEvent revitEvent, IssueExternalEventHandler eventHandler, string version)
    {
        this.doc = doc;
        this.revitEvent = revitEvent;
        this.eventHandler = eventHandler;
        this.storage = new IssueStorage(doc);
        this.currentModelName = IssueStorage.GetModelName(doc);

        Instance = this;
        Closed += (s, e) => { Instance = null; };

        RoBimTheme.Apply(this);
        Title = $"BIM 이슈 검토 (ACC 스타일) · {version} · {doc.Title}";
        Width = 1140;
        Height = 780;
        MinWidth = 940;
        MinHeight = 620;
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
            // Ignore
        }

        config = storage.LoadConfig();
        columnPrefs = UserPreferences.LoadColumns(config.CustomFields);

        SetupEventHandlerCallbacks();
        InitializeUI();
        BuildGridColumns();
        CheckAndPromptInitialUser();
        UpdateUsersCombos();
        LoadIssues();
        PrepareNewIssue();
    }

    private void CheckAndPromptInitialUser()
    {
        string revitUser = (doc.Application.Username ?? "").Trim();
        bool needPrompt = false;

        if (config.Users.Count == 0)
        {
            needPrompt = true;
        }
        else
        {
            var matched = config.Users.FirstOrDefault(u =>
                (!string.IsNullOrWhiteSpace(u.RevitUsername) && u.RevitUsername.Equals(revitUser, StringComparison.OrdinalIgnoreCase)) ||
                u.DisplayName.Equals(revitUser, StringComparison.OrdinalIgnoreCase));
            if (matched == null) needPrompt = true;
        }

        if (needPrompt)
        {
            var dlg = new UserManagerDialog(
                config,
                revitUser,
                IsCurrentOperatorAdmin(),
                $"현재 Revit 사용자 [{revitUser}]가 참여자로 등록되지 않았습니다.\n이슈 작성자 및 담당자에 '이름 (회사)' 형태로 표시되도록 실명과 회사를 등록해 주세요.")
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.IsSaved)
            {
                config.Users = dlg.GetResultUsers();
                storage.SaveConfig(config);
                UpdateUsersCombos();
                UpdateCurrentUserUI();
                UpdateCommentsUI();
            }
        }
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

        eventHandler.OnViewInfoRetrieved = (viewId, uniqueId, viewName) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (currentEditingIssue == null) currentEditingIssue = new IssueItem();
                currentEditingIssue.LinkedViewId = viewId;
                currentEditingIssue.LinkedViewUniqueId = uniqueId;
                currentEditingIssue.LinkedViewName = viewName;
                UpdateLinkedViewUI();
                SetStatus($"뷰 '{viewName}'가 연결되었습니다.");
            });
        };

        eventHandler.OnViewCaptured = (fileName) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (currentEditingIssue == null) currentEditingIssue = new IssueItem();
                if (!currentEditingIssue.SnapshotFileNames.Contains(fileName))
                {
                    currentEditingIssue.SnapshotFileNames.Add(fileName);
                }
                UpdateSnapshotGalleryUI();
                SetStatus($"새 스냅샷이 추가되었습니다: {fileName}");
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
        manageUsersBtn.Click += OpenUserManagerDialog;
        configColumnsBtn.Click += OpenColumnConfigDialog;
        configFieldsBtn.Click += OpenConfigDialog;
        refreshBtn.Click += (s, e) => { LoadIssues(); SetStatus("이슈 목록을 새로고침했습니다."); };
        openFolderBtn.Click += (s, e) => OpenIssueFolder();

        topButtons.Children.Add(openFolderBtn);
        topButtons.Children.Add(manageUsersBtn);
        topButtons.Children.Add(configColumnsBtn);
        topButtons.Children.Add(configFieldsBtn);
        topButtons.Children.Add(refreshBtn);
        topButtons.Children.Add(newIssueBtn);
        topBar.Children.Add(topButtons);

        var folderInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        folderInfo.Children.Add(new TextBlock { Text = "저장 위치: ", FontWeight = FontWeights.SemiBold });
        folderPathText.Text = storage.IssueDirectory;
        folderPathText.ToolTip = storage.IssueDirectory;
        folderInfo.Children.Add(folderPathText);

        folderInfo.Children.Add(new TextBlock { Text = "  |  현재 모델: ", FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0) });
        var modelTag = new TextBlock { Text = currentModelName, FontWeight = FontWeights.SemiBold, Foreground = Brushes.RoyalBlue, ToolTip = currentModelName };
        folderInfo.Children.Add(modelTag);

        folderInfo.Children.Add(currentUserTag);
        UpdateCurrentUserUI();

        topBar.Children.Add(folderInfo);

        root.Children.Add(topBar);

        // 3. Main Split View: Left List (46%), Right Form (54%)
        var mainGrid = new WpfGrid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54, GridUnitType.Star) });

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

        filterBar.Children.Add(new TextBlock { Text = " 보기 범위: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        modelFilterCombo.SelectionChanged += (s, e) => ApplyFilter();
        filterBar.Children.Add(modelFilterCombo);

        filterBar.Children.Add(new TextBlock { Text = " 상태: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        statusFilterCombo.ItemsSource = new[] { "전체", "Open", "In Review", "Pending", "Closed", "Void" };
        statusFilterCombo.SelectedIndex = 0;
        statusFilterCombo.SelectionChanged += (s, e) => ApplyFilter();
        filterBar.Children.Add(statusFilterCombo);

        filterBar.Children.Add(new TextBlock { Text = " 우선: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        priorityFilterCombo.ItemsSource = new[] { "전체" }.Concat(config.PriorityList).ToArray();
        priorityFilterCombo.SelectedIndex = 0;
        priorityFilterCombo.SelectionChanged += (s, e) => ApplyFilter();
        filterBar.Children.Add(priorityFilterCombo);

        archiveFilterCheck.Checked += (s, e) => LoadIssues();
        archiveFilterCheck.Unchecked += (s, e) => LoadIssues();
        filterBar.Children.Add(archiveFilterCheck);

        panel.Children.Add(filterBar);

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

    private void BuildGridColumns()
    {
        issueGrid.Columns.Clear();

        foreach (var col in columnPrefs.Where(c => c.IsVisible))
        {
            DataGridColumn dgc;
            if (col.IsCustomField)
            {
                string cfId = col.ColumnId.Replace("CF_", "");
                dgc = new DataGridTextColumn
                {
                    Header = col.HeaderText,
                    Binding = new WpfBinding($"CustomFields[{cfId}]"),
                    Width = col.Width,
                    CanUserSort = false
                };
            }
            else
            {
                dgc = col.ColumnId switch
                {
                    "Number" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("DisplayNumber"), SortMemberPath = "Number", Width = col.Width },
                    "Status" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("Status"), SortMemberPath = "Status", Width = col.Width },
                    "Priority" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("Priority"), SortMemberPath = "Priority", Width = col.Width },
                    "DisciplinesDisplay" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("DisciplinesDisplay"), SortMemberPath = "Discipline1", Width = col.Width },
                    "SourceModel" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("SourceModelDisplay"), SortMemberPath = "SourceModel", Width = col.Width },
                    "Title" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("Title"), SortMemberPath = "Title", Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                    "AssignedTo" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("AssignedToDisplay"), SortMemberPath = "AssignedTo", Width = col.Width },
                    "CreatedBy" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("CreatedBy"), SortMemberPath = "CreatedBy", Width = col.Width },
                    "CommentsSummary" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("CommentsSummary"), SortMemberPath = "Comments.Count", Width = col.Width },
                    "LinkedView" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("LinkedViewName"), SortMemberPath = "LinkedViewName", Width = col.Width },
                    "Elements" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("ElementsSummary"), Width = col.Width, CanUserSort = false },
                    "DueDate" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("DueDate"), SortMemberPath = "DueDate", Width = col.Width },
                    "Type" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("Type"), SortMemberPath = "Type", Width = col.Width },
                    "RootCause" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("RootCause"), SortMemberPath = "RootCause", Width = col.Width },
                    "Location" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("Location"), SortMemberPath = "Location", Width = col.Width },
                    "CreatedAt" => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding("CreatedAt") { StringFormat = "yyyy-MM-dd" }, SortMemberPath = "CreatedAt", Width = col.Width },
                    _ => new DataGridTextColumn { Header = col.HeaderText, Binding = new WpfBinding(col.ColumnId), SortMemberPath = col.ColumnId, Width = col.Width }
                };
            }
            issueGrid.Columns.Add(dgc);
        }
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
        archiveIssueBtn.Click += (s, e) => ArchiveCurrentIssue();
        restoreIssueBtn.Click += (s, e) => RestoreCurrentIssue();
        cancelEditBtn.Click += (s, e) => PrepareNewIssue();

        bottomActions.Children.Add(saveIssueBtn);
        bottomActions.Children.Add(archiveIssueBtn);
        bottomActions.Children.Add(restoreIssueBtn);
        bottomActions.Children.Add(cancelEditBtn);
        root.Children.Add(bottomActions);

        // Scrollable Form
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var form = new StackPanel();
        scroll.Content = form;
        root.Children.Add(scroll);

        // Form Header
        formHeaderPanel.Children.Add(formHeaderTitle);
        formHeaderPanel.Children.Add(formHeaderSubtitle);
        form.Children.Add(formHeaderPanel);

        // Standard Fields Grid
        var g = new WpfGrid { Margin = new Thickness(0, 0, 0, 10) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < 7; i++)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Row 0: Title (Span 3)
        AddGridRow(g, 0, "제목 *:", titleBox, 1, 3);

        // Row 1: Status & Priority
        statusCombo.ItemsSource = new[] { "Open", "In Review", "Pending", "Closed", "Void" };
        AddGridRow(g, 1, "상태:", statusCombo, 1, 1);
        priorityCombo.ItemsSource = config.PriorityList;
        AddGridRow(g, 1, "우선순위:", priorityCombo, 3, 1);

        // Row 2: Discipline 1 & Discipline 2 (간섭 대비 2개 공종 선택)
        discipline1Combo.ItemsSource = config.DisciplineList;
        AddGridRow(g, 2, "공종 1:", discipline1Combo, 1, 1);
        discipline2Combo.ItemsSource = new[] { "(해당 없음)" }.Concat(config.DisciplineList).ToList();
        AddGridRow(g, 2, "공종 2(간섭):", discipline2Combo, 3, 1);

        // Row 3: AssignedTo & CreatedBy (이름 (회사) 포맷)
        UpdateUsersCombos();
        AddGridRow(g, 3, "담당자:", assignedToCombo, 1, 1);
        AddGridRow(g, 3, "작성자(원작자):", createdByBox, 3, 1);

        // Row 4: Type & DueDate
        typeCombo.ItemsSource = config.TypeList;
        AddGridRow(g, 4, "유형:", typeCombo, 1, 1);
        AddGridRow(g, 4, "마감일:", dueDatePicker, 3, 1);

        // Row 5: Location & RootCause
        AddGridRow(g, 5, "위치/층:", locationBox, 1, 1);
        rootCauseCombo.ItemsSource = config.RootCauseList;
        AddGridRow(g, 5, "근본 원인:", rootCauseCombo, 3, 1);

        // Row 6: Source Model
        AddGridRow(g, 6, "작성 모델:", sourceModelBox, 1, 3);

        form.Children.Add(g);

        // Description
        form.Children.Add(new TextBlock { Text = "상세 설명:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) });
        form.Children.Add(descBox);

        // Linked View Section
        var viewGroup = new GroupBox { Header = "연계 뷰 (Active View)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var viewStack = new StackPanel();
        var viewBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        linkActiveViewBtn.Click += LinkActiveView;
        openViewBtn.Click += OpenLinkedView;
        clearViewBtn.Click += (s, e) =>
        {
            if (currentEditingIssue != null)
            {
                currentEditingIssue.LinkedViewId = 0;
                currentEditingIssue.LinkedViewUniqueId = "";
                currentEditingIssue.LinkedViewName = "";
                UpdateLinkedViewUI();
            }
        };

        viewBar.Children.Add(linkActiveViewBtn);
        viewBar.Children.Add(openViewBtn);
        viewBar.Children.Add(clearViewBtn);
        viewStack.Children.Add(viewBar);
        viewStack.Children.Add(linkedViewText);
        viewGroup.Content = viewStack;
        form.Children.Add(viewGroup);

        // Linked Elements Section
        var elemGroup = new GroupBox { Header = "연계 객체 (ElementId)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var elemMainStack = new StackPanel();

        // 1. 등록 / 연결 영역
        var registerCard = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(248, 249, 250)),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var registerStack = new StackPanel();
        var regHeader = new TextBlock
        {
            Text = "📥 1. 객체 연결 (이슈에 객체 등록 / 수정)",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.DarkSlateGray,
            Margin = new Thickness(0, 0, 0, 6)
        };
        registerStack.Children.Add(regHeader);

        getSelectedElementsBtn.Click += GetSelectedFromRevit;
        pickRectElementsBtn.Click += PickRectElementsFromRevit;
        pickElementsBtn.Click += PickElementsFromRevit;
        clearElementsBtn.Click += (s, e) =>
        {
            if (currentEditingIssue != null)
            {
                currentEditingIssue.ElementIds.Clear();
                currentEditingIssue.UniqueIds.Clear();
                UpdateLinkedElementsUI();
                SetStatus("연계 객체가 모두 해제되었습니다.");
            }
        };

        var regButtonsBar = new WrapPanel();
        regButtonsBar.Children.Add(getSelectedElementsBtn);
        regButtonsBar.Children.Add(pickRectElementsBtn);
        regButtonsBar.Children.Add(pickElementsBtn);
        regButtonsBar.Children.Add(clearElementsBtn);
        registerStack.Children.Add(regButtonsBar);
        registerCard.Child = registerStack;
        elemMainStack.Children.Add(registerCard);

        // 2. 확인 / 검토 영역
        var inspectCard = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromRgb(255, 255, 255)),
            BorderBrush = Brushes.LightSteelBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };
        var inspectStack = new StackPanel();
        var insHeader = new TextBlock
        {
            Text = "🔍 2. 연결된 객체 확인 (Revit 뷰 연동)",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.DarkSlateBlue,
            Margin = new Thickness(0, 0, 0, 6)
        };
        inspectStack.Children.Add(insHeader);

        selectOnlyElementsBtn.Click += (s, e) => SelectAllElements(zoom: false);
        zoomElementsBtn.Click += (s, e) => SelectAllElements(zoom: true);

        var inspectActionBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        inspectActionBar.Children.Add(linkedElementsText);
        inspectActionBar.Children.Add(selectOnlyElementsBtn);
        inspectActionBar.Children.Add(zoomElementsBtn);
        inspectStack.Children.Add(inspectActionBar);

        var chipsContainer = new ScrollViewer
        {
            MaxHeight = 110,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 4, 0, 0)
        };
        chipsContainer.Content = linkedElementsChipsPanel;
        inspectStack.Children.Add(chipsContainer);

        var hintText = new TextBlock
        {
            Text = "※ 각 ID 버튼을 클릭하면 해당 객체만 Revit에서 선택되며, 더블클릭하면 줌 이동합니다.",
            FontSize = 11,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 0)
        };
        inspectStack.Children.Add(hintText);

        inspectCard.Child = inspectStack;
        elemMainStack.Children.Add(inspectCard);

        elemGroup.Content = elemMainStack;
        form.Children.Add(elemGroup);

        // Snapshot Section (Multi-Image Gallery)
        var snapGroup = new GroupBox { Header = "화면 캡처 (스냅샷 갤러리 - 클릭 시 확대)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        var snapStack = new StackPanel();
        var snapBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        captureViewBtn.Click += CaptureActiveViewFromRevit;
        addLocalImageBtn.Click += AddLocalImageSnapshot;

        snapBar.Children.Add(captureViewBtn);
        snapBar.Children.Add(addLocalImageBtn);
        snapBar.Children.Add(snapshotCountText);
        snapStack.Children.Add(snapBar);

        var snapScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 4, 0, 4) };
        snapScroll.Content = snapshotGalleryPanel;
        snapStack.Children.Add(snapScroll);
        snapGroup.Content = snapStack;
        form.Children.Add(snapGroup);

        // Dynamic Custom Fields Group
        var customGroup = new GroupBox { Header = "사용자 정의 필드", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6) };
        customGroup.Content = customFieldsPanel;
        form.Children.Add(customGroup);

        // Comments & Activity History Section (제일 아래 댓글)
        var commentsGroup = new GroupBox { Header = "활동 내역 및 댓글 (Comments)", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(8) };
        var commentsStack = new StackPanel();

        commentsStack.Children.Add(commentsListPanel);

        var inputBorder = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(6), Margin = new Thickness(0, 8, 0, 0) };
        var inputStack = new StackPanel();
        inputStack.Children.Add(new TextBlock { Text = "새 댓글 작성:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        inputStack.Children.Add(newCommentBox);
        addCommentBtn.Click += AddUserComment;
        inputStack.Children.Add(addCommentBtn);
        inputBorder.Child = inputStack;
        commentsStack.Children.Add(inputBorder);

        commentsGroup.Content = commentsStack;
        form.Children.Add(commentsGroup);

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

    private void UpdateUsersCombos()
    {
        var options = new List<string> { "(미지정)" };
        foreach (var u in config.Users)
        {
            options.Add(u.DisplayText); // 이름 (회사)
        }
        assignedToCombo.ItemsSource = options;
    }

    private ProjectUser? GetCurrentOperator()
    {
        string revitUser = (doc.Application.Username ?? "").Trim();
        string winUser = Environment.UserName;

        var matched = config.Users.FirstOrDefault(u =>
            !string.IsNullOrWhiteSpace(u.RevitUsername) &&
            (u.RevitUsername.Equals(revitUser, StringComparison.OrdinalIgnoreCase) ||
             u.RevitUsername.Equals(winUser, StringComparison.OrdinalIgnoreCase)));

        if (matched != null) return matched;

        matched = config.Users.FirstOrDefault(u =>
            u.DisplayName.Equals(revitUser, StringComparison.OrdinalIgnoreCase) ||
            u.DisplayName.Equals(winUser, StringComparison.OrdinalIgnoreCase));

        return matched;
    }

    private string GetCurrentOperatorDisplayText()
    {
        var op = GetCurrentOperator();
        if (op != null) return op.DisplayText;

        string revitUser = (doc.Application.Username ?? "").Trim();
        string winUser = Environment.UserName;
        if (config.Users.Count > 0) return config.Users[0].DisplayText;

        return !string.IsNullOrWhiteSpace(revitUser) ? $"{revitUser} (회사미지정)" : $"{winUser} (회사미지정)";
    }

    private bool IsCurrentOperatorAdmin()
    {
        if (config.Users.Count == 0 || !config.Users.Any(u => u.IsAdmin))
        {
            return true; // 등록된 관리자가 없는 초기 상태는 작업 허용
        }
        var op = GetCurrentOperator();
        return op?.IsAdmin == true;
    }

    private void UpdateCurrentUserUI()
    {
        bool isAdmin = IsCurrentOperatorAdmin();
        currentUserTag.Text = $"  |  현재 작업자: {GetCurrentOperatorDisplayText()} {(isAdmin ? "[관리자]" : "")}";
        currentUserTag.Foreground = isAdmin ? Brushes.Crimson : Brushes.DarkSlateGray;
        currentUserTag.ToolTip = isAdmin
            ? "현재 관리자 권한으로 작업 중입니다. (댓글/활동내역 삭제 권한 보유)"
            : "일반 참여자 권한으로 작업 중입니다.";
    }

    private string ResolveCurrentAuthor() => GetCurrentOperatorDisplayText();

    private string NormalizeAuthor(string? rawAuthor, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(rawAuthor)) return allowEmpty ? "" : ResolveCurrentAuthor();

        if (rawAuthor.Equals("(미지정)", StringComparison.OrdinalIgnoreCase) || rawAuthor.Equals("미지정", StringComparison.OrdinalIgnoreCase))
        {
            return allowEmpty ? "" : "(미지정)";
        }

        // 이미 '이름 (회사)' 형식이면 그대로 반환
        if (rawAuthor.Contains("(") && rawAuthor.Contains(")"))
        {
            return rawAuthor;
        }

        // 등록된 사용자 목록에서 찾기
        var matched = config.Users.FirstOrDefault(u =>
            u.DisplayName.Equals(rawAuthor, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(u.RevitUsername) && u.RevitUsername.Equals(rawAuthor, StringComparison.OrdinalIgnoreCase)));

        if (matched != null) return matched.DisplayText;

        return rawAuthor;
    }

    private void LoadIssues()
    {
        issues.Clear();
        bool showArchived = archiveFilterCheck.IsChecked == true;
        var list = storage.LoadAllIssues(includeArchived: true);

        var targetList = showArchived ? list.Where(x => x.IsArchived) : list.Where(x => !x.IsArchived);
        foreach (var item in targetList)
        {
            // 작성자/담당자 문자열을 이름(회사) 포맷으로 정규화
            item.CreatedBy = NormalizeAuthor(item.CreatedBy);
            item.AssignedTo = NormalizeAuthor(item.AssignedTo, allowEmpty: true);
            issues.Add(item);
        }
        UpdateModelFilterOptions();
        ApplyFilter();
    }

    private void UpdateModelFilterOptions()
    {
        string? prevSelection = modelFilterCombo.SelectedItem?.ToString();

        var otherModels = issues
            .Select(i => i.SourceModel?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        var items = new List<string>
        {
            "모든 모델 이슈 보기",
            $"현재 모델만 ({currentModelName})"
        };

        foreach (var om in otherModels)
        {
            items.Add($"{om} 이슈만");
        }

        modelFilterCombo.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(prevSelection) && items.Contains(prevSelection))
        {
            modelFilterCombo.SelectedItem = prevSelection;
        }
        else
        {
            modelFilterCombo.SelectedIndex = 0;
        }
    }

    private void ApplyFilter()
    {
        string query = searchBox.Text.Trim().ToLowerInvariant();
        string statusFilter = statusFilterCombo.SelectedItem?.ToString() ?? "전체";
        string priorityFilter = priorityFilterCombo.SelectedItem?.ToString() ?? "전체";
        string modelFilter = modelFilterCombo.SelectedItem?.ToString() ?? "모든 모델 이슈 보기";

        var filtered = issues.Where(i =>
        {
            bool matchStatus = statusFilter == "전체" || i.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase);
            bool matchPriority = priorityFilter == "전체" || i.Priority.Equals(priorityFilter, StringComparison.OrdinalIgnoreCase);

            bool matchModel = true;
            if (modelFilter.StartsWith("현재 모델만"))
            {
                matchModel = string.IsNullOrWhiteSpace(i.SourceModel) ||
                             i.SourceModel.Equals(currentModelName, StringComparison.OrdinalIgnoreCase);
            }
            else if (modelFilter.EndsWith(" 이슈만"))
            {
                string target = modelFilter.Substring(0, modelFilter.Length - " 이슈만".Length).Trim();
                matchModel = i.SourceModel.Equals(target, StringComparison.OrdinalIgnoreCase);
            }

            bool matchQuery = string.IsNullOrWhiteSpace(query) ||
                              i.Title.ToLowerInvariant().Contains(query) ||
                              i.DisplayNumber.ToLowerInvariant().Contains(query) ||
                              i.SourceModelDisplay.ToLowerInvariant().Contains(query) ||
                              i.AssignedToDisplay.ToLowerInvariant().Contains(query) ||
                              i.CreatedBy.ToLowerInvariant().Contains(query) ||
                              i.Discipline1.ToLowerInvariant().Contains(query) ||
                              i.Discipline2.ToLowerInvariant().Contains(query) ||
                              i.LinkedViewName.ToLowerInvariant().Contains(query) ||
                              i.Location.ToLowerInvariant().Contains(query);

            return matchStatus && matchPriority && matchModel && matchQuery;
        }).ToList();

        issueGrid.ItemsSource = filtered;
    }

    private void PrepareNewIssue()
    {
        originalSnapshot = null;
        string currentAuthor = ResolveCurrentAuthor();

        currentEditingIssue = new IssueItem
        {
            Status = "Open",
            Priority = "보통",
            Discipline1 = config.DisciplineList.FirstOrDefault() ?? "건축",
            Discipline2 = "(해당 없음)",
            Type = "설계검토",
            RootCause = "미지정",
            AssignedTo = "",
            CreatedBy = currentAuthor,
            SourceModel = currentModelName
        };

        try
        {
            var av = doc.ActiveView;
            if (av != null && !av.IsTemplate)
            {
                currentEditingIssue.LinkedViewId = av.Id.Value;
                currentEditingIssue.LinkedViewUniqueId = av.UniqueId;
                currentEditingIssue.LinkedViewName = av.Name;
            }
        }
        catch
        {
            // Ignore
        }

        formHeaderTitle.Text = "새 이슈 등록";
        formHeaderSubtitle.Text = $"작성자: {currentAuthor} | 작성 파일: {currentModelName}";
        archiveIssueBtn.Visibility = WpfVisibility.Collapsed;
        restoreIssueBtn.Visibility = WpfVisibility.Collapsed;
        saveIssueBtn.IsEnabled = true;

        PopulateForm(currentEditingIssue);
    }

    private void EditIssue(IssueItem item)
    {
        currentEditingIssue = item;
        originalSnapshot = new IssueItem
        {
            Id = item.Id,
            Number = item.Number,
            Title = item.Title,
            Status = item.Status,
            Priority = item.Priority,
            Discipline1 = item.Discipline1,
            Discipline2 = item.Discipline2,
            Type = item.Type,
            AssignedTo = item.AssignedTo,
            CreatedBy = item.CreatedBy,
            DueDate = item.DueDate,
            Location = item.Location,
            RootCause = item.RootCause,
            Description = item.Description,
            LinkedViewId = item.LinkedViewId,
            LinkedViewName = item.LinkedViewName,
            ElementIds = new List<long>(item.ElementIds),
            SnapshotFileNames = new List<string>(item.SnapshotFileNames),
            CustomFields = new Dictionary<string, string>(item.CustomFields)
        };

        formHeaderTitle.Text = $"이슈 {item.DisplayNumber} · {item.Title}";
        string displayAuthor = NormalizeAuthor(item.CreatedBy);
        string srcModel = item.SourceModelDisplay;
        formHeaderSubtitle.Text = $"[{item.Status}] · [{item.Priority}] · [{item.DisciplinesDisplay}] | 모델: {srcModel} | 작성자: {displayAuthor} ({item.CreatedAt:yyyy-MM-dd HH:mm})";

        if (item.IsArchived)
        {
            archiveIssueBtn.Visibility = WpfVisibility.Collapsed;
            restoreIssueBtn.Visibility = WpfVisibility.Visible;
            saveIssueBtn.IsEnabled = false;
        }
        else
        {
            archiveIssueBtn.Visibility = WpfVisibility.Visible;
            restoreIssueBtn.Visibility = WpfVisibility.Collapsed;
            saveIssueBtn.IsEnabled = true;
        }

        PopulateForm(item);
    }

    private void PopulateForm(IssueItem item)
    {
        titleBox.Text = item.Title;
        statusCombo.SelectedItem = item.Status;
        priorityCombo.Text = item.Priority;
        discipline1Combo.Text = item.Discipline1;
        discipline2Combo.Text = item.Discipline2;
        typeCombo.Text = item.Type;
        assignedToCombo.Text = string.IsNullOrWhiteSpace(item.AssignedTo) ? "(미지정)" : NormalizeAuthor(item.AssignedTo, allowEmpty: true);
        createdByBox.Text = NormalizeAuthor(item.CreatedBy);
        sourceModelBox.Text = item.SourceModelDisplay;

        if (DateTime.TryParse(item.DueDate, out var dt))
            dueDatePicker.SelectedDate = dt;
        else
            dueDatePicker.SelectedDate = null;

        locationBox.Text = item.Location;
        rootCauseCombo.Text = item.RootCause;
        descBox.Text = item.Description;

        UpdateSnapshotGalleryUI();
        UpdateLinkedElementsUI();
        UpdateLinkedViewUI();
        UpdateCommentsUI();

        RebuildCustomFieldsUI(item.CustomFields);
    }

    private void UpdateSnapshotGalleryUI()
    {
        snapshotGalleryPanel.Children.Clear();
        if (currentEditingIssue == null || currentEditingIssue.SnapshotFileNames.Count == 0)
        {
            snapshotCountText.Text = "(스냅샷 없음)";
            return;
        }

        snapshotCountText.Text = $"총 {currentEditingIssue.SnapshotFileNames.Count}장 (클릭 시 확대 보기)";

        foreach (var snapName in currentEditingIssue.SnapshotFileNames.ToList())
        {
            string fullPath = storage.GetSnapshotFullPath(snapName, currentEditingIssue.IsArchived);

            var container = new Border
            {
                Width = 110,
                Height = 85,
                Margin = new Thickness(4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Black,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "클릭하여 크게 보기"
            };

            var grid = new WpfGrid();
            var img = new System.Windows.Controls.Image { Stretch = Stretch.Uniform };

            if (File.Exists(fullPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.DecodePixelWidth = 240;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(fullPath);
                    bmp.EndInit();
                    img.Source = bmp;
                }
                catch
                {
                    // Ignore
                }
            }

            grid.Children.Add(img);

            // Delete 'X' button
            var delBtn = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(MediaColor.FromArgb(180, 200, 40, 40)),
                Foreground = Brushes.White,
                FontSize = 10,
                ToolTip = "스냅샷 삭제"
            };
            delBtn.Click += (s, e) =>
            {
                e.Handled = true;
                currentEditingIssue.SnapshotFileNames.Remove(snapName);
                UpdateSnapshotGalleryUI();
            };
            grid.Children.Add(delBtn);

            container.Child = grid;

            // Click to open high-res viewer
            container.MouseLeftButtonUp += (s, e) =>
            {
                if (File.Exists(fullPath))
                {
                    var viewer = new ImageViewerDialog(fullPath, currentEditingIssue.DisplayNumber) { Owner = this };
                    viewer.ShowDialog();
                }
            };

            snapshotGalleryPanel.Children.Add(container);
        }
    }

    private void UpdateCommentsUI()
    {
        commentsListPanel.Children.Clear();
        if (currentEditingIssue == null || currentEditingIssue.Comments.Count == 0)
        {
            commentsListPanel.Children.Add(new TextBlock
            {
                Text = "(등록된 댓글이나 수정 내역이 없습니다.)",
                Foreground = Brushes.Gray,
                Margin = new Thickness(4)
            });
            return;
        }

        foreach (var c in currentEditingIssue.Comments.OrderBy(x => x.CreatedAt))
        {
            var itemBorder = new Border
            {
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(4),
                Background = c.IsSystem ? new SolidColorBrush(MediaColor.FromRgb(245, 247, 250)) : new SolidColorBrush(MediaColor.FromRgb(255, 255, 255)),
                BorderBrush = c.IsSystem ? Brushes.LightSteelBlue : Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();
            var metaBar = new DockPanel();

            var authorText = new TextBlock
            {
                Text = c.IsSystem ? $"[활동 내역] {c.Author}" : c.Author,
                FontWeight = FontWeights.Bold,
                Foreground = c.IsSystem ? Brushes.SteelBlue : Brushes.DarkSlateBlue
            };

            var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(rightPanel, Dock.Right);

            var timeText = new TextBlock
            {
                Text = c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightPanel.Children.Add(timeText);

            if (IsCurrentOperatorAdmin())
            {
                var delCommentBtn = new Button
                {
                    Content = "삭제",
                    FontSize = 10,
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Crimson,
                    BorderBrush = Brushes.Crimson,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "관리자 권한: 이 댓글/활동 내역을 삭제합니다."
                };

                delCommentBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    var confirm = MessageBox.Show(this, "이 댓글/활동 내역을 영구히 삭제하시겠습니까?", "댓글 삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        currentEditingIssue.Comments.Remove(c);
                        storage.SaveIssue(currentEditingIssue, config);
                        UpdateCommentsUI();
                        SetStatus("댓글/활동 내역이 삭제되었습니다.");
                    }
                };

                rightPanel.Children.Add(delCommentBtn);
            }

            metaBar.Children.Add(rightPanel);
            metaBar.Children.Add(authorText);
            stack.Children.Add(metaBar);

            var contentText = new TextBlock
            {
                Text = c.Content,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = c.IsSystem ? Brushes.DimGray : Brushes.Black
            };
            stack.Children.Add(contentText);

            itemBorder.Child = stack;
            commentsListPanel.Children.Add(itemBorder);
        }
    }

    private void AddUserComment(object sender, RoutedEventArgs e)
    {
        string text = newCommentBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (currentEditingIssue == null) return;

        string author = GetCurrentOperatorDisplayText();
        if (string.IsNullOrWhiteSpace(author)) author = ResolveCurrentAuthor();

        currentEditingIssue.Comments.Add(new IssueComment
        {
            Author = author,
            Content = text,
            CreatedAt = DateTime.Now,
            IsSystem = false
        });

        newCommentBox.Clear();
        storage.SaveIssue(currentEditingIssue, config);
        UpdateCommentsUI();
        SetStatus("댓글이 등록되었습니다.");
    }

    private void UpdateLinkedViewUI()
    {
        if (currentEditingIssue == null || string.IsNullOrWhiteSpace(currentEditingIssue.LinkedViewName))
        {
            linkedViewText.Text = "(연계된 뷰 없음)";
            openViewBtn.IsEnabled = false;
        }
        else
        {
            linkedViewText.Text = $"연계 뷰: {currentEditingIssue.LinkedViewName} (ID: {currentEditingIssue.LinkedViewId})";
            openViewBtn.IsEnabled = true;
        }
    }

    private void UpdateLinkedElementsUI()
    {
        linkedElementsChipsPanel.Children.Clear();

        if (currentEditingIssue == null || currentEditingIssue.ElementIds.Count == 0)
        {
            linkedElementsText.Text = "(연결된 객체 없음)";
            selectOnlyElementsBtn.IsEnabled = false;
            zoomElementsBtn.IsEnabled = false;
            return;
        }

        linkedElementsText.Text = $"연결된 객체: 총 {currentEditingIssue.ElementIds.Count}개  ";
        selectOnlyElementsBtn.IsEnabled = true;
        zoomElementsBtn.IsEnabled = true;

        foreach (var idVal in currentEditingIssue.ElementIds)
        {
            var chip = new Button
            {
                Content = $"🆔 {idVal}",
                Margin = new Thickness(0, 0, 5, 5),
                Padding = new Thickness(7, 2, 7, 2),
                FontSize = 11,
                Background = new SolidColorBrush(MediaColor.FromRgb(240, 244, 250)),
                BorderBrush = Brushes.SteelBlue,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"클릭: Revit에서 이 객체만 선택 (줌 없음)\n더블클릭: 이 객체로 줌 이동"
            };

            chip.Click += (s, e) => SelectSingleElement(idVal, zoom: false);
            chip.MouseDoubleClick += (s, e) => { e.Handled = true; SelectSingleElement(idVal, zoom: true); };

            linkedElementsChipsPanel.Children.Add(chip);
        }
    }

    private void RebuildCustomFieldsUI(Dictionary<string, string> values)
    {
        customFieldsPanel.Children.Clear();
        customFieldControls.Clear();

        if (config.CustomFields.Count == 0)
        {
            customFieldsPanel.Children.Add(new TextBlock
            {
                Text = "(설정된 추가 커스텀 필드가 없습니다.)",
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

    // Event Triggers
    private void LinkActiveView(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.GetCurrentViewInfo;
        revitEvent.Raise();
    }

    private void OpenLinkedView(object sender, RoutedEventArgs e)
    {
        if (currentEditingIssue == null || currentEditingIssue.LinkedViewId <= 0) return;

        if (!string.IsNullOrWhiteSpace(currentEditingIssue.SourceModel) &&
            !currentEditingIssue.SourceModel.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus($"참고: 해당 연계 뷰는 '{currentEditingIssue.SourceModel}' 모델의 뷰입니다. (현재 모델: {currentModelName})");
        }

        eventHandler.CurrentAction = IssueRevitAction.SwitchToViewAndHighlight;
        eventHandler.TargetViewId = currentEditingIssue.LinkedViewId;
        eventHandler.TargetElementIds = currentEditingIssue.ElementIds;
        revitEvent.Raise();
        SetStatus($"연계 뷰 '{currentEditingIssue.LinkedViewName}'로 전환하고 객체를 하이라이트합니다.");
    }

    private void PickRectElementsFromRevit(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.PickElementsByRectangle;
        revitEvent.Raise();
        SetStatus("마우스로 영역을 드래그하여 객체들을 한 번에 선택하세요...");
    }

    private void GetSelectedFromRevit(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.GetSelectedElements;
        revitEvent.Raise();
    }

    private void PickElementsFromRevit(object sender, RoutedEventArgs e)
    {
        eventHandler.CurrentAction = IssueRevitAction.PickElements;
        revitEvent.Raise();
        SetStatus("Ctrl 키를 누르고 객체들을 선택하세요 (완료 시 Finish 클릭)...");
    }

    private void SelectSingleElement(long idVal, bool zoom)
    {
        if (currentEditingIssue != null && !string.IsNullOrWhiteSpace(currentEditingIssue.SourceModel) &&
            !currentEditingIssue.SourceModel.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus($"참고: 해당 객체(ID: {idVal})는 '{currentEditingIssue.SourceModel}' 모델의 객체입니다.");
        }

        eventHandler.CurrentAction = zoom ? IssueRevitAction.HighlightElements : IssueRevitAction.SelectElementsOnly;
        eventHandler.TargetElementIds = [idVal];
        revitEvent.Raise();
        SetStatus(zoom ? $"객체 ID: {idVal}로 줌 이동합니다." : $"객체 ID: {idVal}를 Revit에서 선택했습니다. (줌 없음)");
    }

    private void SelectAllElements(bool zoom)
    {
        if (currentEditingIssue == null || currentEditingIssue.ElementIds.Count == 0) return;

        if (!string.IsNullOrWhiteSpace(currentEditingIssue.SourceModel) &&
            !currentEditingIssue.SourceModel.Equals(currentModelName, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus($"참고: 해당 객체들은 '{currentEditingIssue.SourceModel}' 모델의 객체입니다.");
        }

        eventHandler.CurrentAction = zoom ? IssueRevitAction.HighlightElements : IssueRevitAction.SelectElementsOnly;
        eventHandler.TargetElementIds = currentEditingIssue.ElementIds;
        revitEvent.Raise();
        SetStatus(zoom ? $"연계 객체 {currentEditingIssue.ElementIds.Count}개를 선택하고 줌 이동합니다." : $"연계 객체 {currentEditingIssue.ElementIds.Count}개를 Revit에서 선택했습니다. (줌 없음)");
    }

    private void CaptureActiveViewFromRevit(object sender, RoutedEventArgs e)
    {
        int snapIndex = (currentEditingIssue?.SnapshotFileNames.Count ?? 0) + 1;
        string baseName = (currentEditingIssue != null && !string.IsNullOrWhiteSpace(currentEditingIssue.Id))
            ? $"{currentEditingIssue.Id}_snap_{snapIndex}_{DateTime.Now:HHmmss}"
            : $"snap_{DateTime.Now:yyyyMMdd_HHmmss}_{snapIndex}";

        eventHandler.CurrentAction = IssueRevitAction.CaptureView;
        eventHandler.CaptureTargetFolder = storage.IssueDirectory;
        eventHandler.CaptureTargetBaseName = baseName;
        revitEvent.Raise();
        SetStatus("현재 활성 뷰를 캡처 중입니다...");
    }

    private void AddLocalImageSnapshot(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "스냅샷에 추가할 이미지 선택"
        };
        if (ofd.ShowDialog() == true)
        {
            try
            {
                string ext = Path.GetExtension(ofd.FileName);
                int snapIndex = (currentEditingIssue?.SnapshotFileNames.Count ?? 0) + 1;
                string destName = $"local_{DateTime.Now:yyyyMMdd_HHmmss}_{snapIndex}{ext}";
                string destPath = Path.Combine(storage.IssueDirectory, destName);
                File.Copy(ofd.FileName, destPath, true);

                if (currentEditingIssue == null) currentEditingIssue = new IssueItem();
                currentEditingIssue.SnapshotFileNames.Add(destName);
                UpdateSnapshotGalleryUI();
                SetStatus($"외부 이미지가 추가되었습니다: {destName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"이미지 추가 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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

        string currentOperator = GetCurrentOperatorDisplayText();

        // Check if author or assigned user exists in config, prompt/register if new
        string assignedText = assignedToCombo.Text.Trim();
        if (assignedText.Equals("(미지정)", StringComparison.OrdinalIgnoreCase) || assignedText.Equals("미지정", StringComparison.OrdinalIgnoreCase))
        {
            assignedText = "";
        }
        else if (!string.IsNullOrWhiteSpace(assignedText))
        {
            var matchedUser = config.Users.FirstOrDefault(u => u.DisplayText.Equals(assignedText, StringComparison.OrdinalIgnoreCase) || u.DisplayName.Equals(assignedText, StringComparison.OrdinalIgnoreCase));
            if (matchedUser == null)
            {
                var ask = MessageBox.Show(this, $"담당자 '{assignedText}'는 등록된 참여자 목록에 없습니다.\n신규 참여자로 추가하시겠습니까?", "참여자 등록 안내", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ask == MessageBoxResult.Yes)
                {
                    config.Users.Add(new ProjectUser { DisplayName = assignedText });
                    storage.SaveConfig(config);
                    UpdateUsersCombos();
                }
            }
            else
            {
                assignedText = matchedUser.DisplayText; // 정규화된 이름 (회사)
            }
        }

        bool isNew = currentEditingIssue == null || string.IsNullOrWhiteSpace(currentEditingIssue.Id);
        if (currentEditingIssue == null) currentEditingIssue = new IssueItem();

        string newStatus = statusCombo.SelectedItem?.ToString() ?? "Open";
        string newPriority = priorityCombo.Text.Trim();
        string newD1 = discipline1Combo.Text.Trim();
        string newD2 = discipline2Combo.Text.Trim();
        string newType = typeCombo.Text.Trim();
        string newDue = dueDatePicker.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
        string newLoc = locationBox.Text.Trim();
        string newRoot = rootCauseCombo.Text.Trim();
        string newDesc = descBox.Text;
        var newCustomFields = CollectCustomFieldValues();

        // Automatic Activity History Comments on Modification
        if (!isNew && originalSnapshot != null)
        {
            var changes = new List<string>();

            // 1. 상태
            if (!originalSnapshot.Status.Equals(newStatus, StringComparison.OrdinalIgnoreCase))
                changes.Add($"상태: '{originalSnapshot.Status}' → '{newStatus}'");

            // 2. 우선순위
            if (!originalSnapshot.Priority.Equals(newPriority, StringComparison.OrdinalIgnoreCase))
                changes.Add($"우선순위: '{originalSnapshot.Priority}' → '{newPriority}'");

            // 3. 담당자
            if (!originalSnapshot.AssignedTo.Equals(assignedText, StringComparison.OrdinalIgnoreCase))
            {
                string oldAssigned = string.IsNullOrWhiteSpace(originalSnapshot.AssignedTo) ? "미지정" : originalSnapshot.AssignedTo;
                string newAssigned = string.IsNullOrWhiteSpace(assignedText) ? "미지정" : assignedText;
                changes.Add($"담당자: '{oldAssigned}' → '{newAssigned}'");
            }

            // 4. 공종 1 & 공종 2(간섭)
            if (!originalSnapshot.Discipline1.Equals(newD1, StringComparison.OrdinalIgnoreCase))
                changes.Add($"공종 1: '{originalSnapshot.Discipline1}' → '{newD1}'");

            if (!originalSnapshot.Discipline2.Equals(newD2, StringComparison.OrdinalIgnoreCase))
                changes.Add($"공종 2(간섭): '{originalSnapshot.Discipline2}' → '{newD2}'");

            // 5. 제목
            if (!originalSnapshot.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                string Trunc(string s) => s.Length <= 20 ? s : s[..17] + "...";
                changes.Add($"제목: '{Trunc(originalSnapshot.Title)}' → '{Trunc(title)}'");
            }

            // 6. 유형
            if (!originalSnapshot.Type.Equals(newType, StringComparison.OrdinalIgnoreCase))
                changes.Add($"유형: '{originalSnapshot.Type}' → '{newType}'");

            // 7. 마감일
            if (!originalSnapshot.DueDate.Equals(newDue, StringComparison.OrdinalIgnoreCase))
            {
                string oldDue = string.IsNullOrWhiteSpace(originalSnapshot.DueDate) ? "미지정" : originalSnapshot.DueDate;
                string dueDisplay = string.IsNullOrWhiteSpace(newDue) ? "미지정" : newDue;
                changes.Add($"마감일: '{oldDue}' → '{dueDisplay}'");
            }

            // 8. 위치/층
            if (!originalSnapshot.Location.Equals(newLoc, StringComparison.OrdinalIgnoreCase))
            {
                string oldLoc = string.IsNullOrWhiteSpace(originalSnapshot.Location) ? "미지정" : originalSnapshot.Location;
                string locDisplay = string.IsNullOrWhiteSpace(newLoc) ? "미지정" : newLoc;
                changes.Add($"위치: '{oldLoc}' → '{locDisplay}'");
            }

            // 9. 근본 원인
            if (!originalSnapshot.RootCause.Equals(newRoot, StringComparison.OrdinalIgnoreCase))
                changes.Add($"근본원인: '{originalSnapshot.RootCause}' → '{newRoot}'");

            // 10. 상세 설명
            if (!originalSnapshot.Description.Equals(newDesc, StringComparison.Ordinal))
                changes.Add("상세 설명 내용 수정");

            // 11. 연계 뷰
            if (originalSnapshot.LinkedViewId != currentEditingIssue.LinkedViewId)
            {
                if (currentEditingIssue.LinkedViewId <= 0)
                    changes.Add("연계 뷰 해제");
                else
                    changes.Add($"연계 뷰: '{currentEditingIssue.LinkedViewName}'");
            }

            // 12. 연계 객체
            if (!originalSnapshot.ElementIds.SequenceEqual(currentEditingIssue.ElementIds))
            {
                changes.Add($"연계 객체 변경 ({originalSnapshot.ElementIds.Count}개 → {currentEditingIssue.ElementIds.Count}개)");
            }

            // 13. 스냅샷 이미지
            if (!originalSnapshot.SnapshotFileNames.SequenceEqual(currentEditingIssue.SnapshotFileNames))
            {
                changes.Add($"스냅샷 이미지 변경 ({originalSnapshot.SnapshotFileNames.Count}장 → {currentEditingIssue.SnapshotFileNames.Count}장)");
            }

            // 14. 커스텀 필드
            foreach (var cf in config.CustomFields)
            {
                originalSnapshot.CustomFields.TryGetValue(cf.Id, out var oldV);
                newCustomFields.TryGetValue(cf.Id, out var newV);
                oldV ??= "";
                newV ??= "";
                if (!oldV.Equals(newV, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add($"{cf.Name}: '{(string.IsNullOrWhiteSpace(oldV) ? "미지정" : oldV)}' → '{(string.IsNullOrWhiteSpace(newV) ? "미지정" : newV)}'");
                }
            }

            if (changes.Count > 0)
            {
                currentEditingIssue.Comments.Add(new IssueComment
                {
                    Author = currentOperator,
                    Content = $"[수정 내역]\n• {string.Join("\n• ", changes)}",
                    CreatedAt = DateTime.Now,
                    IsSystem = true
                });
            }
        }
        else if (isNew)
        {
            currentEditingIssue.Comments.Add(new IssueComment
            {
                Author = currentOperator,
                Content = $"이슈 신규 등록 (상태: {newStatus}, 우선순위: {newPriority}, 담당자: {(string.IsNullOrWhiteSpace(assignedText) ? "미지정" : assignedText)})",
                CreatedAt = DateTime.Now,
                IsSystem = true
            });
        }

        currentEditingIssue.Title = title;
        currentEditingIssue.Status = newStatus;
        currentEditingIssue.Priority = newPriority;
        currentEditingIssue.Discipline1 = newD1;
        currentEditingIssue.Discipline2 = newD2;
        currentEditingIssue.Type = newType;
        currentEditingIssue.AssignedTo = assignedText;
        if (isNew || string.IsNullOrWhiteSpace(currentEditingIssue.CreatedBy))
        {
            currentEditingIssue.CreatedBy = currentOperator;
        }
        currentEditingIssue.DueDate = newDue;
        currentEditingIssue.Location = newLoc;
        currentEditingIssue.RootCause = newRoot;
        currentEditingIssue.Description = newDesc;
        currentEditingIssue.CustomFields = newCustomFields;

        if (isNew || string.IsNullOrWhiteSpace(currentEditingIssue.SourceModel))
        {
            currentEditingIssue.SourceModel = currentModelName;
        }

        storage.SaveIssue(currentEditingIssue, config);

        string savedId = currentEditingIssue.Id;
        SetStatus($"이슈 {currentEditingIssue.DisplayNumber} 저장 완료.");
        LoadIssues();

        // 저장 후 새 이슈로 리셋되지 않고 현재 이슈를 그대로 유지하여 계속 확인/수정 가능하도록 처리
        var reloaded = issues.FirstOrDefault(x => x.Id == savedId);
        if (reloaded != null)
        {
            issueGrid.SelectedItem = reloaded;
            EditIssue(reloaded);
        }
        else
        {
            EditIssue(currentEditingIssue);
        }
    }

    private void ArchiveCurrentIssue()
    {
        if (currentEditingIssue == null || string.IsNullOrWhiteSpace(currentEditingIssue.Id)) return;

        var result = MessageBox.Show(this, $"이슈 {currentEditingIssue.DisplayNumber} ('{currentEditingIssue.Title}')를 보관함(아카이브)으로 이동하시겠습니까?\n(영구 삭제되지 않으며 언제든 복원할 수 있습니다.)", "보관함 이동 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            storage.ArchiveIssue(currentEditingIssue.Id);
            SetStatus($"이슈 {currentEditingIssue.DisplayNumber}를 보관함으로 이동했습니다.");
            LoadIssues();
            PrepareNewIssue();
        }
    }

    private void RestoreCurrentIssue()
    {
        if (currentEditingIssue == null || string.IsNullOrWhiteSpace(currentEditingIssue.Id)) return;

        storage.RestoreIssue(currentEditingIssue.Id);
        SetStatus($"이슈 {currentEditingIssue.DisplayNumber}가 정상 복원되었습니다.");
        LoadIssues();
        PrepareNewIssue();
    }

    private void OpenUserManagerDialog(object sender, RoutedEventArgs e)
    {
        var dlg = new UserManagerDialog(config, doc.Application.Username, IsCurrentOperatorAdmin()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.IsSaved)
        {
            config.Users = dlg.GetResultUsers();
            storage.SaveConfig(config);
            UpdateUsersCombos();
            UpdateCurrentUserUI();
            UpdateCommentsUI();
            SetStatus("프로젝트 참여자 목록 및 관리자 권한이 저장되었습니다.");
        }
    }

    private void OpenColumnConfigDialog(object sender, RoutedEventArgs e)
    {
        var dlg = new ColumnConfigDialog(columnPrefs) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.IsSaved)
        {
            columnPrefs = dlg.GetResult();
            UserPreferences.SaveColumns(columnPrefs);
            BuildGridColumns();
            LoadIssues();
            SetStatus("상단 제목 및 열 설정이 적용되었습니다.");
        }
    }

    private void OpenConfigDialog(object sender, RoutedEventArgs e)
    {
        var dlg = new ProjectSettingsDialog(config) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.IsSaved)
        {
            storage.SaveConfig(config);
            RefreshConfigDrivenUI();
            SetStatus("프로젝트 분류 및 필드 설정이 저장되었습니다.");
        }
    }

    private void RefreshConfigDrivenUI()
    {
        priorityCombo.ItemsSource = config.PriorityList;
        priorityFilterCombo.ItemsSource = new[] { "전체" }.Concat(config.PriorityList).ToArray();
        discipline1Combo.ItemsSource = config.DisciplineList;
        discipline2Combo.ItemsSource = new[] { "(해당 없음)" }.Concat(config.DisciplineList).ToList();
        typeCombo.ItemsSource = config.TypeList;
        rootCauseCombo.ItemsSource = config.RootCauseList;
        RebuildCustomFieldsUI(currentEditingIssue?.CustomFields ?? []);
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
