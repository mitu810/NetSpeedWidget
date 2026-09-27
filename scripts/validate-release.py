"""验证 Release 更新内容的固定结构，避免软件内显示缺项或与版本不匹配。"""
import argparse
import re
from pathlib import Path

SECTIONS = ["新增功能", "优化改进", "问题修复", "更新说明", "下载文件", "验证情况"]

def validate(notes, tag):
    if not re.fullmatch(r"v\d+\.\d+\.\d+", tag):
        raise ValueError("版本号必须为 v主版本.次版本.修订号")
    if not notes.startswith(f"# NetSpeedWidget {tag}\n"):
        raise ValueError("首行版本标题与 tag 不一致")
    headings = re.findall(r"^## (.+)$", notes, re.M)
    if headings != SECTIONS:
        raise ValueError("二级标题必须按顺序为：" + "、".join(SECTIONS))
    for section in SECTIONS:
        content = notes.split(f"## {section}\n", 1)[1].split("\n## ", 1)[0].strip()
        if not content or not re.search(r"^- \S", content, re.M):
            raise ValueError(f"{section} 至少需要一条列表内容，无改动时写 - 无。")
    for suffix in ["Setup.exe", "Portable.zip"]:
        if f"NetSpeedWidget-{tag}-win-x64-{suffix}" not in notes:
            raise ValueError("缺少对应版本的下载包名称：" + suffix)
    if "SHA256SUMS.txt" not in notes:
        raise ValueError("缺少校验文件说明")

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("notes")
    parser.add_argument("tag")
    args = parser.parse_args()
    validate(Path(args.notes).read_text(encoding="utf-8-sig"), args.tag)
    print("Release notes structure passed.")
