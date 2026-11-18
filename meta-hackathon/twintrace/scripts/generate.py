"""Regenerate the bundled Twin Trace cases with Python's standard library."""

import json
import math
import random
from pathlib import Path


def generate_case(seed):
    rng = random.Random(seed)

    def make_groups(count, prefix):
        labels = [0, 1] * (count // 2)
        rng.shuffle(labels)
        return [
            (f"{prefix}{i:02d}", tuple(rng.random() for _ in range(6)), label)
            for i, label in enumerate(labels)
        ]

    def observe(groups, role):
        return [
            {
                "id": f"{role}-{group_id}",
                "groupId": group_id,
                "features": [value + rng.uniform(-0.005, 0.005) for value in center],
                "label": label,
            }
            for group_id, center, label in groups
        ]

    # Preserve RNG call order from the published Split Lab experiment.
    base = make_groups(80, "B")
    reserve = make_groups(40, "R")
    evaluation = make_groups(40, "E")
    audit = make_groups(60, "A")
    return {
        "seed": seed,
        "caseKind": "leakage",
        "common": observe(base, "common"),
        "reserve": observe(reserve, "reserve"),
        "twins": observe(evaluation, "twin"),
        "evaluation": observe(evaluation, "test"),
        "audit": observe(audit, "audit"),
    }


def generate_control(seed=101):
    # Independent objects share a smooth visible distribution. Each label comes
    # exclusively from the first observed feature, including on the unseen audit.
    rng = random.Random(seed)

    def draw(count, role):
        rows = []
        for index in range(count):
            features = [rng.random(), *(rng.uniform(0.3, 0.7) for _ in range(5))]
            group = f"{role[0].upper()}{index:03d}"
            rows.append({
                "id": f"{role}-{group}",
                "groupId": group,
                "features": features,
                "label": int(features[0] > 0.5),
            })
        return rows

    case = {
        "seed": seed, "caseKind": "control",
        "common": draw(120, "common"), "reserve": [], "twins": [],
        "evaluation": draw(40, "evaluation"), "audit": draw(60, "audit"),
    }
    # Challenge the shortcut that look-alike shapes must share an origin. This
    # selects the example shown to the player; scores still use every sample.
    case["inspectId"] = min(
        case["evaluation"],
        key=lambda row: min(math.dist(row["features"], candidate["features"])
                            for candidate in case["common"]),
    )["id"]
    return case


if __name__ == "__main__":
    output = Path(__file__).resolve().parents[1] / "data" / "cases.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    cases = [generate_case(0), generate_control(), generate_case(1), generate_case(2)]
    payload = {"version": 2, "cases": cases}
    output.write_bytes((json.dumps(payload, separators=(",", ":")) + "\n").encode())
