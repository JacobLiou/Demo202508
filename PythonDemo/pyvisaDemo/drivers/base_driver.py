"""
仪器驱动基类
提供所有仪器驱动的通用接口和基础功能
"""
import pyvisa
from abc import ABC, abstractmethod
from typing import Optional, Any, Dict
import logging
import time


class BaseDriver(ABC):
    """仪器驱动基类"""
    
    def __init__(self, resource_string: str, timeout: int = 5000, **kwargs):
        """
        初始化驱动
        
        Args:
            resource_string: VISA资源字符串
            timeout: 超时时间(毫秒)
            **kwargs: 其他参数
        """
        self.resource_string = resource_string
        self.timeout = timeout
        self.instrument: Optional[pyvisa.Resource] = None
        self.rm: Optional[pyvisa.ResourceManager] = None
        self.logger = logging.getLogger(self.__class__.__name__)
        self.connected = False
        self.parameters = kwargs.get('parameters', {})
        self._backend = kwargs.get('backend', '')
        
    def connect(self) -> bool:
        """
        连接仪器
        
        Returns:
            bool: 连接是否成功
        """
        try:
            self.rm = pyvisa.ResourceManager(self._backend)
            self.instrument = self.rm.open_resource(self.resource_string)
            self.instrument.timeout = self.timeout
            self.connected = True
            self.logger.info(f"成功连接到仪器: {self.resource_string}")
            
            # 执行初始化命令
            self._initialize()
            return True
            
        except pyvisa.VisaIOError as e:
            self.logger.error(f"连接仪器失败: {e}")
            self.connected = False
            return False
    
    def disconnect(self):
        """断开仪器连接"""
        try:
            if self.instrument:
                self.instrument.close()
            if self.rm:
                self.rm.close()
            self.connected = False
            self.logger.info(f"已断开仪器连接: {self.resource_string}")
        except Exception as e:
            self.logger.error(f"断开连接时出错: {e}")
    
    def write(self, command: str):
        """
        发送命令到仪器
        
        Args:
            command: SCPI命令
        """
        if not self.connected:
            raise RuntimeError("仪器未连接")
        self.logger.debug(f"发送命令: {command}")
        self.instrument.write(command)
    
    def read(self) -> str:
        """
        从仪器读取响应
        
        Returns:
            str: 仪器响应
        """
        if not self.connected:
            raise RuntimeError("仪器未连接")
        response = self.instrument.read()
        self.logger.debug(f"接收响应: {response}")
        return response
    
    def query(self, command: str) -> str:
        """
        发送命令并读取响应
        
        Args:
            command: SCPI命令
            
        Returns:
            str: 仪器响应
        """
        if not self.connected:
            raise RuntimeError("仪器未连接")
        self.logger.debug(f"查询命令: {command}")
        response = self.instrument.query(command)
        self.logger.debug(f"查询响应: {response}")
        return response.strip()
    
    def query_binary(self, command: str) -> bytes:
        """
        发送命令并读取二进制响应
        
        Args:
            command: SCPI命令
            
        Returns:
            bytes: 二进制数据
        """
        if not self.connected:
            raise RuntimeError("仪器未连接")
        return self.instrument.query_binary_values(command, datatype='f', container=list)
    
    def get_idn(self) -> str:
        """
        获取仪器标识信息
        
        Returns:
            str: 仪器IDN响应
        """
        return self.query("*IDN?")
    
    def reset(self):
        """重置仪器"""
        self.write("*RST")
        time.sleep(1)
    
    def clear_status(self):
        """清除状态寄存器"""
        self.write("*CLS")
    
    def wait_operation_complete(self, timeout: float = 30):
        """
        等待操作完成
        
        Args:
            timeout: 超时时间(秒)
        """
        self.write("*OPC")
        start_time = time.time()
        while time.time() - start_time < timeout:
            try:
                result = self.query("*OPC?")
                if result == "1":
                    return
            except:
                pass
            time.sleep(0.1)
        raise TimeoutError("操作超时")
    
    def check_error(self) -> tuple:
        """
        检查仪器错误
        
        Returns:
            tuple: (错误代码, 错误信息)
        """
        response = self.query("SYST:ERR?")
        parts = response.split(',', 1)
        code = int(parts[0])
        message = parts[1].strip('"') if len(parts) > 1 else ""
        return code, message
    
    @abstractmethod
    def _initialize(self):
        """初始化仪器配置（子类实现）"""
        pass
    
    @abstractmethod
    def self_test(self) -> bool:
        """
        仪器自检
        
        Returns:
            bool: 自检是否通过
        """
        pass
    
    def __enter__(self):
        """上下文管理器入口"""
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器出口"""
        self.disconnect()
        return False
    
    @property
    def is_connected(self) -> bool:
        """检查连接状态"""
        return self.connected


class SimulatedDriver(BaseDriver):
    """仿真驱动基类，用于无硬件环境的测试"""
    
    def __init__(self, resource_string: str, **kwargs):
        super().__init__(resource_string, **kwargs)
        self.simulated = True
        self._idn = "Simulated Instrument, Model 0000, SN 12345"
        
    def connect(self) -> bool:
        self.connected = True
        self.logger.info(f"仿真模式: 已连接到 {self.resource_string}")
        self._initialize()
        return True
    
    def disconnect(self):
        self.connected = False
        self.logger.info(f"仿真模式: 已断开 {self.resource_string}")
    
    def write(self, command: str):
        self.logger.debug(f"仿真写入: {command}")
    
    def read(self) -> str:
        return "SIMULATED_RESPONSE"
    
    def query(self, command: str) -> str:
        self.logger.debug(f"仿真查询: {command}")
        if command == "*IDN?":
            return self._idn
        return "SIMULATED_RESPONSE"
    
    def get_idn(self) -> str:
        return self._idn
