# Split Lab VR (`Twin Trace`): Official 110-Second Demo Video Treatment & Production Guide

> **Target Competition:** [Meta VR Start Developer Competition 2026](https://start-developer-competition-26.devpost.com/)  
> **Track:** Productivity | **Division:** New Experience  
> **Target Video Runtime:** Exactly **110 Seconds** (Strict requirement: <180 seconds)  
> **Platform / Capture:** Meta Quest 2 / Quest 3 / Quest Pro (Native 1080p 60 FPS) or Chromium WebXR Emulator  
> **Input Mode:** 100% Controller-Free Hands Tracking (Pinch, Grasp, Ray, Palm Tap)

---

## 1. Director's Vision & Narrative Treatment

### The Creative Thesis
Most machine learning demo videos fail with judges because they show static charts or code editors. **Split Lab VR** treats data leakage not as an abstract math problem, but as a spatial mystery. In VR, an invisible statistical flaw becomes a physical crime scene: an apparently "100% accurate" classifier is caught cheating when a physical 3D light-vector connects an evaluation card directly to its twin in the training tray.

### Cinematic Grammar
- **POV:** Seated First-Person Developer Perspective (natural eye-level, 1.2m virtual elevation, seated desk posture).
- **Camera Movement:** Deliberate, smooth head motion (avoiding fast saccades; smooth 0.5s damped rotation for comfortable viewer consumption).
- **Hands Staging:** Virtual hands remain in lower-third peripheral view when resting, entering active workspace only for intentional gestures.
- **Lighting & Atmosphere:** Focused desk luminaire illuminating the active trays, with soft ambient falloff into deep neutral charcoal background.

---

## 2. Second-by-Second Shot List, Camera Grammar & Voiceover Script

| Time | Scene & Spatial Action | Visual Focus & On-Screen UI | Sound Design / Audio Score | Voiceover Narration Script |
|---|---|---|---|---|
| **0:00–0:15** | **The Hook: The 100% Illusion**<br/>Developer sits at seated desk. Masthead shows *Split Lab*. Central dashboard displays **Evaluation Accuracy: 100.0%**. | Camera focuses on the glowing green **100%** badge. Below it, the **Unseen Audit Tray** is locked behind a frosted glass shield marked *“SEALED”*. | Soft, low-frequency atmospheric hum (C-minor). A crisp digital chime sounds as the accuracy badge pulsates. | *"Every year, machine learning models fail in production because of catastrophic data leakage. Here, our classifier claims 100% accuracy on evaluation. Would you release this model to the real world? Let's check with our hands."* |
| **0:15–0:40** | **The Spatial Discovery: The Twin Trace**<br/>Right hand enters scene. Developer extends index and thumb to pinch the highlighted evaluation card (`test-E00`). As the card lifts 10cm, an illuminated cyan 3D beam streaks across the partition directly into the training tray. | Specimen viewer rotates 30° to show the 6D feature contour. The beam connects `test-E00` to `twin-E00`. Both specimens pulse with identical red/blue striped textures and show `Origin: Group E00`. | Tactile grab sound (soft wooden snap). The connecting beam emits a resonant, rising crystalline harmonic drone. | *"In seated VR, we pinch the test specimen. Immediately, a 3D ray illuminates across the split into the training tray. Look at the group origin and feature fingerprint: they are identical twins. The model hasn't learned to generalize—it simply memorized a training duplicate."* |
| **0:40–1:05** | **The Physical Repair**<br/>Developer drags `twin-E00` off the training tray into the red-bordered quarantine bin. The action button illuminates: **[Apply to All Matching Groups]**. Developer taps the virtual button with index finger. | The 40 duplicate training twins dissolve. 40 fresh reserve groups slide in. The **Overlap Metric** drops from **40 → 0**. The **Evaluation Accuracy** instantly recalculates from **100.0% → 47.5%**. | A deep mechanical sliding thud as the partition snaps shut. A resolving, honest bass drop as the score plummets. | *"We drag the twin into quarantine and apply group disjointness across the whole dataset. 40 duplicate entities are evicted and replaced with fresh reserve groups. The training size remains exactly 120, but the measured evaluation score drops to 47.5%. The illusion is broken."* |
| **1:05–1:30** | **The Blind Audit Revelation**<br/>The prompt asks: *“Predict the audit score: Will unseen objects perform better, worse, or equal to 47.5%?”* Developer taps **[Hold Release]**. The frosted shield over the 60-sample audit tray shatters and clears. | Audit Tray displays **18 of 60 correct (30.0%)**. A model review receipt generates with the verdict: **RELEASE HELD — TASK HAS NO SIGNAL (CHANCE BASELINE ~50%)**. | Glass shatter cue followed by an affirming, warm harmonic chime. Ledger metrics lock into place. | *"Now we predict what happens on unseen data. We select 'Hold Release' and unseal the independent 60-sample audit tray. It scores just 18 out of 60—pure chance. The dropping evaluation score wasn't a defect; it was a successful repair that saved our team from a disastrous deployment."* |
| **1:30–1:50** | **The Control Case Verification**<br/>Developer taps **[Open Next Case]**. Case 2 loads: An honest classifier boasting 90.0% accuracy. Developer pinches a specimen; the nearest training neighbor has a completely different origin (`R014` vs `E002`). | Developer taps **[Confirm Current Split]**. Audit tray reveals **57 of 60 correct (95.0%)**. Developer selects **[Approve Release]**. Green checkmark stamp appears. | Uptempo, confident acoustic-electronic arpeggio. Dual high-tone success chimes. | *"To ensure we don't reject honest models, Case 2 presents a classifier with 90% accuracy. We trace the nearest neighbor: it shares similar features, but originates from an independent group. We confirm the split, verify against the audit, and approve release."* |
| **1:50–1:55** | **Outro & Call to Action**<br/>Wide view of the complete Split Lab workbench. Meta Quest hands-only badge and GitHub repository links appear on a floating card. | Title card: **Split Lab VR — Hands-First Spatial Data Leakage Workshop**. Built with Meta IWSDK. | Final musical resolve with gentle ambient decay. | *"Split Lab VR. Making high-dimensional data leakage visible, tangible, and solvable with your hands."* |

---

## 3. Headset Audio & Sound Design Architecture

1. **Spatialized 3D Audio:**
   - The connecting beam resonance is positioned strictly at the midpoint between the two cards in 3D space (`x: 0, y: 1.15m, z: -0.45m`).
   - Button taps emit non-fatiguing, tactile micro-clicks with low reverberation.
2. **Accessible Visual Redundancy:**
   - Every audio cue is paired with a distinct visual feedback state (color pulse, geometric boundary change, haptic ray thickening) for hearing-impaired judges.
3. **Voiceover Mixing:**
   - Normalization: Target -14 LUFS integrated loudness.
   - Ducking: Background ambient music ducks by -8 dB during spoken narration passages.

---

## 4. Headset Recording & Capture Guidelines

### Option A: Meta Quest 2 / 3 / Pro (Recommended)
1. Connect Quest to PC via USB-C.
2. Enable Quest Developer Mode. In **Quest Developer Hub (QDH)** or **SideQuest**, set capture settings:
   - **Resolution:** `1920x1080` (Full HD landscape)
   - **Frame Rate:** `60 FPS`
   - **Bitrate:** `25,000,000 bps (25 Mbps)`
   - **Full Fov:** Disabled (crop to comfortable central 16:9)
3. Launch the deployed WebXR URL in the Meta Quest Browser.
4. Ensure controllers are turned off or set aside so the headset switches to optical hand tracking.
5. Tap **Record Screen** from the Quest system menu, execute the 110-second run, and stop recording.

### Option B: Chromium WebXR Emulator (Fast Fallback)
1. Launch Chromium with the official **WebXR API Emulator Extension**.
2. Select **Meta Quest 3** device profile with **Hands** mode active.
3. Capture the browser window at 1920x1080 60 FPS using OBS Studio.
4. Animate the hand poses using the emulator slider controls.
