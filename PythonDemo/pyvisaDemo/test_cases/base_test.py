"""
测试基类
所有测试用例的基础类
"""
import logging
from abc import ABC, abstractmethod
from typing import Dict, Any, Optional
from datetime import datetime
import numpy as np


class BaseTest(ABC):
    """测试基类"""
    
    def __init__(self, config: Dict, instruments: Dict, result):
        """
        初始化测试
        
        Args:
            config: 测试流程配置
            instruments: 仪器实例字典
            result: TestResult对象
        """
        self.config = config
        self.instruments = instruments
        self.result = result
        self.parameters = config.get('parameters', {})
        self.pass_criteria = config.get('pass_criteria', {})
        self.logger = logging.getLogger(self.__class__.__name__)
        
        # 测试数据存储
        self.measurements: Dict[str, Any] = {}
        self.reference_values: Dict[str, float] = {}
    
    def initialize_instruments(self) -> bool:
        """
        初始化所有仪器
        
        Returns:
            bool: 初始化是否成功
        """
        self.logger.info("初始化仪器...")
        for name, instrument in self.instruments.items():
            if instrument is None:
                self.logger.error(f"仪器未连接: {name}")
                return False
            
            # 检查仪器连接
            if not instrument.is_connected:
                self.logger.error(f"仪器已断开: {name}")
                return False
            
            self.logger.info(f"仪器 {name} 就绪")
        
        return True
    
    def get_instrument(self, instrument_id: str):
        """获取仪器实例"""
        return self.instruments.get(instrument_id)
    
    def add_measurement(self, name: str, value: Any, unit: str = ""):
        """
        添加测量结果
        
        Args:
            name: 测量项名称
            value: 测量值
            unit: 单位
        """
        self.measurements[name] = {
            'value': value,
            'unit': unit,
            'timestamp': datetime.now().isoformat()
        }
        self.result.measurements[name] = value
        self.logger.info(f"测量: {name} = {value} {unit}")
    
    def check_limit(self, name: str, value: float, 
                   min_val: float = None, max_val: float = None) -> bool:
        """
        检查测量值是否在限值范围内
        
        Args:
            name: 测量项名称
            value: 测量值
            min_val: 最小限值
            max_val: 最大限值
            
        Returns:
            bool: 是否在限值范围内
        """
        passed = True
        
        if min_val is not None and value < min_val:
            self.logger.warning(f"{name}: {value} 低于最小限值 {min_val}")
            passed = False
        
        if max_val is not None and value > max_val:
            self.logger.warning(f"{name}: {value} 超过最大限值 {max_val}")
            passed = False
        
        if passed:
            self.logger.info(f"{name}: {value} 在限值范围内")
        
        return passed
    
    def calculate_statistics(self, values: list) -> Dict:
        """
        计算统计数据
        
        Args:
            values: 数值列表
            
        Returns:
            Dict: 统计结果
        """
        arr = np.array(values)
        return {
            'mean': float(np.mean(arr)),
            'std': float(np.std(arr)),
            'min': float(np.min(arr)),
            'max': float(np.max(arr)),
            'range': float(np.ptp(arr)),
            'count': len(values)
        }
    
    def wait_for_stability(self, measure_func, threshold: float = 0.01, 
                          timeout: float = 10, interval: float = 0.5) -> float:
        """
        等待测量值稳定
        
        Args:
            measure_func: 测量函数
            threshold: 稳定阈值
            timeout: 超时时间(秒)
            interval: 采样间隔(秒)
            
        Returns:
            float: 稳定后的测量值
        """
        import time
        
        start_time = time.time()
        prev_value = measure_func()
        
        while time.time() - start_time < timeout:
            time.sleep(interval)
            current_value = measure_func()
            
            if abs(current_value - prev_value) < threshold:
                return current_value
            
            prev_value = current_value
        
        self.logger.warning("等待稳定超时")
        return measure_func()
    
    @abstractmethod
    def calibrate_reference(self, **kwargs) -> float:
        """
        校准参考值（子类实现）
        
        Returns:
            float: 参考值
        """
        pass
    
    @abstractmethod
    def generate_report(self) -> Dict:
        """
        生成测试报告数据（子类实现）
        
        Returns:
            Dict: 报告数据
        """
        pass
