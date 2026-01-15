"""
配置管理器
负责加载、保存和管理所有配置文件
"""
import os
import yaml
from typing import Dict, Any, Optional, List
from pathlib import Path
import logging
import copy


class ConfigManager:
    """配置管理器"""
    
    def __init__(self, config_dir: str = None):
        """
        初始化配置管理器
        
        Args:
            config_dir: 配置文件目录路径
        """
        if config_dir is None:
            # 默认使用项目根目录下的config文件夹
            self.config_dir = Path(__file__).parent.parent / 'config'
        else:
            self.config_dir = Path(config_dir)
        
        self.logger = logging.getLogger(self.__class__.__name__)
        
        # 配置缓存
        self._instruments_config: Dict = {}
        self._test_flows_config: Dict = {}
        self._products_config: Dict = {}
        
        # 加载所有配置
        self.reload_all()
    
    def reload_all(self):
        """重新加载所有配置文件"""
        self._instruments_config = self._load_yaml('instruments.yaml')
        self._test_flows_config = self._load_yaml('test_flows.yaml')
        self._products_config = self._load_yaml('products.yaml')
        self.logger.info("所有配置文件已加载")
    
    def _load_yaml(self, filename: str) -> Dict:
        """
        加载YAML配置文件
        
        Args:
            filename: 文件名
            
        Returns:
            Dict: 配置字典
        """
        filepath = self.config_dir / filename
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                config = yaml.safe_load(f)
                self.logger.debug(f"已加载配置: {filename}")
                return config or {}
        except FileNotFoundError:
            self.logger.warning(f"配置文件不存在: {filepath}")
            return {}
        except yaml.YAMLError as e:
            self.logger.error(f"YAML解析错误 {filename}: {e}")
            return {}
    
    def _save_yaml(self, filename: str, data: Dict):
        """
        保存YAML配置文件
        
        Args:
            filename: 文件名
            data: 配置数据
        """
        filepath = self.config_dir / filename
        try:
            with open(filepath, 'w', encoding='utf-8') as f:
                yaml.dump(data, f, default_flow_style=False, allow_unicode=True, 
                         sort_keys=False, indent=2)
                self.logger.info(f"配置已保存: {filename}")
        except Exception as e:
            self.logger.error(f"保存配置失败 {filename}: {e}")
            raise
    
    # ========== 仪器配置 ==========
    
    @property
    def instruments(self) -> Dict:
        """获取仪器配置"""
        return self._instruments_config.get('instruments', {})
    
    @property
    def visa_settings(self) -> Dict:
        """获取VISA设置"""
        return self._instruments_config.get('visa_settings', {})
    
    def get_instrument_config(self, instrument_id: str) -> Optional[Dict]:
        """
        获取指定仪器的配置
        
        Args:
            instrument_id: 仪器ID
            
        Returns:
            Dict: 仪器配置
        """
        return self.instruments.get(instrument_id)
    
    def get_enabled_instruments(self) -> Dict:
        """获取所有已启用的仪器"""
        return {k: v for k, v in self.instruments.items() if v.get('enabled', True)}
    
    def update_instrument_config(self, instrument_id: str, config: Dict):
        """
        更新仪器配置
        
        Args:
            instrument_id: 仪器ID
            config: 新配置
        """
        if 'instruments' not in self._instruments_config:
            self._instruments_config['instruments'] = {}
        self._instruments_config['instruments'][instrument_id] = config
        self._save_yaml('instruments.yaml', self._instruments_config)
    
    def add_instrument(self, instrument_id: str, name: str, driver: str, 
                      resource_string: str, **kwargs):
        """
        添加新仪器
        
        Args:
            instrument_id: 仪器ID
            name: 仪器名称
            driver: 驱动类型
            resource_string: VISA资源字符串
            **kwargs: 其他参数
        """
        config = {
            'name': name,
            'driver': driver,
            'resource_string': resource_string,
            'timeout': kwargs.get('timeout', 5000),
            'enabled': kwargs.get('enabled', True),
            'parameters': kwargs.get('parameters', {})
        }
        self.update_instrument_config(instrument_id, config)
    
    def remove_instrument(self, instrument_id: str):
        """删除仪器配置"""
        if instrument_id in self._instruments_config.get('instruments', {}):
            del self._instruments_config['instruments'][instrument_id]
            self._save_yaml('instruments.yaml', self._instruments_config)
    
    # ========== 测试流程配置 ==========
    
    @property
    def test_flows(self) -> Dict:
        """获取测试流程配置"""
        return self._test_flows_config.get('test_flows', {})
    
    @property
    def scheduler_config(self) -> Dict:
        """获取调度器配置"""
        return self._test_flows_config.get('scheduler', {})
    
    def get_test_flow(self, flow_id: str) -> Optional[Dict]:
        """
        获取指定测试流程
        
        Args:
            flow_id: 流程ID
            
        Returns:
            Dict: 流程配置
        """
        return self.test_flows.get(flow_id)
    
    def get_enabled_test_flows(self) -> Dict:
        """获取所有已启用的测试流程"""
        return {k: v for k, v in self.test_flows.items() if v.get('enabled', True)}
    
    def update_test_flow(self, flow_id: str, config: Dict):
        """
        更新测试流程配置
        
        Args:
            flow_id: 流程ID
            config: 新配置
        """
        if 'test_flows' not in self._test_flows_config:
            self._test_flows_config['test_flows'] = {}
        self._test_flows_config['test_flows'][flow_id] = config
        self._save_yaml('test_flows.yaml', self._test_flows_config)
    
    def create_test_flow(self, flow_id: str, name: str, test_class: str,
                        instruments_required: List[str], steps: List[Dict], **kwargs):
        """
        创建新测试流程
        
        Args:
            flow_id: 流程ID
            name: 流程名称
            test_class: 测试类名
            instruments_required: 所需仪器列表
            steps: 测试步骤列表
            **kwargs: 其他参数
        """
        config = {
            'name': name,
            'description': kwargs.get('description', ''),
            'enabled': kwargs.get('enabled', True),
            'test_class': test_class,
            'instruments_required': instruments_required,
            'parameters': kwargs.get('parameters', {}),
            'pass_criteria': kwargs.get('pass_criteria', {}),
            'steps': steps
        }
        self.update_test_flow(flow_id, config)
    
    # ========== 产品配置 ==========
    
    @property
    def products(self) -> Dict:
        """获取产品配置"""
        return self._products_config.get('products', {})
    
    @property
    def test_stations(self) -> Dict:
        """获取测试站点配置"""
        return self._products_config.get('test_stations', {})
    
    def get_product_config(self, product_id: str) -> Optional[Dict]:
        """
        获取产品配置
        
        Args:
            product_id: 产品ID
            
        Returns:
            Dict: 产品配置
        """
        return self.products.get(product_id)
    
    def get_product_test_requirements(self, product_id: str) -> List[str]:
        """
        获取产品的测试需求
        
        Args:
            product_id: 产品ID
            
        Returns:
            List[str]: 需要执行的测试流程ID列表
        """
        product = self.get_product_config(product_id)
        if product:
            return product.get('test_requirements', [])
        return []
    
    def get_product_limits(self, product_id: str) -> Dict:
        """
        获取产品的测试限值
        
        Args:
            product_id: 产品ID
            
        Returns:
            Dict: 限值配置
        """
        product = self.get_product_config(product_id)
        if product:
            return product.get('limits', {})
        return {}
    
    # ========== 辅助方法 ==========
    
    def validate_config(self) -> List[str]:
        """
        验证配置完整性
        
        Returns:
            List[str]: 错误消息列表
        """
        errors = []
        
        # 检查测试流程所需的仪器是否都已配置
        for flow_id, flow in self.test_flows.items():
            if not flow.get('enabled', True):
                continue
            for instr_id in flow.get('instruments_required', []):
                if instr_id not in self.instruments:
                    errors.append(f"测试流程 '{flow_id}' 需要的仪器 '{instr_id}' 未配置")
                elif not self.instruments[instr_id].get('enabled', True):
                    errors.append(f"测试流程 '{flow_id}' 需要的仪器 '{instr_id}' 未启用")
        
        # 检查产品测试需求对应的流程是否存在
        for product_id, product in self.products.items():
            for flow_id in product.get('test_requirements', []):
                if flow_id not in self.test_flows:
                    errors.append(f"产品 '{product_id}' 需要的测试流程 '{flow_id}' 不存在")
        
        return errors
    
    def export_config(self, filepath: str):
        """
        导出所有配置到单个文件
        
        Args:
            filepath: 导出文件路径
        """
        combined = {
            'instruments': self._instruments_config,
            'test_flows': self._test_flows_config,
            'products': self._products_config
        }
        with open(filepath, 'w', encoding='utf-8') as f:
            yaml.dump(combined, f, default_flow_style=False, allow_unicode=True)
    
    def import_config(self, filepath: str):
        """
        从文件导入配置
        
        Args:
            filepath: 配置文件路径
        """
        with open(filepath, 'r', encoding='utf-8') as f:
            combined = yaml.safe_load(f)
        
        if 'instruments' in combined:
            self._instruments_config = combined['instruments']
            self._save_yaml('instruments.yaml', self._instruments_config)
        
        if 'test_flows' in combined:
            self._test_flows_config = combined['test_flows']
            self._save_yaml('test_flows.yaml', self._test_flows_config)
        
        if 'products' in combined:
            self._products_config = combined['products']
            self._save_yaml('products.yaml', self._products_config)
        
        self.logger.info("配置导入完成")
