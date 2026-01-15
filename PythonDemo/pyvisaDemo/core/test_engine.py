"""
测试引擎
负责执行测试流程和管理测试状态
"""
import logging
import time
import importlib
from typing import Dict, Optional, List, Any, Callable
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
import traceback

from .config_manager import ConfigManager
from .instrument_manager import InstrumentManager


class TestStatus(Enum):
    """测试状态枚举"""
    PENDING = "pending"
    RUNNING = "running"
    PASSED = "passed"
    FAILED = "failed"
    ERROR = "error"
    SKIPPED = "skipped"
    ABORTED = "aborted"


@dataclass
class StepResult:
    """测试步骤结果"""
    step_id: int
    name: str
    status: TestStatus
    start_time: datetime
    end_time: datetime
    duration: float
    data: Dict = field(default_factory=dict)
    error_message: str = ""


@dataclass
class TestResult:
    """测试结果"""
    flow_id: str
    flow_name: str
    status: TestStatus
    start_time: datetime
    end_time: Optional[datetime] = None
    duration: float = 0.0
    step_results: List[StepResult] = field(default_factory=list)
    measurements: Dict = field(default_factory=dict)
    pass_criteria: Dict = field(default_factory=dict)
    passed_criteria: Dict = field(default_factory=dict)
    error_message: str = ""
    product_info: Dict = field(default_factory=dict)


class TestEngine:
    """测试引擎"""
    
    def __init__(self, config_manager: ConfigManager, 
                 instrument_manager: InstrumentManager):
        """
        初始化测试引擎
        
        Args:
            config_manager: 配置管理器
            instrument_manager: 仪器管理器
        """
        self.config_manager = config_manager
        self.instrument_manager = instrument_manager
        self.logger = logging.getLogger(self.__class__.__name__)
        
        # 当前测试状态
        self.current_result: Optional[TestResult] = None
        self._abort_requested = False
        self._pause_requested = False
        
        # 回调函数
        self._callbacks: Dict[str, List[Callable]] = {
            'on_flow_start': [],
            'on_flow_end': [],
            'on_step_start': [],
            'on_step_end': [],
            'on_measurement': [],
            'on_error': []
        }
    
    def register_callback(self, event: str, callback: Callable):
        """
        注册回调函数
        
        Args:
            event: 事件名称
            callback: 回调函数
        """
        if event in self._callbacks:
            self._callbacks[event].append(callback)
    
    def _trigger_callback(self, event: str, *args, **kwargs):
        """触发回调函数"""
        for callback in self._callbacks.get(event, []):
            try:
                callback(*args, **kwargs)
            except Exception as e:
                self.logger.error(f"回调函数执行错误: {e}")
    
    def run_test_flow(self, flow_id: str, product_info: Dict = None) -> TestResult:
        """
        执行测试流程
        
        Args:
            flow_id: 测试流程ID
            product_info: 产品信息（可选）
            
        Returns:
            TestResult: 测试结果
        """
        self._abort_requested = False
        
        # 获取流程配置
        flow_config = self.config_manager.get_test_flow(flow_id)
        if flow_config is None:
            self.logger.error(f"测试流程不存在: {flow_id}")
            return self._create_error_result(flow_id, "测试流程不存在")
        
        # 初始化测试结果
        self.current_result = TestResult(
            flow_id=flow_id,
            flow_name=flow_config.get('name', flow_id),
            status=TestStatus.RUNNING,
            start_time=datetime.now(),
            pass_criteria=flow_config.get('pass_criteria', {}),
            product_info=product_info or {}
        )
        
        self._trigger_callback('on_flow_start', self.current_result)
        self.logger.info(f"开始执行测试流程: {flow_config.get('name')}")
        
        try:
            # 连接所需仪器
            if not self.instrument_manager.connect_instruments_for_flow(flow_id):
                raise RuntimeError("无法连接所需仪器")
            
            # 加载测试类
            test_class = self._load_test_class(flow_config.get('test_class'))
            if test_class is None:
                raise RuntimeError(f"无法加载测试类: {flow_config.get('test_class')}")
            
            # 创建测试实例
            test_instance = test_class(
                config=flow_config,
                instruments=self._get_required_instruments(flow_config),
                result=self.current_result
            )
            
            # 执行测试步骤
            steps = flow_config.get('steps', [])
            for step in steps:
                if self._abort_requested:
                    self.logger.warning("测试已中止")
                    self.current_result.status = TestStatus.ABORTED
                    break
                
                while self._pause_requested:
                    time.sleep(0.1)
                
                step_result = self._execute_step(test_instance, step, flow_config)
                self.current_result.step_results.append(step_result)
                
                if step_result.status == TestStatus.FAILED:
                    self.current_result.status = TestStatus.FAILED
                    self.current_result.error_message = step_result.error_message
                    break
            
            # 评估测试结果
            if self.current_result.status == TestStatus.RUNNING:
                self._evaluate_pass_criteria()
            
        except Exception as e:
            self.logger.error(f"测试执行错误: {e}")
            self.logger.debug(traceback.format_exc())
            self.current_result.status = TestStatus.ERROR
            self.current_result.error_message = str(e)
            self._trigger_callback('on_error', e)
        
        # 完成测试
        self.current_result.end_time = datetime.now()
        self.current_result.duration = (
            self.current_result.end_time - self.current_result.start_time
        ).total_seconds()
        
        self._trigger_callback('on_flow_end', self.current_result)
        self.logger.info(
            f"测试流程完成: {self.current_result.flow_name}, "
            f"状态: {self.current_result.status.value}, "
            f"耗时: {self.current_result.duration:.2f}s"
        )
        
        return self.current_result
    
    def _execute_step(self, test_instance, step: Dict, 
                      flow_config: Dict) -> StepResult:
        """
        执行单个测试步骤
        
        Args:
            test_instance: 测试实例
            step: 步骤配置
            flow_config: 流程配置
            
        Returns:
            StepResult: 步骤结果
        """
        step_id = step.get('step_id', 0)
        step_name = step.get('name', f'Step {step_id}')
        action = step.get('action')
        
        self.logger.info(f"执行步骤 {step_id}: {step_name}")
        self._trigger_callback('on_step_start', step_id, step_name)
        
        start_time = datetime.now()
        step_result = StepResult(
            step_id=step_id,
            name=step_name,
            status=TestStatus.RUNNING,
            start_time=start_time,
            end_time=start_time,
            duration=0
        )
        
        try:
            # 获取测试方法
            if not hasattr(test_instance, action):
                raise AttributeError(f"测试方法不存在: {action}")
            
            method = getattr(test_instance, action)
            
            # 处理循环执行
            loop_over = step.get('loop_over')
            if loop_over and loop_over in flow_config.get('parameters', {}):
                loop_values = flow_config['parameters'][loop_over]
                results = []
                for value in loop_values:
                    result = method(value, **step.get('parameters', {}))
                    results.append(result)
                    self._trigger_callback('on_measurement', action, value, result)
                step_result.data = {loop_over: results}
            else:
                result = method(**step.get('parameters', {}))
                step_result.data = {'result': result}
                self._trigger_callback('on_measurement', action, None, result)
            
            step_result.status = TestStatus.PASSED
            
        except Exception as e:
            self.logger.error(f"步骤执行失败: {e}")
            self.logger.debug(traceback.format_exc())
            step_result.status = TestStatus.FAILED
            step_result.error_message = str(e)
        
        step_result.end_time = datetime.now()
        step_result.duration = (step_result.end_time - step_result.start_time).total_seconds()
        
        self._trigger_callback('on_step_end', step_result)
        return step_result
    
    def _evaluate_pass_criteria(self):
        """评估测试通过标准"""
        if not self.current_result.pass_criteria:
            self.current_result.status = TestStatus.PASSED
            return
        
        all_passed = True
        measurements = self.current_result.measurements
        
        for criterion, limit in self.current_result.pass_criteria.items():
            if criterion not in measurements:
                continue
            
            value = measurements[criterion]
            passed = True
            
            if isinstance(limit, dict):
                if 'max' in limit and value > limit['max']:
                    passed = False
                if 'min' in limit and value < limit['min']:
                    passed = False
            else:
                if value > limit:
                    passed = False
            
            self.current_result.passed_criteria[criterion] = passed
            if not passed:
                all_passed = False
        
        self.current_result.status = TestStatus.PASSED if all_passed else TestStatus.FAILED
    
    def _load_test_class(self, class_name: str):
        """
        动态加载测试类
        
        Args:
            class_name: 测试类名
            
        Returns:
            测试类
        """
        try:
            module = importlib.import_module('test_cases')
            return getattr(module, class_name, None)
        except ImportError as e:
            self.logger.error(f"导入测试模块失败: {e}")
            return None
    
    def _get_required_instruments(self, flow_config: Dict) -> Dict:
        """获取流程所需的仪器实例"""
        instruments = {}
        for instr_id in flow_config.get('instruments_required', []):
            instruments[instr_id] = self.instrument_manager.get_instrument(instr_id)
        return instruments
    
    def _create_error_result(self, flow_id: str, error_message: str) -> TestResult:
        """创建错误结果"""
        return TestResult(
            flow_id=flow_id,
            flow_name=flow_id,
            status=TestStatus.ERROR,
            start_time=datetime.now(),
            end_time=datetime.now(),
            error_message=error_message
        )
    
    def abort(self):
        """中止当前测试"""
        self._abort_requested = True
        self.logger.warning("请求中止测试")
    
    def pause(self):
        """暂停当前测试"""
        self._pause_requested = True
        self.logger.info("测试已暂停")
    
    def resume(self):
        """恢复测试"""
        self._pause_requested = False
        self.logger.info("测试已恢复")
    
    @property
    def is_running(self) -> bool:
        """检查是否正在运行测试"""
        return (self.current_result is not None and 
                self.current_result.status == TestStatus.RUNNING)
    
    @property
    def is_paused(self) -> bool:
        """检查是否已暂停"""
        return self._pause_requested
