using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace MarkdownEditor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private GridLength _lastEditorWidth = new(1, GridUnitType.Star);
    private GridLength _lastPreviewWidth = new(1, GridUnitType.Star);

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        // 监听主题变化以刷新预览
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // 监听视图模式变化
        _viewModel.ViewModeChanged += OnViewModeChanged;
        
        // 监听插入文本请求
        _viewModel.InsertTextRequested += OnInsertTextRequested;
        
        InitializeWebView();
        UpdateLineNumbers();
    }

    private void OnInsertTextRequested(string prefix, string suffix)
    {
        var selectionStart = Editor.SelectionStart;
        var selectionLength = Editor.SelectionLength;
        var selectedText = Editor.SelectedText;
        
        // 构建新文本
        var newText = prefix + selectedText + suffix;
        
        // 插入文本
        Editor.Text = Editor.Text.Remove(selectionStart, selectionLength)
                                 .Insert(selectionStart, newText);
        
        // 设置光标位置
        if (selectionLength == 0)
        {
            // 如果没有选中文本，将光标放在prefix后面
            Editor.SelectionStart = selectionStart + prefix.Length;
        }
        else
        {
            // 如果有选中文本，选中插入后的文本
            Editor.SelectionStart = selectionStart + prefix.Length;
            Editor.SelectionLength = selectedText.Length;
        }
        
        Editor.Focus();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            UpdatePreview();
        }
    }

    private void OnViewModeChanged(ViewMode mode)
    {
        switch (mode)
        {
            case ViewMode.EditorOnly:
                // 保存当前宽度
                if (EditorColumn.Width.Value > 0)
                    _lastEditorWidth = EditorColumn.Width;
                if (PreviewColumn.Width.Value > 0)
                    _lastPreviewWidth = PreviewColumn.Width;
                
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorColumn.MinWidth = 100;
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                PreviewColumn.MinWidth = 0;
                
                EditorPanel.Visibility = Visibility.Visible;
                SplitterGrid.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Collapsed;
                break;
                
            case ViewMode.PreviewOnly:
                // 保存当前宽度
                if (EditorColumn.Width.Value > 0)
                    _lastEditorWidth = EditorColumn.Width;
                if (PreviewColumn.Width.Value > 0)
                    _lastPreviewWidth = PreviewColumn.Width;
                
                EditorColumn.Width = new GridLength(0);
                EditorColumn.MinWidth = 0;
                SplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewColumn.MinWidth = 100;
                
                EditorPanel.Visibility = Visibility.Collapsed;
                SplitterGrid.Visibility = Visibility.Collapsed;
                PreviewPanel.Visibility = Visibility.Visible;
                break;
                
            case ViewMode.Split:
            default:
                EditorColumn.Width = _lastEditorWidth;
                EditorColumn.MinWidth = 200;
                SplitterColumn.Width = new GridLength(5);
                PreviewColumn.Width = _lastPreviewWidth;
                PreviewColumn.MinWidth = 200;
                
                EditorPanel.Visibility = Visibility.Visible;
                SplitterGrid.Visibility = Visibility.Visible;
                PreviewPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private async void InitializeWebView()
    {
        try
        {
            await Preview.EnsureCoreWebView2Async();
            Preview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 初始化失败: {ex.Message}\n请确保已安装 WebView2 Runtime", 
                          "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
        UpdatePreview();
        _viewModel.UpdateWordCount(Editor.Text);
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var lineIndex = Editor.GetLineIndexFromCharacterIndex(Editor.CaretIndex);
        _viewModel.CursorPosition = lineIndex + 1;
    }

    private void UpdateLineNumbers()
    {
        // 计算行数（基于换行符）
        var text = Editor.Text ?? string.Empty;
        var lineCount = text.Split('\n').Length;
        if (lineCount == 0) lineCount = 1;
        
        var lines = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            lines.AppendLine(i.ToString());
        }
        LineNumbers.Text = lines.ToString().TrimEnd();
    }

    private void UpdatePreview()
    {
        if (Preview.CoreWebView2 == null) return;
        
        var html = _viewModel.GetHtmlContent();
        Preview.NavigateToString(html);
    }
}
