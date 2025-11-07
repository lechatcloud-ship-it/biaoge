"""错误恢复机制 - 商业级"""
import traceback
from typing import Callable, Any
from functools import wraps
from ..utils.logger import logger

def safe_execute(fallback_value=None, log_error=True):
    """安全执行装饰器"""
    def decorator(func: Callable) -> Callable:
        @wraps(func)
        def wrapper(*args, **kwargs) -> Any:
            try:
                return func(*args, **kwargs)
            except Exception as e:
                if log_error:
                    logger.error(f"{func.__name__} 执行失败: {e}")
                    logger.debug(traceback.format_exc())
                return fallback_value
        return wrapper
    return decorator

def retry(max_attempts=3, delay=1.0, exceptions=(Exception,)):
    """重试装饰器"""
    def decorator(func: Callable) -> Callable:
        @wraps(func)
        def wrapper(*args, **kwargs) -> Any:
            import time
            last_exception = None
            for attempt in range(max_attempts):
                try:
                    return func(*args, **kwargs)
                except exceptions as e:
                    last_exception = e
                    if attempt < max_attempts - 1:
                        logger.warning(f"{func.__name__} 失败 (尝试 {attempt + 1}/{max_attempts}): {e}")
                        time.sleep(delay * (2 ** attempt))
            logger.error(f"{func.__name__} 所有重试失败")
            raise last_exception
        return wrapper
    return decorator
