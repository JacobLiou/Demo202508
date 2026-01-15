# 光通信硬件测试自动化平台

基于 PyVISA 的光通信硬件自动化测试框架，支持配置文件驱动的测试流程管理和调度。

## 项目结构

```
pyvisaDemo/
├── config/                     # 配置文件目录
│   ├── instruments.yaml        # 仪器配置
│   ├── test_flows.yaml         # 测试流程配置
│   └── products.yaml           # 产品配置
├── drivers/                    # 硬件驱动层
│   ├── __init__.py
│   ├── base_driver.py          # 驱动基类
│   ├── optical_power_meter.py  # 光功率计驱动
│   ├── optical_switch.py       # 光开关驱动
│   ├── osa.py                  # 光谱分析仪驱动
│   └── laser_source.py         # 激光光源驱动
├── core/                       # 核心模块
│   ├── __init__.py
│   ├── config_manager.py       # 配置管理器
│   ├── instrument_manager.py   # 仪器管理器
│   ├── test_engine.py          # 测试引擎
│   └── scheduler.py            # 调度器
├── test_cases/                 # 测试用例
│   ├── __init__.py
│   ├── base_test.py            # 测试基类
│   ├── insertion_loss_test.py  # 插损测试
│   ├── return_loss_test.py     # 回损测试
│   └── spectrum_test.py        # 光谱测试
├── utils/                      # 工具模块
│   ├── __init__.py
│   ├── logger.py               # 日志工具
│   └── report_generator.py     # 报告生成器
├── reports/                    # 测试报告输出目录
├── logs/                       # 日志目录
├── main.py                     # 主程序入口
├── gui_app.py                  # GUI界面
└── requirements.txt            # 依赖包
```

## 快速开始

### 1. 安装依赖
```bash
pip install -r requirements.txt
```

### 2. 配置仪器
编辑 `config/instruments.yaml` 配置您的测试仪器

### 3. 配置测试流程
编辑 `config/test_flows.yaml` 定义测试流程

### 4. 运行测试
```bash
# 命令行模式
python main.py --flow optical_test_flow

# GUI模式
python gui_app.py
```

## 功能特性

- ✅ 配置文件驱动的测试流程
- ✅ 支持多种光通信测试仪器
- ✅ 灵活的测试调度机制
- ✅ 自动生成测试报告
- ✅ 完整的日志记录
- ✅ GUI操作界面

## 支持的仪器类型

- 光功率计 (Optical Power Meter)
- 光谱分析仪 (OSA)
- 光开关 (Optical Switch)
- 激光光源 (Laser Source)
- 可调谐激光器 (Tunable Laser)
