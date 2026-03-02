using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Markdig;

namespace MarkWithVibe;

public partial class MainWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _previewDebounce;
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MainWindow()
    {
        InitializeComponent();
        _previewDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _previewDebounce.Tick += (_, _) =>
        {
            _previewDebounce.Stop();
            UpdatePreview();
        };
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PreviewWebView.EnsureCoreWebView2Async(null);
            PreviewWebView.CoreWebView2.Settings.IsScriptEnabled = true;
            // Initial content
            EditorBox.Text = GetSampleMarkdown();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 运行时未安装或初始化失败。请安装 Microsoft Edge WebView2 运行时。\n\n" + ex.Message,
                "Mark With Vibe", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private void UpdatePreview()
    {
        if (PreviewWebView.CoreWebView2 == null) return;

        var markdown = EditorBox?.Text ?? string.Empty;
        var html = Markdown.ToHtml(markdown, MarkdownPipeline);
        var fullHtml = BuildPreviewHtml(html);
        PreviewWebView.NavigateToString(fullHtml);
    }

    private static string BuildPreviewHtml(string bodyHtml)
    {
        const string css = """
            * { box-sizing: border-box; }
            body {
                font-family: var(--vscode-font-family, "Segoe UI", "SF Pro Display", sans-serif);
                font-size: 14px;
                line-height: 1.6;
                color: #d4d4d4;
                background: #1e1e1e;
                margin: 0;
                padding: 16px 20px;
                max-width: 800px;
            }
            h1, h2, h3, h4, h5, h6 { font-weight: 600; margin-top: 1.2em; margin-bottom: 0.5em; color: #fff; }
            h1 { font-size: 1.8em; border-bottom: 1px solid #3c3c3c; padding-bottom: 0.3em; }
            h2 { font-size: 1.4em; border-bottom: 1px solid #3c3c3c; padding-bottom: 0.2em; }
            h3 { font-size: 1.2em; }
            p { margin: 0.5em 0; }
            code { background: #2d2d2d; color: #d4d4d4; padding: 0.2em 0.4em; border-radius: 4px; font-family: "Cascadia Code", Consolas, monospace; font-size: 0.9em; }
            pre { background: #2d2d2d; padding: 12px 16px; border-radius: 6px; overflow-x: auto; margin: 0.8em 0; }
            pre code { background: none; padding: 0; }
            blockquote { border-left: 4px solid #0078d4; margin: 0.5em 0; padding-left: 16px; color: #9e9e9e; }
            a { color: #3794ff; text-decoration: none; }
            a:hover { text-decoration: underline; }
            ul, ol { margin: 0.5em 0; padding-left: 1.5em; }
            table { border-collapse: collapse; width: 100%; margin: 0.8em 0; }
            th, td { border: 1px solid #3c3c3c; padding: 6px 10px; text-align: left; }
            th { background: #2d2d2d; font-weight: 600; }
            hr { border: none; border-top: 1px solid #3c3c3c; margin: 1.2em 0; }
            img { max-width: 100%; height: auto; }
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
                <meta charset="utf-8"/>
                <meta name="viewport" content="width=device-width, initial-scale=1"/>
                <style>{css}</style>
            </head>
            <body>
            {bodyHtml}
            </body>
            </html>
            """;
    }

    private static string GetSampleMarkdown()
    {
        return """
            # 欢迎使用 Mark With Vibe

            左侧编辑 **Markdown**，右侧实时预览，类似 VS Code 的体验。

            ## 示例

            - 列表项一
            - 列表项二
            - 列表项三

            行内 `code` 与代码块：

            ```csharp
            public static void Main()
            {
                Console.WriteLine("Hello, Markdown!");
            }
            ```

            > 这是一段引用文字。

            [链接示例](https://github.com) | **粗体** | *斜体*

            ---

            继续编写，预览会即时更新。
            """;
    }
}
