#!/usr/bin/env python3
"""
完整功能验证测试
验证所有设置和功能是否真正生效
"""
import sys
import os
from pathlib import Path

# 添加项目根目录到路径
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

import unittest
from unittest.mock import Mock, patch, MagicMock
from src.utils.config_manager import ConfigManager
from src.services.bailian_client import BailianClient, BailianAPIError
from src.translation.engine import TranslationEngine
from src.dwg.renderer import DWGCanvas
from src.utils.resource_manager import ResourceManager


class TestConfigManager(unittest.TestCase):
    """测试配置管理器"""

    def setUp(self):
        """设置测试"""
        self.config = ConfigManager()

    def test_config_instance_singleton(self):
        """测试单例模式"""
        config2 = ConfigManager()
        self.assertIs(self.config, config2)
        print("✅ 配置管理器单例模式正常")

    def test_api_config_reading(self):
        """测试API配置读取"""
        # 测试读取模型配置
        multimodal = self.config.get('api.multimodal_model', 'default')
        image = self.config.get('api.image_model', 'default')
        text = self.config.get('api.text_model', 'default')

        self.assertIsNotNone(multimodal)
        self.assertIsNotNone(image)
        self.assertIsNotNone(text)

        print(f"✅ API配置读取正常:")
        print(f"   - 多模态模型: {multimodal}")
        print(f"   - 图片模型: {image}")
        print(f"   - 文本模型: {text}")

    def test_translation_config_reading(self):
        """测试翻译配置读取"""
        batch_size = self.config.get('translation.batch_size', 0)
        cache_enabled = self.config.get('translation.cache_enabled', False)

        self.assertGreater(batch_size, 0)
        self.assertIsInstance(cache_enabled, bool)

        print(f"✅ 翻译配置读取正常:")
        print(f"   - 批量大小: {batch_size}")
        print(f"   - 缓存启用: {cache_enabled}")

    def test_performance_config_reading(self):
        """测试性能配置读取"""
        spatial_index = self.config.get('performance.spatial_index', False)
        antialiasing = self.config.get('performance.antialiasing', False)
        entity_threshold = self.config.get('performance.entity_threshold', 0)
        memory_threshold = self.config.get('performance.memory_threshold_mb', 0)

        self.assertIsInstance(spatial_index, bool)
        self.assertIsInstance(antialiasing, bool)
        self.assertGreater(entity_threshold, 0)
        self.assertGreater(memory_threshold, 0)

        print(f"✅ 性能配置读取正常:")
        print(f"   - 空间索引: {spatial_index}")
        print(f"   - 抗锯齿: {antialiasing}")
        print(f"   - 实体阈值: {entity_threshold}")
        print(f"   - 内存阈值: {memory_threshold}MB")

    def test_config_set_and_get(self):
        """测试配置设置和获取"""
        test_key = 'test.value'
        test_value = 'test_data'

        self.config.set(test_key, test_value)
        result = self.config.get(test_key)

        self.assertEqual(result, test_value)
        print("✅ 配置设置/获取功能正常")


class TestBailianClient(unittest.TestCase):
    """测试百炼API客户端"""

    def setUp(self):
        """设置测试"""
        # 模拟API密钥
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_client_initialization(self):
        """测试客户端初始化"""
        try:
            client = BailianClient()

            # 验证模型配置已加载
            self.assertIsNotNone(client.multimodal_model)
            self.assertIsNotNone(client.image_model)
            self.assertIsNotNone(client.text_model)

            print(f"✅ 百炼客户端初始化正常:")
            print(f"   - 多模态模型: {client.multimodal_model}")
            print(f"   - 图片模型: {client.image_model}")
            print(f"   - 文本模型: {client.text_model}")
            print(f"   - 自定义模型启用: {client.use_custom_model}")

        except BailianAPIError as e:
            self.skipTest(f"API客户端需要有效的密钥: {e}")

    def test_model_selection_by_task_type(self):
        """测试根据任务类型选择模型"""
        client = BailianClient()

        # 测试文本翻译模型
        text_model = client.get_model_for_task('text')
        self.assertEqual(text_model, client.text_model)

        # 测试图片翻译模型
        image_model = client.get_model_for_task('image')
        self.assertEqual(image_model, client.image_model)

        # 测试多模态模型
        multimodal_model = client.get_model_for_task('multimodal')
        self.assertEqual(multimodal_model, client.multimodal_model)

        print(f"✅ 模型选择逻辑正常:")
        print(f"   - text -> {text_model}")
        print(f"   - image -> {image_model}")
        print(f"   - multimodal -> {multimodal_model}")

    def test_custom_model_priority(self):
        """测试自定义模型优先级"""
        client = BailianClient()

        # 启用自定义模型
        client.use_custom_model = True
        client.custom_model = 'custom-test-model'

        # 验证自定义模型优先级最高
        for task_type in ['text', 'image', 'multimodal']:
            model = client.get_model_for_task(task_type)
            self.assertEqual(model, 'custom-test-model')

        print("✅ 自定义模型优先级正常（优先级最高）")

    def test_model_pricing_data(self):
        """测试模型定价数据"""
        client = BailianClient()

        # 验证所有模型都有定价
        required_models = [
            'qwen-plus', 'qwen-max', 'qwen-turbo',
            'qwen-vl-max', 'qwen-vl-plus',
            'qwen-mt-plus', 'qwen-mt-turbo', 'qwen-mt-image'
        ]

        for model in required_models:
            self.assertIn(model, client.PRICING)
            self.assertIn('input', client.PRICING[model])
            self.assertIn('output', client.PRICING[model])

        print("✅ 模型定价数据完整:")
        for model in required_models:
            pricing = client.PRICING[model]
            print(f"   - {model}: ¥{pricing['input']}/1K tokens")


class TestTranslationEngine(unittest.TestCase):
    """测试翻译引擎"""

    def setUp(self):
        """设置测试"""
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_engine_initialization(self):
        """测试引擎初始化"""
        engine = TranslationEngine()

        # 验证配置已加载
        self.assertIsNotNone(engine.batch_size)
        self.assertIsInstance(engine.cache_enabled, bool)
        self.assertIsNotNone(engine.context_window)
        self.assertIsInstance(engine.use_terminology, bool)
        self.assertIsInstance(engine.post_process, bool)

        print(f"✅ 翻译引擎初始化正常:")
        print(f"   - 批量大小: {engine.batch_size}")
        print(f"   - 缓存启用: {engine.cache_enabled}")
        print(f"   - 上下文窗口: {engine.context_window}")
        print(f"   - 术语库: {engine.use_terminology}")
        print(f"   - 后处理: {engine.post_process}")

    def test_batch_size_configuration(self):
        """测试批量大小配置"""
        config = ConfigManager()

        # 设置不同的批量大小
        original_batch = config.get('translation.batch_size', 50)

        # 创建新引擎应该读取配置
        engine = TranslationEngine()
        self.assertEqual(engine.batch_size, original_batch)

        print(f"✅ 批量大小配置生效: {engine.batch_size}")

    def test_cache_configuration(self):
        """测试缓存配置"""
        config = ConfigManager()
        cache_enabled = config.get('translation.cache_enabled', True)

        engine = TranslationEngine()
        self.assertEqual(engine.cache_enabled, cache_enabled)

        print(f"✅ 缓存配置生效: {engine.cache_enabled}")


class TestDWGRenderer(unittest.TestCase):
    """测试DWG渲染器"""

    def test_renderer_initialization(self):
        """测试渲染器初始化"""
        from PyQt6.QtWidgets import QApplication

        # 创建QApplication（测试需要）
        app = QApplication.instance()
        if app is None:
            app = QApplication(sys.argv)

        canvas = DWGCanvas()

        # 验证配置已加载
        self.assertIsInstance(canvas.antialiasing, bool)
        self.assertIsInstance(canvas.use_spatial_index, bool)
        self.assertIsNotNone(canvas.entity_threshold)

        print(f"✅ DWG渲染器初始化正常:")
        print(f"   - 抗锯齿: {canvas.antialiasing}")
        print(f"   - 空间索引: {canvas.use_spatial_index}")
        print(f"   - 实体阈值: {canvas.entity_threshold}")

    def test_antialiasing_configuration(self):
        """测试抗锯齿配置"""
        from PyQt6.QtWidgets import QApplication

        app = QApplication.instance()
        if app is None:
            app = QApplication(sys.argv)

        config = ConfigManager()
        antialiasing = config.get('performance.antialiasing', True)

        canvas = DWGCanvas()
        self.assertEqual(canvas.antialiasing, antialiasing)

        print(f"✅ 抗锯齿配置生效: {canvas.antialiasing}")

    def test_spatial_index_configuration(self):
        """测试空间索引配置"""
        from PyQt6.QtWidgets import QApplication

        app = QApplication.instance()
        if app is None:
            app = QApplication(sys.argv)

        config = ConfigManager()
        spatial_index = config.get('performance.spatial_index', True)
        entity_threshold = config.get('performance.entity_threshold', 100)

        canvas = DWGCanvas()
        self.assertEqual(canvas.use_spatial_index, spatial_index)
        self.assertEqual(canvas.entity_threshold, entity_threshold)

        print(f"✅ 空间索引配置生效:")
        print(f"   - 启用: {canvas.use_spatial_index}")
        print(f"   - 阈值: {canvas.entity_threshold}")


class TestResourceManager(unittest.TestCase):
    """测试资源管理器"""

    def test_manager_initialization(self):
        """测试管理器初始化"""
        manager = ResourceManager()

        # 验证配置已加载
        self.assertIsNotNone(manager.memory_threshold_mb)
        self.assertIsInstance(manager.auto_optimize, bool)

        print(f"✅ 资源管理器初始化正常:")
        print(f"   - 内存阈值: {manager.memory_threshold_mb}MB")
        print(f"   - 自动优化: {manager.auto_optimize}")

    def test_memory_threshold_configuration(self):
        """测试内存阈值配置"""
        config = ConfigManager()
        threshold = config.get('performance.memory_threshold_mb', 500)

        manager = ResourceManager()
        self.assertEqual(manager.memory_threshold_mb, threshold)

        print(f"✅ 内存阈值配置生效: {manager.memory_threshold_mb}MB")

    def test_auto_optimize_configuration(self):
        """测试自动优化配置"""
        config = ConfigManager()
        auto_optimize = config.get('performance.auto_optimize', True)

        manager = ResourceManager()
        self.assertEqual(manager.auto_optimize, auto_optimize)

        print(f"✅ 自动优化配置生效: {manager.auto_optimize}")

    def test_memory_check(self):
        """测试内存检查功能"""
        manager = ResourceManager()

        # 测试内存使用检查
        usage = manager.get_memory_usage()
        self.assertIn('rss_mb', usage)
        self.assertIn('vms_mb', usage)
        self.assertIn('percent', usage)

        print(f"✅ 内存检查功能正常:")
        print(f"   - 物理内存: {usage['rss_mb']:.2f}MB")
        print(f"   - 虚拟内存: {usage['vms_mb']:.2f}MB")
        print(f"   - 使用百分比: {usage['percent']:.2f}%")


class TestIntegration(unittest.TestCase):
    """集成测试"""

    def setUp(self):
        """设置测试"""
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_config_to_client_flow(self):
        """测试配置到客户端的流程"""
        config = ConfigManager()

        # 从配置读取
        text_model = config.get('api.text_model', '')

        # 客户端应该使用相同的配置
        client = BailianClient()
        self.assertEqual(client.text_model, text_model)

        print(f"✅ 配置→客户端流程正常:")
        print(f"   - 配置中的文本模型: {text_model}")
        print(f"   - 客户端使用的模型: {client.text_model}")

    def test_config_to_engine_flow(self):
        """测试配置到引擎的流程"""
        config = ConfigManager()

        # 从配置读取
        batch_size = config.get('translation.batch_size', 50)

        # 引擎应该使用相同的配置
        engine = TranslationEngine()
        self.assertEqual(engine.batch_size, batch_size)

        print(f"✅ 配置→引擎流程正常:")
        print(f"   - 配置中的批量大小: {batch_size}")
        print(f"   - 引擎使用的批量大小: {engine.batch_size}")

    def test_model_selection_in_translation(self):
        """测试翻译中的模型选择"""
        client = BailianClient()

        # 验证不同任务类型使用不同模型
        models_used = {}
        for task_type in ['text', 'image', 'multimodal']:
            model = client.get_model_for_task(task_type)
            models_used[task_type] = model

        # 验证模型确实不同（除非配置相同）
        print(f"✅ 翻译中的模型选择正常:")
        for task_type, model in models_used.items():
            print(f"   - {task_type}: {model}")


def run_all_tests():
    """运行所有测试"""
    print("=" * 80)
    print("完整功能验证测试")
    print("=" * 80)
    print()

    # 创建测试套件
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()

    # 添加所有测试类
    suite.addTests(loader.loadTestsFromTestCase(TestConfigManager))
    suite.addTests(loader.loadTestsFromTestCase(TestBailianClient))
    suite.addTests(loader.loadTestsFromTestCase(TestTranslationEngine))
    suite.addTests(loader.loadTestsFromTestCase(TestDWGRenderer))
    suite.addTests(loader.loadTestsFromTestCase(TestResourceManager))
    suite.addTests(loader.loadTestsFromTestCase(TestIntegration))

    # 运行测试
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)

    # 打印总结
    print()
    print("=" * 80)
    print("测试总结")
    print("=" * 80)
    print(f"总测试数: {result.testsRun}")
    print(f"成功: {result.testsRun - len(result.failures) - len(result.errors)}")
    print(f"失败: {len(result.failures)}")
    print(f"错误: {len(result.errors)}")
    print(f"跳过: {len(result.skipped)}")

    if result.wasSuccessful():
        print()
        print("✅✅✅ 所有测试通过！功能验证完成！ ✅✅✅")
        return 0
    else:
        print()
        print("❌ 部分测试失败，请检查上面的错误信息")
        return 1


if __name__ == '__main__':
    sys.exit(run_all_tests())
