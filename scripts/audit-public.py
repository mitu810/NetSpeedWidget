"""审计暂存区，不输出疑似敏感值；退出码非零时禁止公开推送。"""
import argparse
import re
import subprocess
from pathlib import PurePosixPath


def main():
    # 1. 仅审计准备提交的实际 Git blob，避免忽略规则和磁盘内容不一致。
    parser = argparse.ArgumentParser()
    parser.add_argument("--forbid-value", action="append", default=[])
    options = parser.parse_args()
    paths = subprocess.check_output(["git", "ls-files", "-z"]).decode("utf-8").split("\0")
    rules = {
        "用户绝对路径": re.compile(r"[A-Za-z]:[\\/]+Users[\\/]+[^\\/\s]+", re.I),
        "私人密钥": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----"),
        "GitHub token": re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b"),
        "AWS key": re.compile(r"\b(?:AKIA|ASIA)[A-Z0-9]{16}\b"),
        "服务访问 key": re.compile(r"\bsk-[A-Za-z0-9_-]{24,}\b"),
        "JWT": re.compile(r"\beyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{15,}\b"),
    }
    for index, value in enumerate(options.forbid_value):
        rules[f"本地禁止值 {index + 1}"] = re.compile(r"(?<!\w)" + re.escape(value) + r"(?!\w)", re.I)
    findings = []
    for name in filter(None, paths):
        path = PurePosixPath(name)
        forbidden_dirs = {"bin", "obj", "publish", "artifacts", ".backups", ".vs", ".superpowers"}
        if (forbidden_dirs.intersection(path.parts) or path.suffix.lower() in {".exe", ".dll", ".pdb", ".dmp", ".log"}
                or path.name == "settings.json" or path.name.startswith(".env")
                or path.name.startswith("hardware-diagnostics")):
            findings.append((name, "不应提交的本地文件"))
        blob = subprocess.check_output(["git", "show", ":" + name])
        if b"\0" in blob or path.suffix.lower() in {".ico", ".png"}:
            continue
        text = blob.decode("utf-8-sig")
        for label, expression in rules.items():
            if expression.search(text):
                findings.append((name, label))
        # 2. 上游版权声明中的公开联系方式原样保留；项目文件不能含私人邮箱。
        if path.parts[0] != "licenses":
            addresses = re.findall(r"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", text)
            if any(not address.endswith("@users.noreply.github.com") for address in addresses):
                findings.append((name, "项目文件中发现需审核的邮箱"))
    if findings:
        for name, label in findings:
            print(f"拒绝公开：{name}：{label}")
        raise SystemExit(1)
    print(f"公开文件检查通过：{len(list(filter(None, paths)))} 个 Git 文件；没有命中配置的隐私/密钥规则。")


if __name__ == "__main__":
    main()
