"""Mutation tests for report dependency review gates; also runs in verify.py and CI."""
import csv
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_rpt_dependency_gates as gate


class RptDependencyTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        self.paths = []
        for path in (gate.SOURCE, gate.LINKS, gate.SCREENS, gate.FIELDS, gate.REVIEW):
            dest = root / path.name
            shutil.copyfile(path, dest)
            self.paths.append(dest)

    def edit(self, index, transform):
        path = self.paths[index]
        rows = gate.rows(path)
        transform(rows)
        with path.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)

    def test_original_links_and_reviewed_mapping(self):
        self.assertEqual(gate.check(*self.paths), (15, 19))

    def test_missing_reverse_link_rejected(self):
        self.edit(1, lambda rows: rows.pop(next(i for i,r in enumerate(rows) if r["direction"] == "incoming")))
        with self.assertRaisesRegex(ValueError, "differ from original"):
            gate.check(*self.paths)

    def test_duplicate_link_rejected(self):
        self.edit(1, lambda rows: rows.append(dict(rows[0])))
        with self.assertRaisesRegex(ValueError, "differ from original"):
            gate.check(*self.paths)

    def test_source_status_change_requires_review(self):
        self.edit(2, lambda rows: next(r for r in rows if r["screen_id"] == "INV07").update(status="partial"))
        with self.assertRaisesRegex(ValueError, "coverage changed.*INV07"):
            gate.check(*self.paths)

    def test_source_field_binding_change_requires_review(self):
        self.edit(3, lambda rows: next(r for r in rows if r["screen_id"] == "INV08").update(binding="new source mapping"))
        with self.assertRaisesRegex(ValueError, "coverage changed.*INV08"):
            gate.check(*self.paths)

    def test_unrelated_screen_change_allowed(self):
        self.edit(3, lambda rows: next(r for r in rows if r["screen_id"] == "SYS01").update(binding="unrelated change"))
        self.assertEqual(gate.check(*self.paths), (15, 19))

    def test_review_evidence_required(self):
        review = json.loads(self.paths[4].read_text())
        review["evidence"] = ""
        self.paths[4].write_text(json.dumps(review))
        with self.assertRaisesRegex(ValueError, "Missing dependency review evidence"):
            gate.check(*self.paths)
