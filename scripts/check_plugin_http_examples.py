from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
src = root / "src"

# Directories that are not standalone plugins and are exempt from the
# runnable .http example requirement (see docs/plugin-http-examples.md).
EXEMPT_DIR_NAMES = {"ControlPlane.Core", "ControlPlane.ServiceDefaults"}

REQUIRED_MARKERS = ["{{HOST}}", ".env"]


def is_plugin_dir(path: Path) -> bool:
    if not path.is_dir() or path.name in EXEMPT_DIR_NAMES:
        return False
    if not path.name.startswith("ControlPlane."):
        return False
    # A plugin project exposes operations and/or status providers.
    return any(path.glob("Operations/*.cs")) or any(path.glob("StatusProviders/*.cs"))


errors = []
plugin_dirs = sorted(p for p in src.iterdir() if is_plugin_dir(p))

if not plugin_dirs:
    errors.append("no plugin projects discovered under src/ — check EXEMPT_DIR_NAMES / discovery heuristic")

for plugin_dir in plugin_dirs:
    http_files = list(plugin_dir.glob("*.http"))
    if not http_files:
        errors.append(f"{plugin_dir.relative_to(root)} has operations/status providers but no runnable .http example")
        continue
    if len(http_files) > 1:
        errors.append(f"{plugin_dir.relative_to(root)} has multiple .http files: {[f.name for f in http_files]} — keep one canonical example")

    http_text = http_files[0].read_text(encoding="utf-8")
    for marker in REQUIRED_MARKERS:
        if marker not in http_text:
            errors.append(f"{http_files[0].relative_to(root)} must reference '{marker}' (host variable / .env secrets guidance)")

    # Heuristic: reject content that looks like a real committed secret
    # (long hex/base64-ish tokens) rather than a placeholder. Placeholder
    # values in these examples always contain "example".
    suspicious = [
        token for token in re.findall(r"\b[A-Za-z0-9+/_-]{32,}\b", http_text)
        if "example" not in token.lower()
    ]
    if suspicious:
        errors.append(f"{http_files[0].relative_to(root)} contains a token-like string that may be a real secret: {suspicious[0][:12]}...")

if errors:
    print("Plugin .http example check failed:", *errors, sep="\n- ")
    sys.exit(1)
print(f"Plugin .http example check passed ({len(plugin_dirs)} plugin project(s) checked)")
