#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量添加UTF-8编码声明到所有Python文件
确保Windows平台兼容性
"""
import os
from pathlib import Path


def add_utf8_declaration(filepath: Path):
    """给Python文件添加UTF-8编码声明"""
    try:
        # 读取文件内容
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        # 检查是否已有编码声明
        lines = content.split('\n')
        has_shebang = lines[0].startswith('#!')
        has_encoding = any('coding' in line or 'encoding' in line for line in lines[:3])

        if has_encoding:
            print(f"✓ 跳过 {filepath} (已有编码声明)")
            return False

        # 准备编码声明
        encoding_line = "# -*- coding: utf-8 -*-"

        # 构建新内容
        new_lines = []
        if has_shebang:
            new_lines.append(lines[0])
            new_lines.append(encoding_line)
            new_lines.extend(lines[1:])
        else:
            new_lines.append(encoding_line)
            new_lines.extend(lines)

        # 写回文件
        new_content = '\n'.join(new_lines)
        with open(filepath, 'w', encoding='utf-8', newline='\n') as f:
            f.write(new_content)

        print(f"✓ 已添加编码声明: {filepath}")
        return True

    except Exception as e:
        print(f"✗ 错误 {filepath}: {e}")
        return False


def main():
    """主函数"""
    project_root = Path(__file__).parent

    # 查找所有Python文件
    python_files = list(project_root.rglob('*.py'))

    print(f"找到 {len(python_files)} 个Python文件")
    print("=" * 60)

    modified_count = 0
    for filepath in sorted(python_files):
        # 跳过虚拟环境和构建目录
        if any(part in filepath.parts for part in ['.venv', 'venv', '__pycache__', 'build', 'dist']):
            continue

        if add_utf8_declaration(filepath):
            modified_count += 1

    print("=" * 60)
    print(f"完成！共修改了 {modified_count} 个文件")


if __name__ == '__main__':
    main()
