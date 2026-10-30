using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class ProjectSettingsDialog : Window
{
    private readonly IssueProjectConfig config;

    private readonly ObservableCollection<string> disciplineList;
    private readonly ObservableCollection<string> typeList;
    private readonly ObservableCollection<string> rootCauseList;
    private readonly ObservableCollection<string> priorityList;
    private readonly ObservableCollection<CustomFieldDefinition> customFields;

    public bool IsSaved { get; private set; }

    public ProjectSettingsDialog(IssueProjectConfig config)
    {
        this.config = config;

        disciplineList = new ObservableCollection<string>(config.DisciplineList);
        typeList = new ObservableCollection<string>(config.TypeList);
        rootCauseList = new ObservableCollection<string>(config.RootCauseList);
        priorityList = new ObservableCollection<string>(config.PriorityList);

        customFields = new ObservableCollection<CustomFieldDefinition>(
            config.CustomFields.Select(f => new CustomFieldDefinition
            {
                Id = f.Id,
                Name = f.Name,
                FieldType = f.FieldType,
                Options = new List<string>(f.Options),
                DefaultValue = f.DefaultValue
            }));

        RoBimTheme.Apply(this);
        Title = "프로젝트 항목 및 분류 설정 (공종, 유형, 원인, 필드)";
        Width = 680;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI();
    }

    public void ApplyToConfig(IssueProjectConfig target)
    {
        target.DisciplineList = disciplineList.ToList();
        target.TypeList = typeList.ToList();
        target.RootCauseList = rootCauseList.ToList();
        target.PriorityList = priorityList.ToList();
        target.CustomFields = customFields.ToList();
    }

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        // Bottom Actions
        var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        var saveBtn = new Button { Content = "설정 저장 및 닫기", MinWidth = 110, Margin = new Thickness(0, 0, 6, 0) };
        var cancelBtn = new Button { Content = "취소", MinWidth = 70 };

        saveBtn.Click += (s, e) =>
        {
            ApplyToConfig(config);
            IsSaved = true;
            DialogResult = true;
            Close();
        };

        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(saveBtn);
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Tab Control
        var tabCtrl = new TabControl();

        // Tab 1: Disciplines (공종 1 & 공종 2 공통 목록)
        var tab1 = new TabItem { Header = " 공종 목록 (공종 1·2 공통) " };
        var dEditor = CreateListEditor("공종 목록 (간섭 검토 시 공종 1과 공종 2에서 선택할 공통 목록)", disciplineList);
        tab1.Content = dEditor;
        tabCtrl.Items.Add(tab1);

        // Tab 2: Types & Root Causes
        var tab2 = new TabItem { Header = " 유형 및 근본원인 " };
        var grid2 = new Grid { Margin = new Thickness(8) };
        grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        grid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var typeEditor = CreateListEditor("이슈 유형 (Type)", typeList);
        Grid.SetColumn(typeEditor, 0);
        grid2.Children.Add(typeEditor);
        var rootCauseEditor = CreateListEditor("근본 원인 (Root Cause)", rootCauseList);
        Grid.SetColumn(rootCauseEditor, 2);
        grid2.Children.Add(rootCauseEditor);
        tab2.Content = grid2;
        tabCtrl.Items.Add(tab2);

        // Tab 3: Priority
        var tab3 = new TabItem { Header = " 우선순위 " };
        var grid3 = new Grid { Margin = new Thickness(8) };
        grid3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var priorityEditor = CreateListEditor("우선순위 (Priority)", priorityList);
        grid3.Children.Add(priorityEditor);
        tab3.Content = grid3;
        tabCtrl.Items.Add(tab3);

        // Tab 4: Custom Fields
        var tab4 = new TabItem { Header = " 추가 커스텀 필드 " };
        tab4.Content = BuildCustomFieldsEditor();
        tabCtrl.Items.Add(tab4);

        root.Children.Add(tabCtrl);
    }

    private System.Windows.UIElement CreateListEditor(string title, ObservableCollection<string> list)
    {
        var groupBox = new GroupBox { Header = title, Padding = new Thickness(6) };
        var panel = new DockPanel();
        groupBox.Content = panel;

        var addBar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(addBar, Dock.Top);

        var addBtn = new Button { Content = "추가", MinWidth = 55, Margin = new Thickness(4, 0, 0, 0) };
        DockPanel.SetDock(addBtn, Dock.Right);
        addBar.Children.Add(addBtn);

        var input = new TextBox { Margin = new Thickness(0) };
        addBar.Children.Add(input);

        panel.Children.Add(addBar);

        var bottomActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(bottomActions, Dock.Bottom);

        var delBtn = new Button { Content = "선택 삭제", MinWidth = 70 };
        bottomActions.Children.Add(delBtn);
        panel.Children.Add(bottomActions);

        var listBox = new ListBox { ItemsSource = list };
        panel.Children.Add(listBox);

        addBtn.Click += (s, e) =>
        {
            string val = input.Text.Trim();
            if (!string.IsNullOrWhiteSpace(val) && !list.Contains(val))
            {
                list.Add(val);
                input.Clear();
            }
        };

        delBtn.Click += (s, e) =>
        {
            if (listBox.SelectedItem is string sel)
            {
                list.Remove(sel);
            }
        };

        return groupBox;
    }

    private System.Windows.UIElement BuildCustomFieldsEditor()
    {
        var panel = new DockPanel { Margin = new Thickness(8) };

        var addBox = new GroupBox { Header = "새 커스텀 필드 추가", Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(addBox, Dock.Top);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var nameBox = new TextBox();
        var typeCombo = new ComboBox { ItemsSource = new[] { "Text", "Dropdown", "Radio", "Checkbox" }, SelectedIndex = 0 };
        var optionsBox = new TextBox { ToolTip = "드롭다운/라디오 옵션을 쉼표(,)로 구분하여 입력" };
        var addBtn = new Button { Content = "필드 추가", MinWidth = 75 };
        var delBtn = new Button { Content = "선택 삭제", MinWidth = 70, Margin = new Thickness(6, 0, 0, 0) };

        grid.Children.Add(new TextBlock { Text = "필드명:", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(nameBox, 0); Grid.SetColumn(nameBox, 1);
        grid.Children.Add(nameBox);

        var typeLbl = new TextBlock { Text = "유형:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetRow(typeLbl, 0); Grid.SetColumn(typeLbl, 2);
        grid.Children.Add(typeLbl);
        Grid.SetRow(typeCombo, 0); Grid.SetColumn(typeCombo, 3);
        grid.Children.Add(typeCombo);

        var optLbl = new TextBlock { Text = "옵션:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(optLbl, 1); Grid.SetColumn(optLbl, 0);
        grid.Children.Add(optLbl);
        optionsBox.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(optionsBox, 1); Grid.SetColumn(optionsBox, 1);
        grid.Children.Add(optionsBox);

        var btnSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6, 6, 0, 0) };
        Grid.SetRow(btnSp, 1); Grid.SetColumn(btnSp, 2); Grid.SetColumnSpan(btnSp, 2);
        btnSp.Children.Add(addBtn);
        btnSp.Children.Add(delBtn);
        grid.Children.Add(btnSp);

        addBox.Content = grid;
        panel.Children.Add(addBox);

        var listView = new ListView();
        var gv = new GridView();
        gv.Columns.Add(new GridViewColumn { Header = "필드명", DisplayMemberBinding = new System.Windows.Data.Binding("Name"), Width = 140 });
        gv.Columns.Add(new GridViewColumn { Header = "유형", DisplayMemberBinding = new System.Windows.Data.Binding("FieldType"), Width = 90 });
        gv.Columns.Add(new GridViewColumn
        {
            Header = "선택 옵션",
            DisplayMemberBinding = new System.Windows.Data.Binding("Options") { Converter = new OptionsStringConverter() },
            Width = 260
        });
        listView.View = gv;
        listView.ItemsSource = customFields;
        panel.Children.Add(listView);

        addBtn.Click += (s, e) =>
        {
            string name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("필드명을 입력하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string type = typeCombo.SelectedItem?.ToString() ?? "Text";
            var opts = optionsBox.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();

            customFields.Add(new CustomFieldDefinition
            {
                Name = name,
                FieldType = type,
                Options = opts,
                DefaultValue = opts.FirstOrDefault() ?? ""
            });

            nameBox.Clear();
            optionsBox.Clear();
        };

        delBtn.Click += (s, e) =>
        {
            if (listView.SelectedItem is CustomFieldDefinition sel)
            {
                customFields.Remove(sel);
            }
        };

        return panel;
    }
}
