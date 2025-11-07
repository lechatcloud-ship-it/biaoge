#!/usr/bin/env python3
"""
快速启动脚本
"""
import sys
from pathlib import Path

# 添加src到路径
sys.path.insert(0, str(Path(__file__).parent))

from src.main import main

if __name__ == '__main__':
    main()
