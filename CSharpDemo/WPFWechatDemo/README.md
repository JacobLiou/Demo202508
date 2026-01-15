# WPF 微信PC版 Demo

这是一个使用 C# WPF 和 .NET 8 开发的仿微信PC客户端程序，实现了基本的UI界面和消息收发功能。

## 功能特性

- ✅ 仿微信PC版UI界面设计
- ✅ 左侧会话列表展示
- ✅ 右侧聊天窗口
- ✅ 发送消息功能
- ✅ 模拟接收消息功能（自动回复）
- ✅ 消息气泡样式（发送/接收不同颜色）
- ✅ 消息自动滚动到底部

## 技术栈

- **.NET 8** - 目标框架
- **WPF** - Windows Presentation Foundation
- **MVVM模式** - Model-View-ViewModel架构
- **数据绑定** - XAML数据绑定

## 项目结构

```
WPFWechatDemo/
├── Models/              # 数据模型
│   ├── Message.cs      # 消息模型
│   └── Contact.cs      # 联系人模型
├── ViewModels/         # 视图模型
│   ├── MainViewModel.cs      # 主视图模型
│   ├── ViewModelBase.cs      # 视图模型基类
│   └── RelayCommand.cs       # 命令实现
├── Converters/         # 值转换器
│   ├── BoolToAlignmentConverter.cs
│   ├── BoolToBrushConverter.cs
│   ├── BoolToVisibilityConverter.cs
│   ├── InverseBoolToVisibilityConverter.cs
│   └── IntToVisibilityConverter.cs
├── Styles/             # 样式资源
│   └── WechatStyles.xaml
├── App.xaml            # 应用程序入口
├── MainWindow.xaml     # 主窗口
└── WPFWechatDemo.csproj
```

## 使用方法

### 1. 编译运行

```bash
dotnet build
dotnet run
```

或者在 Visual Studio 中直接按 F5 运行。

### 2. 发送消息

1. 在左侧会话列表中选择一个联系人
2. 在底部输入框中输入消息内容
3. 点击"发送"按钮或按 `Ctrl+Enter` 发送消息
4. 系统会自动模拟对方回复（延迟1-3秒）

### 3. 切换会话

点击左侧会话列表中的任意联系人，右侧聊天窗口会显示对应的聊天记录。

## UI说明

- **左侧面板**：显示会话列表，包括联系人头像、名称、最后一条消息和时间
- **右侧面板**：
  - **顶部**：显示当前选中联系人的信息和操作按钮
  - **中间**：消息列表，发送的消息显示在右侧（绿色气泡），接收的消息显示在左侧（白色气泡）
  - **底部**：消息输入框和发送按钮

## 扩展功能建议

- [ ] 添加表情选择器
- [ ] 添加文件发送功能
- [ ] 添加图片发送和预览
- [ ] 添加消息搜索功能
- [ ] 添加消息时间戳显示
- [ ] 添加未读消息计数
- [ ] 添加消息状态（已发送/已读）
- [ ] 添加语音消息功能
- [ ] 添加群聊功能
- [ ] 添加数据库持久化

## 注意事项

- 这是一个演示项目，所有数据都是模拟的，不会真正发送或接收消息
- 消息数据在程序关闭后会丢失（未实现持久化）
- UI样式参考了微信PC版的设计，但进行了简化

## 许可证

本项目仅供学习和演示使用。

