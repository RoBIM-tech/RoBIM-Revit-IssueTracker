using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public sealed class UserManagerDialog : Window
{
    private readonly ObservableCollection<ProjectUser> users;
    private readonly ListView listView = new();
    private readonly TextBox nameBox = new() { MinWidth = 100 };
    private readonly TextBox revitUserBox = new() { MinWidth = 110 };
    private readonly TextBox companyBox = new() { MinWidth = 90 };
    private readonly string currentRevitUser;

    public bool IsSaved { get; private set; }

    public UserManagerDialog(List<ProjectUser> currentUsers, string currentRevitUser)
    {
        this.currentRevitUser = currentRevitUser;
        users = new ObservableCollection<ProjectUser>(
            currentUsers.Select(u => new ProjectUser
            {
                DisplayName = u.DisplayName,
                RevitUsername = u.RevitUsername,
                Company = u.Company
            }));

        RoBimTheme.Apply(this);
        Title = "프로젝트 참여자(사용자) 관리";
        Width = 580;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI();
    }

    public List<ProjectUser> GetResultUsers() => users.ToList();

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        // Bottom Actions
        var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        var saveBtn = new Button { Content = "저장 및 닫기", MinWidth = 90, Margin = new Thickness(0, 0, 6, 0) };
        var cancelBtn = new Button { Content = "취소", MinWidth = 70 };

        saveBtn.Click += (s, e) => { IsSaved = true; DialogResult = true; Close(); };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(saveBtn);
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Top Add Box
        var groupBox = new GroupBox
        {
            Header = "신규 참여자 등록",
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8)
        };
        DockPanel.SetDock(groupBox, Dock.Top);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock { Text = "이름(실명) *:", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(nameBox, 0); Grid.SetColumn(nameBox, 1);
        grid.Children.Add(nameBox);

        var revitLabel = new TextBlock { Text = "Revit 사용자명:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetRow(revitLabel, 0); Grid.SetColumn(revitLabel, 2);
        grid.Children.Add(revitLabel);
        revitUserBox.Text = currentRevitUser;
        Grid.SetRow(revitUserBox, 0); Grid.SetColumn(revitUserBox, 3);
        grid.Children.Add(revitUserBox);

        var compLabel = new TextBlock { Text = "소속/회사:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(compLabel, 1); Grid.SetColumn(compLabel, 0);
        grid.Children.Add(compLabel);
        companyBox.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(companyBox, 1); Grid.SetColumn(companyBox, 1);
        grid.Children.Add(companyBox);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(btnPanel, 1); Grid.SetColumn(btnPanel, 2); Grid.SetColumnSpan(btnPanel, 2);

        var addBtn = new Button { Content = "사용자 추가", MinWidth = 80, Margin = new Thickness(0, 0, 6, 0) };
        var deleteBtn = new Button { Content = "선택 삭제", MinWidth = 70 };

        addBtn.Click += (s, e) =>
        {
            string name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "이름을 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            users.Add(new ProjectUser
            {
                DisplayName = name,
                RevitUsername = revitUserBox.Text.Trim(),
                Company = companyBox.Text.Trim()
            });

            nameBox.Clear();
            companyBox.Clear();
        };

        deleteBtn.Click += (s, e) =>
        {
            if (listView.SelectedItem is ProjectUser selected)
            {
                users.Remove(selected);
            }
        };

        btnPanel.Children.Add(addBtn);
        btnPanel.Children.Add(deleteBtn);
        grid.Children.Add(btnPanel);

        groupBox.Content = grid;
        root.Children.Add(groupBox);

        // User List
        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn { Header = "이름(표시)", DisplayMemberBinding = new System.Windows.Data.Binding("DisplayName"), Width = 130 });
        gridView.Columns.Add(new GridViewColumn { Header = "Revit 사용자명", DisplayMemberBinding = new System.Windows.Data.Binding("RevitUsername"), Width = 150 });
        gridView.Columns.Add(new GridViewColumn { Header = "소속/회사", DisplayMemberBinding = new System.Windows.Data.Binding("Company"), Width = 130 });
        gridView.Columns.Add(new GridViewColumn { Header = "선택 표시 텍스트", DisplayMemberBinding = new System.Windows.Data.Binding("DisplayText"), Width = 130 });

        listView.View = gridView;
        listView.ItemsSource = users;
        root.Children.Add(listView);
    }
}
