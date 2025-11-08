# -*- coding: utf-8 -*-
"""
DWG密码输入对话框
"""
from PyQt6.QtCore import Qt
from PyQt6.QtGui import QIcon

from qfluentwidgets import (
    Dialog, LineEdit, PushButton, PrimaryPushButton,
    CheckBox, BodyLabel, MessageBox, FluentIcon
)


class PasswordDialog(Dialog):
    """DWG文件密码输入对话框"""

    def __init__(self, filename: str, parent=None):
        """
        初始化密码对话框

        Args:
            filename: DWG文件名（用于显示）
            parent: 父窗口
        """
        super().__init__(parent)
        self.password = None
        self.remember = False
        self.filename = filename
        self.setup_ui()

    def setup_ui(self):
        """设置UI"""
        from PyQt6.QtWidgets import QVBoxLayout, QHBoxLayout

        self.setWindowTitle("需要密码")
        self.setModal(True)
        self.setFixedWidth(400)

        layout = QVBoxLayout(self)
        layout.setSpacing(15)

        # 提示信息
        info_label = BodyLabel(f"文件已加密，请输入密码：")
        info_label.setWordWrap(True)
        layout.addWidget(info_label)

        # 文件名
        filename_label = BodyLabel(f"<b>{self.filename}</b>")
        filename_label.setStyleSheet("color: #0078D4; padding: 5px;")
        layout.addWidget(filename_label)

        # 密码输入框
        from PyQt6.QtWidgets import QHBoxLayout
        password_layout = QHBoxLayout()
        password_layout.addWidget(BodyLabel("密码:"))

        self.password_input = LineEdit()
        self.password_input.setEchoMode(LineEdit.EchoMode.Password)
        self.password_input.setPlaceholderText("请输入DWG文件密码")
        self.password_input.returnPressed.connect(self.accept)
        password_layout.addWidget(self.password_input)

        # 显示/隐藏密码按钮
        self.show_password_btn = PushButton(FluentIcon.VIEW, "")
        self.show_password_btn.setFixedWidth(40)
        self.show_password_btn.setCheckable(True)
        self.show_password_btn.toggled.connect(self.toggle_password_visibility)
        self.show_password_btn.setToolTip("显示/隐藏密码")
        password_layout.addWidget(self.show_password_btn)

        layout.addLayout(password_layout)

        # 记住密码选项
        self.remember_checkbox = CheckBox("记住此文件的密码（本次会话）")
        self.remember_checkbox.setToolTip("密码将保存在内存中，关闭程序后失效")
        layout.addWidget(self.remember_checkbox)

        # 提示文字
        hint_label = BodyLabel(
            "提示：\n"
            "• 如果不知道密码，请联系图纸提供方\n"
            "• 记住密码仅在本次会话有效，不会永久保存\n"
            "• 密码错误时，文件将无法打开"
        )
        hint_label.setStyleSheet("""
            QLabel {
                background-color: #FFF4CE;
                border: 1px solid #FFD700;
                border-radius: 4px;
                padding: 10px;
                color: #856404;
                font-size: 11px;
            }
        """)
        hint_label.setWordWrap(True)
        layout.addWidget(hint_label)

        # 按钮
        button_layout = QHBoxLayout()
        button_layout.addStretch()

        cancel_btn = PushButton("取消")
        cancel_btn.clicked.connect(self.reject)
        cancel_btn.setFixedWidth(80)
        button_layout.addWidget(cancel_btn)

        ok_btn = PrimaryPushButton("确定")
        ok_btn.clicked.connect(self.accept)
        ok_btn.setDefault(True)
        ok_btn.setFixedWidth(80)
        button_layout.addWidget(ok_btn)

        layout.addLayout(button_layout)

        # 焦点设置到密码输入框
        self.password_input.setFocus()

    def toggle_password_visibility(self, checked: bool):
        """切换密码可见性"""
        if checked:
            self.password_input.setEchoMode(LineEdit.EchoMode.Normal)
            self.show_password_btn.setIcon(FluentIcon.HIDE)
        else:
            self.password_input.setEchoMode(LineEdit.EchoMode.Password)
            self.show_password_btn.setIcon(FluentIcon.VIEW)

    def accept(self):
        """确认按钮点击"""
        password = self.password_input.text().strip()

        if not password:
            MessageBox(
                "密码为空",
                "请输入密码后再确定。\n\n如果图纸确实没有密码，请点击取消。",
                self
            ).exec()
            self.password_input.setFocus()
            return

        self.password = password
        self.remember = self.remember_checkbox.isChecked()
        super().accept()

    def get_password(self) -> tuple[str, bool]:
        """
        获取输入的密码

        Returns:
            (密码, 是否记住)
        """
        return self.password, self.remember


def get_dwg_password(filename: str, parent=None) -> tuple[str | None, bool]:
    """
    显示密码输入对话框

    Args:
        filename: DWG文件名
        parent: 父窗口

    Returns:
        (密码, 是否记住) 或 (None, False) 如果用户取消
    """
    from PyQt6.QtWidgets import QDialog
    dialog = PasswordDialog(filename, parent)
    if dialog.exec() == QDialog.DialogCode.Accepted:
        return dialog.get_password()
    return None, False
