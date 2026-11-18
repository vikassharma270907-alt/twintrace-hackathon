const SAVE_VERSION = 2;
const SAVE_KEY = "twintrace.review.v2";
const STAGES = ["inspect", "repair", "vote", "done"];
const VERDICTS = ["hold", "release"];

// Squared Euclidean distance preserves the ranking without a square root.
export function nearestNeighbor(training, sample) {
  let best = null;
  let bestDistance = Infinity;
  for (const candidate of training) {
    let distance = 0;
    for (let i = 0; i < sample.features.length; i += 1) {
      const gap = sample.features[i] - candidate.features[i];
      distance += gap * gap;
    }
    if (distance < bestDistance) {
      best = candidate;
      bestDistance = distance;
    }
  }
  return best;
}

export function splitEvidence(fixture, repaired) {
  const training = [...fixture.common, ...(repaired ? fixture.reserve : fixture.twins)];
  const trainGroups = new Set(training.map(row => row.groupId));
  const overlap = new Set(fixture.evaluation.filter(row => trainGroups.has(row.groupId))
    .map(row => row.groupId)).size;
  const countCorrect = rows => rows.reduce(
    (count, row) => count + Number(nearestNeighbor(training, row).label === row.label), 0
  );
  return {
    trainCount: training.length,
    overlap,
    evalCorrect: countCorrect(fixture.evaluation),
    auditCorrect: countCorrect(fixture.audit),
  };
}

function loadPosition(storage, cases) {
  try {
    const saved = JSON.parse(storage?.getItem(SAVE_KEY) ?? "null");
    if (saved?.version === SAVE_VERSION && cases.some(row => row.seed === saved.seed) &&
        STAGES.includes(saved.stage) &&
        (saved.stage === "done" ? VERDICTS.includes(saved.verdict) : saved.verdict === null)) {
      return { seed: saved.seed, stage: saved.stage, verdict: saved.verdict };
    }
  } catch {
    // In-memory review still works when persisted data or storage is unavailable.
  }
  return { seed: cases[0].seed, stage: "inspect", verdict: null };
}

export function createSession(fixtures, storage) {
  const cases = Array.isArray(fixtures) ? fixtures : fixtures.cases;
  let position = loadPosition(storage, cases);
  const evidenceBySeed = new Map();

  function getState() {
    const index = cases.findIndex(row => row.seed === position.seed);
    const fixture = cases[index];
    if (!evidenceBySeed.has(fixture.seed)) {
      const sample = fixture.evaluation.find(row => row.id === fixture.inspectId)
        ?? fixture.evaluation[fixture.seed % fixture.evaluation.length];
      const neighbor = nearestNeighbor([...fixture.common, ...fixture.twins], sample);
      evidenceBySeed.set(fixture.seed, {
        before: splitEvidence(fixture, false), after: splitEvidence(fixture, true),
        sample: { ...sample, group: sample.groupId },
        neighbor: { ...neighbor, group: neighbor.groupId }
      });
    }
    const { before, after, sample, neighbor } = evidenceBySeed.get(fixture.seed);
    const repaired = position.stage === "vote" || position.stage === "done";
    const finished = position.stage === "done";
    const control = fixture.caseKind === "control";
    const active = repaired ? after : before;
    return {
      ...position, sample, neighbor, overlap: active.overlap, trainCount: active.trainCount,
      caseKind: fixture.caseKind, initialOverlap: before.overlap,
      expectedVerdict: finished ? (control ? "release" : "hold") : null,
      rationale: finished ? (control
        ? "These similar shapes come from different objects. The first feature carries a repeatable signal on unseen groups in this synthetic case."
        : "The same objects appeared on both sides. Once separated, the score gives little support for releasing this model on new objects.") : null,
      evalCorrect: active.evalCorrect, evalTotal: fixture.evaluation.length,
      leakyEvalCorrect: before.evalCorrect, cleanEvalCorrect: repaired ? after.evalCorrect : null,
      auditCorrect: finished ? after.auditCorrect : null, auditTotal: finished ? fixture.audit.length : null,
      leakyAuditCorrect: finished ? before.auditCorrect : null, caseCount: cases.length, caseIndex: index
    };
  }

  function selectCase(targetSeed) {
    const target = cases.find(row => row.seed === targetSeed);
    if (target) {
      position = { seed: target.seed, stage: "inspect", verdict: null };
      try {
        storage?.setItem(SAVE_KEY, JSON.stringify({ version: SAVE_VERSION, ...position }));
      } catch {
        // Storage may be blocked or full. Keep the current in-memory position.
      }
    }
    return getState();
  }

  function act(action) {
    const { stage } = position;
    if (action === "reset") {
      position = { seed: position.seed, stage: "inspect", verdict: null };
    } else if (action === "next" && stage === "done") {
      const index = cases.findIndex(row => row.seed === position.seed);
      position = { seed: cases[(index + 1) % cases.length].seed, stage: "inspect", verdict: null };
    } else if (typeof action === "string" && action.startsWith("case:")) {
      const targetSeed = Number(action.slice(5));
      return selectCase(targetSeed);
    } else if (action === "inspect" && stage === "inspect") {
      position = { ...position, stage: "repair" };
    } else if (action === "repair" && stage === "repair") {
      position = { ...position, stage: "vote" };
    } else if (VERDICTS.includes(action) && stage === "vote") {
      position = { ...position, stage: "done", verdict: action };
    } else {
      return getState();
    }
    try {
      storage?.setItem(SAVE_KEY, JSON.stringify({ version: SAVE_VERSION, ...position }));
    } catch {
      // Storage may be blocked or full. Keep the current in-memory position.
    }
    return getState();
  }

  return { getState, act, selectCase };
}
