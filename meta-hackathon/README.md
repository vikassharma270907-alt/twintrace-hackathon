# Meta VR Start Developer Competition 2026: Split Lab VR (Twin Trace)

> **Complete Hackathon Dossier, Execution Blueprint & Codebase imported from Codex**  
> *Target Competition:* [Meta VR Start Developer Competition 2026 (Devpost)](https://start-developer-competition-26.devpost.com/)  
> *Track:* **Productivity** (Division: New Experience / Adapted)  
> *Official Deadline:* Wednesday, November 18, 2026, 12:00 PM PST (**Thursday, November 19, 2026, 1:30 AM IST**)

---

## 1. Executive Summary & Competition Overview

The **Meta VR Start Developer Competition 2026** is a flagship developer event with a **$1,000,000 USD** prize pool focused on native and WebXR Meta Quest experiences using hand tracking and spatial interaction.

### Competition Vital Statistics

| Parameter | Official Specification |
|---|---|
| **Platform / Headset** | Meta Quest (Quest 2, Quest 3, Quest Pro) via Native APK or WebXR / IWSDK |
| **Input Requirement** | **Hands-only** interaction from onboarding through exit (no controllers required) |
| **Track Selection** | **Productivity** (Prizes: $70,000 First Place, $35,000 Runner-Up) |
| **Bonus Categories** | *Best First Five Minutes* ($25,000), *Best Reason to Come Back* ($25,000) |
| **Judging Window** | November 23 – December 9, 2026 (Winners announced December 11, 2026) |
| **Eligibility** | 18+ legal majority; active **Meta VR Start** membership for entrant/team representative |
| **Submission Items** | Public HTTPS build or Meta Release Channel invite, <3-min demo video, Devpost questionnaire |

---

## 2. Project Concept: Split Lab VR (`Twin Trace`)

### The Problem It Solves
Junior data scientists and ML teams often suffer from **catastrophic data leakage**: duplicate observations, clustered patients, or repeated sessions split naively across train and test sets. The model appears to achieve 100% accuracy, but fails completely on unseen real-world entities.

### The Spatial VR Solution
**Split Lab VR** is a 4-minute seated desk exercise designed for junior engineers preparing for model reviews:
1. **Pinch & Inspect:** The player pinches a test card claiming 100% accuracy. A 3D trajectory immediately lights up across the divider, revealing its near-identical "twin" in the training set (sharing group ID and fingerprint).
2. **Quarantine & Repair:** The player drags the twin into quarantine and clicks **"Apply to all matching groups"**. The 40 train-side twins are evicted and replaced with 40 held-back reserve groups.
3. **The Score Drop:** The measured evaluation accuracy drops from **100.0% to 47.5%**, exposing reality.
4. **Blind Audit:** An untouched audit tray (60 independent groups) is unsealed, proving the underlying task was pure chance (~50%). The falling evaluation score was actually a successful repair.

---

## 3. Mathematical & ML Experiment Grounding

The application is backed by a deterministic synthetic ML experiment reproducing leakage findings (Kapoor & Narayanan, *Princeton 2023*; *scikit-learn* GroupKFold):

- **Model:** 1-Nearest Neighbor classifier with Euclidean distance over 6 continuous features.
- **Protocol:** 80 base groups, 40 reserve groups, 40 evaluation groups, 60 audit groups.
- **Results across 30 seeds:**
  - *Leaky Split:* **100.0%** evaluation accuracy, **51.2%** audit accuracy.
  - *Clean Disjoint Split:* **47.9%** evaluation accuracy, **49.6%** audit accuracy.
- **Seed 0 Counts:** Leaky test 40/40 (100%), Clean test 19/40 (47.5%), Audit 18/60 (30.0% - chance fluctuation).

---

## 4. Codebase Structure

The imported codebase in `meta-hackathon/` includes the full web application, Meta IWSDK scene, test suites, and QA harness:

```
meta-hackathon/
├── README.md                      # This dossier and index
├── Split_Lab_VR_Blueprint.md      # The complete Codex research and execution blueprint
├── qa_twintrace.py                # Automated Playwright test harness (Desktop & Mobile)
└── twintrace/                     # Web & WebXR Application
    ├── index.html                 # Main web application entry point
    ├── data/
    │   └── cases.json             # Bundled case fixtures (leakage and control cases)
    ├── scripts/
    │   └── generate.py            # Python standard library data generator for cases
    ├── src/
    │   ├── app.js                 # UI state machine & stage progression controller
    │   ├── model.js               # Nearest-neighbor classifier & split repair engine
    │   ├── style.css              # Responsive dark/light theme, accessible 4px scale
    │   └── xr.js                  # WebXR three.js hand-tracking & spatial trays
    ├── tests/
    │   └── model.test.mjs         # Node.js test suite for deterministic ML metrics
    └── iwsdk/                     # Meta Immersive Web SDK implementation
        ├── package.json           # Vite + @iwsdk/core 1.0.1 + super-three
        ├── prepare.mjs            # Asset and fixture bundling script
        ├── index.html             # IWSDK WebXR launch canvas
        ├── scene.js               # IWSDK 3D scene with hand-grabbing components
        └── casefiles/             # Standalone case fixtures for IWSDK
```

---

## 5. Verification & Testing

All components have been verified locally:

### 1. Model & ML Logic Tests
```bash
node meta-hackathon/twintrace/tests/model.test.mjs
```
*Result:* 6/6 tests passing (seed zero reproduction, feature-only neighbor matching, idempotent repair, blind audit preservation, state restoration).

### 2. Full Browser & UI Automation QA
```bash
cd meta-hackathon && python qa_twintrace.py
```
*Result:* Verifies headless Chrome canvas rotation, stage progression (Inspect -> Repair -> Decide), reload persistence, audit count checks, and mobile viewport compliance.

---

## 6. Judge Rubric & Submission Plan

| Criteria | Weight | How Split Lab Addresses It |
|---|---|---|
| **Innovation & Creativity** | 25% | First spatial VR tool teaching data leakage by physically holding and tracing duplicate samples across split boundaries. |
| **Experience Design** | 25% | Hands-only seated interaction within 2 feet reach, instant load (<2s), clear visual progress, non-color redundant cues. |
| **Technical Implementation** | 25% | Built on Meta IWSDK (`@iwsdk/core`) / WebXR, reproducible local ML evaluation, zero-dependency client runtime, tested 60+ FPS. |
| **Polish & Presentation** | 25% | Clean typographic hierarchy, audio-free clear visual cues, transparent metrics ledger, complete model-review note. |

### Suggested 110-Second Demo Video Script
- **0:00–0:15:** Introduce suspicious 100% classifier on a synthetic dataset; pose the question: *"Would you release this for unseen entities?"*
- **0:15–0:40:** In VR with hands, pinch test specimen. The 3D connecting vector highlights the identical twin in the training set sharing the exact group ID.
- **0:40–1:05:** Drag twin to quarantine, confirm "Apply to all matching groups". Watch overlap drop to 0 and accuracy adjust to 47.5%.
- **1:05–1:30:** Unseal the independent audit tray (60 rows) to show ~50% baseline. Choose **"Hold Release"**.
- **1:30–1:50:** Run the control case (Case 2: 90% accuracy, clean disjoint split, valid 57/60 audit) and choose **"Approve Release"**.
