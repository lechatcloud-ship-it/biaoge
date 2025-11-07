"""
配置管理器
"""
import os
from pathlib import Path
from typing import Any, Optional
import toml


class ConfigManager:
    """配置管理器"""

    _instance = None

    def __new__(cls):
        """单例模式"""
        if cls._instance is None:
            cls._instance = super().__new__(cls)
        return cls._instance

    def __init__(self):
        if not hasattr(self, 'initialized'):
            self.config_dir = Path(__file__).parent.parent / 'config'
            self.default_config_path = self.config_dir / 'default.toml'
            self.user_config_dir = Path.home() / '.biaoge'
            self.user_config_path = self.user_config_dir / 'config.toml'

            self._config = {}
            self._load_config()
            self.initialized = True

    def _load_config(self):
        """加载配置"""
        # 加载默认配置
        if self.default_config_path.exists():
            with open(self.default_config_path, 'r', encoding='utf-8') as f:
                self._config = toml.load(f)

        # 加载用户配置（覆盖默认配置）
        if self.user_config_path.exists():
            with open(self.user_config_path, 'r', encoding='utf-8') as f:
                user_config = toml.load(f)
                self._deep_update(self._config, user_config)

        # 确保数据目录存在
        self._ensure_dirs()

    def _deep_update(self, base_dict: dict, update_dict: dict):
        """深度更新字典"""
        for key, value in update_dict.items():
            if key in base_dict and isinstance(base_dict[key], dict) and isinstance(value, dict):
                self._deep_update(base_dict[key], value)
            else:
                base_dict[key] = value

    def _ensure_dirs(self):
        """确保必要的目录存在"""
        self.user_config_dir.mkdir(parents=True, exist_ok=True)

        # 创建数据目录
        data_dir = Path(self.get('paths.data_dir', '~/.biaoge')).expanduser()
        data_dir.mkdir(parents=True, exist_ok=True)

        # 创建日志目录
        log_dir = Path(self.get('paths.log_dir', '~/.biaoge/logs')).expanduser()
        log_dir.mkdir(parents=True, exist_ok=True)

    def get(self, key: str, default: Any = None) -> Any:
        """
        获取配置值

        Args:
            key: 配置键，支持点号分隔的路径，如 'api.model'
            default: 默认值

        Returns:
            配置值
        """
        keys = key.split('.')
        value = self._config

        for k in keys:
            if isinstance(value, dict) and k in value:
                value = value[k]
            else:
                return default

        return value

    def set(self, key: str, value: Any):
        """
        设置配置值

        Args:
            key: 配置键
            value: 配置值
        """
        keys = key.split('.')
        config = self._config

        for k in keys[:-1]:
            if k not in config:
                config[k] = {}
            config = config[k]

        config[keys[-1]] = value

    def save(self):
        """保存配置到用户配置文件"""
        self.user_config_dir.mkdir(parents=True, exist_ok=True)

        with open(self.user_config_path, 'w', encoding='utf-8') as f:
            toml.dump(self._config, f)

    @property
    def api_endpoint(self) -> str:
        """API端点"""
        return self.get('api.endpoint', 'https://dashscope.aliyuncs.com/api/v1')

    @property
    def api_model(self) -> str:
        """API模型"""
        return self.get('api.model', 'qwen-plus')

    @property
    def cache_db_path(self) -> Path:
        """缓存数据库路径"""
        path = self.get('paths.cache_db', '~/.biaoge/cache.db')
        return Path(path).expanduser()

    @property
    def log_dir(self) -> Path:
        """日志目录"""
        path = self.get('paths.log_dir', '~/.biaoge/logs')
        return Path(path).expanduser()


# 全局配置实例
config = ConfigManager()
