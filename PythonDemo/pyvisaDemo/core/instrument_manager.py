"""
仪器管理器
负责管理所有仪器的连接、断开和状态监控
"""
import logging
from typing import Dict, Optional, Type, Any
from concurrent.futures import ThreadPoolExecutor, as_completed
import threading

from .config_manager import ConfigManager
from drivers import (
    BaseDriver,
    OpticalPowerMeter,
    OpticalSwitch,
    OpticalSpectrumAnalyzer,
    LaserSource
)
from drivers.optical_power_meter import SimulatedOpticalPowerMeter
from drivers.optical_switch import SimulatedOpticalSwitch
from drivers.osa import SimulatedOSA
from drivers.laser_source import SimulatedLaserSource


class InstrumentManager:
    """仪器管理器"""
    
    # 驱动类型映射
    DRIVER_MAP: Dict[str, Type[BaseDriver]] = {
        'optical_power_meter': OpticalPowerMeter,
        'optical_switch': OpticalSwitch,
        'osa': OpticalSpectrumAnalyzer,
        'laser_source': LaserSource,
    }
    
    # 仿真驱动类型映射
    SIMULATED_DRIVER_MAP: Dict[str, Type] = {
        'optical_power_meter': SimulatedOpticalPowerMeter,
        'optical_switch': SimulatedOpticalSwitch,
        'osa': SimulatedOSA,
        'laser_source': SimulatedLaserSource,
    }
    
    def __init__(self, config_manager: ConfigManager, simulation_mode: bool = False):
        """
        初始化仪器管理器
        
        Args:
            config_manager: 配置管理器实例
            simulation_mode: 是否使用仿真模式
        """
        self.config_manager = config_manager
        self.simulation_mode = simulation_mode
        self.logger = logging.getLogger(self.__class__.__name__)
        
        # 仪器实例缓存
        self._instruments: Dict[str, BaseDriver] = {}
        self._lock = threading.Lock()
    
    def _create_driver_instance(self, instrument_id: str, 
                                config: Dict) -> Optional[BaseDriver]:
        """
        创建驱动实例
        
        Args:
            instrument_id: 仪器ID
            config: 仪器配置
            
        Returns:
            BaseDriver: 驱动实例
        """
        driver_type = config.get('driver')
        
        # 选择驱动类
        if self.simulation_mode:
            driver_class = self.SIMULATED_DRIVER_MAP.get(driver_type)
        else:
            driver_class = self.DRIVER_MAP.get(driver_type)
        
        if driver_class is None:
            self.logger.error(f"未知的驱动类型: {driver_type}")
            return None
        
        # 创建驱动实例
        try:
            driver = driver_class(
                resource_string=config.get('resource_string'),
                timeout=config.get('timeout', 5000),
                parameters=config.get('parameters', {}),
                backend=self.config_manager.visa_settings.get('backend', '')
            )
            self.logger.debug(f"已创建驱动实例: {instrument_id}")
            return driver
        except Exception as e:
            self.logger.error(f"创建驱动实例失败 {instrument_id}: {e}")
            return None
    
    def connect_instrument(self, instrument_id: str) -> bool:
        """
        连接单个仪器
        
        Args:
            instrument_id: 仪器ID
            
        Returns:
            bool: 连接是否成功
        """
        with self._lock:
            # 检查是否已连接
            if instrument_id in self._instruments:
                if self._instruments[instrument_id].is_connected:
                    self.logger.info(f"仪器已连接: {instrument_id}")
                    return True
            
            # 获取配置
            config = self.config_manager.get_instrument_config(instrument_id)
            if config is None:
                self.logger.error(f"仪器配置不存在: {instrument_id}")
                return False
            
            if not config.get('enabled', True):
                self.logger.warning(f"仪器未启用: {instrument_id}")
                return False
            
            # 创建驱动实例
            driver = self._create_driver_instance(instrument_id, config)
            if driver is None:
                return False
            
            # 连接仪器
            try:
                if driver.connect():
                    self._instruments[instrument_id] = driver
                    self.logger.info(f"成功连接仪器: {instrument_id} ({config.get('name')})")
                    return True
                else:
                    return False
            except Exception as e:
                self.logger.error(f"连接仪器失败 {instrument_id}: {e}")
                return False
    
    def disconnect_instrument(self, instrument_id: str):
        """
        断开单个仪器
        
        Args:
            instrument_id: 仪器ID
        """
        with self._lock:
            if instrument_id in self._instruments:
                try:
                    self._instruments[instrument_id].disconnect()
                    del self._instruments[instrument_id]
                    self.logger.info(f"已断开仪器: {instrument_id}")
                except Exception as e:
                    self.logger.error(f"断开仪器失败 {instrument_id}: {e}")
    
    def connect_all(self, parallel: bool = True) -> Dict[str, bool]:
        """
        连接所有已启用的仪器
        
        Args:
            parallel: 是否并行连接
            
        Returns:
            Dict[str, bool]: 各仪器的连接状态
        """
        enabled_instruments = self.config_manager.get_enabled_instruments()
        results = {}
        
        if parallel:
            with ThreadPoolExecutor(max_workers=len(enabled_instruments)) as executor:
                futures = {
                    executor.submit(self.connect_instrument, instr_id): instr_id
                    for instr_id in enabled_instruments.keys()
                }
                for future in as_completed(futures):
                    instr_id = futures[future]
                    try:
                        results[instr_id] = future.result()
                    except Exception as e:
                        self.logger.error(f"连接仪器异常 {instr_id}: {e}")
                        results[instr_id] = False
        else:
            for instr_id in enabled_instruments.keys():
                results[instr_id] = self.connect_instrument(instr_id)
        
        return results
    
    def disconnect_all(self):
        """断开所有仪器"""
        instrument_ids = list(self._instruments.keys())
        for instr_id in instrument_ids:
            self.disconnect_instrument(instr_id)
        self.logger.info("已断开所有仪器")
    
    def get_instrument(self, instrument_id: str) -> Optional[BaseDriver]:
        """
        获取仪器实例
        
        Args:
            instrument_id: 仪器ID
            
        Returns:
            BaseDriver: 仪器驱动实例
        """
        return self._instruments.get(instrument_id)
    
    def get_connected_instruments(self) -> Dict[str, BaseDriver]:
        """获取所有已连接的仪器"""
        return {k: v for k, v in self._instruments.items() if v.is_connected}
    
    def is_instrument_connected(self, instrument_id: str) -> bool:
        """检查仪器是否已连接"""
        instrument = self._instruments.get(instrument_id)
        return instrument is not None and instrument.is_connected
    
    def get_instrument_status(self) -> Dict[str, Dict]:
        """
        获取所有仪器状态
        
        Returns:
            Dict[str, Dict]: 仪器状态信息
        """
        all_instruments = self.config_manager.instruments
        status = {}
        
        for instr_id, config in all_instruments.items():
            status[instr_id] = {
                'name': config.get('name'),
                'driver': config.get('driver'),
                'resource_string': config.get('resource_string'),
                'enabled': config.get('enabled', True),
                'connected': self.is_instrument_connected(instr_id)
            }
            
            # 如果已连接，获取更多信息
            if status[instr_id]['connected']:
                try:
                    instrument = self._instruments[instr_id]
                    status[instr_id]['idn'] = instrument.get_idn()
                except:
                    pass
        
        return status
    
    def check_instruments_for_flow(self, flow_id: str) -> Dict[str, bool]:
        """
        检查测试流程所需仪器的状态
        
        Args:
            flow_id: 测试流程ID
            
        Returns:
            Dict[str, bool]: 各所需仪器的连接状态
        """
        flow_config = self.config_manager.get_test_flow(flow_id)
        if flow_config is None:
            return {}
        
        required = flow_config.get('instruments_required', [])
        return {instr_id: self.is_instrument_connected(instr_id) for instr_id in required}
    
    def connect_instruments_for_flow(self, flow_id: str) -> bool:
        """
        连接测试流程所需的仪器
        
        Args:
            flow_id: 测试流程ID
            
        Returns:
            bool: 所有所需仪器是否都已连接
        """
        flow_config = self.config_manager.get_test_flow(flow_id)
        if flow_config is None:
            self.logger.error(f"测试流程不存在: {flow_id}")
            return False
        
        required = flow_config.get('instruments_required', [])
        all_connected = True
        
        for instr_id in required:
            if not self.is_instrument_connected(instr_id):
                if not self.connect_instrument(instr_id):
                    all_connected = False
        
        return all_connected
    
    def self_test_all(self) -> Dict[str, bool]:
        """
        对所有已连接仪器执行自检
        
        Returns:
            Dict[str, bool]: 各仪器的自检结果
        """
        results = {}
        for instr_id, instrument in self._instruments.items():
            try:
                results[instr_id] = instrument.self_test()
            except Exception as e:
                self.logger.error(f"仪器自检失败 {instr_id}: {e}")
                results[instr_id] = False
        return results
    
    def __enter__(self):
        """上下文管理器入口"""
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器出口"""
        self.disconnect_all()
        return False
