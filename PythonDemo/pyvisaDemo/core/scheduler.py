"""
测试调度器
负责管理测试任务队列和调度执行
"""
import logging
import threading
import queue
import time
from typing import Dict, Optional, List, Callable, Any
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
import json
from pathlib import Path

from .config_manager import ConfigManager
from .instrument_manager import InstrumentManager
from .test_engine import TestEngine, TestResult, TestStatus


class TaskPriority(Enum):
    """任务优先级"""
    LOW = 1
    NORMAL = 2
    HIGH = 3
    URGENT = 4


@dataclass
class TestTask:
    """测试任务"""
    task_id: str
    flow_id: str
    priority: TaskPriority = TaskPriority.NORMAL
    product_info: Dict = field(default_factory=dict)
    created_at: datetime = field(default_factory=datetime.now)
    scheduled_at: Optional[datetime] = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    status: str = "pending"
    result: Optional[TestResult] = None
    retry_count: int = 0
    max_retries: int = 3
    
    def __lt__(self, other):
        """用于优先级队列排序"""
        if self.priority.value != other.priority.value:
            return self.priority.value > other.priority.value
        return self.created_at < other.created_at


class TestScheduler:
    """测试调度器"""
    
    def __init__(self, config_manager: ConfigManager,
                 instrument_manager: InstrumentManager,
                 test_engine: TestEngine):
        """
        初始化调度器
        
        Args:
            config_manager: 配置管理器
            instrument_manager: 仪器管理器
            test_engine: 测试引擎
        """
        self.config_manager = config_manager
        self.instrument_manager = instrument_manager
        self.test_engine = test_engine
        self.logger = logging.getLogger(self.__class__.__name__)
        
        # 任务队列
        self._task_queue: queue.PriorityQueue = queue.PriorityQueue()
        self._all_tasks: Dict[str, TestTask] = {}
        self._completed_tasks: List[TestTask] = []
        
        # 调度器状态
        self._running = False
        self._worker_thread: Optional[threading.Thread] = None
        self._lock = threading.Lock()
        
        # 配置
        scheduler_config = config_manager.scheduler_config
        self.max_retries = scheduler_config.get('max_retries', 3)
        self.retry_delay = scheduler_config.get('retry_delay', 5)
        self.auto_save_results = scheduler_config.get('auto_save_results', True)
        self.results_format = scheduler_config.get('results_format', ['json'])
        
        # 回调
        self._callbacks: Dict[str, List[Callable]] = {
            'on_task_added': [],
            'on_task_started': [],
            'on_task_completed': [],
            'on_task_failed': [],
            'on_queue_empty': []
        }
        
        # 任务ID计数器
        self._task_counter = 0
    
    def register_callback(self, event: str, callback: Callable):
        """注册回调函数"""
        if event in self._callbacks:
            self._callbacks[event].append(callback)
    
    def _trigger_callback(self, event: str, *args, **kwargs):
        """触发回调"""
        for callback in self._callbacks.get(event, []):
            try:
                callback(*args, **kwargs)
            except Exception as e:
                self.logger.error(f"回调执行错误: {e}")
    
    def add_task(self, flow_id: str, priority: TaskPriority = TaskPriority.NORMAL,
                 product_info: Dict = None, scheduled_at: datetime = None) -> str:
        """
        添加测试任务
        
        Args:
            flow_id: 测试流程ID
            priority: 优先级
            product_info: 产品信息
            scheduled_at: 计划执行时间
            
        Returns:
            str: 任务ID
        """
        with self._lock:
            self._task_counter += 1
            task_id = f"TASK_{self._task_counter:06d}"
        
        task = TestTask(
            task_id=task_id,
            flow_id=flow_id,
            priority=priority,
            product_info=product_info or {},
            scheduled_at=scheduled_at,
            max_retries=self.max_retries
        )
        
        self._all_tasks[task_id] = task
        self._task_queue.put(task)
        
        self.logger.info(f"已添加任务: {task_id} ({flow_id})")
        self._trigger_callback('on_task_added', task)
        
        return task_id
    
    def add_batch_tasks(self, tasks: List[Dict]) -> List[str]:
        """
        批量添加任务
        
        Args:
            tasks: 任务配置列表
            
        Returns:
            List[str]: 任务ID列表
        """
        task_ids = []
        for task_config in tasks:
            task_id = self.add_task(
                flow_id=task_config.get('flow_id'),
                priority=TaskPriority[task_config.get('priority', 'NORMAL')],
                product_info=task_config.get('product_info', {}),
                scheduled_at=task_config.get('scheduled_at')
            )
            task_ids.append(task_id)
        return task_ids
    
    def add_product_test(self, product_id: str, serial_number: str = None,
                        priority: TaskPriority = TaskPriority.NORMAL) -> List[str]:
        """
        添加产品测试（自动添加产品所需的所有测试流程）
        
        Args:
            product_id: 产品ID
            serial_number: 序列号
            priority: 优先级
            
        Returns:
            List[str]: 任务ID列表
        """
        product_config = self.config_manager.get_product_config(product_id)
        if product_config is None:
            self.logger.error(f"产品不存在: {product_id}")
            return []
        
        product_info = {
            'product_id': product_id,
            'product_name': product_config.get('name'),
            'serial_number': serial_number or f"SN_{datetime.now().strftime('%Y%m%d%H%M%S')}",
            'limits': self.config_manager.get_product_limits(product_id)
        }
        
        test_flows = self.config_manager.get_product_test_requirements(product_id)
        task_ids = []
        
        for flow_id in test_flows:
            task_id = self.add_task(
                flow_id=flow_id,
                priority=priority,
                product_info=product_info
            )
            task_ids.append(task_id)
        
        return task_ids
    
    def start(self):
        """启动调度器"""
        if self._running:
            self.logger.warning("调度器已在运行")
            return
        
        self._running = True
        self._worker_thread = threading.Thread(target=self._worker_loop, daemon=True)
        self._worker_thread.start()
        self.logger.info("调度器已启动")
    
    def stop(self, wait: bool = True):
        """
        停止调度器
        
        Args:
            wait: 是否等待当前任务完成
        """
        self._running = False
        if wait and self._worker_thread:
            self._worker_thread.join(timeout=30)
        self.logger.info("调度器已停止")
    
    def _worker_loop(self):
        """工作线程主循环"""
        while self._running:
            try:
                # 尝试获取任务
                try:
                    task = self._task_queue.get(timeout=1)
                except queue.Empty:
                    continue
                
                # 检查计划执行时间
                if task.scheduled_at and datetime.now() < task.scheduled_at:
                    self._task_queue.put(task)
                    time.sleep(1)
                    continue
                
                # 执行任务
                self._execute_task(task)
                
            except Exception as e:
                self.logger.error(f"工作线程错误: {e}")
        
        self._trigger_callback('on_queue_empty')
    
    def _execute_task(self, task: TestTask):
        """
        执行单个任务
        
        Args:
            task: 测试任务
        """
        task.status = "running"
        task.started_at = datetime.now()
        self._trigger_callback('on_task_started', task)
        self.logger.info(f"开始执行任务: {task.task_id}")
        
        try:
            # 执行测试
            result = self.test_engine.run_test_flow(
                flow_id=task.flow_id,
                product_info=task.product_info
            )
            task.result = result
            
            if result.status == TestStatus.PASSED:
                task.status = "completed"
                self._trigger_callback('on_task_completed', task)
            else:
                # 判断是否需要重试
                if (task.retry_count < task.max_retries and 
                    result.status in [TestStatus.ERROR, TestStatus.FAILED]):
                    task.retry_count += 1
                    task.status = "pending"
                    self.logger.info(f"任务 {task.task_id} 将重试 ({task.retry_count}/{task.max_retries})")
                    time.sleep(self.retry_delay)
                    self._task_queue.put(task)
                    return
                else:
                    task.status = "failed"
                    self._trigger_callback('on_task_failed', task)
            
        except Exception as e:
            self.logger.error(f"任务执行错误: {e}")
            task.status = "error"
            self._trigger_callback('on_task_failed', task)
        
        task.completed_at = datetime.now()
        self._completed_tasks.append(task)
        
        # 自动保存结果
        if self.auto_save_results and task.result:
            self._save_task_result(task)
    
    def _save_task_result(self, task: TestTask):
        """保存任务结果"""
        results_dir = Path(__file__).parent.parent / 'reports'
        results_dir.mkdir(exist_ok=True)
        
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        base_name = f"{task.task_id}_{timestamp}"
        
        result_data = {
            'task_id': task.task_id,
            'flow_id': task.flow_id,
            'status': task.status,
            'product_info': task.product_info,
            'created_at': task.created_at.isoformat(),
            'started_at': task.started_at.isoformat() if task.started_at else None,
            'completed_at': task.completed_at.isoformat() if task.completed_at else None,
            'retry_count': task.retry_count,
            'result': {
                'status': task.result.status.value if task.result else None,
                'duration': task.result.duration if task.result else None,
                'measurements': task.result.measurements if task.result else {},
                'error_message': task.result.error_message if task.result else None
            }
        }
        
        if 'json' in self.results_format:
            json_path = results_dir / f"{base_name}.json"
            with open(json_path, 'w', encoding='utf-8') as f:
                json.dump(result_data, f, indent=2, ensure_ascii=False)
    
    def get_task(self, task_id: str) -> Optional[TestTask]:
        """获取任务"""
        return self._all_tasks.get(task_id)
    
    def get_task_status(self, task_id: str) -> Optional[str]:
        """获取任务状态"""
        task = self.get_task(task_id)
        return task.status if task else None
    
    def get_pending_tasks(self) -> List[TestTask]:
        """获取待执行任务列表"""
        return [t for t in self._all_tasks.values() if t.status == "pending"]
    
    def get_completed_tasks(self) -> List[TestTask]:
        """获取已完成任务列表"""
        return self._completed_tasks.copy()
    
    def get_queue_size(self) -> int:
        """获取队列大小"""
        return self._task_queue.qsize()
    
    def clear_queue(self):
        """清空任务队列"""
        while not self._task_queue.empty():
            try:
                self._task_queue.get_nowait()
            except queue.Empty:
                break
        self.logger.info("任务队列已清空")
    
    def get_statistics(self) -> Dict:
        """获取调度统计信息"""
        total = len(self._all_tasks)
        completed = len([t for t in self._all_tasks.values() if t.status == "completed"])
        failed = len([t for t in self._all_tasks.values() if t.status in ["failed", "error"]])
        pending = len([t for t in self._all_tasks.values() if t.status == "pending"])
        running = len([t for t in self._all_tasks.values() if t.status == "running"])
        
        return {
            'total_tasks': total,
            'completed': completed,
            'failed': failed,
            'pending': pending,
            'running': running,
            'queue_size': self.get_queue_size(),
            'success_rate': (completed / total * 100) if total > 0 else 0
        }
    
    @property
    def is_running(self) -> bool:
        """检查调度器是否运行中"""
        return self._running
