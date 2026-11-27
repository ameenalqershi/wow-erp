#!/usr/bin/env python3
"""DIAG-module-TAX (#563): reproducible source-to-implementation audit."""
import argparse
from collections import Counter
import csv
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
REPORT = "docs/backend/tax-diagram-audit.md"
EXPECTED = {"TAX01": 15, "TAX02": 19, "TAX03": 12, "TAX04": 15,
            "TAX05": 14, "TAX06": 16, "TAX07": 26, "TAX08": 16}
CHAINS = (("cat0", "TAX01", "TAX05", "TAX08"),
          ("cat1", "TAX02"), ("cat2", "TAX03"),
          ("cat3", "TAX04", "TAX06"), ("cat4", "TAX07"))


def require(condition, message):
    if not condition:
        raise ValueError("TAX audit: " + message)


def rows(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def index(records, key):
    result = {r[key]: r for r in records}
    require(len(result) == len(records), "duplicate " + key)
    return result


def read(path):
    return path.read_text(encoding="utf-8")


def svg_edges(path, nodes):
    ns = {"s": "http://www.w3.org/2000/svg"}
    tree = ET.parse(path)
    texts = tree.findall(".//s:text", ns)
    boxes = {}
    for rect in tree.findall(".//s:rect", ns):
        if rect.get("fill") != "white" or rect.get("stroke") is None:
            continue
        x, y, w, h = (float(rect.get(k)) for k in ("x", "y", "width", "height"))
        contained = sorted((float(t.get("y")), "".join(t.itertext()))
                           for t in texts if x < float(t.get("x")) < x+w
                           and y < float(t.get("y")) < y+h)
        label = " ".join(t for _, t in contained)
        matches = [key for key, value in nodes.items() if value == label]
        require(len(matches) == 1 and matches[0] not in boxes, "SVG node label " + label)
        boxes[matches[0]] = ((x+w/2, y), (x+w/2, y+h))
    require(set(boxes) == set(nodes), "missing SVG node")
    edges = []
    for p in tree.findall(".//s:path", ns):
        if not p.get("marker-end"):
            continue
        require(p.get("marker-end") == "url(#arrow-module-TAX)", "wrong SVG arrow marker")
        nums = [float(x) for x in re.findall(r"-?\d+(?:\.\d+)?", p.get("d", ""))]
        require(len(nums) == 8, "unexpected SVG edge geometry")
        start, end = tuple(nums[:2]), tuple(nums[-2:])
        a = [key for key, (_, bottom) in boxes.items() if bottom == start]
        b = [key for key, (top, _) in boxes.items() if top == end]
        require(len(a) == len(b) == 1, "SVG edge endpoint mismatch")
        edges.append((a[0], b[0]))
    labels = Counter("".join(t.itertext()) for t in texts)
    require(labels["قائمة"] == labels["عنصر قائمة"] == 5, "SVG edge labels")
    return edges


def audit(root=ROOT):
    src = root / "muhaseb-erp-analysis"
    impl = root / "docs/backend"
    mermaid = read(src / "diagrams/source/module-TAX.mmd")
    pairs = re.findall(r'^\s*(\w+)\["([^"]+)"\]\s*$', mermaid, re.M)
    nodes = dict(pairs)
    expected_edges = {("root", c[0]) for c in CHAINS}
    expected_edges.update((a,b) for c in CHAINS for a,b in zip(c,c[1:]))
    triples = re.findall(r'^\s*(\w+)\s+-->\s*(?:\|"([^"]+)"\|\s*)?(\w+)\s*$', mermaid, re.M)
    edges = [(a,b) for a,_,b in triples]
    require(len(pairs) == len(nodes) == 14 and set(nodes) == {"root", *(c[0] for c in CHAINS), *EXPECTED}, "Mermaid nodes")
    require(len(edges) == len(expected_edges) == 13 and set(edges) == expected_edges, "Mermaid edges")
    for a,label,b in triples:
        expected_label = "قائمة" if a == "root" else "عنصر قائمة" if a.startswith("cat") else ""
        require(label == expected_label, "Mermaid edge label")
    rendered = svg_edges(src / "diagrams/svg/module-TAX.svg", nodes)
    require(Counter(rendered) == Counter(edges), "SVG/Mermaid edge mismatch")

    screens = index(rows(src / "data/screens.csv"), "معرف")
    field_source = [r for r in rows(src / "data/fields.csv") if r["الشاشة"] in EXPECTED]
    fields = index(field_source, "معرف الحقل")
    require(len(fields) == 133, "field total")
    mapped = index(rows(impl / "fields-implementation.csv"), "field_id")
    screen_map = index(rows(impl / "screens-implementation.csv"), "screen_id")
    rules = index(rows(impl / "rules-implementation.csv"), "rule_id")
    original_rules = dict(re.findall(r"^\| (BR\d\d) \| (.*?) \|$", read(src / "docs/02-accounting-and-business-rules.md"), re.M))
    sections = re.split(r"^## (TAX\d\d) — (.+)$", read(src / "docs/modules/TAX.md"), flags=re.M)
    require(len(sections) == 25, "module section count")
    docs = {sections[i]: (sections[i+1], sections[i+2]) for i in range(1,len(sections),3)}
    require(set(docs) == set(EXPECTED), "module screen headings")

    output = ["# DIAG-module-TAX — مطابقة خريطة الضرائب والفوترة الإلكترونية",
              "", "القضية [#563](https://github.com/ameenalqershi/wow-erp/issues/563). هذا تدقيق تغطية مستقل، وليس تنفيذًا أو اعتمادًا ضريبيًا. المصدر الأصلي لم يتغير.",
              "", "المراجع: data/screens.csv وdata/fields.csv وdocs/modules/TAX.md وdocs/02-accounting-and-business-rules.md وdiagrams/source/module-TAX.mmd وdiagrams/svg/module-TAX.svg داخل muhaseb-erp-analysis؛ ومصفوفات التنفيذ داخل docs/backend.",
              "", "## العقد والعلاقات", "",
              "14 عقدة (جذر، 5 قوائم، 8 شاشات)، 13 سهمًا مطابقًا في Mermaid وSVG بما في ذلك نقطتا بداية ونهاية كل سهم. الأسهم للتنقل فقط وليست اعتماديات أو تسلسلًا محاسبيًا.", ""]
    for c in CHAINS:
        output.append("- " + " → ".join(nodes[x] for x in ("root",*c)))
    output += ["", "## الشاشات والضوابط والحقول", ""]
    statuses = Counter()
    for code, count in EXPECTED.items():
        title, body = docs[code]
        s, m = screens[code], screen_map[code]
        category = next(c[0] for c in CHAINS if code in c)
        require(nodes[code] == code + " • " + title == code + " • " + s["الشاشة"], code + " label")
        require(s["قسم"] == m["module"] == "TAX" and m["name"] == title, code + " registry")
        require(s["مسار القائمة"] == " ← ".join((nodes["root"], nodes[category], title)), code + " menu")
        sf = [r for r in field_source if r["الشاشة"] == code]
        ids = [f"{code}-F{i:02}" for i in range(1,count+1)]
        require([r["معرف الحقل"] for r in sf] == ids, code + " field IDs")
        table = [tuple(cell.strip() for cell in line.strip("|").split("|"))
                 for line in body.splitlines() if line.startswith("|")]
        require(len(table) == count+2, code + " field table count")
        keys = ("الحقل","الموضع","النوع","الإلزام","المصدر")
        require(table[2:] == [tuple(r[k] for k in keys) for r in sf], code + " field table mismatch")
        rule_match = re.search(r"^قواعد مشتركة: (.+?)\. الارتباطات: (.+?)\.$", body, re.M)
        require(rule_match is not None, code + " missing rules")
        br, links = (part.split(", ") for part in rule_match.groups())
        require(all(x in rules and x in original_rules for x in br), code + " missing rule")
        require(all(x in screens for x in links) and ", ".join(links) == s["ارتباطات"], code + " links")
        for prefix, value in (("**ضابط الشاشة:** ", s["تحقق"]), ("**المخرجات والأثر:** ", s["مخرجات"])):
            require(prefix + value + "." in body or prefix + value in body, code + " screen contract")
        require("قبول إيجابي:" in body and "قبول سلبي:" in body and "الأفعال:" in body, code + " acceptance")
        require(m["status"] == "not-started" and not any(m[k] for k in ("api","implemented_scope","test_file")), code + " implementation changed; review audit")
        output += [f"### {code} — {title}", "",
                   f"- الضابط: {s['تحقق']}",
                   f"- المخرجات: {s['مخرجات']}",
                   f"- قواعد الأعمال: {', '.join(br)}؛ الارتباطات: {', '.join(links)}.",
                   "- الحالة: لم يبدأ. لا عقد API أو اختبار تنفيذ خاص بالشاشة مسجل؛ لا دليل على تطبيق ضابطها أو التزامن أو التدقيق أو الأثر المالي فيها.",
                   "", "| معرف الحقل | الاسم | حالة التنفيذ |", "|---|---|---|"]
        for r in sf:
            f = mapped[r["معرف الحقل"]]
            require(f["screen_id"] == code and f["name"] == r["الحقل"], "field binding " + r["معرف الحقل"])
            require(f["status"] == "pending" and not f["binding"] and not f["api"], "field implementation changed " + r["معرف الحقل"])
            statuses[f["status"]] += 1
            output.append(f"| {f['field_id']} | {f['name']} | {f['status']} |")
        output.append("")
    require(statuses == {"pending":133}, "field status totals")
    # BR20 and BR14 are cross-module gates even though not listed in every screen.
    require(rules["BR20"]["scope"] == "not-started", "BR20 status changed")
    output += ["## بوابات التنفيذ والتكامل", "",
               "- BR20: لا اعتماد أو تكامل لدولة دون ملف قانوني مؤرخ ومراجع؛ القاعدة غير منفذة. لا تُستخدم نسبة اختبار في الإنتاج.",
               "- BR14: لا إقفال قبل مطابقة الضريبة مع الأستاذ؛ التنفيذ الحالي جزئي وليس قبولًا ضريبيًا.",
               "- BR02/BR09/BR18: يوجد أساس عزل وتدقيق في وحدات أخرى فقط. يلزم إثباته في كل مسار ضريبي، لا استعارة قبول GL.",
               "- TAX07 يحتاج قبول الاتزان والذرية ومنع التكرار والعكس والعملة (BR01/03/05/06/07/10) مع القاعدة المحلية المعتمدة.",
               "- TAX06 يحتاج فصل حالة الإرسال عن الترحيل وإعادة المحاولة بالهوية نفسها وأدلة عدم تعديل الوثيقة المصدرة.",
               "", "## النتيجة", "",
               "8 شاشات غير منفذة و133 حقلًا معلقًا. لا تغيير لحالة تنفيذ المخطط أو الشاشات أو الحقول. التحقق يثبت التغطية فقط؛ كل اختبار تشغيلي ضريبي مؤجل إلى تنفيذ الشاشة وسياسة الدولة المعتمدة.",
               "", "إعادة التحقق: python3 backend/scripts/audit_tax_diagram.py --check. اختبارات الانحراف ضمن backend/scripts/tests/test_tax_diagram_audit.py؛ لا تتطلب SQL أو بيانات إنتاج.", ""]
    return "\n".join(output)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    result = audit()
    path = ROOT / REPORT
    if args.check:
        require(read(path) == result, "stale report")
    else:
        path.write_text(result, encoding="utf-8")
    print("PASS DIAG-module-TAX: 14 nodes, 13 matching SVG edges, 8 screens, 133 fields")


if __name__ == "__main__":
    main()
