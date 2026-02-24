/*
 * Copyright (c) 2026 Erik Darling, Darling Data LLC
 *
 * This file is part of the SQL Server Performance Monitor.
 *
 * Licensed under the MIT License. See LICENSE file in the project root for full license information.
 */

using System;
using System.Windows;
using PerformanceMonitorDashboard.Models;
using PerformanceMonitorDashboard.Services;

namespace PerformanceMonitorDashboard
{
    public partial class AddServerDialog : Window
    {
        public ServerConnection ServerConnection { get; private set; }
        public string? Username { get; private set; }
        public string? Password { get; private set; }
        private bool _isEditMode;

        public AddServerDialog()
        {
            InitializeComponent();
            _isEditMode = false;
            ServerConnection = new ServerConnection();
            Title = "Add SQL Server";
        }

        public AddServerDialog(ServerConnection existingServer)
        {
            InitializeComponent();
            _isEditMode = true;
            ServerConnection = existingServer;
            Title = "Edit SQL Server";

            DisplayNameTextBox.Text = existingServer.DisplayName;
            ServerNameTextBox.Text = existingServer.ServerName;
            DescriptionTextBox.Text = existingServer.Description;
            IsFavoriteCheckBox.IsChecked = existingServer.IsFavorite;

            // Load encryption settings
            EncryptModeComboBox.SelectedIndex = existingServer.EncryptMode switch
            {
                "Mandatory" => 1,
                "Strict" => 2,
                _ => 0 // Optional
            };
            TrustServerCertificateCheckBox.IsChecked = existingServer.TrustServerCertificate;
            IsEnabledCheckBox.IsChecked = existingServer.IsEnabled;

            if (existingServer.UseWindowsAuth)
            {
                WindowsAuthRadio.IsChecked = true;
            }
            else
            {
                SqlAuthRadio.IsChecked = true;

                var credentialService = new CredentialService();
                var cred = credentialService.GetCredential(existingServer.Id);
                if (cred.HasValue)
                {
                    UsernameTextBox.Text = cred.Value.Username;
                    PasswordBox.Password = cred.Value.Password;
                }
            }
        }

        private void AuthType_Changed(object sender, RoutedEventArgs e)
        {
            if (SqlAuthPanel != null)
            {
                SqlAuthPanel.IsEnabled = SqlAuthRadio.IsChecked == true;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerNameTextBox.Text))
            {
                MessageBox.Show(
                    "Please enter a server name or address.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (SqlAuthRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    MessageBox.Show(
                        "Please enter a username for SQL Server authentication.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
            }

            try
            {
                var testConnection = DatabaseService.BuildConnectionString(
                    ServerNameTextBox.Text.Trim(),
                    WindowsAuthRadio.IsChecked == true,
                    UsernameTextBox.Text.Trim(),
                    PasswordBox.Password,
                    GetSelectedEncryptMode(),
                    TrustServerCertificateCheckBox.IsChecked == true
                ).ConnectionString;

                var dbService = new DatabaseService(testConnection);
                bool connected = await dbService.TestConnectionAsync();

                if (connected)
                {
                    MessageBox.Show(
                        $"Successfully connected to {ServerNameTextBox.Text}!",
                        "Connection Test Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to connect to {ServerNameTextBox.Text}.\n\nPlease check:\n" +
                        "• Server name/address is correct\n" +
                        "• Server is accessible from this machine\n" +
                        "• Firewall allows SQL Server connections\n" +
                        "• SQL Server service is running",
                        "Connection Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Connection test failed:\n\n{ex.Message}",
                    "Connection Test Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerNameTextBox.Text))
            {
                MessageBox.Show(
                    "Please enter a server name or address.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (SqlAuthRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    MessageBox.Show(
                        "Please enter a username for SQL Server authentication.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
            }

            // Test connection when data collection is enabled
            if (IsEnabledCheckBox.IsChecked == true)
            {
                SaveButton.IsEnabled = false;
                StatusText.Text = "Testing connection...";
                StatusText.Visibility = System.Windows.Visibility.Visible;

                bool connected;
                string? errorMessage = null;
                try
                {
                    var cs = DatabaseService.BuildConnectionString(
                        ServerNameTextBox.Text.Trim(),
                        WindowsAuthRadio.IsChecked == true,
                        UsernameTextBox.Text.Trim(),
                        PasswordBox.Password,
                        GetSelectedEncryptMode(),
                        TrustServerCertificateCheckBox.IsChecked == true
                    ).ConnectionString;

                    var dbService = new DatabaseService(cs);
                    connected = await dbService.TestConnectionAsync();
                }
                catch (Exception ex)
                {
                    connected = false;
                    errorMessage = ex.Message;
                }
                finally
                {
                    SaveButton.IsEnabled = true;
                }

                if (!connected)
                {
                    StatusText.Text = string.Empty;
                    StatusText.Visibility = System.Windows.Visibility.Collapsed;

                    var detail = errorMessage != null ? $"\n\nError: {errorMessage}" : string.Empty;
                    MessageBox.Show(
                        $"Failed to connect to {ServerNameTextBox.Text}.{detail}\n\n" +
                        "To save this server without a working connection, uncheck \"Enable data collection for this server\".",
                        "Connection Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                StatusText.Text = string.Empty;
                StatusText.Visibility = System.Windows.Visibility.Collapsed;
            }

            // Use server name as display name if not provided
            var displayName = string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
                ? ServerNameTextBox.Text.Trim()
                : DisplayNameTextBox.Text.Trim();

            if (_isEditMode)
            {
                ServerConnection.DisplayName = displayName;
                ServerConnection.ServerName = ServerNameTextBox.Text.Trim();
                ServerConnection.UseWindowsAuth = WindowsAuthRadio.IsChecked == true;
                ServerConnection.Description = DescriptionTextBox.Text.Trim();
                ServerConnection.IsFavorite = IsFavoriteCheckBox.IsChecked == true;
                ServerConnection.IsEnabled = IsEnabledCheckBox.IsChecked == true;
                ServerConnection.EncryptMode = GetSelectedEncryptMode();
                ServerConnection.TrustServerCertificate = TrustServerCertificateCheckBox.IsChecked == true;
            }
            else
            {
                ServerConnection = new ServerConnection
                {
                    DisplayName = displayName,
                    ServerName = ServerNameTextBox.Text.Trim(),
                    UseWindowsAuth = WindowsAuthRadio.IsChecked == true,
                    Description = DescriptionTextBox.Text.Trim(),
                    IsFavorite = IsFavoriteCheckBox.IsChecked == true,
                    IsEnabled = IsEnabledCheckBox.IsChecked == true,
                    CreatedDate = DateTime.Now,
                    LastConnected = DateTime.Now,
                    EncryptMode = GetSelectedEncryptMode(),
                    TrustServerCertificate = TrustServerCertificateCheckBox.IsChecked == true
                };
            }

            if (SqlAuthRadio.IsChecked == true)
            {
                Username = UsernameTextBox.Text.Trim();
                Password = PasswordBox.Password;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string GetSelectedEncryptMode()
        {
            return EncryptModeComboBox.SelectedIndex switch
            {
                1 => "Mandatory",
                2 => "Strict",
                _ => "Optional"
            };
        }
    }
}
