"""
AI助手模块
"""
# 先导入非GUI组件
from .ai_assistant import AIAssistant
from .context_manager import ContextManager

# GUI组件延迟导入（避免在无GUI环境中导入失败）
def __getattr__(name):
    if name == 'AIAssistantWidget':
        from .assistant_widget import AIAssistantWidget
        return AIAssistantWidget
    raise AttributeError(f"module '{__name__}' has no attribute '{name}'")

__all__ = ['AIAssistantWidget', 'AIAssistant', 'ContextManager']
