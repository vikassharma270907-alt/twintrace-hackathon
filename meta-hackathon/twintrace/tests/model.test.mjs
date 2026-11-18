import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { createSession, nearestNeighbor, splitEvidence } from "../src/model.js";

const fixtures = JSON.parse(await readFile(new URL("../data/cases.json", import.meta.url), "utf8"));
function memoryStore(initial) {
  let data = initial;
  return { getItem: () => data, setItem: (_key, value) => { data = value; } };
}

test("seed zero reproduces published measurements and uses fixed evaluation and audit rows", () => {
  assert.deepEqual(splitEvidence(fixtures.cases[0], false),
    {trainCount: 120, overlap: 40, evalCorrect: 40, auditCorrect: 26});
  assert.deepEqual(splitEvidence(fixtures.cases[0], true),
    {trainCount: 120, overlap: 0, evalCorrect: 19, auditCorrect: 18});
  for (const fixture of fixtures.cases) {
    const auditGroups = new Set(fixture.audit.map(row => row.groupId));
    for (const row of [...fixture.common, ...fixture.twins, ...fixture.reserve, ...fixture.evaluation]) {
      assert.equal(auditGroups.has(row.groupId), false);
      assert.equal(row.features.length, 6);
    }
    if (fixture.caseKind === "leakage") {
      assert.deepEqual(fixture.twins.map(row => row.groupId), fixture.evaluation.map(row => row.groupId));
    }
  }
});

test("nearest training match only sees numeric features", () => {
  const input = {features: [0, 0], groupId: "same", label: 0};
  const byMetadata = {features: [4, 5], groupId: "same", label: 0};
  const byDistance = {features: [0.001, 0], groupId: "other", label: 1};
  assert.equal(nearestNeighbor([byMetadata, byDistance], input), byDistance);
});

test("repair and verdict are ordered, idempotent and keep audit evidence blind", () => {
  const session = createSession(fixtures);
  const first = session.getState();
  assert.equal(first.sample.group, first.neighbor.groupId);
  assert.notEqual(first.sample.id, first.neighbor.id);
  assert.deepEqual(session.act("repair"), first);
  assert.equal(session.act("inspect").stage, "repair");
  const repaired = session.act("repair");
  assert.deepEqual(session.act("repair"), repaired);
  assert.equal(repaired.cleanEvalCorrect, 19);
  for (const state of [first, repaired]) {
    for (const field of ["auditCorrect", "auditTotal", "leakyAuditCorrect"]) assert.equal(state[field], null);
  }
  const done = session.act("hold");
  assert.equal(done.auditCorrect, 18);
  assert.equal(done.leakyAuditCorrect, 26);
  assert.equal(done.auditTotal, 60);
  assert.deepEqual(session.act("release"), done);
  assert.equal(session.act("next").seed, 101);
  session.act("inspect");
  session.act("repair");
  assert.equal(session.act("release").verdict, "release");
  const reset = session.act("reset");
  assert.equal(reset.stage, "inspect");
  assert.equal(reset.seed, 101);
});

test("control shows a near match from an independent object with preserved train membership and generalization", () => {
  const control = fixtures.cases[1];
  assert.equal(control.caseKind, "control");
  const rows = [...control.common, ...control.evaluation, ...control.audit];
  assert.equal(new Set(rows.map(row => row.groupId)).size, rows.length);
  assert.ok(rows.every(row => row.label === Number(row.features[0] > 0.5)));
  assert.deepEqual(control.reserve, []);
  assert.deepEqual(control.twins, []);
  const before = splitEvidence(control, false);
  assert.deepEqual(before, {trainCount: 120, overlap: 0, evalCorrect: 36, auditCorrect: 57});
  assert.deepEqual(splitEvidence(control, true), before);
  const shown = createSession([control]).getState();
  assert.notEqual(shown.sample.group, shown.neighbor.group);
  assert.ok(Math.hypot(...shown.sample.features.map((x, i) => x - shown.neighbor.features[i])) < 0.07);
});

test("both decisions are accepted on both case types, independent evidence and guidance appear only after voting", () => {
  for (const fixture of fixtures.cases) {
    for (const verdict of ["release", "hold"]) {
      const session = createSession([fixture]);
      const start = session.getState();
      const inspected = session.act("inspect");
      const reviewed = session.act("repair");
      for (const pending of [start, inspected, reviewed]) {
        for (const field of ["expectedVerdict", "rationale", "auditCorrect", "leakyAuditCorrect", "auditTotal"]) {
          assert.equal(pending[field], null);
        }
        assert.equal(pending.initialOverlap, fixture.caseKind === "control" ? 0 : 40);
      }
      const done = session.act(verdict);
      assert.equal(done.verdict, verdict);
      assert.equal(done.expectedVerdict, fixture.caseKind === "control" ? "release" : "hold");
      assert.ok(done.rationale.length > 20);
    }
  }
});

test("restores valid positions and safely ignores corrupted or obsolete storage", () => {
  const storage = memoryStore();
  const first = createSession(fixtures, storage);
  first.act("inspect");
  first.act("repair");
  assert.deepEqual(createSession(fixtures, storage).getState(), first.getState());
  first.act("hold");
  assert.equal(createSession(fixtures, storage).getState().verdict, "hold");
  for (const corrupt of ["{", '{"version":0}', '{"version":1,"seed":0,"stage":"done","verdict":"hold"}',
    '{"version":2,"seed":80,"stage":"done","verdict":"hold"}',
    '{"version":2,"seed":0,"stage":"done","verdict":null}']) {
    assert.equal(createSession(fixtures, memoryStore(corrupt)).getState().stage, "inspect");
  }
  const denied = {getItem() { throw Error("access denied"); }, setItem() { throw Error("disk full"); }};
  assert.equal(createSession(fixtures, denied).act("inspect").stage, "repair");
});
