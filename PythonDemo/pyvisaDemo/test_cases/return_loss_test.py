"""
回波损耗测试
测量光器件的回波损耗
"""
import time
from typing import Dict, List, Any
import numpy as np

from .base_test import BaseTest


class ReturnLossTest(BaseTest):
    """回波损耗测试"""
    
    def __init__(self, config: Dict, instruments: Dict, result):
        super().__init__(config, instruments, result)
        
        # 获取仪器
        self.laser = self.get_instrument('laser_source')
        self.power_meter = self.get_instrument('optical_power_meter')
        
        # 测试参数
        self.wavelengths = self.parameters.get('wavelengths', [1550])
        self.input_power = self.parameters.get('input_power', 0)
        self.measurement_count = self.parameters.get('measurement_count', 5)
        
        # 测试数据
        self.input_powers: Dict[float, float] = {}
        self.reflected_powers: Dict[float, float] = {}
        self.return_losses: Dict[float, float] = {}
    
    def calibrate_reference(self, wavelength: float = None, **kwargs) -> float:
        """
        校准输入功率
        
        Args:
            wavelength: 波长(nm)
            
        Returns:
            float: 输入功率值(dBm)
        """
        if wavelength is None:
            wavelength = 1550
        
        self.logger.info(f"校准输入功率 @ {wavelength} nm")
        
        # 设置激光器
        self.laser.set_wavelength(wavelength)
        self.laser.set_power(self.input_power)
        self.laser.output_on()
        
        # 设置功率计
        self.power_meter.set_wavelength(wavelength)
        
        time.sleep(0.5)
        
        # 测量输入功率
        powers = self.power_meter.measure_multiple(count=self.measurement_count)
        input_power = np.mean(powers)
        
        self.input_powers[wavelength] = input_power
        self.reference_values[f'input_power_{wavelength}nm'] = input_power
        
        self.logger.info(f"输入功率 @ {wavelength} nm: {input_power:.3f} dBm")
        return input_power
    
    def measure_return_loss(self, wavelength: float) -> Dict:
        """
        测量回波损耗
        
        Args:
            wavelength: 波长(nm)
            
        Returns:
            Dict: 测量结果
        """
        self.logger.info(f"测量回损 @ {wavelength} nm")
        
        # 确保有输入功率校准值
        if wavelength not in self.input_powers:
            self.calibrate_reference(wavelength)
        
        input_power = self.input_powers[wavelength]
        
        # 设置激光器
        self.laser.set_wavelength(wavelength)
        self.laser.output_on()
        
        # 设置功率计
        self.power_meter.set_wavelength(wavelength)
        
        time.sleep(0.5)
        
        # 测量反射功率
        powers = self.power_meter.measure_multiple(count=self.measurement_count)
        reflected_power = np.mean(powers)
        
        # 计算回波损耗 (RL = Pin - Preflected)
        return_loss = input_power - reflected_power
        
        # 保存数据
        self.reflected_powers[wavelength] = reflected_power
        self.return_losses[wavelength] = return_loss
        
        # 添加测量结果
        self.add_measurement(f'RL_{wavelength}nm', return_loss, 'dB')
        self.add_measurement(f'reflected_power_{wavelength}nm', reflected_power, 'dBm')
        
        # 检查限值 (回损越大越好)
        min_rl = self.pass_criteria.get('min_return_loss', 0)
        passed = self.check_limit(f'RL_{wavelength}nm', return_loss, min_val=min_rl)
        
        result = {
            'wavelength': wavelength,
            'input_power': input_power,
            'reflected_power': reflected_power,
            'return_loss': return_loss,
            'measurements': powers,
            'std_dev': float(np.std(powers)),
            'passed': passed
        }
        
        self.logger.info(
            f"回损 @ {wavelength} nm: {return_loss:.2f} dB "
            f"({'PASS' if passed else 'FAIL'})"
        )
        
        return result
    
    def run_wavelength_sweep(self) -> List[Dict]:
        """
        执行波长扫描测试
        
        Returns:
            List[Dict]: 所有波长的测试结果
        """
        results = []
        
        for wavelength in self.wavelengths:
            result = self.measure_return_loss(wavelength)
            results.append(result)
        
        return results
    
    def generate_report(self) -> Dict:
        """
        生成测试报告数据
        
        Returns:
            Dict: 报告数据
        """
        report = {
            'test_name': '回波损耗测试',
            'test_class': self.__class__.__name__,
            'parameters': {
                'wavelengths': self.wavelengths,
                'input_power': self.input_power,
                'measurement_count': self.measurement_count
            },
            'input_powers': self.input_powers,
            'return_losses': self.return_losses,
            'measurements': self.measurements,
            'pass_criteria': self.pass_criteria,
            'summary': {}
        }
        
        if self.return_losses:
            rl_values = list(self.return_losses.values())
            min_rl_limit = self.pass_criteria.get('min_return_loss', 0)
            
            report['summary'] = {
                'min_rl': min(rl_values),
                'max_rl': max(rl_values),
                'avg_rl': np.mean(rl_values),
                'all_passed': all(rl >= min_rl_limit for rl in rl_values)
            }
        
        return report
    
    def cleanup(self):
        """测试清理"""
        if self.laser:
            self.laser.output_off()
        self.logger.info("测试清理完成")
