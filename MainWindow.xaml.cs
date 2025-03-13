using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using File = System.IO.File;

namespace SingboxGUI
{
    public partial class MainWindow : Window
    {
        private Process singboxProcess;
        private readonly string assetsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Assets"
        );
        private readonly string configFilePath;
        private readonly string singboxPath;
        private ClashApiConfig config;
        private NotifyIcon notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            singboxPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "sing-box.exe"
            );
            configFilePath = Path.Combine(assetsPath, "config.json");

            // Load the stored Subscription URL from settings
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.SubscriptionUrl))
            {
                txtSubscriptionUrl.Text = Properties.Settings.Default.SubscriptionUrl;
            }

            // Set the initial visibility of the placeholder
            txtPlaceholder.Visibility = string.IsNullOrWhiteSpace(txtSubscriptionUrl.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Enable the start button if the config file exists
            if (File.Exists(configFilePath))
                btnStart.IsEnabled = true;

            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Windows.Forms.Application.ExecutablePath
                ),
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip(),
            };

            notifyIcon.ContextMenuStrip.Items.Add(
                "Start",
                null,
                (s, e) => StartSingbox(null, null)
            );
            notifyIcon.ContextMenuStrip.Items.Add("Stop", null, (s, e) => StopSingbox(null, null));
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => CloseApp());

            notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowWindow();
            }
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void CloseApp()
        {
            StopSingbox(null, null);
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Hide the placeholder when the TextBox gets focus
            if (string.IsNullOrWhiteSpace(txtSubscriptionUrl.Text))
            {
                txtPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Show the placeholder if the TextBox is empty when it loses focus
            if (string.IsNullOrWhiteSpace(txtSubscriptionUrl.Text))
            {
                txtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Hide or show the placeholder depending on whether the TextBox has text
            txtPlaceholder.Visibility = string.IsNullOrWhiteSpace(txtSubscriptionUrl.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Override OnClosing to stop Sing-box process when closing the application
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Stop Sing-box if it's running
            StopSingbox(null, null);
            notifyIcon.Dispose();
            base.OnClosing(e);
        }

        private async void InitializeWebView2()
        {
            if (config != null)
            {
                try
                {
                    // Initialize WebView2 properly
                    await WebViewLog.EnsureCoreWebView2Async();

                    if (WebViewLog.CoreWebView2 != null)
                    {
                        // Use the config data to navigate to the required URL
                        WebViewLog.CoreWebView2.Navigate(config.AccessControlAllowOrigin);
                    }
                    else
                    {
                        Debug.WriteLine("WebView2 CoreWebView2 is null.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error initializing WebView2: " + ex.Message);
                }
            }
            else
            {
                // Log error if config is not loaded
                Debug.WriteLine("Config could not be loaded.");
            }
        }

        // Start Sing-box process
        private void StartSingbox(object sender, RoutedEventArgs e)
        {
            // Check if Sing-box executable and config file exist
            if (!File.Exists(singboxPath))
            {
                System.Windows.MessageBox.Show(
                    "sing-box.exe file not found!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (!File.Exists(configFilePath))
            {
                System.Windows.MessageBox.Show(
                    "Valid config file not selected!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Load the config file from the app's assets folder
            config = LoadConfig();

            if (singboxProcess == null || singboxProcess.HasExited)
            {
                singboxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = singboxPath,
                        Arguments = $"run -c \"{configFilePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        Verb = "runas", // Run Sing-box with Administrator privileges
                    },
                };

                // Capture output and error data
                singboxProcess.OutputDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            logText.Text = args.Data;
                        });
                    }
                };

                singboxProcess.ErrorDataReceived += (s, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            logText.Text = args.Data;
                        });
                    }
                };

                Dispatcher.Invoke(() =>
                {
                    InitializeWebView2();
                });
                singboxProcess.Start();
                singboxProcess.BeginOutputReadLine();
                singboxProcess.BeginErrorReadLine();
            }
        }

        // Stop Sing-box process
        private void StopSingbox(object sender, RoutedEventArgs e)
        {
            singboxProcess?.Kill();
            singboxProcess = null;
        }

        // Fetch and update config from the internet
        private async void UpdateConfig(object sender, RoutedEventArgs e)
        {
            string url = txtSubscriptionUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                System.Windows.MessageBox.Show(
                    "Please enter a valid URL!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Get the response with original encoding (don't use GetStringAsync here)
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    // Read the content as bytes to preserve the original encoding
                    byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();

                    // Create directory if not exists
                    Directory.CreateDirectory(assetsPath);

                    // Write the content with its original encoding
                    File.WriteAllBytes(configFilePath, contentBytes);

                    // Save the URL in settings
                    Properties.Settings.Default.SubscriptionUrl = url;
                    Properties.Settings.Default.Save(); // Save the settings

                    System.Windows.MessageBox.Show(
                        "Config updated successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    btnStart.IsEnabled = true; // Enable the Start button after receiving config
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Error retrieving settings: " + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // Run a specific command in Sing-box
        private void RunCommand(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(singboxPath))
            {
                System.Windows.MessageBox.Show(
                    "sing-box.exe file not found!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            string selectedCommand = (
                commandSelector.SelectedItem as ComboBoxItem
            )?.Content.ToString();
            if (string.IsNullOrEmpty(selectedCommand))
                return;

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = singboxPath,
                    Arguments = selectedCommand,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas", // Run Sing-box with Administrator privileges
                },
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            logText.Text = output;
        }

        // Load the configuration file
        private ClashApiConfig LoadConfig()
        {
            if (!File.Exists(configFilePath))
            {
                Debug.WriteLine("Config file not found at: " + configFilePath);
                return null;
            }

            try
            {
                // Read the file content
                string jsonString = File.ReadAllText(configFilePath);
                Debug.WriteLine("Config file content: " + jsonString);

                // Deserialize the JSON string using JsonConvert from Newtonsoft.Json
                var configRoot = JsonConvert.DeserializeObject<RootConfig>(jsonString);

                if (configRoot?.Experimental?.ClashApi != null)
                {
                    Debug.WriteLine(
                        "ClashApiConfig loaded: "
                            + configRoot.Experimental.ClashApi.AccessControlAllowOrigin
                    );
                    return configRoot.Experimental.ClashApi;
                }

                return null;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine("Serialization Error: " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error loading config: " + ex.Message);
                return null;
            }
        }

        private void ShowAboutMessage(object sender, RoutedEventArgs e)
        {
            // Create a custom window for the About message
            Window aboutWindow = new Window
            {
                Title = "About SingBoxGUI",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize, // Disable resizing
            };

            // Create a StackPanel to hold the content
            StackPanel panel = new StackPanel { Margin = new Thickness(10) };

            // Extract and display the application icon
            System.Windows.Controls.Image appIcon = new System.Windows.Controls.Image
            {
                Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    System
                        .Drawing.Icon.ExtractAssociatedIcon(
                            System.Windows.Forms.Application.ExecutablePath
                        )
                        .Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                ),
                Width = 48,
                Height = 48,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            };

            // Create a clickable GitHub link
            TextBlock link = new TextBlock
            {
                Text = "GitHub Repository",
                Foreground = System.Windows.Media.Brushes.Blue,
                TextDecorations = TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
            };
            link.MouseLeftButtonDown += (s, args) =>
                Process.Start(
                    new ProcessStartInfo("https://github.com/BrontoDIY/SingBoxGUI")
                    {
                        UseShellExecute = true,
                    }
                );

            // Create a ScrollViewer to display long descriptions
            ScrollViewer scrollViewer = new ScrollViewer
            {
                Height = 150, // Set height to avoid a huge window
                Content = new TextBlock
                {
                    Text =
                        "SingBoxGUI is a graphical user interface for Sing-box.\n\n"
                        + "This application provides an intuitive and user-friendly way to manage Sing-box configurations, "
                        + "control system proxy settings, and monitor real-time logs. Whether you are a beginner or an advanced user, "
                        + "SingBoxGUI helps you efficiently interact with Sing-box without the need to manually edit configuration files.\n\n"
                        + "Key Features:\n"
                        + "- Start and stop Sing-box with a single click\n"
                        + "- Manage subscription URLs and update configurations from the internet\n"
                        + "- Enable or disable system proxy settings\n"
                        + "- View real-time logs in an embedded WebView\n"
                        + "- Minimize to system tray with quick access controls\n"
                        + "- Automatically clear proxy settings upon exit\n\n"
                        + "SingBoxGUI is an open-source project developed to simplify the usage of Sing-box. "
                        + "The source code is available on GitHub for contributions and enhancements.\n\n"
                        + "Developed by BrontoDIY.",
                    TextWrapping = TextWrapping.Wrap,
                },
            };

            // Create a close button
            System.Windows.Controls.Button closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Width = 80,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            };
            closeButton.Click += (s, args) => aboutWindow.Close(); // Close the window when clicked

            // Add all elements to the panel
            panel.Children.Add(appIcon);
            panel.Children.Add(link);
            panel.Children.Add(scrollViewer);
            panel.Children.Add(closeButton);

            // Set the window content
            aboutWindow.Content = panel;
            aboutWindow.ShowDialog(); // Show the window as a modal dialog
        }
    }
}
