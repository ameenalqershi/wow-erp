#!/usr/bin/env python3
"""Guard the reviewed RPT relationships and require review when mapped source coverage changes."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/backend"
SOURCE = ROOT / "muhaseb-erp-analysis/data/screens.csv"
LINKS = DOCS / "rpt-dependency-gates-20261127.csv"
SCREENS = DOCS / "screens-implementation.csv"
FIELDS = DOCS / "fields-implementation.csv"
REVIEW = DOCS / "rpt-dependency-review.json"
TARGETS = {"RPT08": 2130, "RPT09": 2150, "RPT10": 2169, "RPT13": 2211, "RPT14": 2225}


def rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def original_links(source):
    expected = set()
    for row in rows(source):
        screen = row["معرف"]
        for linked in (x.strip() for x in row["ارتباطات"].split(",")):
            if not linked:
                continue
            if screen in TARGETS:
                expected.add((screen, linked, "outgoing"))
            if linked in TARGETS:
                expected.add((linked, screen, "incoming"))
    return expected


def coverage(screens, fields, tracked):
    screen_rows = {r["screen_id"]: r for r in rows(screens)}
    field_rows = rows(fields)
    result = {}
    for key in sorted(tracked):
        if key not in screen_rows:
            raise ValueError(f"Missing screen mapping: {key}")
        mapped = sorted((r["field_id"], r["status"], r["binding"]) for r in field_rows if r["screen_id"] == key)
        if not mapped or len({r[0] for r in mapped}) != len(mapped):
            raise ValueError(f"Missing or duplicate field mapping: {key}")
        result[key] = {
            "status": screen_rows[key]["status"],
            "fieldCount": len(mapped),
            "fieldMappingSha256": hashlib.sha256(json.dumps(mapped, ensure_ascii=False, separators=(",", ":")).encode()).hexdigest()
        }
    return result


def check(source=SOURCE, links=LINKS, screens=SCREENS, fields=FIELDS, review=REVIEW):
    expected = original_links(source)
    records = rows(links)
    actual = [(r["report"], r["linked_screen"], r["direction"]) for r in records]
    if len(actual) != len(set(actual)) or set(actual) != expected:
        raise ValueError("Dependency links differ from original screen associations")
    by_screen = dict(TARGETS)
    for r in records:
        if r["parent_issue"] != str(TARGETS[r["report"]]) or not r["linked_issue"].isdigit() or int(r["linked_issue"]) <= 0 or not r["relationship"].strip():
            raise ValueError("Invalid issue linkage or missing review relationship")
        linked = r["linked_screen"]
        issue = int(r["linked_issue"])
        if linked in by_screen and by_screen[linked] != issue:
            raise ValueError(f"Inconsistent issue link: {linked}")
        by_screen[linked] = issue
    current = coverage(screens, fields, by_screen)
    reviewed = json.loads(review.read_text(encoding="utf-8"))
    if not reviewed.get("reviewIssue", "").startswith("https://github.com/ameenalqershi/wow-erp/issues/") or not reviewed.get("evidence", "").strip():
        raise ValueError("Missing dependency review evidence")
    old = reviewed.get("coverage", {})
    changed = sorted(k for k in set(current) | set(old) if current.get(k) != old.get(k))
    if changed:
        raise ValueError("Dependency coverage changed; review regression gates and record evidence before updating snapshot: " + ", ".join(changed))
    return len(actual), len(current)


if __name__ == "__main__":
    try:
        links, screens = check()
        print(f"PASS RPT dependency gates: {links} directed associations, {screens} reviewed screen mappings")
    except ValueError as exc:
        raise SystemExit(str(exc))
