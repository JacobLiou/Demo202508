using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Markdig;

namespace MarkdownEditor
{
    public partial class MainWindow : Window
    {
        private bool _isEditorReady = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try 
            {
                await EditorWebView.EnsureCoreWebView2Async();
                await PreviewWebView.EnsureCoreWebView2Async();

                // Load Editor
                string editorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "editor.html");
                EditorWebView.CoreWebView2.Navigate(new Uri(editorPath).AbsoluteUri);

                // Load Preview Skeleton
                string previewPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "preview.html");
                PreviewWebView.CoreWebView2.Navigate(new Uri(previewPath).AbsoluteUri);

                // Configure Editor Messaging
                EditorWebView.CoreWebView2.WebMessageReceived += EditorWebView_WebMessageReceived;
                
                // Set Editor Ready flag after navigation completes
                EditorWebView.NavigationCompleted += (s, e) => { _isEditorReady = true; };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditorWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonMessage = e.TryGetWebMessageAsString();
                
                using (JsonDocument doc = JsonDocument.Parse(jsonMessage))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty("type", out JsonElement typeElement) && typeElement.GetString() == "text")
                    {
                        if (root.TryGetProperty("content", out JsonElement contentElement))
                        {
                            string markdown = contentElement.GetString() ?? "";
                            UpdatePreview(markdown);
                        }
                    }
                }
            }
            catch
            {
                // Verify if it's a simple string or handle parsing errors
            }
        }

        private async void UpdatePreview(string markdown)
        {
            try 
            {
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string html = Markdown.ToHtml(markdown, pipeline);

                // JSON Serialize automatically escapes the string and adds quotes
                string jsonHtml = JsonSerializer.Serialize(html);
                
                // document.getElementById('content').innerHTML = "escaped_html";
                string script = $"document.getElementById('content').innerHTML = {jsonHtml};";
                await PreviewWebView.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                // Silently fail or log in debug
                System.Diagnostics.Debug.WriteLine($"Preview update error: {ex.Message}");
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}