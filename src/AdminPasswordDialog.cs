using System;
using System.Windows;
using System.Windows.Controls;
using RoBIM.Wpf;

namespace RoBIM.Feature;

public enum PasswordDialogMode
{
    Verify,
    SetInitial,
    Change
}

public sealed class AdminPasswordDialog : Window
{
    private readonly PasswordDialogMode mode;
    private readonly string? existingHash;
    private readonly string? existingSalt;

    private readonly PasswordBox oldPasswordBox = new();
    private readonly PasswordBox newPasswordBox = new();
    private readonly PasswordBox confirmPasswordBox = new();

    public bool IsSuccess { get; private set; }
    public string NewHash { get; private set; } = string.Empty;
    public string NewSalt { get; private set; } = string.Empty;

    public AdminPasswordDialog(PasswordDialogMode mode, string? existingHash = null, string? existingSalt = null)
    {
        this.mode = mode;
        this.existingHash = existingHash;
        this.existingSalt = existingSalt;

        RoBimTheme.Apply(this);
        Title = mode switch
        {
            PasswordDialogMode.Verify => "🔒 관리자 마스터 암호 인증",
            PasswordDialogMode.SetInitial => "🔒 관리자 마스터 암호 최초 설정",
            PasswordDialogMode.Change => "🔑 관리자 마스터 암호 변경",
            _ => "관리자 암호"
        };

        Width = 380;
        Height = mode switch
        {
            PasswordDialogMode.Verify => 190,
            PasswordDialogMode.SetInitial => 230,
            PasswordDialogMode.Change => 280,
            _ => 220
        };
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        InitializeUI();
    }

    private void InitializeUI()
    {
        var root = new DockPanel { Margin = new Thickness(14) };
        Content = root;

        // Bottom Buttons
        var bottomPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(bottomPanel, Dock.Bottom);

        var okBtn = new Button { Content = mode == PasswordDialogMode.Verify ? "인증" : "설정 완료", MinWidth = 80, Margin = new Thickness(0, 0, 6, 0), FontWeight = FontWeights.SemiBold };
        var cancelBtn = new Button { Content = "취소", MinWidth = 65 };

        okBtn.Click += OnOkClicked;
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        bottomPanel.Children.Add(okBtn);
        bottomPanel.Children.Add(cancelBtn);
        root.Children.Add(bottomPanel);

        // Form Fields Grid
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int r = 0;

        if (mode == PasswordDialogMode.Change)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lblOld = new TextBlock { Text = "현재 비밀번호 *:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblOld, r); Grid.SetColumn(lblOld, 0);
            grid.Children.Add(lblOld);

            oldPasswordBox.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(oldPasswordBox, r); Grid.SetColumn(oldPasswordBox, 1);
            grid.Children.Add(oldPasswordBox);
            r++;
        }

        if (mode == PasswordDialogMode.Verify)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lblPwd = new TextBlock { Text = "관리자 비밀번호 *:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPwd, r); Grid.SetColumn(lblPwd, 0);
            grid.Children.Add(lblPwd);

            newPasswordBox.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(newPasswordBox, r); Grid.SetColumn(newPasswordBox, 1);
            grid.Children.Add(newPasswordBox);
            r++;
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lblNew = new TextBlock { Text = "새 비밀번호 *:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblNew, r); Grid.SetColumn(lblNew, 0);
            grid.Children.Add(lblNew);

            newPasswordBox.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(newPasswordBox, r); Grid.SetColumn(newPasswordBox, 1);
            grid.Children.Add(newPasswordBox);
            r++;

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lblConfirm = new TextBlock { Text = "비밀번호 확인 *:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblConfirm, r); Grid.SetColumn(lblConfirm, 0);
            grid.Children.Add(lblConfirm);

            confirmPasswordBox.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(confirmPasswordBox, r); Grid.SetColumn(confirmPasswordBox, 1);
            grid.Children.Add(confirmPasswordBox);
            r++;
        }

        var descText = new TextBlock
        {
            Text = mode switch
            {
                PasswordDialogMode.Verify => "관리자 마스터 암호를 입력하면 관리자 권한이 인증됩니다.",
                PasswordDialogMode.SetInitial => "프로젝트 관리자 마스터 암호를 설정하세요. (최소 4자리 이상 권장)",
                PasswordDialogMode.Change => "현재 비밀번호 확인 후 새 마스터 암호로 변경합니다.",
                _ => ""
            },
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(descText, Dock.Top);
        root.Children.Add(descText);
        root.Children.Add(grid);

        Loaded += (s, e) =>
        {
            if (mode == PasswordDialogMode.Change) oldPasswordBox.Focus();
            else newPasswordBox.Focus();
        };
    }

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        if (mode == PasswordDialogMode.Verify)
        {
            string pwd = newPasswordBox.Password;
            if (string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show(this, "비밀번호를 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                newPasswordBox.Focus();
                return;
            }

            if (!PasswordHelper.VerifyPassword(pwd, existingSalt ?? "", existingHash ?? ""))
            {
                MessageBox.Show(this, "관리자 비밀번호가 일치하지 않습니다.", "인증 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                newPasswordBox.SelectAll();
                newPasswordBox.Focus();
                return;
            }

            IsSuccess = true;
            DialogResult = true;
            Close();
            return;
        }

        if (mode == PasswordDialogMode.Change)
        {
            string oldPwd = oldPasswordBox.Password;
            if (!PasswordHelper.VerifyPassword(oldPwd, existingSalt ?? "", existingHash ?? ""))
            {
                MessageBox.Show(this, "현재 관리자 비밀번호가 일치하지 않습니다.", "확인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                oldPasswordBox.SelectAll();
                oldPasswordBox.Focus();
                return;
            }
        }

        // SetInitial & Change validation
        string newPwd = newPasswordBox.Password;
        string confirmPwd = confirmPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(newPwd))
        {
            MessageBox.Show(this, "새 비밀번호를 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            newPasswordBox.Focus();
            return;
        }

        if (newPwd.Length < 4)
        {
            MessageBox.Show(this, "비밀번호는 최소 4자리 이상 입력해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            newPasswordBox.Focus();
            return;
        }

        if (newPwd != confirmPwd)
        {
            MessageBox.Show(this, "새 비밀번호와 비밀번호 확인이 일치하지 않습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            confirmPasswordBox.Focus();
            return;
        }

        string salt = PasswordHelper.GenerateSalt();
        string hash = PasswordHelper.HashPassword(newPwd, salt);

        NewSalt = salt;
        NewHash = hash;
        IsSuccess = true;
        DialogResult = true;
        Close();
    }
}
