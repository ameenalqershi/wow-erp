"""Check or regenerate the QA-REF-01 crosswalk from a Notion migration snapshot."""
import argparse
import csv
import html
import io
import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "docs/backend/field-task-crosswalk.csv"
FIELD_ID = re.compile(r"\b([A-Z]+\d+-F\d+)\b")
FIELD_TASK = re.compile(r"[A-Z]+\d+-(?:FG|TOT|TOTAL|TOTQA)\d+")


def rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        return [(reader.line_num, row) for row in reader]


def index(rows_with_lines, key):
    result = {row[key]: (line, row) for line, row in rows_with_lines}
    if len(result) != len(rows_with_lines):
        raise ValueError(f"Repeated identifier in {key}")
    return result


def render(snapshot):
    original = index(rows(ROOT / "muhaseb-erp-analysis/data/fields.csv"), "معرف الحقل")
    matrix = index(rows(ROOT / "docs/backend/fields-implementation.csv"), "field_id")
    issues = index(rows(snapshot / "wow-erp-notion-github-issues.csv"), "رقم المهمة")
    if original.keys() != matrix.keys():
        raise ValueError("Original fields and implementation matrix have different identifiers")
    pages = defaultdict(set)
    for path in snapshot.glob("source-view-*.json"):
        for row in json.loads(path.read_text()):
            pages[row["رقم المهمة"]].add(row["url"])
    links = defaultdict(set)
    for path in snapshot.glob("details-*.json"):
        for task, body in json.loads(path.read_text()).items():
            if not FIELD_TASK.fullmatch(task):
                continue
            issue = issues[task][1]
            url = "https://github.com/ameenalqershi/wow-erp/issues/" + issue["رقم القضية"]
            if issue["رابط نوشن"] not in pages[task] or issue["رابط GitHub"] != url:
                raise ValueError(f"Mapping differs from snapshot for {task}")
            body = re.sub(r"<[^>]+>", " ", html.unescape(body))
            matches = list(FIELD_ID.finditer(body))
            for n, match in enumerate(matches):
                field = match[1]
                if field not in original or not task.startswith(field.split("-")[0] + "-"):
                    continue
                # Some task notes mention another field as a blocker. Require the original
                # field name in the same short declaration before counting the reference.
                end = matches[n + 1].start() if n + 1 < len(matches) else len(body)
                declaration = body[match.end():min(end, match.end() + 75)]
                if original[field][1]["الحقل"] in declaration:
                    links[field].add(task)

    result = io.StringIO(newline="")
    writer = csv.writer(result, lineterminator="\n")
    writer.writerow(["field_id", "source_csv_line", "implementation_csv_line", "implementation_status",
                     "task_ids", "github_issues"])
    for field, (line, source) in original.items():
        matrix_line, mapped = matrix[field]
        if mapped["screen_id"] != source["الشاشة"] or mapped["name"] != source["الحقل"]:
            raise ValueError(f"Screen or field name mismatch for {field}")
        if not all(source[k] for k in ("النوع", "المصدر", "الإلزام", "الافتراضي", "التحقق")):
            raise ValueError(f"Incomplete source definition for {field}")
        tasks = sorted(links[field])
        if not tasks:
            raise ValueError(f"No matching field task for {field}")
        urls = [issues[task][1]["رابط GitHub"] for task in tasks]
        writer.writerow([field, line, matrix_line, mapped["status"], " ".join(tasks), " ".join(urls)])
    return result.getvalue()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content = render(args.snapshot)
    if args.check:
        if OUTPUT.read_text() != content:
            raise SystemExit("Field-task crosswalk differs from snapshot")
        print("PASS QA-REF-01: field-task crosswalk matches source, implementation matrix and task snapshot")
    else:
        OUTPUT.write_text(content)
