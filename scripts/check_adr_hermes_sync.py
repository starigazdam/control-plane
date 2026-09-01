from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
index = root / "docs" / "adr" / "README.md"
hermes = root / ".hermes.md"

errors = []
if not index.is_file():
    errors.append("missing docs/adr/README.md")
if not hermes.is_file():
    errors.append("missing .hermes.md")

if not errors:
    index_text = index.read_text(encoding="utf-8")
    hermes_text = hermes.read_text(encoding="utf-8")
    if "docs/adr/README.md" not in hermes_text:
        errors.append(".hermes.md must link to docs/adr/README.md")
    records = sorted(path.name for path in index.parent.glob("*.md") if path.name not in {"README.md", "0000-template.md", "template.md"})
    for record in records:
        if f"]({record})" not in index_text:
            errors.append(f"ADR index does not list {record}")

if errors:
    print("ADR/Hermes consistency check failed:", *errors, sep="\n- ")
    sys.exit(1)
print("ADR/Hermes consistency check passed")
