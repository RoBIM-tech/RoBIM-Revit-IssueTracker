using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class CustomFieldsDialog : Window
{
    private readonly ObservableCollection<CustomFieldDefinition> fields;
    private readonly ListView listView = new();
    private readonly TextBox nameBox = new() { MinWidth = 140 };
    private readonly ComboBox typeCombo = new() { MinWidth = 100 };
    private readonly TextBox optionsBox = new() { MinWidth = 180, ToolTip = "드롭다운 또는 라디오버튼 옵션을 쉼표(,)로 구분하여 입력하세요." };
    private readonly TextBox defaultBox = new() { MinWidth = 100 };
    private readonly Button addBtn = new() { Content = "필드 추가", MinWidth = 80 };
    private readonly Button deleteBtn = new() { Content = "선택 삭제", MinWidth = 80 };
    private readonly Button saveBtn = new() { Content = "저장 및 닫기", MinWidth = 100 };
    private readonly Button cancelBtn = new() { Content = "취소", MinWidth = 80 };

    public bool IsSaved { get; private set; }

    public CustomFieldsDialog(List<CustomFieldDefinition> existingFields)
    {
        fields = new ObservableCollection<CustomFieldDefinition>(
            existingFields.Select(f => new CustomFieldDefinition
            {
                Id = f.Id,
                Name = f.Name,
                FieldType = f.FieldType,
                Options = new List<string>(f.Options),
                DefaultValue = f.DefaultValue
            }));

        RoBimTheme.Apply(this);
        Title = "프로젝트 커스텀 필드 설정";
        Width = 650;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI();
    }

    public List<CustomFieldDefinition> GetResultFields() => fields.ToList();

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        // Bottom Actions
        var bottomPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        saveBtn.Click += (s, e) => { IsSaved = true; DialogResult = true; Close(); };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(saveBtn);
        bottomPanel.Children.Add(new Border { Width = 8 });
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Input Area (Group Box)
        var inputGroup = new GroupBox
        {
            Header = "새 필드 추가",
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8)
        };
        DockPanel.SetDock(inputGroup, Dock.Top);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        typeCombo.ItemsSource = new[] { "Text", "Dropdown", "Radio", "Checkbox" };
        typeCombo.SelectedIndex = 0;

        grid.Children.Add(new TextBlock { Text = "필드명:", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(nameBox, 0); Grid.SetColumn(nameBox, 1);
        grid.Children.Add(nameBox);

        var typeLabel = new TextBlock { Text = "유형:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetRow(typeLabel, 0); Grid.SetColumn(typeLabel, 2);
        grid.Children.Add(typeLabel);
        Grid.SetRow(typeCombo, 0); Grid.SetColumn(typeCombo, 3);
        grid.Children.Add(typeCombo);

        var optLabel = new TextBlock { Text = "선택 옵션:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(optLabel, 1); Grid.SetColumn(optLabel, 0);
        grid.Children.Add(optLabel);
        optionsBox.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(optionsBox, 1); Grid.SetColumn(optionsBox, 1);
        grid.Children.Add(optionsBox);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 6, 0, 0) };
        Grid.SetRow(btnPanel, 1); Grid.SetColumn(btnPanel, 2); Grid.SetColumnSpan(btnPanel, 2);
        addBtn.Click += AddField;
        deleteBtn.Click += DeleteSelected;
        btnPanel.Children.Add(addBtn);
        btnPanel.Children.Add(new Border { Width = 6 });
        btnPanel.Children.Add(deleteBtn);
        grid.Children.Add(btnPanel);

        inputGroup.Content = grid;
        root.Children.Add(inputGroup);

        // List Area
        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn { Header = "필드명", DisplayMemberBinding = new System.Windows.Data.Binding("Name"), Width = 150 });
        gridView.Columns.Add(new GridViewColumn { Header = "유형", DisplayMemberBinding = new System.Windows.Data.Binding("FieldType"), Width = 100 });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "선택 옵션(드롭다운/라디오)",
            DisplayMemberBinding = new System.Windows.Data.Binding("Options")
            {
                Converter = new OptionsStringConverter()
            },
            Width = 320
        });

        listView.View = gridView;
        listView.ItemsSource = fields;
        root.Children.Add(listView);
    }

    private void AddField(object sender, RoutedEventArgs e)
    {
        string name = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("필드명을 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string type = typeCombo.SelectedItem?.ToString() ?? "Text";
        var options = optionsBox.Text
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        if ((type == "Dropdown" || type == "Radio") && options.Count == 0)
        {
            MessageBox.Show("드롭다운 또는 라디오버튼은 선택 옵션을 1개 이상 입력해야 합니다. (콤마 구분)", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        fields.Add(new CustomFieldDefinition
        {
            Name = name,
            FieldType = type,
            Options = options,
            DefaultValue = options.FirstOrDefault() ?? ""
        });

        nameBox.Clear();
        optionsBox.Clear();
    }

    private void DeleteSelected(object sender, RoutedEventArgs e)
    {
        if (listView.SelectedItem is CustomFieldDefinition selected)
        {
            fields.Remove(selected);
        }
        else
        {
            MessageBox.Show("삭제할 필드를 목록에서 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

public sealed class OptionsStringConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is IEnumerable<string> list)
        {
            return string.Join(", ", list);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
