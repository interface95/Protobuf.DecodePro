#!/bin/bash

# 这个脚本用于提交截图到 GitHub
# 使用方法：将截图保存到 docs/images/screenshot.png 后，运行此脚本

cd /Users/wujun/Desktop/demo/Protobuf.Decode

# 检查截图是否存在
if [ -f "docs/images/screenshot.png" ]; then
    echo "✅ 发现截图文件"
    
    # 提交 README 和截图
    git add README.md docs/images/screenshot.png
    git commit -m "docs: 更新 README 和添加应用截图"
    git push
    
    echo "🎉 截图已成功推送到 GitHub!"
else
    echo "❌ 未找到截图文件: docs/images/screenshot.png"
    echo "请先将截图保存到该位置"
fi

