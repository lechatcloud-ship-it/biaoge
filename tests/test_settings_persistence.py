# -*- coding: utf-8 -*-
"""
测试配置持久化功能
验证用户设置能够正确保存和加载
"""
import unittest
import os
import tempfile
import shutil
from pathlib import Path

# 临时修改配置路径用于测试
import sys
sys.path.insert(0, str(Path(__file__).parent.parent / 'src'))

from utils.settings_manager import SettingsManager
from utils.config_manager import ConfigManager


class TestSettingsPersistence(unittest.TestCase):
    """测试设置持久化"""

    def setUp(self):
        """设置测试环境"""
        # 创建临时目录用于测试
        self.temp_dir = tempfile.mkdtemp()
        self.temp_config_path = Path(self.temp_dir) / 'config.toml'

        # 创建新的配置管理器实例（使用临时目录）
        ConfigManager._instance = None
        self.config = ConfigManager()
        self.config.user_config_path = self.temp_config_path
        self.config.user_config_dir = Path(self.temp_dir)

        # 创建设置管理器
        self.settings = SettingsManager()
        self.settings.config = self.config

    def tearDown(self):
        """清理测试环境"""
        # 删除临时目录
        if os.path.exists(self.temp_dir):
            shutil.rmtree(self.temp_dir)

        # 重置单例
        ConfigManager._instance = None

    def test_01_save_and_load_api_key(self):
        """测试保存和加载API密钥"""
        # 保存API密钥
        api_key = "sk-test123456"
        result = self.settings.save_api_key(api_key)
        self.assertTrue(result, "保存API密钥应该成功")

        # 验证配置文件已创建
        self.assertTrue(self.temp_config_path.exists(), "配置文件应该存在")

        # 重新加载配置
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        new_settings = SettingsManager()
        new_settings.config = new_config

        # 验证API密钥已持久化
        loaded_key = new_settings.get_api_key()
        self.assertEqual(loaded_key, api_key, "加载的API密钥应该与保存的一致")

        print(f"✓ API密钥持久化测试通过")
        print(f"  保存: {api_key}")
        print(f"  加载: {loaded_key}")

    def test_02_save_and_load_model(self):
        """测试保存和加载模型选择"""
        # 保存模型选择
        model_name = "qwen-mt-turbo"
        result = self.settings.save_text_model(model_name)
        self.assertTrue(result, "保存模型应该成功")

        # 重新加载配置
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        new_settings = SettingsManager()
        new_settings.config = new_config

        # 验证模型选择已持久化
        loaded_model = new_settings.get_text_model()
        self.assertEqual(loaded_model, model_name, "加载的模型应该与保存的一致")

        print(f"✓ 模型选择持久化测试通过")
        print(f"  保存: {model_name}")
        print(f"  加载: {loaded_model}")

    def test_03_save_all_settings(self):
        """测试一次性保存所有设置"""
        settings = {
            'api.api_key': 'sk-batch-test',
            'api.text_model': 'qwen-mt-plus',
            'translation.default_source_lang': 'auto',
            'translation.default_target_lang': 'zh-CN',
            'ui.theme': 1,
            'ui.font_size': 10,
        }

        # 保存所有设置
        result = self.settings.save_all_settings(settings)
        self.assertTrue(result, "批量保存设置应该成功")

        # 重新加载配置
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        # 验证所有设置已持久化
        self.assertEqual(new_config.get('api.api_key'), 'sk-batch-test')
        self.assertEqual(new_config.get('api.text_model'), 'qwen-mt-plus')
        self.assertEqual(new_config.get('translation.default_source_lang'), 'auto')
        self.assertEqual(new_config.get('translation.default_target_lang'), 'zh-CN')
        self.assertEqual(new_config.get('ui.theme'), 1)
        self.assertEqual(new_config.get('ui.font_size'), 10)

        print(f"✓ 批量设置持久化测试通过")
        print(f"  保存: {len(settings)} 项设置")
        print(f"  验证: 全部通过")

    def test_04_translation_settings(self):
        """测试翻译设置持久化"""
        # 保存翻译设置
        result = self.settings.save_translation_settings(
            source_lang="auto",
            target_lang="zh-CN",
            use_terminology=True,
            use_cache=True
        )
        self.assertTrue(result, "保存翻译设置应该成功")

        # 重新加载
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        new_settings = SettingsManager()
        new_settings.config = new_config

        # 验证翻译设置
        trans_settings = new_settings.get_translation_settings()
        self.assertEqual(trans_settings['source_lang'], 'auto')
        self.assertEqual(trans_settings['target_lang'], 'zh-CN')
        self.assertTrue(trans_settings['use_terminology'])
        self.assertTrue(trans_settings['use_cache'])

        print(f"✓ 翻译设置持久化测试通过")
        print(f"  源语言: {trans_settings['source_lang']}")
        print(f"  目标语言: {trans_settings['target_lang']}")

    def test_05_custom_model(self):
        """测试自定义模型设置"""
        # 保存自定义模型
        custom_model = "my-custom-model-v1"
        result = self.settings.save_custom_model(custom_model, enabled=True)
        self.assertTrue(result, "保存自定义模型应该成功")

        # 重新加载
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        new_settings = SettingsManager()
        new_settings.config = new_config

        # 验证自定义模型
        enabled, model_name = new_settings.get_custom_model()
        self.assertTrue(enabled, "自定义模型应该是启用状态")
        self.assertEqual(model_name, custom_model, "自定义模型名称应该一致")

        print(f"✓ 自定义模型持久化测试通过")
        print(f"  启用: {enabled}")
        print(f"  模型: {model_name}")

    def test_06_ui_settings(self):
        """测试UI设置持久化"""
        # 保存UI设置
        result = self.settings.save_ui_settings(
            theme=2,
            font_family="Arial",
            font_size=12,
            start_maximized=True
        )
        self.assertTrue(result, "保存UI设置应该成功")

        # 重新加载
        ConfigManager._instance = None
        new_config = ConfigManager()
        new_config.user_config_path = self.temp_config_path
        new_config.user_config_dir = Path(self.temp_dir)
        new_config._load_config()

        new_settings = SettingsManager()
        new_settings.config = new_config

        # 验证UI设置
        ui_settings = new_settings.get_ui_settings()
        self.assertEqual(ui_settings['theme'], 2)
        self.assertEqual(ui_settings['font_family'], 'Arial')
        self.assertEqual(ui_settings['font_size'], 12)
        self.assertTrue(ui_settings['start_maximized'])

        print(f"✓ UI设置持久化测试通过")
        print(f"  主题: {ui_settings['theme']}")
        print(f"  字体: {ui_settings['font_family']}, {ui_settings['font_size']}pt")


def run_tests():
    """运行所有测试"""
    loader = unittest.TestLoader()
    suite = unittest.TestSuite()

    suite.addTests(loader.loadTestsFromTestCase(TestSettingsPersistence))

    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)

    # 打印总结
    print("\n" + "="*70)
    print("测试总结")
    print("="*70)
    print(f"运行测试: {result.testsRun}")
    print(f"成功: {result.testsRun - len(result.failures) - len(result.errors)}")
    print(f"失败: {len(result.failures)}")
    print(f"错误: {len(result.errors)}")

    if result.wasSuccessful():
        print("\n✅ 所有测试通过！")
    else:
        print("\n❌ 有测试失败")

    return result.wasSuccessful()


if __name__ == '__main__':
    success = run_tests()
    exit(0 if success else 1)
