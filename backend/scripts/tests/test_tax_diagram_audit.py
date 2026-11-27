"""Acceptance and failure injection for the TAX coverage audit."""
import importlib.util
from pathlib import Path
import shutil
from tempfile import TemporaryDirectory
import unittest

SPEC = importlib.util.spec_from_file_location(
    "tax_audit", Path(__file__).resolve().parents[1] / "audit_tax_diagram.py")
audit = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(audit)


class TaxDiagramAcceptance(unittest.TestCase):
    FILES = (
        "muhaseb-erp-analysis/diagrams/source/module-TAX.mmd",
        "muhaseb-erp-analysis/diagrams/svg/module-TAX.svg",
        "muhaseb-erp-analysis/data/screens.csv",
        "muhaseb-erp-analysis/data/fields.csv",
        "muhaseb-erp-analysis/docs/02-accounting-and-business-rules.md",
        "muhaseb-erp-analysis/docs/modules/TAX.md",
        "docs/backend/fields-implementation.csv",
        "docs/backend/screens-implementation.csv",
        "docs/backend/rules-implementation.csv",
    )

    def setUp(self):
        temp = TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        for name in self.FILES:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(audit.ROOT / name, path)

    def replace(self, index, old, new):
        path = self.root / self.FILES[index]
        value = path.read_text(encoding="utf-8")
        self.assertIn(old, value)
        path.write_text(value.replace(old, new, 1), encoding="utf-8")

    def test_real_snapshot_and_report(self):
        actual = audit.audit(self.root)
        self.assertEqual(actual, (audit.ROOT / audit.REPORT).read_text(encoding="utf-8"))
        self.assertEqual(133, sum(line.startswith("| TAX") for line in actual.splitlines()))

    def test_wrong_mermaid_relationship(self):
        self.replace(0, "TAX05 --> TAX08", "TAX06 --> TAX08")
        with self.assertRaisesRegex(ValueError, "Mermaid edges"):
            audit.audit(self.root)

    def test_wrong_svg_relationship(self):
        self.replace(1, "187.0 245\"", "482.0 245\"")
        with self.assertRaisesRegex(ValueError, "SVG/Mermaid edge mismatch"):
            audit.audit(self.root)

    def test_bad_svg_label(self):
        self.replace(1, "TAX01 • ملف الدولة", "TAX01 • ملف آخر")
        with self.assertRaisesRegex(ValueError, "SVG node label"):
            audit.audit(self.root)

    def test_missing_field_id(self):
        self.replace(3, "TAX01-F01", "TAX01-F99")
        with self.assertRaisesRegex(ValueError, "TAX01 field IDs"):
            audit.audit(self.root)

    def test_wrong_field_definition(self):
        self.replace(5, "| رقم التسجيل |", "| رقم مختلف |")
        with self.assertRaisesRegex(ValueError, "field table mismatch"):
            audit.audit(self.root)

    def test_unknown_rule(self):
        self.replace(5, "BR02, BR09, BR18", "BR99, BR09, BR18")
        with self.assertRaisesRegex(ValueError, "missing rule"):
            audit.audit(self.root)

    def test_unverified_completion_rejected(self):
        self.replace(6, "TAX01-F01,الشركة,system,pending", "TAX01-F01,الشركة,system,implemented")
        with self.assertRaisesRegex(ValueError, "field implementation changed"):
            audit.audit(self.root)

    def test_unverified_screen_completion_rejected(self):
        self.replace(7, "TAX01,TAX,ملف الدولة والتسجيل,not-started", "TAX01,TAX,ملف الدولة والتسجيل,partial")
        with self.assertRaisesRegex(ValueError, "implementation changed"):
            audit.audit(self.root)


if __name__ == "__main__":
    unittest.main()
