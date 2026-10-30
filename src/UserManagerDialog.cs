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
    private readonly TextBox nameBox = new() { MinWidth = 110 };
    private readonly TextBox companyBox = new() { MinWidth = 110 };
    private readonly TextBox revitUserBox = new() { MinWidth = 120 };
    private readonly CheckBox adminCheck = new() { Content = "관리자 권한 부여", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 6, 0, 0) };
    private readonly string currentRevitUser;

    public bool IsSaved { get; private set; }

    public UserManagerDialog(List<ProjectUser> currentUsers, string currentRevitUser, string? promptMessage = null)
    {
        this.currentRevitUser = currentRevitUser;
        users = new ObservableCollection<ProjectUser>(
            currentUsers.Select(u => new ProjectUser
            {
                DisplayName = u.DisplayName,
                Company = u.Company,
                RevitUsername = u.RevitUsername,
                IsAdmin = u.IsAdmin
            }));

        RoBimTheme.Apply(this);
        Title = "프로젝트 참여자(사용자) 관리 · 이름(회사) 및 관리자 권한";
        Width = 650;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InitializeUI(promptMessage);
    }

    public List<ProjectUser> GetResultUsers() => users.ToList();

    private void InitializeUI(string? promptMessage)
    {
        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        // Bottom Actions
        var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        var saveBtn = new Button { Content = "저장 및 닫기", MinWidth = 90, Margin = new Thickness(0, 0, 6, 0) };
        var cancelBtn = new Button { Content = "취소", MinWidth = 70 };

        saveBtn.Click += (s, e) =>
        {
            // 사용자가 [사용자 추가] 버튼을 누르지 않고 바로 저장하더라도 입력된 내용 자동 반영
            string pendingName = nameBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(pendingName))
            {
                users.Add(new ProjectUser
                {
                    DisplayName = pendingName,
                    Company = companyBox.Text.Trim(),
                    RevitUsername = revitUserBox.Text.Trim(),
                    IsAdmin = adminCheck.IsChecked == true
                });
            }

            if (users.Count == 0)
            {
                MessageBox.Show(this, "등록할 참여자(이름, 회사)를 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                nameBox.Focus();
                return;
            }

            IsSaved = true;
            DialogResult = true;
            Close();
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(saveBtn);
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Optional Prompt Message Banner
        if (!string.IsNullOrWhiteSpace(promptMessage))
        {
            var banner = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 140, 0)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(banner, Dock.Top);
            banner.Child = new TextBlock
            {
                Text = promptMessage,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 0))
            };
            root.Children.Add(banner);
        }

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
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock { Text = "이름(실명) *:", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(nameBox, 0); Grid.SetColumn(nameBox, 1);
        grid.Children.Add(nameBox);

        var compLabel = new TextBlock { Text = "소속/회사 *:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetRow(compLabel, 0); Grid.SetColumn(compLabel, 2);
        grid.Children.Add(compLabel);
        Grid.SetRow(companyBox, 0); Grid.SetColumn(companyBox, 3);
        grid.Children.Add(companyBox);

        var revitLabel = new TextBlock { Text = "Revit ID:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetRow(revitLabel, 1); Grid.SetColumn(revitLabel, 0);
        grid.Children.Add(revitLabel);
        revitUserBox.Text = currentRevitUser;
        revitUserBox.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetRow(revitUserBox, 1); Grid.SetColumn(revitUserBox, 1);
        grid.Children.Add(revitUserBox);

        Grid.SetRow(adminCheck, 1); Grid.SetColumn(adminCheck, 2); Grid.SetColumnSpan(adminCheck, 2);
        grid.Children.Add(adminCheck);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetRow(btnPanel, 2); Grid.SetColumn(btnPanel, 0); Grid.SetColumnSpan(btnPanel, 4);

        var addBtn = new Button { Content = "사용자 추가", MinWidth = 85, Margin = new Thickness(0, 0, 6, 0) };
        var toggleAdminBtn = new Button { Content = "선택자 관리자 권한 전환", MinWidth = 135, Margin = new Thickness(0, 0, 6, 0) };
        var deleteBtn = new Button { Content = "선택 삭제", MinWidth = 70 };

        addBtn.Click += (s, e) =>
        {
            string name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "이름을 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                nameBox.Focus();
                return;
            }

            users.Add(new ProjectUser
            {
                DisplayName = name,
                Company = companyBox.Text.Trim(),
                RevitUsername = revitUserBox.Text.Trim(),
                IsAdmin = adminCheck.IsChecked == true
            });

            nameBox.Clear();
            companyBox.Clear();
            adminCheck.IsChecked = false;
        };

        toggleAdminBtn.Click += (s, e) =>
        {
            if (listView.SelectedItem is ProjectUser selected)
            {
                selected.IsAdmin = !selected.IsAdmin;
                listView.Items.Refresh();
            }
            else
            {
                MessageBox.Show(this, "아래 목록에서 권한을 변경할 사용자를 먼저 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };

        deleteBtn.Click += (s, e) =>
        {
            if (listView.SelectedItem is ProjectUser selected)
            {
                users.Remove(selected);
            }
        };

        btnPanel.Children.Add(addBtn);
        btnPanel.Children.Add(toggleAdminBtn);
        btnPanel.Children.Add(deleteBtn);
        grid.Children.Add(btnPanel);

        groupBox.Content = grid;
        root.Children.Add(groupBox);

        // User List
        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn { Header = "권한", DisplayMemberBinding = new System.Windows.Data.Binding("AdminDisplay"), Width = 65 });
        gridView.Columns.Add(new GridViewColumn { Header = "이름(실명)", DisplayMemberBinding = new System.Windows.Data.Binding("DisplayName"), Width = 110 });
        gridView.Columns.Add(new GridViewColumn { Header = "소속/회사", DisplayMemberBinding = new System.Windows.Data.Binding("Company"), Width = 120 });
        gridView.Columns.Add(new GridViewColumn { Header = "Revit 사용자명", DisplayMemberBinding = new System.Windows.Data.Binding("RevitUsername"), Width = 120 });
        gridView.Columns.Add(new GridViewColumn { Header = "표시 형태: 이름 (회사)", DisplayMemberBinding = new System.Windows.Data.Binding("DisplayText"), Width = 180 });

        listView.View = gridView;
        listView.ItemsSource = users;
        root.Children.Add(listView);
    }
}
