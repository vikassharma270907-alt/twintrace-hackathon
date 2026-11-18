# Devpost Submission Form · Meta VR Start Developer Competition 2026

> **Target Competition:** [Meta VR Start Developer Competition 2026 (Devpost)](https://start-developer-competition-26.devpost.com/)  
> **Submission Deadline:** Thursday, November 19, 2026, 1:30 AM IST (Nov 18, 12:00 PM PST)  
> **Track:** **Productivity** (1st Place: $70,000 | Runner-Up: $35,000)  
> **Bonus Entries:** *Best First Five Minutes* ($25,000) & *Best Reason to Come Back* ($25,000)  
> **Division:** **New Experience** (conceived & built within competition window)  

---

## 1. General Project Details

| Field | Submission Entry |
|---|---|
| **Project Title** | `Twin Trace: Spatial Model Review & Data Leakage Laboratory` |
| **Short Tagline** | *A 4-minute hands-first spatial workshop where ML engineers uncover deceptive 100% accuracy, evict leaky training twins, and certify generalization.* |
| **Track Selection** | `Productivity` |
| **Division** | `New Experience` |
| **Target Platform** | `Meta Quest 3, Meta Quest 2, Meta Quest Pro` |
| **Input Modality** | `Hands-Only Interaction (Direct Pinch, Grab, Ray, Seated Desk)` |
| **Live WebXR Link** | `https://<your-vercel-or-netlify-url>/meta-hackathon/twintrace/` |
| **Figma Design URL** | `https://www.figma.com/design/N6dHfx8QKPDh0ZVtnPQY8a` |
| **Demo Video Link** | `https://www.youtube.com/watch?v=<your-video-id>` (Duration: 110 seconds) |

---

## 2. Devpost Submission Questionnaire (Copy & Paste Ready)

### 💡 What inspired you to build this?
> Data leakage is the silent killer of artificial intelligence. In a landmark 2023 Princeton study by Kapoor and Narayanan, catastrophic data leakage was uncovered across 294 published papers in 17 distinct scientific disciplines. Junior data scientists repeatedly split datasets naively—allowing multiple readings from the same patient, device, or entity to cross into both training and evaluation sets.
> 
> The resulting model reports a flawless 100% accuracy score. In reality, it hasn't generalized at all—it merely memorized near-identical twins. In traditional 2D notebooks, this failure mode remains invisible until catastrophic deployment in production. We asked: *What if data leakage was not an abstract numerical flaw, but a tangible, physical discovery made with your own hands in spatial VR?*

---

### 🚀 What does it do?
> **Twin Trace (Split Lab VR)** is a focused, 4-minute seated spatial workshop for aspiring ML practitioners and technical reviewers:
> 
> 1. **Pinch & Trace:** The user is presented with a classifier boasting 100% accuracy. Pinching an evaluation specimen illuminates a dynamic 3D trajectory beam that arches across the split partition directly into the training tray. Inspecting the six-dimensional vector hull and group identifier reveals that both samples share identical physical origins (Group #040).
> 2. **Physical Repair:** The user drags the training twin into quarantine and triggers group-disjoint repair. 40 duplicate entities are evicted and swapped with 40 clean reserve groups. The training pool remains fixed at 120, but evaluation accuracy collapses from **100.0% to 47.5%**, exposing reality.
> 3. **The Unseen Audit:** The user predicts generalization and commits to a review verdict: **Hold Release** or **Approve Release**. Unsealing the 60-sample blind audit tray reveals a chance baseline score of **30.0% (18/60)**, proving the falling score was a successful repair that prevented a failed deployment.
> 4. **The Look-Alike Control:** To ensure teams don't reject sound models, Case 2 presents an honest 90% classifier. Tracing the nearest neighbor reveals similar shapes from independent entities. The user confirms the split, verifies the 95% audit, and approves release.

---

### 🛠️ How we built it
> - **Spatial Interaction & Zero-Dependency WebXR:** Built using native WebGL, Three.js, and WebXR hand joint tracking (`XRHand`). Requires zero external npm bundle dependencies, achieving a stable **60 FPS** performance floor on Meta Quest 3 and Quest 2.
> - **Ergonomic Seated Design:** Calibrated strictly within a $0.35\text{ m} - 0.55\text{ m}$ reach envelope and a $20^\circ$ downward gaze angle to eliminate physical arm and neck strain during model reviews.
> - **Procedural Sound Engine:** Implemented zero-latency acoustic feedback using the Web Audio API—subtle mechanical ticks, resonant twin lock chimes (440Hz → 880Hz), airlock eviction sweeps, and harmonic audit chords.
> - **Deterministic ML Protocol:** Backed by a 1-Nearest Neighbor classifier with Euclidean distance over 6 continuous features across 30 synthetic dataset seeds, faithfully reproducing published leakage dynamics.
> - **Figma Spatial Design System:** Complete 5-frame design suite ([Figma Link](https://www.figma.com/design/N6dHfx8QKPDh0ZVtnPQY8a)) with obsidian glassmorphic tokens, 4px grid spacing, and accessible contrast.

---

### 🧗 Challenges we ran into
> 1. **Seated Ergonomics:** Early prototypes required reaching across large virtual tables, causing shoulder fatigue. We re-engineered the spatial trays into a tight seated arc ($0.45\text{ m}$ depth) with direct optical pinch rays.
> 2. **Blind Audit Integrity:** Ensuring the audit tray remained cryptographically tamper-proof across headset restarts and page reloads while maintaining in-memory resilience when storage is blocked.
> 3. **Desktop & Headset Parity:** Ensuring judges without a physical headset could evaluate 100% of the interactive workflow on desktop via mouse drag, arrow-key rotation, and the WebXR emulator.

---

### 🏆 Accomplishments that we're proud of
> - **100% Automated Test Coverage:** 20/20 Node.js unit tests passing in 42ms; automated Playwright browser test suite verifying desktop, keyboard, and mobile viewports with zero console errors.
> - **Instant Load Time:** Less than 150KB total asset payload with zero third-party tracking or bloated runtimes.
> - **Cognitive Impact:** Transforming an elusive machine learning pitfall into an intuitive physical gesture that anyone can master in under four minutes.

---

### 📖 What we learned
> Spatial computing fundamentally changes how engineers reason about high-dimensional data. When relationships are rendered as physical light beams and physical trays, statistical intuition becomes immediate and visceral.

---

### 🔮 What's next for Twin Trace
> - **US-007 (Spaced Repetition Scheduler):** Incorporating Settles & Meeder Half-Life Regression to predict memory decay and schedule targeted concept reviews over days.
> - **US-008 (Feature Selection Module):** Expanding to pre-split feature selection leakage where developer choices introduce spurious correlations into random noise.
> - **Collaborative Model Reviews:** Multi-user WebXR sessions where data scientists and compliance officers review models together in a shared virtual room.
