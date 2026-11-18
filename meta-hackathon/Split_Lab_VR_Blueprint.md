# Split Lab VR
## A hands-first data-leakage workshop for the Meta VR Start Developer Competition 2026

Research and execution blueprint · 18 November 2026 · All build times are estimates in India Standard Time.

**Decision.** Build a four-minute desk exercise for junior data scientists preparing for model reviews. The player investigates a suspiciously perfect classifier by lifting a test sample, tracing its nearest training match, repairing the dataset split, and deciding which evaluation to trust. Pitch it as a recurring professional practice tool in **Productivity**. The working title is provisional. The numerical experiment below was run for this report. The Quest app, hardware performance and user outcomes remain to be tested.

### 1. Entry decision

| Item | What matters for this build |
|---|---|
| Deadline | Wednesday 18 November at noon PST equals Thursday **19 November at 1:30 a.m. IST**. Aim to finish the form by 11:30 p.m. IST on the 18th. Judging runs November 23–December 9, with winners scheduled for December 11. [Schedule](https://start-developer-competition-26.devpost.com/details/dates) |
| Eligibility | Check age (18+ and legal majority at the start of the window), residence, developer account/access, Start membership and any project funding exclusions in the rules. India is not among the explicitly named excluded locations. Individual eligibility and hardware access are unverified. [Official rules §§2–4](https://start-developer-competition-26.devpost.com/rules) |
| Membership discrepancy | The rules name the representative or submitter. A later organizer notice says one team member can qualify a team. Have a confirmed member submit as the representative to satisfy both readings. A solo entrant needs their own confirmed membership. On the Meta VR Start homepage, choose **Apply today** and sign in. A benefits page indicates membership. An application form indicates an application is still needed. [Organizer notice](https://start-developer-competition-26.devpost.com/updates/47802-action-required-verify-your-meta-vr-start-membership-for-the-developer-competition-before-the-submission-deadline) · [Rules §2](https://start-developer-competition-26.devpost.com/rules) |
| Track and division | Choose from Productivity, Gaming or Entertainment. Choose **New Experience** only if conceived and built within the window starting September 24 and eligible under the codebase restriction. An earlier prototype with substantial new functionality delivered during the window goes in **Adapted / Significantly Updated**. Decide from actual project history. A plan for another XR platform does not itself provide a working Quest build. [Rules §4](https://start-developer-competition-26.devpost.com/rules) |
| Fit and stakes | $1M across the competition. Productivity lists $70,000 first and $35,000 runner-up in each division. Best First Five Minutes and Best Reason to Come Back are each $25,000. Prioritize the former with a strong first round. A repeatable exercise would make the latter credible. [Prize list](https://start-developer-competition-26.devpost.com/) |
| What qualifies | Build a Meta VR/MR application with hands-only access from onboarding through exit. Seated reach within two feet, an experience under ten minutes, fast start, and clean pause/resume are design guidelines. A playable, original experience matters more than the number of services integrated. [Competition overview](https://start-developer-competition-26.devpost.com/) |

**Decision under time pressure.** If you have Start membership, an established native Quest workflow and a qualifying existing app, compare the effort of adding this mode against starting fresh. Document the previous build and the new mode if adapting. For a fresh build, use Meta IWSDK and a publicly accessible HTTPS link. Prove that the blank deployed scene accepts a real or emulated hand before writing lesson content. If eligibility cannot be established in time, the same build and experiment make a standalone ML/XR portfolio project.

### 2. Why choose this idea?

| Candidate | Strongest moment | Delivery trade-off |
|---|---|---|
| **Split Lab VR** | Lift one test card and its almost identical training twin lights up across the divider. Fix the overlap and watch the measured score fall. | One scene, a tiny real classifier, no external calls. The gesture exposes the exact cause of the evaluation error. |
| A gesture-based neural network sandbox | Move units and watch a decision boundary change. | TensorFlow Playground already covers much of the value in a browser. A Quest entry would need a specific spatial problem worth solving. |
| An adaptive spatial interview deck | Rebuild a workflow from memory before an interview. | Suits repeated practice, but generic cards underuse depth and a personalized scheduler needs repeat visits to evaluate. |

The user's interest in data internships suggests a clear first audience. Aim for the moment in a model review when a colleague asks, “Would it still work on an entirely new item?” After the round, the learner should be able to describe why two observations of one object must stay together when evaluating performance on unseen objects. That explanation is also a meaningful test of transfer outside VR.

### 3. Four-minute experience

| Time | Player action | Software response |
|---|---|---|
| 0:00–0:20 | Seated, tap **Start** and pinch the highlighted test card. A dashboard claims 100% accuracy. | The card enlarges and a line reveals the nearest training example. The pair shares a distinctive edge pattern and group ID. |
| 0:20–1:05 | Inspect two or three representative pairs. The training and evaluation trays sit side by side. | Render triangles or stripes as well as color. On release, the card snaps home. The two tray counts and a visible sample/total label distinguish examples from the full dataset. |
| 1:05–2:10 | Move one surfaced twin into quarantine, then confirm **Apply to all matching groups**. | Remove the 40 train-side twins, replace them with 40 fresh held-back training examples, keep the evaluation tray fixed, and rerun the classifier. Show both training-set sizes and overlap counts. |
| 2:10–3:00 | Predict what happens on an untouched audit tray, then reveal it. Choose which score you would send to a teammate assessing generalization. | Display correct/total, accuracy, and one sentence explaining the chosen split. A falling score is a successful repair. |
| 3:00–4:00 | Try a fresh case without the highlighted clue. Optionally pause midway and resume. | Save the stage and seed locally. Offer a model-review note containing the split unit, group overlap and audit score. |

**Spatial design.** Keep the divider, pair, metrics and next action together in front of the seated player. Highlight the full 3D path between the two samples only while inspecting. Make labels face the player, keep the audit tray collapsed until needed, and support the same flow with one hand. Provide point-and-pinch selection for reduced reach. A tracking interruption returns the held item to its last valid location. Undo reverses the last confirmed move. Use an opaque VR environment for the first release. The task has no dependence on room geometry.

### 4. The ML experiment behind the demo

**Question.** Can a model appear perfect even when there is no information that predicts the labels of new objects? Use a one-nearest-neighbor classifier with Euclidean distance over six numeric features. Think of a feature vector as a synthetic object's fingerprint. A second reading of the same object has a nearly identical fingerprint. Assign balanced random binary labels independently of those features. Keep group IDs exclusively for visualization and split auditing.

**Protocol.** Each seed generates 80 base training groups, 40 reserve groups, 40 evaluation groups and 60 independent audit groups. The leaky training set contains the base training rows plus another observation of every evaluation group. The clean training set uses the same base rows plus the reserve groups. Both training sets contain 120 rows. Both models are evaluated on exactly the same 40 evaluation rows and 60 audit rows. This deliberately isolates the effect of group leakage.

| Mean accuracy across seeds 0–29 | Evaluation tray (40 rows/seed) | Untouched audit tray (60 rows/seed) |
|---|---:|---:|
| Training contains evaluation twins | 100.0% | 51.2% |
| Disjoint training groups | 47.9% | 49.6% |

The audit is close to the 50% chance baseline under both conditions. Cleaning the split improves what the evaluation tells you. The underlying task on new groups still lacks predictive signal. For the single seed 0, the four correct counts are 40/40, 26/60, 19/40 and 18/60 respectively. A single small audit can deviate considerably from 50%. Show counts and the active seed. Keep aggregate results labeled as a 30-seed experiment. These are synthetic model results, separate from future usability and learning measurements.

**Reproduce the table.** Run the following with standard-library Python. Tested with Python 3.14.6. Export the resulting group IDs, features and labels from the same generator if the web build should match these seeds exactly. A JavaScript random-number generator will ordinarily yield different rows for the same seed.

```python
import math
import random
import statistics


def run_case(seed):
    rng = random.Random(seed)

    def make_groups(count):
        labels = [0, 1] * (count // 2)
        rng.shuffle(labels)
        return [(tuple(rng.random() for _ in range(6)), label) for label in labels]

    def observe(groups):
        return [
            (tuple(value + rng.uniform(-0.005, 0.005) for value in center), label)
            for center, label in groups
        ]

    def accuracy(training, rows):
        correct = 0
        for features, expected in rows:
            nearest = min(training, key=lambda row: math.dist(row[0], features))
            correct += nearest[1] == expected
        return correct / len(rows)

    base, reserve, evaluation, audit = [make_groups(n) for n in (80, 40, 40, 60)]
    common, fresh, twins, test_rows, audit_rows = [
        observe(groups) for groups in (base, reserve, evaluation, evaluation, audit)
    ]
    return [
        accuracy(training, rows)
        for training in (common + twins, common + fresh)
        for rows in (test_rows, audit_rows)
    ]


results = [run_case(seed) for seed in range(30)]
print("Seed zero:", results[0])
print("Mean accuracies:", [statistics.mean(col) for col in zip(*results)])
```

### 5. Research and projects worth borrowing from

| Source | Evidence | Decision for Split Lab |
|---|---|---|
| Kapoor & Narayanan, *Leakage and the reproducibility crisis in machine-learning-based science* (2023) | Survey identified leakage in 294 papers spanning 17 fields and developed a taxonomy of eight types. | Teach one reproducible failure well. End every case with a tiny model-review note naming the independent unit and suspected leak. [Paper](https://collaborate.princeton.edu/en/publications/leakage-and-the-reproducibility-crisis-in-machine-learning-based-/) |
| scikit-learn grouped cross-validation and leakage example | Grouped splits keep the same entity out of both folds. The feature-selection example independently illustrates inflated accuracy with random targets. | Enforce group disjointness in the first case. Feature selection before splitting is a distinct, suitable second lesson. [Grouped splits](https://scikit-learn.org/stable/modules/cross_validation.html#group-k-fold) · [Leakage example](https://scikit-learn.org/stable/common_pitfalls.html#data-leakage) |
| *Cubism*, Thomas Van Bouwel | A released Quest puzzle built around assembling small blocks, with hand tracking. | Use a small number of graspable items with distinct silhouettes. The learner manipulates sample relationships and gets an auditable score as feedback. [Developer press kit](https://www.cubism-vr.com/presskit/index.html) |
| *Inkphony*, prior Meta Horizon Start Best Lifestyle winner | Handwriting notes and scrubbing a surface to erase them give the gesture a specific musical meaning. | Give the pinch one investigative meaning: show this sample's closest training match. The previous award belongs to a different competition year and category. [Project](https://devpost.com/software/inkphony) · [Winner announcement](https://start-developer-competition.devpost.com/updates/39655-and-the-winners-are) |
| TensorFlow *Neural Network Playground* | Interactive browser project makes model behavior visible as inputs and controls change. | Keep a manipulable decision and its measured consequence visible together. Use original assets and a local nearest-neighbor routine in the entry. [Live project](https://playground.tensorflow.org/) · [Code](https://github.com/tensorflow/playground) |
| Roediger & Karpicke, *Test-enhanced learning* (2006) | On prose passages, prior recall practice outperformed repeated study at delayed tests, whereas restudy performed better on the immediate five-minute test. | Ask for an unprompted diagnosis on a fresh case. Measure next-day transfer before making a learning claim. The study did not test VR or data science training. [Paper](https://pubmed.ncbi.nlm.nih.gov/16507066/) |
| Settles & Meeder, half-life regression (2016) | An interpretable model estimates retention from practice history. Duolingo released an implementation and comparison baselines for language learning. | A later ML feature can schedule the next case using elapsed time, unaided correctness and prior attempts per concept. First ship a transparent retry rule and compare prospective predictions. [Paper](https://aclanthology.org/P16-1174/) · [Repository](https://github.com/duolingo/halflife-regression) |
| Piech et al., *Deep Knowledge Tracing* (2015), and pyKT | A recurrent neural network predicts future responses from learner interaction sequences. pyKT supplies implementations and benchmarking tools. | A later DL experiment can rank which misconception to revisit. Train on consented practice histories across multiple topics, split by person, respect time order, and compare with the simple scheduler. Existing education datasets can verify your implementation but cannot validate this product. [Paper](https://proceedings.neurips.cc/paper/2015/file/bac9162b47c56fc8a4d2a519803d51b3-Paper.pdf) · [Author code](https://github.com/chrispiech/DeepKnowledgeTracing) · [pyKT](https://github.com/pykt-team/pykt-toolkit) |

### 6. Build architecture and evidence

Use TypeScript and Meta IWSDK for a new web implementation. Use the documented grabbing and ray-interaction components and deploy via HTTPS. Validate immersive launch through an explicit user action. Keep datasets bundled locally so the exercise has no inference-service dependency. [IWSDK examples](https://developers.meta.com/vr/documentation/iwsdk/examples/) · [Interaction guide](https://developers.meta.com/vr/documentation/iwsdk/guides/06-built-in-interactions/)

| Component | Inputs and responsibility | Acceptance check |
|---|---|---|
| Scenario data | Versioned seed fixture with sample ID, group ID, six numeric features, binary label and role. Keep reserved and audit groups immutable. | Independent test recomputes metrics from the fixture. Leaky overlap is 40 groups. Clean overlap is zero. Audit overlap is always zero. |
| Model and scoring | Nearest neighbor on features only. Show nearest match on inspect. Compute accuracy after each confirmed full repair. | Same seed and split reproduce the same counts. A repeated confirmation keeps 120 training rows. No guessed scores. |
| Interaction | Enter, inspect, quarantine, confirm repair, predict, reveal, retry. Use large targets, snap previews, short reach, undo and a hand-accessible recenter control. | One-handed run completes. A failed grab cannot advance the lesson or lose a card. |
| Persistence | Save schema version, seed, stage, chosen answer and split mode after each committed action. Rehydrate from the bundled fixture. | Resume after tab reload or headset removal without double-counting or revealing the audit early. |
| Acceptance evidence | Capture device type, run duration, observed frame rate, input mode, completion, misgrabs and whether help was needed. | Target the rubric's 60 fps minimum on Quest, including during recomputation. Record hardware findings separately from emulator results. [Judging rules](https://start-developer-competition-26.devpost.com/rules) |

**Learning evaluation after the deadline.** Compare the same exercise in VR and on a flat screen with equivalent content and timing. Counterbalance order or randomly assign learners. On the next day, show an unfamiliar case such as multiple photos of one product and ask for the correct split unit and an explanation. Track unaided transfer accuracy and completion time separately. A first five-person pilot measures friction; it is too small to support a broad efficacy claim. For a later learned scheduler compare Brier score and log loss for next-attempt correctness against a concept-level base rate and the fixed rule. Only add a sequence model if it improves held-out predictions and justifies the complexity.

### 7. To-do list in dependency order

The time boxes below are estimates from a working developer environment. Start with the entry gate. Preserve capture and upload time if setup takes longer.

- [ ] **First 30 minutes.** Confirm Start status, eligible representative, project history, access to a Quest or emulator and camera-free screen recording. Open Devpost and record which fields and build link are needed.
- [ ] **Next 60–90 minutes.** Deploy one original IWSDK scene to HTTPS. Enter it from the judge URL. Complete a hands-only grab, release and menu tap before implementing the model.
- [ ] **Next 90 minutes.** Export one seed fixture, port the scorer, add split-overlap checks. Show before/after and independent audit counts. Match the seed-zero counts in §4 if using the supplied generator.
- [ ] **Next two hours.** Implement the two trays, nearest-match thread, group repair and blind audit question. Give a specific explanation for the wrong answer. Add a fresh case if the full first case works.
- [ ] **Next hour.** Finish recenter, enlarged targets, pause/resume, undo, replay, reduced-reach input and text feedback. Test a failed release, a resumed round, and a fresh browser session.
- [ ] **Next hour.** Ask a first-time tester to finish hands-only. Time their first successful inspection and capture any instruction they misunderstood. Profile the busiest moment on Quest if available. Targets: first successful pinch within 30 seconds, full round within five minutes, no verbal coaching.
- [ ] **Reserve the final two hours before your internal cutoff.** Record genuine Quest or permitted emulator footage, upload the public video, open build and video links without developer privileges, complete the English Devpost form, and submit. Save confirmation. The recommended internal cutoff of 11:30 p.m. IST leaves two additional hours before the official close.
- [ ] **After submission.** Keep that exact build free and accessible through the winner announcement. Develop further features in a separate build. If the deadline passes without an eligible entry, publish the workshop as a portfolio project.

**Cut order if behind.** Drop personalization, sound effects, extra case families, and aggregate charts first. Preserve hands-only launch, one audited repair, measured scores, accessible judge link and real footage. If a second training module becomes feasible, use the scikit-learn feature-selection example and give it a separate concept ID.

### 8. Judge-facing proof and recording plan

| Criterion | Weight | Evidence to capture |
|---|---:|---|
| Innovation and creativity | 25% | One hand lifts an apparently independent test sample and exposes its twin in training. The gesture answers a genuine model-review question. |
| Experience design | 25% | A new user finishes seated with one hand, understands why the score dropped, and can retry independently. |
| Technical implementation | 25% | Show reproducible counts, stable tracking, resume, measured performance and a functioning judge URL. |
| Polish and presentation | 25% | Keep the same two trays and recognizable sample throughout the video. Use aligned counts, legible type and distinct non-color cues. |

Stage one checks viability and theme fit before scored judging. [Official rubric](https://start-developer-competition-26.devpost.com/rules)

**Suggested 110-second demo.** 0–12s: show the claim of perfect accuracy and ask whether it would survive a new object. 12–35s: use a hand to trace the first test example to its training twin. 35–60s: repair all groups and show overlap fall from 40 to zero. 60–80s: compare the measured score with the untouched audit tray. 80–100s: complete a fresh case unaided or resume a saved case, whichever is implemented. 100–110s: show the resulting model-review note and say who would use it tomorrow. Identify emulator footage if applicable.

**Submission inventory.** A tested IWSDK/WebXR link or, for a native build, an APK on the Meta developer dashboard's **Competition** channel plus its invite URL. Include a public YouTube or Vimeo video under three minutes with Quest or accepted emulator footage, and a Devpost entry with name, tagline, track, division, team information, inspiration, implementation, plans and target launch date if unpublished. For adapted entries describe and evidence the significant new work. Use English materials or the translations required by the rules. Confirm asset rights and free judge access through December 11. [Submission instructions](https://start-developer-competition-26.devpost.com/) · [Rules](https://start-developer-competition-26.devpost.com/rules)

**Definition of done.** A judge using a fresh link can launch, inspect, repair and explain one split with hands. The holdout remains untouched, the numerical result is reproducible, the submitted video shows that build, and every performance claim states the device on which it was measured.
