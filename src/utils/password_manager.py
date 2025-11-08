"""
DWG密码管理器（会话级别）
"""
from typing import Optional, Dict
from pathlib import Path
import hashlib


class PasswordManager:
    """
    DWG文件密码管理器

    特性：
    - 会话级别密码缓存（进程结束后失效）
    - 基于文件路径哈希存储
    - 不持久化到磁盘，保证安全
    """

    def __init__(self):
        """初始化密码管理器"""
        self._passwords: Dict[str, str] = {}  # file_hash -> password

    def _get_file_hash(self, filepath: str | Path) -> str:
        """
        获取文件路径哈希（用作键）

        Args:
            filepath: 文件路径

        Returns:
            文件路径的MD5哈希
        """
        filepath_str = str(Path(filepath).resolve())
        return hashlib.md5(filepath_str.encode('utf-8')).hexdigest()

    def save_password(self, filepath: str | Path, password: str):
        """
        保存密码到缓存

        Args:
            filepath: 文件路径
            password: 密码
        """
        file_hash = self._get_file_hash(filepath)
        self._passwords[file_hash] = password

    def get_password(self, filepath: str | Path) -> Optional[str]:
        """
        获取缓存的密码

        Args:
            filepath: 文件路径

        Returns:
            密码，如果不存在则返回None
        """
        file_hash = self._get_file_hash(filepath)
        return self._passwords.get(file_hash)

    def has_password(self, filepath: str | Path) -> bool:
        """
        检查是否有缓存的密码

        Args:
            filepath: 文件路径

        Returns:
            是否有密码
        """
        return self.get_password(filepath) is not None

    def remove_password(self, filepath: str | Path):
        """
        移除密码缓存

        Args:
            filepath: 文件路径
        """
        file_hash = self._get_file_hash(filepath)
        if file_hash in self._passwords:
            del self._passwords[file_hash]

    def clear(self):
        """清除所有密码缓存"""
        self._passwords.clear()

    def count(self) -> int:
        """获取缓存的密码数量"""
        return len(self._passwords)


# 全局单例
_password_manager = PasswordManager()


def get_password_manager() -> PasswordManager:
    """获取全局密码管理器实例"""
    return _password_manager
