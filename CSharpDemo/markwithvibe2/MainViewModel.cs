using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Markdig;
using Microsoft.Win32;

namespace MarkdownEditor;

public enum ViewMode
{
    Split,
    EditorOnly,
    PreviewOnly
}

public class MainViewModel : INotifyPropertyChanged
{
    private string _markdownContent = string.Empty;
    private string _fileName = "未命名";
    private string? _filePath;
    private bool _isModified;
    private string _statusMessage = "就绪";
    private int _cursorPosition = 1;
    private int _wordCount;
    private bool _isDarkTheme = true;
    private ViewMode _viewMode = ViewMode.Split;
    private readonly MarkdownPipeline _pipeline;

    // 视图切换事件
    public event Action<ViewMode>? ViewModeChanged;
    
    // 插入文本事件
    public event Action<string, string>? InsertTextRequested; // (prefix, suffix)

    public MainViewModel()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseAutoLinks()
            .Build();

        // 初始化示例内容
        MarkdownContent = GetSampleContent();
        IsModified = false;

        // 初始化命令
        NewCommand = new RelayCommand(NewFile);
        OpenCommand = new RelayCommand(OpenFile);
        SaveCommand = new RelayCommand(SaveFile);
        SaveAsCommand = new RelayCommand(SaveAsFile);
        ExportHtmlCommand = new RelayCommand(ExportHtml);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        
        ViewEditorOnlyCommand = new RelayCommand(ViewEditorOnly);
        ViewPreviewOnlyCommand = new RelayCommand(ViewPreviewOnly);
        ViewSplitCommand = new RelayCommand(ViewSplit);
        
        MarkdownHelpCommand = new RelayCommand(ShowMarkdownHelp);
        AboutCommand = new RelayCommand(ShowAbout);

        InsertBoldCommand = new RelayCommand(InsertBold);
        InsertItalicCommand = new RelayCommand(InsertItalic);
        InsertStrikethroughCommand = new RelayCommand(InsertStrikethrough);
        InsertHeading1Command = new RelayCommand(() => InsertHeading(1));
        InsertHeading2Command = new RelayCommand(() => InsertHeading(2));
        InsertHeading3Command = new RelayCommand(() => InsertHeading(3));
        InsertLinkCommand = new RelayCommand(InsertLink);
        InsertImageCommand = new RelayCommand(InsertImage);
        InsertCodeCommand = new RelayCommand(InsertCode);
        InsertQuoteCommand = new RelayCommand(InsertQuote);
        InsertListCommand = new RelayCommand(InsertList);
        InsertTableCommand = new RelayCommand(InsertTable);

        // 主题命令
        SwitchToDarkThemeCommand = new RelayCommand(SwitchToDarkTheme);
        SwitchToLightThemeCommand = new RelayCommand(SwitchToLightTheme);
    }

    #region Properties

    public string MarkdownContent
    {
        get => _markdownContent;
        set
        {
            if (_markdownContent != value)
            {
                _markdownContent = value;
                IsModified = true;
                OnPropertyChanged();
            }
        }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int CursorPosition
    {
        get => _cursorPosition;
        set { _cursorPosition = value; OnPropertyChanged(); }
    }

    public int WordCount
    {
        get => _wordCount;
        set { _wordCount = value; OnPropertyChanged(); }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set { _isDarkTheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLightTheme)); }
    }

    public bool IsLightTheme => !_isDarkTheme;

    public ViewMode CurrentViewMode
    {
        get => _viewMode;
        set 
        { 
            if (_viewMode != value)
            {
                _viewMode = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditorOnlyView));
                OnPropertyChanged(nameof(IsPreviewOnlyView));
                OnPropertyChanged(nameof(IsSplitView));
                ViewModeChanged?.Invoke(value);
            }
        }
    }

    public bool IsSplitView => _viewMode == ViewMode.Split;
    public bool IsEditorOnlyView => _viewMode == ViewMode.EditorOnly;
    public bool IsPreviewOnlyView => _viewMode == ViewMode.PreviewOnly;

    #endregion

    #region Commands

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExportHtmlCommand { get; }
    public ICommand ExitCommand { get; }
    
    public ICommand ViewEditorOnlyCommand { get; }
    public ICommand ViewPreviewOnlyCommand { get; }
    public ICommand ViewSplitCommand { get; }
    
    public ICommand MarkdownHelpCommand { get; }
    public ICommand AboutCommand { get; }

    public ICommand InsertBoldCommand { get; }
    public ICommand InsertItalicCommand { get; }
    public ICommand InsertStrikethroughCommand { get; }
    public ICommand InsertHeading1Command { get; }
    public ICommand InsertHeading2Command { get; }
    public ICommand InsertHeading3Command { get; }
    public ICommand InsertLinkCommand { get; }
    public ICommand InsertImageCommand { get; }
    public ICommand InsertCodeCommand { get; }
    public ICommand InsertQuoteCommand { get; }
    public ICommand InsertListCommand { get; }
    public ICommand InsertTableCommand { get; }

    public ICommand SwitchToDarkThemeCommand { get; }
    public ICommand SwitchToLightThemeCommand { get; }

    #endregion

    #region Methods

    private void SwitchToDarkTheme()
    {
        App.SwitchTheme(true);
        IsDarkTheme = true;
        StatusMessage = "已切换到深色主题";
    }

    private void SwitchToLightTheme()
    {
        App.SwitchTheme(false);
        IsDarkTheme = false;
        StatusMessage = "已切换到浅色主题";
    }

    public string GetHtmlContent()
    {
        var htmlBody = Markdig.Markdown.ToHtml(_markdownContent ?? string.Empty, _pipeline);
        return WrapHtml(htmlBody, _isDarkTheme);
    }

    public void UpdateWordCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            WordCount = 0;
            return;
        }

        // 统计中文字符和英文单词
        var chineseCount = text.Count(c => c >= 0x4e00 && c <= 0x9fff);
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                       .Count(w => w.Any(c => char.IsLetter(c) && c < 0x4e00));
        WordCount = chineseCount + words;
    }

    private void NewFile()
    {
        if (IsModified && !ConfirmDiscard()) return;
        
        MarkdownContent = string.Empty;
        _filePath = null;
        FileName = "未命名";
        IsModified = false;
        StatusMessage = "新建文件";
    }

    private void OpenFile()
    {
        if (IsModified && !ConfirmDiscard()) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Markdown文件|*.md;*.markdown|所有文件|*.*",
            Title = "打开Markdown文件"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                MarkdownContent = System.IO.File.ReadAllText(dialog.FileName);
                _filePath = dialog.FileName;
                FileName = System.IO.Path.GetFileName(dialog.FileName);
                IsModified = false;
                StatusMessage = $"已打开: {FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败: {ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile()
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            SaveAsFile();
            return;
        }

        try
        {
            System.IO.File.WriteAllText(_filePath, MarkdownContent);
            IsModified = false;
            StatusMessage = $"已保存: {FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存文件失败: {ex.Message}", "错误", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAsFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown文件|*.md|所有文件|*.*",
            Title = "保存Markdown文件",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            _filePath = dialog.FileName;
            FileName = System.IO.Path.GetFileName(dialog.FileName);
            SaveFile();
        }
    }

    private void ExportHtml()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "HTML文件|*.html|所有文件|*.*",
            Title = "导出HTML",
            DefaultExt = ".html"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var html = GetHtmlContent();
                System.IO.File.WriteAllText(dialog.FileName, html);
                StatusMessage = "HTML导出成功";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private bool ConfirmDiscard()
    {
        var result = MessageBox.Show("当前文件已修改，是否放弃更改？", "确认", 
                                    MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    private void ViewEditorOnly()
    {
        CurrentViewMode = ViewMode.EditorOnly;
        StatusMessage = "仅编辑器视图";
    }

    private void ViewPreviewOnly()
    {
        CurrentViewMode = ViewMode.PreviewOnly;
        StatusMessage = "仅预览视图";
    }

    private void ViewSplit()
    {
        CurrentViewMode = ViewMode.Split;
        StatusMessage = "分栏视图";
    }

    private void ShowMarkdownHelp()
    {
        MarkdownContent = GetMarkdownHelpContent();
    }

    private void ShowAbout()
    {
        MessageBox.Show("Markdown Editor v1.0\n\n使用 .NET 8 + WPF + WebView2 构建\n\n" +
                       "功能特性:\n• 实时预览\n• 语法高亮\n• 导出HTML\n• 暗色主题", 
                       "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void InsertBold()
    {
        InsertTextRequested?.Invoke("**", "**");
        StatusMessage = "已插入粗体";
    }
    
    private void InsertItalic()
    {
        InsertTextRequested?.Invoke("*", "*");
        StatusMessage = "已插入斜体";
    }
    
    private void InsertStrikethrough()
    {
        InsertTextRequested?.Invoke("~~", "~~");
        StatusMessage = "已插入删除线";
    }
    
    private void InsertHeading(int level)
    {
        var prefix = new string('#', level) + " ";
        InsertTextRequested?.Invoke(prefix, "");
        StatusMessage = $"已插入{level}级标题";
    }
    
    private void InsertLink()
    {
        InsertTextRequested?.Invoke("[", "](url)");
        StatusMessage = "已插入链接";
    }
    
    private void InsertImage()
    {
        InsertTextRequested?.Invoke("![", "](image-url)");
        StatusMessage = "已插入图片";
    }
    
    private void InsertCode()
    {
        InsertTextRequested?.Invoke("```\n", "\n```");
        StatusMessage = "已插入代码块";
    }
    
    private void InsertQuote()
    {
        InsertTextRequested?.Invoke("> ", "");
        StatusMessage = "已插入引用";
    }
    
    private void InsertList()
    {
        InsertTextRequested?.Invoke("- ", "");
        StatusMessage = "已插入列表";
    }
    
    private void InsertTable()
    {
        var table = "| 列 1 | 列 2 | 列 3 |\n|------|------|------|\n| 内容 | 内容 | 内容 |";
        InsertTextRequested?.Invoke(table, "");
        StatusMessage = "已插入表格";
    }

    private static string GetSampleContent()
    {
        return """
            # 欢迎使用 Markdown 编辑器 ✨

            这是一个使用 **C# .NET 8** 和 **WPF** 构建的现代 Markdown 编辑器。

            ## 功能特性

            - 📝 实时预览
            - 🎨 精美的暗色主题
            - 💾 文件保存和打开
            - 🌐 导出为 HTML
            - ⌨️ 快捷键支持

            ## 代码示例

            ```csharp
            public class HelloWorld
            {
                public static void Main()
                {
                    Console.WriteLine("Hello, Markdown!");
                }
            }
            ```

            ## 表格示例

            | 功能 | 快捷键 | 描述 |
            |------|--------|------|
            | 新建 | Ctrl+N | 新建文件 |
            | 打开 | Ctrl+O | 打开文件 |
            | 保存 | Ctrl+S | 保存文件 |

            ## 引用

            > Markdown 是一种轻量级标记语言，它允许人们使用易读易写的纯文本格式编写文档。

            ## 链接

            访问 [GitHub](https://github.com) 了解更多开源项目。

            ---

            *开始编辑左侧内容，右侧将实时预览！*
            """;
    }

    private static string GetMarkdownHelpContent()
    {
        return """
            # Markdown 语法指南

            ## 标题

            ```
            # 一级标题
            ## 二级标题
            ### 三级标题
            ```

            ## 文本样式

            - **粗体**: `**文本**` 或 `__文本__`
            - *斜体*: `*文本*` 或 `_文本_`
            - ~~删除线~~: `~~文本~~`
            - `行内代码`: `` `代码` ``

            ## 列表

            无序列表：
            ```
            - 项目1
            - 项目2
            - 项目3
            ```

            有序列表：
            ```
            1. 第一项
            2. 第二项
            3. 第三项
            ```

            ## 链接和图片

            链接: `[文本](URL)`
            图片: `![描述](图片URL)`

            ## 代码块

            使用三个反引号包围代码：
            ~~~
            ```语言
            代码内容
            ```
            ~~~

            ## 表格

            ```
            | 列1 | 列2 | 列3 |
            |-----|-----|-----|
            | A   | B   | C   |
            ```

            ## 引用

            ```
            > 这是一段引用文本
            ```

            ## 水平线

            ```
            ---
            ```
            """;
    }

    private static string WrapHtml(string body, bool isDarkTheme)
    {
        var themeVars = isDarkTheme ? """
                    :root {
                        --bg-color: #1e1e1e;
                        --text-color: #d4d4d4;
                        --heading-color: #569cd6;
                        --link-color: #4ec9b0;
                        --code-bg: #2d2d30;
                        --border-color: #404040;
                        --blockquote-border: #569cd6;
                        --blockquote-bg: #252526;
                        --scrollbar-thumb: #555;
                        --scrollbar-hover: #666;
                    }
            """ : """
                    :root {
                        --bg-color: #ffffff;
                        --text-color: #1e1e1e;
                        --heading-color: #0066b8;
                        --link-color: #0066b8;
                        --code-bg: #f5f5f5;
                        --border-color: #e0e0e0;
                        --blockquote-border: #0066b8;
                        --blockquote-bg: #f8f8f8;
                        --scrollbar-thumb: #c0c0c0;
                        --scrollbar-hover: #a0a0a0;
                    }
            """;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <style>
                    {{themeVars}}
                    
                    * {
                        box-sizing: border-box;
                    }
                    
                    body {
                        font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
                        font-size: 15px;
                        line-height: 1.7;
                        color: var(--text-color);
                        background-color: var(--bg-color);
                        padding: 24px 32px;
                        margin: 0;
                        max-width: 100%;
                        overflow-x: hidden;
                    }
                    
                    h1, h2, h3, h4, h5, h6 {
                        color: var(--heading-color);
                        margin-top: 24px;
                        margin-bottom: 16px;
                        font-weight: 600;
                        line-height: 1.25;
                    }
                    
                    h1 {
                        font-size: 2em;
                        padding-bottom: 0.3em;
                        border-bottom: 1px solid var(--border-color);
                    }
                    
                    h2 {
                        font-size: 1.5em;
                        padding-bottom: 0.3em;
                        border-bottom: 1px solid var(--border-color);
                    }
                    
                    h3 { font-size: 1.25em; }
                    h4 { font-size: 1em; }
                    
                    a {
                        color: var(--link-color);
                        text-decoration: none;
                    }
                    
                    a:hover {
                        text-decoration: underline;
                    }
                    
                    p {
                        margin: 0 0 16px 0;
                    }
                    
                    code {
                        font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
                        font-size: 0.9em;
                        background-color: var(--code-bg);
                        padding: 0.2em 0.4em;
                        border-radius: 4px;
                    }
                    
                    pre {
                        background-color: var(--code-bg);
                        padding: 16px;
                        border-radius: 8px;
                        overflow-x: auto;
                        margin: 16px 0;
                        border: 1px solid var(--border-color);
                    }
                    
                    pre code {
                        background: none;
                        padding: 0;
                        font-size: 0.9em;
                        line-height: 1.5;
                    }
                    
                    blockquote {
                        margin: 16px 0;
                        padding: 12px 20px;
                        border-left: 4px solid var(--blockquote-border);
                        background-color: var(--blockquote-bg);
                        border-radius: 0 8px 8px 0;
                    }
                    
                    blockquote p {
                        margin: 0;
                    }
                    
                    ul, ol {
                        padding-left: 24px;
                        margin: 16px 0;
                    }
                    
                    li {
                        margin: 4px 0;
                    }
                    
                    table {
                        border-collapse: collapse;
                        width: 100%;
                        margin: 16px 0;
                    }
                    
                    th, td {
                        border: 1px solid var(--border-color);
                        padding: 10px 14px;
                        text-align: left;
                    }
                    
                    th {
                        background-color: var(--code-bg);
                        font-weight: 600;
                    }
                    
                    tr:nth-child(even) {
                        background-color: var(--blockquote-bg);
                    }
                    
                    hr {
                        border: none;
                        height: 1px;
                        background: var(--border-color);
                        margin: 24px 0;
                    }
                    
                    img {
                        max-width: 100%;
                        height: auto;
                        border-radius: 8px;
                    }
                    
                    /* Scrollbar styling */
                    ::-webkit-scrollbar {
                        width: 10px;
                        height: 10px;
                    }
                    
                    ::-webkit-scrollbar-track {
                        background: var(--bg-color);
                    }
                    
                    ::-webkit-scrollbar-thumb {
                        background: var(--scrollbar-thumb);
                        border-radius: 5px;
                    }
                    
                    ::-webkit-scrollbar-thumb:hover {
                        background: var(--scrollbar-hover);
                    }
                </style>
            </head>
            <body>
                {{body}}
            </body>
            </html>
            """;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
