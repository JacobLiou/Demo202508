"""
日志工具
配置和管理日志系统
"""
import logging
import sys
from pathlib import Path
from datetime import datetime
from typing import Optional
import threading


# 日志格式
DEFAULT_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
DETAILED_FORMAT = '%(asctime)s - %(name)s - %(levelname)s - [%(filename)s:%(lineno)d] - %(message)s'

# 全局日志锁
_log_lock = threading.Lock()
_initialized = False


def setup_logger(
    name: str = 'OpticalTest',
    level: int = logging.INFO,
    log_dir: str = None,
    console: bool = True,
    file: bool = True,
    detailed: bool = False
) -> logging.Logger:
    """
    设置日志系统
    
    Args:
        name: 日志名称
        level: 日志级别
        log_dir: 日志目录
        console: 是否输出到控制台
        file: 是否输出到文件
        detailed: 是否使用详细格式
        
    Returns:
        logging.Logger: 配置好的日志器
    """
    global _initialized
    
    with _log_lock:
        # 获取或创建日志器
        logger = logging.getLogger(name)
        
        # 如果已经配置过，直接返回
        if _initialized and logger.handlers:
            return logger
        
        logger.setLevel(level)
        logger.handlers.clear()
        
        # 选择格式
        log_format = DETAILED_FORMAT if detailed else DEFAULT_FORMAT
        formatter = logging.Formatter(log_format)
        
        # 控制台处理器
        if console:
            console_handler = logging.StreamHandler(sys.stdout)
            console_handler.setLevel(level)
            console_handler.setFormatter(formatter)
            logger.addHandler(console_handler)
        
        # 文件处理器
        if file:
            if log_dir is None:
                log_dir = Path(__file__).parent.parent / 'logs'
            else:
                log_dir = Path(log_dir)
            
            log_dir.mkdir(exist_ok=True)
            
            # 按日期创建日志文件
            log_filename = f"{name}_{datetime.now().strftime('%Y%m%d')}.log"
            log_path = log_dir / log_filename
            
            file_handler = logging.FileHandler(log_path, encoding='utf-8')
            file_handler.setLevel(level)
            file_handler.setFormatter(formatter)
            logger.addHandler(file_handler)
        
        _initialized = True
        return logger


def get_logger(name: str = None) -> logging.Logger:
    """
    获取日志器
    
    Args:
        name: 日志器名称，如果为None则返回根日志器
        
    Returns:
        logging.Logger: 日志器实例
    """
    if name is None:
        return logging.getLogger('OpticalTest')
    
    # 使用层级命名
    full_name = f'OpticalTest.{name}'
    return logging.getLogger(full_name)


class TestLogger:
    """测试日志封装类"""
    
    def __init__(self, test_name: str, log_dir: str = None):
        """
        初始化测试日志
        
        Args:
            test_name: 测试名称
            log_dir: 日志目录
        """
        self.test_name = test_name
        self.logger = get_logger(test_name)
        
        # 可选：为每个测试创建独立日志文件
        if log_dir:
            self._add_test_file_handler(log_dir)
    
    def _add_test_file_handler(self, log_dir: str):
        """添加测试专用文件处理器"""
        log_dir = Path(log_dir)
        log_dir.mkdir(exist_ok=True)
        
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        log_path = log_dir / f"{self.test_name}_{timestamp}.log"
        
        handler = logging.FileHandler(log_path, encoding='utf-8')
        handler.setLevel(logging.DEBUG)
        handler.setFormatter(logging.Formatter(DETAILED_FORMAT))
        
        self.logger.addHandler(handler)
        self._test_handler = handler
        self.log_path = log_path
    
    def info(self, message: str):
        """记录信息级别日志"""
        self.logger.info(f"[{self.test_name}] {message}")
    
    def debug(self, message: str):
        """记录调试级别日志"""
        self.logger.debug(f"[{self.test_name}] {message}")
    
    def warning(self, message: str):
        """记录警告级别日志"""
        self.logger.warning(f"[{self.test_name}] {message}")
    
    def error(self, message: str):
        """记录错误级别日志"""
        self.logger.error(f"[{self.test_name}] {message}")
    
    def step(self, step_num: int, description: str):
        """记录测试步骤"""
        self.logger.info(f"[{self.test_name}] Step {step_num}: {description}")
    
    def measurement(self, name: str, value, unit: str = ""):
        """记录测量结果"""
        self.logger.info(f"[{self.test_name}] Measurement: {name} = {value} {unit}")
    
    def result(self, passed: bool, message: str = ""):
        """记录测试结果"""
        status = "PASS" if passed else "FAIL"
        self.logger.info(f"[{self.test_name}] Result: {status} - {message}")
    
    def close(self):
        """关闭日志处理器"""
        if hasattr(self, '_test_handler'):
            self._test_handler.close()
            self.logger.removeHandler(self._test_handler)
