# -*- coding: utf-8 -*-
"""
UI设置界面示例代码
演示如何在设置界面中使用SettingsManager
"""
import sys
from pathlib import Path

# 添加src目录到路径
sys.path.insert(0, str(Path(__file__).parent.parent / 'src'))

from utils.settings_manager import settings_manager


def example_settings_dialog():
    """
    模拟设置对话框的逻辑

    这个示例展示了：
    1. 加载现有设置
    2. 用户输入API密钥和选择模型
    3. 测试API连通性
    4. 保存设置
    """

    print("="*70)
    print("DWG智能翻译系统 - 设置示例")
    print("="*70)
    print()

    # 步骤1：加载现有设置
    print("【步骤1】加载现有设置...")
    current_api_key = settings_manager.get_api_key()
    current_model = settings_manager.get_text_model()
    trans_settings = settings_manager.get_translation_settings()

    print(f"  当前API密钥: {'已设置' if current_api_key else '未设置'}")
    print(f"  当前翻译模型: {current_model}")
    print(f"  源语言: {trans_settings['source_lang']}")
    print(f"  目标语言: {trans_settings['target_lang']}")
    print()

    # 步骤2：用户输入（在实际UI中，这些会是文本框、下拉框等）
    print("【步骤2】配置API设置...")
    print()
    print("可用的翻译模型:")
    print("  1. qwen-mt-plus  (推荐) - 高质量翻译，性价比高")
    print("  2. qwen-mt-turbo - 快速翻译，成本更低")
    print("  3. qwen-plus     - 通用模型")
    print()

    # 模拟用户输入（实际UI中从输入框获取）
    # 这里为了演示，使用默认值
    user_api_key = input("请输入API密钥 (直接回车跳过测试): ").strip()

    if not user_api_key:
        print("\n⚠️ 未输入API密钥，跳过测试")
        print("提示：在实际使用时，您需要:")
        print("  1. 访问 https://dashscope.aliyun.com")
        print("  2. 获取API密钥")
        print("  3. 在设置界面输入密钥")
        print("  4. 选择模型 (默认: qwen-mt-plus)")
        print("  5. 点击'测试连接'按钮")
        print("  6. 测试成功后点击'保存'按钮")
        return

    model_choice = input("选择模型 (1=qwen-mt-plus, 2=qwen-mt-turbo, 默认=1): ").strip()
    user_model = "qwen-mt-turbo" if model_choice == "2" else "qwen-mt-plus"

    print()

    # 步骤3：测试API连通性
    print("【步骤3】测试API连通性...")
    print("(这相当于点击'测试连接'按钮)")
    print()

    success, message = settings_manager.test_api_connection(
        api_key=user_api_key,
        model=user_model
    )

    print(message)
    print()

    if not success:
        print("❌ 测试失败，请检查:")
        print("  1. API密钥是否正确")
        print("  2. 网络连接是否正常")
        print("  3. 账户余额是否充足")
        return

    # 步骤4：保存设置
    print("【步骤4】保存设置到配置文件...")
    print()

    confirm = input("确认保存设置? (y/n, 默认=y): ").strip().lower()
    if confirm and confirm != 'y':
        print("❌ 已取消保存")
        return

    # 保存API密钥
    if settings_manager.save_api_key(user_api_key):
        print("✓ API密钥已保存")

    # 保存模型选择
    if settings_manager.save_text_model(user_model):
        print(f"✓ 翻译模型已保存: {user_model}")

    # 也可以一次性保存所有设置
    # settings_manager.save_all_settings({
    #     'api.api_key': user_api_key,
    #     'api.text_model': user_model,
    #     'translation.default_source_lang': 'auto',
    #     'translation.default_target_lang': 'zh-CN',
    # })

    print()
    print("="*70)
    print("✅ 设置保存成功！")
    print("="*70)
    print()
    print("配置文件位置: ~/.biaoge/config.toml")
    print("下次启动时将自动加载这些设置")
    print()


def example_quick_usage():
    """快速使用示例"""
    print("\n" + "="*70)
    print("快速使用指南")
    print("="*70)
    print()

    print("在实际的PyQt6 UI代码中，您可以这样使用：")
    print()

    code_example = '''
from utils.settings_manager import settings_manager

class SettingsDialog(QDialog):
    def __init__(self):
        super().__init__()
        self.setup_ui()
        self.load_settings()  # 加载现有设置

    def load_settings(self):
        """加载现有设置到UI"""
        # 加载API密钥
        api_key = settings_manager.get_api_key()
        self.api_key_input.setText(api_key)

        # 加载模型选择
        model = settings_manager.get_text_model()
        self.model_combo.setCurrentText(model)

    def on_test_connection_clicked(self):
        """测试连接按钮点击事件"""
        api_key = self.api_key_input.text()
        model = self.model_combo.currentText()

        # 显示进度对话框
        self.show_progress("正在测试API连接...")

        # 测试连接
        success, message = settings_manager.test_api_connection(
            api_key=api_key,
            model=model
        )

        # 显示结果
        if success:
            QMessageBox.information(self, "成功", message)
        else:
            QMessageBox.warning(self, "失败", message)

    def on_save_clicked(self):
        """保存按钮点击事件"""
        # 保存所有设置
        settings_manager.save_all_settings({
            'api.api_key': self.api_key_input.text(),
            'api.text_model': self.model_combo.currentText(),
            'translation.default_source_lang': 'auto',
            'translation.default_target_lang': 'zh-CN',
        })

        QMessageBox.information(self, "成功", "设置已保存")
        self.accept()  # 关闭对话框
'''

    print(code_example)
    print()


def show_model_recommendations():
    """显示模型选择建议"""
    print("\n" + "="*70)
    print("模型选择建议")
    print("="*70)
    print()

    print("【推荐】CAD图纸文本翻译使用MT（机器翻译）模型：")
    print()

    models = [
        {
            'name': 'qwen-mt-plus',
            'desc': '高质量翻译，性价比高',
            'price': '¥0.006/1K tokens',
            'use_case': '生产环境推荐',
            'recommended': True
        },
        {
            'name': 'qwen-mt-turbo',
            'desc': '快速翻译，成本更低',
            'price': '¥0.003/1K tokens',
            'use_case': '大批量翻译，预算有限',
            'recommended': False
        },
        {
            'name': 'qwen-plus',
            'desc': '通用模型，能力均衡',
            'price': '¥0.004/1K tokens',
            'use_case': '需要额外AI能力时使用',
            'recommended': False
        },
    ]

    for model in models:
        tag = " [推荐]" if model['recommended'] else ""
        print(f"• {model['name']}{tag}")
        print(f"  说明: {model['desc']}")
        print(f"  价格: {model['price']}")
        print(f"  适用: {model['use_case']}")
        print()

    print("MT模型优势:")
    print("  ✓ 专门优化翻译任务，翻译质量高")
    print("  ✓ 速度快，响应时间短")
    print("  ✓ 成本低，适合大批量翻译")
    print("  ✓ 支持自动语言检测")
    print()


if __name__ == '__main__':
    # 显示模型建议
    show_model_recommendations()

    # 运行设置对话框示例
    example_settings_dialog()

    # 显示代码示例
    example_quick_usage()
