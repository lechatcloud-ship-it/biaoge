"""
安装脚本
"""
from setuptools import setup, find_packages
from pathlib import Path

# 读取README
readme_file = Path(__file__).parent / "README.md"
long_description = readme_file.read_text(encoding='utf-8') if readme_file.exists() else ""

# 读取requirements
requirements_file = Path(__file__).parent / "requirements.txt"
requirements = requirements_file.read_text(encoding='utf-8').strip().split('\n') if requirements_file.exists() else []

setup(
    name="biaoge",
    version="1.0.0",
    description="DWG翻译计算软件 - 专业的建筑工程CAD图纸翻译和算量工具",
    long_description=long_description,
    long_description_content_type="text/markdown",
    author="Claude AI",
    author_email="support@biaoge.com",
    url="https://github.com/yourusername/biaoge",
    packages=find_packages(),
    install_requires=requirements,
    entry_points={
        'console_scripts': [
            'biaoge=main:main',
        ],
        'gui_scripts': [
            'biaoge-gui=main:main',
        ],
    },
    classifiers=[
        "Development Status :: 5 - Production/Stable",
        "Intended Audience :: Developers",
        "Intended Audience :: End Users/Desktop",
        "Topic :: Office/Business :: Financial :: Accounting",
        "License :: Other/Proprietary License",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
        "Programming Language :: Python :: 3.11",
        "Operating System :: Microsoft :: Windows",
        "Operating System :: MacOS",
        "Operating System :: POSIX :: Linux",
    ],
    python_requires=">=3.8",
    include_package_data=True,
    package_data={
        'biaoge': ['resources/*', 'docs/*'],
    },
)
