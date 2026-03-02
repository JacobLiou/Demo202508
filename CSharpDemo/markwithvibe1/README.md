# Mark With Vibe

基于 **C# WPF .NET 8** 的 Markdown 编辑器，左侧编辑、右侧实时预览，风格类似 VS Code。

## 功能

- **左侧编辑区**：使用等宽字体（Cascadia Code / Consolas）编辑 Markdown
- **右侧实时预览**：输入时约 200ms 防抖后自动刷新 HTML 预览
- **可调节分栏**：中间拖拽可调整左右宽度
- **VS Code 风格**：深色主题、预览样式与 VS Code 的 Markdown 预览接近

## 运行要求

- .NET 8.0 SDK
- **Microsoft Edge WebView2 运行时**（多数 Windows 10/11 已预装；若未安装，运行时会提示或可从 [Microsoft 官网](https://developer.microsoft.com/microsoft-edge/webview2/) 下载）

## 运行方式

```bash
cd markwithvibe1
dotnet run
```

或在 Visual Studio / Rider 中打开解决方案并运行。

## 技术栈

- **WPF** (.NET 8.0-windows)
- **Markdig**：Markdown 解析与 HTML 输出
- **Microsoft.Web.WebView2**：右侧 HTML 预览

## 项目结构

- `MainWindow.xaml`：主界面布局（工具栏、左侧编辑器、分割条、右侧 WebView2）
- `MainWindow.xaml.cs`：编辑区文本变更、Markdown 转 HTML、预览更新
- `App.xaml`：全局深色主题资源
