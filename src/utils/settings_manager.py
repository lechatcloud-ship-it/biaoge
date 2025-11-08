# -*- coding: utf-8 -*-
"""
设置管理器
用于UI层面的设置持久化（API密钥、模型选择等）
"""
from typing import Optional
from .config_manager import ConfigManager
from .logger import logger


class SettingsManager:
    """
    设置管理器

    负责：
    1. 保存用户在UI中的设置（API密钥、模型选择等）
    2. 持久化到用户配置文件 ~/.biaoge/config.toml
    3. 确保下次启动时自动加载用户设置
    """

    def __init__(self):
        self.config = ConfigManager()

    # ============================================================
    # API设置
    # ============================================================

    def save_api_key(self, api_key: str) -> bool:
        """
        保存API密钥

        Args:
            api_key: API密钥

        Returns:
            是否保存成功
        """
        try:
            self.config.set('api.api_key', api_key)
            self.config.save()
            logger.info("API密钥已保存")
            return True
        except Exception as e:
            logger.error(f"保存API密钥失败: {e}")
            return False

    def get_api_key(self) -> str:
        """
        获取API密钥

        优先级：
        1. 用户配置文件 (~/.biaoge/config.toml)
        2. 环境变量 DASHSCOPE_API_KEY
        3. 默认配置文件 (src/config/default.toml)

        Returns:
            API密钥
        """
        import os

        # 优先从配置文件读取
        api_key = self.config.get('api.api_key', '')

        # 如果配置文件没有，尝试从环境变量读取
        if not api_key:
            api_key = os.getenv('DASHSCOPE_API_KEY', '')

        return api_key

    # ============================================================
    # 模型设置
    # ============================================================

    def save_text_model(self, model_name: str) -> bool:
        """
        保存文本翻译模型

        Args:
            model_name: 模型名称（如 qwen-mt-plus, qwen-mt-turbo）

        Returns:
            是否保存成功
        """
        try:
            self.config.set('api.text_model', model_name)
            self.config.save()
            logger.info(f"文本翻译模型已保存: {model_name}")
            return True
        except Exception as e:
            logger.error(f"保存文本模型失败: {e}")
            return False

    def get_text_model(self) -> str:
        """
        获取文本翻译模型

        Returns:
            模型名称
        """
        return self.config.get('api.text_model', 'qwen-mt-plus')

    def save_custom_model(self, model_name: str, enabled: bool = True) -> bool:
        """
        保存自定义模型

        Args:
            model_name: 自定义模型名称
            enabled: 是否启用自定义模型

        Returns:
            是否保存成功
        """
        try:
            self.config.set('api.use_custom_model', enabled)
            self.config.set('api.custom_model', model_name)
            self.config.save()
            logger.info(f"自定义模型已保存: {model_name} (启用: {enabled})")
            return True
        except Exception as e:
            logger.error(f"保存自定义模型失败: {e}")
            return False

    def get_custom_model(self) -> tuple[bool, str]:
        """
        获取自定义模型设置

        Returns:
            (是否启用, 模型名称)
        """
        enabled = self.config.get('api.use_custom_model', False)
        model_name = self.config.get('api.custom_model', '')
        return enabled, model_name

    # ============================================================
    # 翻译设置
    # ============================================================

    def save_translation_settings(
        self,
        source_lang: str = "auto",
        target_lang: str = "zh-CN",
        use_terminology: bool = True,
        use_cache: bool = True
    ) -> bool:
        """
        保存翻译设置

        Args:
            source_lang: 源语言（auto=自动检测）
            target_lang: 目标语言
            use_terminology: 是否使用术语库
            use_cache: 是否使用翻译缓存

        Returns:
            是否保存成功
        """
        try:
            self.config.set('translation.default_source_lang', source_lang)
            self.config.set('translation.default_target_lang', target_lang)
            self.config.set('translation.use_terminology', use_terminology)
            self.config.set('translation.cache_enabled', use_cache)
            self.config.save()
            logger.info(f"翻译设置已保存: {source_lang} → {target_lang}")
            return True
        except Exception as e:
            logger.error(f"保存翻译设置失败: {e}")
            return False

    def get_translation_settings(self) -> dict:
        """
        获取翻译设置

        Returns:
            翻译设置字典
        """
        return {
            'source_lang': self.config.get('translation.default_source_lang', 'auto'),
            'target_lang': self.config.get('translation.default_target_lang', 'zh-CN'),
            'use_terminology': self.config.get('translation.use_terminology', True),
            'use_cache': self.config.get('translation.cache_enabled', True),
        }

    # ============================================================
    # UI设置
    # ============================================================

    def save_ui_settings(
        self,
        theme: int = 0,
        font_family: str = "微软雅黑",
        font_size: int = 9,
        start_maximized: bool = False
    ) -> bool:
        """
        保存UI设置

        Args:
            theme: 主题（0=亮色, 1=暗色, 2=系统, 3=蓝色, 4=绿色）
            font_family: 字体
            font_size: 字号
            start_maximized: 是否启动时最大化

        Returns:
            是否保存成功
        """
        try:
            self.config.set('ui.theme', theme)
            self.config.set('ui.font_family', font_family)
            self.config.set('ui.font_size', font_size)
            self.config.set('ui.start_maximized', start_maximized)
            self.config.save()
            logger.info("UI设置已保存")
            return True
        except Exception as e:
            logger.error(f"保存UI设置失败: {e}")
            return False

    def get_ui_settings(self) -> dict:
        """
        获取UI设置

        Returns:
            UI设置字典
        """
        return {
            'theme': self.config.get('ui.theme', 0),
            'font_family': self.config.get('ui.font_family', '微软雅黑'),
            'font_size': self.config.get('ui.font_size', 9),
            'start_maximized': self.config.get('ui.start_maximized', False),
        }

    # ============================================================
    # API连通性测试
    # ============================================================

    def test_api_connection(self, api_key: Optional[str] = None, model: Optional[str] = None) -> tuple[bool, str]:
        """
        测试API连通性

        Args:
            api_key: API密钥（如果为None则使用已保存的）
            model: 模型名称（如果为None则使用text_model）

        Returns:
            (是否成功, 消息)

        Example:
            success, message = settings_manager.test_api_connection(
                api_key="sk-xxxxx",
                model="qwen-mt-plus"
            )
            if success:
                print(f"[是] 连接成功：{message}")
            else:
                print(f"[否] 连接失败：{message}")
        """
        try:
            # 导入BailianClient
            from ..services.bailian_client import BailianClient, BailianAPIError

            # 使用提供的API密钥或已保存的密钥
            test_api_key = api_key or self.get_api_key()
            if not test_api_key:
                return False, "API密钥为空，请先输入API密钥"

            # 使用提供的模型或默认模型
            test_model = model or self.get_text_model()

            # 创建客户端
            logger.info(f"测试API连通性: 模型={test_model}")
            client = BailianClient(api_key=test_api_key, model=test_model)

            # 发送测试请求（翻译一个简单的词）
            result = client.translate_batch(
                texts=["Hello"],
                from_lang="auto",
                to_lang="zh-CN",
                task_type='text'
            )

            if result and len(result) > 0:
                translated = result[0].translated_text
                tokens = result[0].tokens_used
                cost = result[0].cost_estimate

                success_msg = (
                    f"[是] API连接成功\n"
                    f"  模型: {test_model}\n"
                    f"  测试翻译: Hello → {translated}\n"
                    f"  Token消耗: {tokens}\n"
                    f"  预估成本: ¥{cost:.4f}"
                )
                logger.info(f"API测试成功: {test_model}")
                return True, success_msg
            else:
                return False, "API返回结果为空"

        except BailianAPIError as e:
            error_msg = f"API错误: {str(e)}"
            logger.error(f"API测试失败: {error_msg}")
            return False, error_msg
        except Exception as e:
            error_msg = f"连接失败: {str(e)}"
            logger.error(f"API测试失败: {error_msg}")
            return False, error_msg

    # ============================================================
    # 完整设置保存
    # ============================================================

    def save_all_settings(self, settings: dict) -> bool:
        """
        保存所有设置（一次性保存）

        Args:
            settings: 设置字典，支持嵌套键名（如 'api.api_key'）

        Returns:
            是否保存成功

        Example:
            settings_manager.save_all_settings({
                'api.api_key': 'sk-xxxxx',
                'api.text_model': 'qwen-mt-plus',
                'translation.default_source_lang': 'auto',
                'translation.default_target_lang': 'zh-CN',
                'ui.theme': 1
            })
        """
        try:
            for key, value in settings.items():
                self.config.set(key, value)

            self.config.save()
            logger.info(f"所有设置已保存 ({len(settings)} 项)")
            return True
        except Exception as e:
            logger.error(f"保存设置失败: {e}")
            return False

    def reset_to_default(self) -> bool:
        """
        重置为默认设置（删除用户配置文件）

        Returns:
            是否重置成功
        """
        try:
            import os
            if self.config.user_config_path.exists():
                os.remove(self.config.user_config_path)
                logger.info("已重置为默认设置")

            # 重新加载配置
            self.config._load_config()
            return True
        except Exception as e:
            logger.error(f"重置设置失败: {e}")
            return False


# 全局设置管理器实例
settings_manager = SettingsManager()
