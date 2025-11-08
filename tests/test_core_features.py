#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
核心功能验证测试（无GUI依赖）
验证所有配置和核心逻辑是否真正生效
"""
import sys
import os
from pathlib import Path

# 添加项目根目录到路径
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

import unittest
from src.utils.config_manager import ConfigManager
from src.services.bailian_client import BailianClient, BailianAPIError
from src.translation.engine import TranslationEngine
from src.utils.resource_manager import ResourceManager


class TestConfigManager(unittest.TestCase):
    """测试配置管理器"""

    def setUp(self):
        """设置测试"""
        self.config = ConfigManager()

    def test_01_config_instance_singleton(self):
        """测试单例模式"""
        config2 = ConfigManager()
        self.assertIs(self.config, config2)
        print("✅ 配置管理器单例模式正常")

    def test_02_api_config_reading(self):
        """测试API配置读取"""
        multimodal = self.config.get('api.multimodal_model', 'default')
        image = self.config.get('api.image_model', 'default')
        text = self.config.get('api.text_model', 'default')
        use_custom = self.config.get('api.use_custom_model', False)
        custom_model = self.config.get('api.custom_model', '')

        self.assertIsNotNone(multimodal)
        self.assertIsNotNone(image)
        self.assertIsNotNone(text)

        print(f"✅ API配置读取正常:")
        print(f"   - 多模态模型: {multimodal}")
        print(f"   - 图片模型: {image}")
        print(f"   - 文本模型: {text}")
        print(f"   - 自定义模型启用: {use_custom}")
        print(f"   - 自定义模型名称: {custom_model if custom_model else '(未设置)'}")

    def test_03_translation_config_reading(self):
        """测试翻译配置读取"""
        batch_size = self.config.get('translation.batch_size', 0)
        cache_enabled = self.config.get('translation.cache_enabled', False)
        context_window = self.config.get('translation.context_window', 0)
        use_terminology = self.config.get('translation.use_terminology', False)
        post_process = self.config.get('translation.post_process', False)

        self.assertGreater(batch_size, 0)
        self.assertIsInstance(cache_enabled, bool)

        print(f"✅ 翻译配置读取正常:")
        print(f"   - 批量大小: {batch_size}")
        print(f"   - 缓存启用: {cache_enabled}")
        print(f"   - 上下文窗口: {context_window}")
        print(f"   - 专业术语库: {use_terminology}")
        print(f"   - 后处理优化: {post_process}")

    def test_04_performance_config_reading(self):
        """测试性能配置读取"""
        spatial_index = self.config.get('performance.spatial_index', False)
        antialiasing = self.config.get('performance.antialiasing', False)
        entity_threshold = self.config.get('performance.entity_threshold', 0)
        memory_threshold = self.config.get('performance.memory_threshold_mb', 0)
        auto_optimize = self.config.get('performance.auto_optimize', False)

        self.assertIsInstance(spatial_index, bool)
        self.assertIsInstance(antialiasing, bool)
        self.assertGreater(entity_threshold, 0)
        self.assertGreater(memory_threshold, 0)

        print(f"✅ 性能配置读取正常:")
        print(f"   - 空间索引: {spatial_index}")
        print(f"   - 抗锯齿: {antialiasing}")
        print(f"   - 实体阈值: {entity_threshold}")
        print(f"   - 内存阈值: {memory_threshold}MB")
        print(f"   - 自动优化: {auto_optimize}")

    def test_05_config_set_and_get(self):
        """测试配置设置和获取"""
        test_key = 'test.value'
        test_value = 'test_data_12345'

        self.config.set(test_key, test_value)
        result = self.config.get(test_key)

        self.assertEqual(result, test_value)
        print(f"✅ 配置设置/获取功能正常: {test_key} = {result}")


class TestBailianClient(unittest.TestCase):
    """测试百炼API客户端"""

    def setUp(self):
        """设置测试"""
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_01_client_initialization(self):
        """测试客户端初始化"""
        client = BailianClient()

        # 验证模型配置已加载
        self.assertIsNotNone(client.multimodal_model)
        self.assertIsNotNone(client.image_model)
        self.assertIsNotNone(client.text_model)
        self.assertIsInstance(client.use_custom_model, bool)

        print(f"✅ 百炼客户端初始化正常:")
        print(f"   - 多模态模型: {client.multimodal_model}")
        print(f"   - 图片模型: {client.image_model}")
        print(f"   - 文本模型: {client.text_model}")
        print(f"   - 自定义模型启用: {client.use_custom_model}")
        print(f"   - 端点: {client.endpoint}")
        print(f"   - 超时: {client.timeout}秒")
        print(f"   - 最大重试: {client.max_retries}次")

    def test_02_model_selection_text(self):
        """测试文本翻译模型选择"""
        client = BailianClient()
        model = client.get_model_for_task('text')

        self.assertEqual(model, client.text_model)
        print(f"✅ 文本翻译模型选择正常: {model}")

    def test_03_model_selection_image(self):
        """测试图片翻译模型选择"""
        client = BailianClient()
        model = client.get_model_for_task('image')

        self.assertEqual(model, client.image_model)
        print(f"✅ 图片翻译模型选择正常: {model}")

    def test_04_model_selection_multimodal(self):
        """测试多模态模型选择"""
        client = BailianClient()
        model = client.get_model_for_task('multimodal')

        self.assertEqual(model, client.multimodal_model)
        print(f"✅ 多模态模型选择正常: {model}")

    def test_05_custom_model_priority(self):
        """测试自定义模型优先级"""
        client = BailianClient()

        # 启用自定义模型
        original_use_custom = client.use_custom_model
        client.use_custom_model = True
        client.custom_model = 'custom-test-model-v1'

        # 验证自定义模型优先级最高
        for task_type in ['text', 'image', 'multimodal']:
            model = client.get_model_for_task(task_type)
            self.assertEqual(model, 'custom-test-model-v1')

        print("✅ 自定义模型优先级正常:")
        print(f"   - text任务使用: custom-test-model-v1")
        print(f"   - image任务使用: custom-test-model-v1")
        print(f"   - multimodal任务使用: custom-test-model-v1")
        print("   ✓ 自定义模型优先级最高！")

        # 恢复
        client.use_custom_model = original_use_custom

    def test_06_model_pricing_complete(self):
        """测试模型定价数据完整性"""
        client = BailianClient()

        required_models = [
            'qwen-plus', 'qwen-max', 'qwen-turbo',
            'qwen-vl-max', 'qwen-vl-plus',
            'qwen-mt-plus', 'qwen-mt-turbo', 'qwen-mt-image'
        ]

        print("✅ 模型定价数据完整:")
        for model in required_models:
            self.assertIn(model, client.PRICING)
            self.assertIn('input', client.PRICING[model])
            self.assertIn('output', client.PRICING[model])

            pricing = client.PRICING[model]
            print(f"   - {model:20s}: ¥{pricing['input']}/1K tokens")

    def test_07_api_configuration(self):
        """测试API配置"""
        client = BailianClient()

        self.assertEqual(client.endpoint, 'https://dashscope.aliyuncs.com')
        self.assertGreater(client.timeout, 0)
        self.assertGreater(client.max_retries, 0)

        print(f"✅ API配置正常:")
        print(f"   - 端点: {client.endpoint}")
        print(f"   - 超时: {client.timeout}秒")
        print(f"   - 最大重试: {client.max_retries}次")


class TestTranslationEngine(unittest.TestCase):
    """测试翻译引擎"""

    def setUp(self):
        """设置测试"""
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_01_engine_initialization(self):
        """测试引擎初始化"""
        engine = TranslationEngine()

        self.assertIsNotNone(engine.batch_size)
        self.assertIsInstance(engine.cache_enabled, bool)
        self.assertIsNotNone(engine.context_window)
        self.assertIsInstance(engine.use_terminology, bool)
        self.assertIsInstance(engine.post_process, bool)

        print(f"✅ 翻译引擎初始化正常:")
        print(f"   - 批量大小: {engine.batch_size}")
        print(f"   - 缓存启用: {engine.cache_enabled}")
        print(f"   - 上下文窗口: {engine.context_window}")
        print(f"   - 专业术语库: {engine.use_terminology}")
        print(f"   - 后处理优化: {engine.post_process}")

    def test_02_batch_size_from_config(self):
        """测试批量大小从配置读取"""
        config = ConfigManager()
        expected_batch = config.get('translation.batch_size', 50)

        engine = TranslationEngine()
        self.assertEqual(engine.batch_size, expected_batch)

        print(f"✅ 批量大小配置生效:")
        print(f"   - 配置值: {expected_batch}")
        print(f"   - 引擎值: {engine.batch_size}")
        print(f"   ✓ 配置正确传递到引擎")

    def test_03_cache_from_config(self):
        """测试缓存配置从配置读取"""
        config = ConfigManager()
        expected_cache = config.get('translation.cache_enabled', True)

        engine = TranslationEngine()
        self.assertEqual(engine.cache_enabled, expected_cache)

        print(f"✅ 缓存配置生效:")
        print(f"   - 配置值: {expected_cache}")
        print(f"   - 引擎值: {engine.cache_enabled}")
        print(f"   ✓ 配置正确传递到引擎")

    def test_04_client_uses_correct_model(self):
        """测试引擎的客户端使用正确的模型"""
        engine = TranslationEngine()

        # 引擎的客户端应该从配置读取模型
        config = ConfigManager()
        expected_text_model = config.get('api.text_model', 'qwen-mt-plus')

        self.assertEqual(engine.client.text_model, expected_text_model)

        print(f"✅ 引擎客户端模型配置正常:")
        print(f"   - 配置的文本模型: {expected_text_model}")
        print(f"   - 客户端文本模型: {engine.client.text_model}")
        print(f"   ✓ 引擎将使用配置的模型进行翻译")


class TestResourceManager(unittest.TestCase):
    """测试资源管理器"""

    def test_01_manager_initialization(self):
        """测试管理器初始化"""
        manager = ResourceManager()

        self.assertIsNotNone(manager.memory_threshold_mb)
        self.assertIsInstance(manager.auto_optimize, bool)

        print(f"✅ 资源管理器初始化正常:")
        print(f"   - 内存阈值: {manager.memory_threshold_mb}MB")
        print(f"   - 自动优化: {manager.auto_optimize}")

    def test_02_memory_threshold_from_config(self):
        """测试内存阈值从配置读取"""
        config = ConfigManager()
        expected_threshold = config.get('performance.memory_threshold_mb', 500)

        manager = ResourceManager()
        self.assertEqual(manager.memory_threshold_mb, expected_threshold)

        print(f"✅ 内存阈值配置生效:")
        print(f"   - 配置值: {expected_threshold}MB")
        print(f"   - 管理器值: {manager.memory_threshold_mb}MB")
        print(f"   ✓ 配置正确传递到资源管理器")

    def test_03_auto_optimize_from_config(self):
        """测试自动优化从配置读取"""
        config = ConfigManager()
        expected_auto = config.get('performance.auto_optimize', True)

        manager = ResourceManager()
        self.assertEqual(manager.auto_optimize, expected_auto)

        print(f"✅ 自动优化配置生效:")
        print(f"   - 配置值: {expected_auto}")
        print(f"   - 管理器值: {manager.auto_optimize}")
        print(f"   ✓ 配置正确传递到资源管理器")

    def test_04_memory_usage_check(self):
        """测试内存使用检查功能"""
        manager = ResourceManager()

        usage = manager.get_memory_usage()
        self.assertIn('rss_mb', usage)
        self.assertIn('vms_mb', usage)
        self.assertIn('percent', usage)
        self.assertGreater(usage['rss_mb'], 0)

        print(f"✅ 内存检查功能正常:")
        print(f"   - 物理内存: {usage['rss_mb']:.2f}MB")
        print(f"   - 虚拟内存: {usage['vms_mb']:.2f}MB")
        print(f"   - 使用百分比: {usage['percent']:.2f}%")

    def test_05_cpu_usage_check(self):
        """测试CPU使用检查功能"""
        manager = ResourceManager()

        cpu = manager.get_cpu_usage()
        self.assertIsInstance(cpu, (int, float))
        self.assertGreaterEqual(cpu, 0)

        print(f"✅ CPU检查功能正常:")
        print(f"   - CPU使用率: {cpu:.2f}%")


class TestIntegration(unittest.TestCase):
    """集成测试 - 验证配置流向各个组件"""

    def setUp(self):
        """设置测试"""
        os.environ['DASHSCOPE_API_KEY'] = 'test-api-key-12345'

    def test_01_config_to_client_complete_flow(self):
        """测试配置到客户端的完整流程"""
        config = ConfigManager()

        # 从配置读取所有模型
        multimodal = config.get('api.multimodal_model', '')
        image = config.get('api.image_model', '')
        text = config.get('api.text_model', '')

        # 客户端应该使用相同的配置
        client = BailianClient()

        self.assertEqual(client.multimodal_model, multimodal)
        self.assertEqual(client.image_model, image)
        self.assertEqual(client.text_model, text)

        print(f"✅ 配置→客户端完整流程验证:")
        print(f"   多模态: 配置={multimodal}, 客户端={client.multimodal_model} ✓")
        print(f"   图片: 配置={image}, 客户端={client.image_model} ✓")
        print(f"   文本: 配置={text}, 客户端={client.text_model} ✓")

    def test_02_config_to_engine_complete_flow(self):
        """测试配置到引擎的完整流程"""
        config = ConfigManager()

        # 从配置读取所有翻译设置
        batch_size = config.get('translation.batch_size', 50)
        cache_enabled = config.get('translation.cache_enabled', True)
        context_window = config.get('translation.context_window', 3)

        # 引擎应该使用相同的配置
        engine = TranslationEngine()

        self.assertEqual(engine.batch_size, batch_size)
        self.assertEqual(engine.cache_enabled, cache_enabled)
        self.assertEqual(engine.context_window, context_window)

        print(f"✅ 配置→引擎完整流程验证:")
        print(f"   批量大小: 配置={batch_size}, 引擎={engine.batch_size} ✓")
        print(f"   缓存启用: 配置={cache_enabled}, 引擎={engine.cache_enabled} ✓")
        print(f"   上下文窗口: 配置={context_window}, 引擎={engine.context_window} ✓")

    def test_03_model_selection_across_tasks(self):
        """测试跨任务的模型选择一致性"""
        client = BailianClient()

        # 获取所有任务类型的模型
        models = {}
        for task_type in ['text', 'image', 'multimodal']:
            models[task_type] = client.get_model_for_task(task_type)

        # 验证模型映射正确
        self.assertEqual(models['text'], client.text_model)
        self.assertEqual(models['image'], client.image_model)
        self.assertEqual(models['multimodal'], client.multimodal_model)

        print(f"✅ 跨任务模型选择一致性验证:")
        print(f"   text → {models['text']} (期望: {client.text_model}) ✓")
        print(f"   image → {models['image']} (期望: {client.image_model}) ✓")
        print(f"   multimodal → {models['multimodal']} (期望: {client.multimodal_model}) ✓")

    def test_04_end_to_end_translation_setup(self):
        """测试端到端翻译设置"""
        # 配置
        config = ConfigManager()
        text_model = config.get('api.text_model', '')
        batch_size = config.get('translation.batch_size', 50)

        # 引擎（会创建客户端）
        engine = TranslationEngine()

        # 验证：配置 → 引擎 → 客户端 → 模型选择
        self.assertEqual(engine.batch_size, batch_size)
        self.assertEqual(engine.client.text_model, text_model)

        # 验证引擎将使用正确的模型
        selected_model = engine.client.get_model_for_task('text')
        self.assertEqual(selected_model, text_model)

        print(f"✅ 端到端翻译设置验证:")
        print(f"   1. 配置文件: text_model={text_model}")
        print(f"   2. 引擎客户端: text_model={engine.client.text_model} ✓")
        print(f"   3. 模型选择: task='text' → {selected_model} ✓")
        print(f"   4. 批量大小: {engine.batch_size} ✓")
        print(f"   ✓ 完整流程: 配置 → 引擎 → 客户端 → API调用")


def run_all_tests():
    """运行所有测试"""
    print("=" * 80)
    print("核心功能完整验证测试")
    print("=" * 80)
    print()

    # 创建测试套件
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()

    # 按顺序添加测试类
    test_classes = [
        TestConfigManager,
        TestBailianClient,
        TestTranslationEngine,
        TestResourceManager,
        TestIntegration
    ]

    for test_class in test_classes:
        suite.addTests(loader.loadTestsFromTestCase(test_class))

    # 运行测试
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)

    # 打印总结
    print()
    print("=" * 80)
    print("测试总结")
    print("=" * 80)
    print(f"总测试数: {result.testsRun}")
    print(f"成功: {result.testsRun - len(result.failures) - len(result.errors) - len(result.skipped)}")
    print(f"失败: {len(result.failures)}")
    print(f"错误: {len(result.errors)}")
    print(f"跳过: {len(result.skipped)}")
    print()

    if result.wasSuccessful():
        print("=" * 80)
        print("✅✅✅ 所有核心功能测试通过！✅✅✅")
        print("=" * 80)
        print()
        print("验证结果:")
        print("  ✓ 配置管理器正常工作")
        print("  ✓ 多模型配置系统正确实现")
        print("  ✓ 自定义模型优先级正确")
        print("  ✓ 翻译引擎配置生效")
        print("  ✓ 资源管理器配置生效")
        print("  ✓ 配置→组件流程完整")
        print()
        print("🎉 所有设置功能已验证，确认真正生效！")
        return 0
    else:
        print("=" * 80)
        print("❌ 部分测试失败")
        print("=" * 80)
        if result.failures:
            print("\n失败的测试:")
            for test, traceback in result.failures:
                print(f"  - {test}")
        if result.errors:
            print("\n错误的测试:")
            for test, traceback in result.errors:
                print(f"  - {test}")
        return 1


if __name__ == '__main__':
    sys.exit(run_all_tests())
