# RecallRoom XR — 2-Minute Demo Video Storyboard

**Format:** 120-Second Pitch & Feature Walkthrough  
**Target Audience:** Meta XR Hackathon Judges, Spatial Computing Researchers, Cognitive Science Educators  
**Platform:** Meta Quest 3 / Quest Pro (Color Passthrough, OpenXR, Meta XR Interaction SDK v65+, MRUK)  
**Audio Style:** Confident, warm, articulate narration with subtle spatial audio cues (haptic clicks, chime confirmations, ambient room tone)  
**Camera Rig:** First-person Quest 3 capture with 500ms smooth damping (eliminating head jitters) + picture-in-picture third-person MR capture view. Seated eye-level (1.25m from floor).

---

## Storyboard Overview & Timeline

```
0:00        0:15               0:40              1:05                 1:25               1:45           2:00
|-----------|------------------|-----------------|--------------------|------------------|--------------|
  Hook:       Placement Mode:    Recall Mode:      Voice & Semantic:    Adaptive Review:   Summary &
  Method of   Spatial Anchors    Exploration &     Whisper + MiniLM     Priority Decay     Outro:
  Loci in MR  & MRUK Snapping    Spatial Cues      3-Tier Feedback      Algorithm          Impact
```

---

## Scene-by-Scene Breakdown

### Scene 1: The Hook — Method of Loci Reimagined (0:00 – 0:15)

* **Duration:** 15 Seconds (0:00 – 0:15)
* **Camera / Viewport:** 
  * Seated user perspective in a modern, naturally lit study room.
  * Passthrough is crystal-clear RGB with subtle depth shading.
  * Virtual floating title plate: `RecallRoom XR: Spatial Method of Loci for Deep Learning`.
* **Visual Action:**
  * The user sits comfortably at their desk. 
  * In the surrounding room, faint luminous constellation particles drift and snap onto physical surfaces (the desk, the bookshelf, the window sill, the lamp).
  * A floating 3D introductory card unfolds smoothly using a spring animation, displaying a deck selector: *"Neuroanatomy & Cellular Signaling"*.
* **On-Screen 3D UI & Spatial Holograms:**
  * Title banner: Neutral 800 background, crisp white 600-weight typography, subtle teal semantic accent border (`#0D9488`).
  * Floating Deck Card showing 10 concepts, estimated completion: 8 mins.
* **Exact Voiceover Script:**
  > *"For thousands of years, memory masters memorized libraries of knowledge using the Method of Loci—anchoring ideas to physical spaces. Today, RecallRoom XR brings this ancient cognitive superpower into mixed reality, supercharged with on-device spatial intelligence and local semantic AI."*
* **Sound Effects & Cues:**
  * Soft low-frequency swell transitioning into a crisp, glassy UI expansion chime. Subtle physical room tone.

---

### Scene 2: Placement Mode — Spatial Anchors & Surface Snapping (0:15 – 0:40)

* **Duration:** 25 Seconds (0:15 – 0:40)
* **Camera / Viewport:**
  * First-person seated eye-level (0.45m above desk plane). Camera tilted 20° downward, adhering to comfortable seated neck ergonomics.
* **Visual Action:**
  * User pinches a card labeled **"Action Potential — Depolarization"** from their floating palette using direct hand tracking (Meta Interaction SDK v65+).
  * As the card enters proximity with the user's physical desk lamp, the Meta MRUK (Mixed Reality Utility Kit) surface detection triggers: a luminous cyan contour outlines the lamp base.
  * The user releases the pinch. The card smoothly magnetic-snaps to the surface with a tactile `snick` sound, establishing a persistent `OVRSpatialAnchor`.
  * User then reaches toward their bookshelf (distance ray-pinch interaction) and places **"Myelin Sheath Saltatory Conduction"** onto the middle shelf.
  * Each card shows an interaction boundary within the 0.35m–0.60m ergonomic action sphere.
* **On-Screen 3D UI & Spatial Holograms:**
  * 3D Card dimensions: 18cm × 12cm × 0.8cm, rounded corners ($R=16\text{mm}$).
  * High-contrast dark slate backing (`#0F172A`), white text (`Inter Display`, 28pt equivalent), semantic badge: `Anatomy | High Priority`.
  * Surface snap highlight: Semi-transparent cyan grid ($30\%$ opacity) projected onto physical desk surface via MRUK surface raycasting.
* **Exact Voiceover Script:**
  > *"In Placement Mode, complex academic concepts become tangible spatial artifacts. Using Meta's Mixed Reality Utility Kit and spatial anchors, cards effortlessly snap to desks, walls, and real-world furniture within your natural seated reach envelope. Your physical environment is now your personal memory palace."*
* **Sound Effects & Cues:**
  * Tactile haptic tick on pinch grab; spatialized magnetic snap sound (`pop-snap.wav`) localized exactly at the lamp's 3D coordinate.

---

### Scene 3: Recall Mode — Spatial Retrieval & Exploration (0:40 – 1:05)

* **Duration:** 25 Seconds (0:40 – 1:05)
* **Camera / Viewport:**
  * User turns their gaze across the room.
  * Smooth damping tracks head rotation without disorientation.
* **Visual Action:**
  * The cards transition to **Recall Mode**.
  * The front-facing prompt is now occluded: instead of revealing the answer, the card displays a thought-provoking retrieval cue: *"What ion channels trigger the rapid upstroke of the neuronal action potential?"*
  * The card at the lamp pulses with an ambient breathing glow (amber glow indicates a review is due).
  * The user gazes at the card for 400ms: a subtle proximity reticle appears, expanding smoothly to invite voice response.
* **On-Screen 3D UI & Spatial Holograms:**
  * Cue card: `#1E293B` card face with glowing amber indicator dot (`#F59E0B`) signaling an active recall trial.
  * 3D Voice Bar: Below the card, an unobtrusive pill appears: `[ Hold Pinch or Say "Answer" to Record ]`.
  * Context hint icon (small question mark) available on the card collar if the user experiences tip-of-the-tongue stall.
* **Exact Voiceover Script:**
  > *"When you return in Recall Mode, the room transforms into an active retrieval arena. Cards test your memory in situ. Walking or glancing through the room activates your brain's place and grid cells, unlocking spatial episodic memory cues that traditional flashcards can never replicate."*
* **Sound Effects & Cues:**
  * Gentle spatial hum emanating from the target card, directional audio panning as the user looks from left to right.

---

### Scene 4: Voice & Semantic AI Scoring — Whisper + MiniLM (1:05 – 1:25)

* **Duration:** 20 Seconds (1:05 – 1:25)
* **Camera / Viewport:**
  * Close focus on the card placed on the desk lamp (0.42m distance, crisp rendering).
* **Visual Action:**
  * User pinches and holds the floating microphone pill (or activates voice trigger).
  * User speaks clearly: *"Voltage-gated sodium channels open rapidly, allowing sodium ions to rush into the axon."*
  * Real-time spatial waveform animates in teal frequencies directly under the card.
  * User releases pinch. Within 380 milliseconds:
    * The backend transcribes audio with Whisper.
    * `all-MiniLM-L6-v2` compares the 384-dimensional embedding against the ground-truth rubric.
    * Score arrives: **Cosine Similarity: 0.88 (`CORRECT`)**.
  * Card flashes an elegant emerald green confirmation wave (`#10B981`), reveals the full canonical explanation, and updates its mastery tier.
* **On-Screen 3D UI & Spatial Holograms:**
  * Waveform visualizer: 12 vertical bars modulating with RMS amplitude.
  * Result HUD:
    * Badge: `CORRECT (88% Match)` in emerald green pill.
    * Delta: `+12 Mastery Points`.
    * Semantic Feedback: *"Key concepts identified: Voltage-gated Na+ channels, rapid influx."*
* **Exact Voiceover Script:**
  > *"Just speak your answer naturally. RecallRoom XR streams your speech to our local FastAPI service, transcribing with Whisper and scoring semantic similarity in under four hundred milliseconds using sentence embeddings. No rigid keyword matching—it understands conceptual mastery."*
* **Sound Effects & Cues:**
  * Audio record start tone (soft dual beep); rapid processing tick; triumphant two-note confirmation chord (`correct-chime.wav`).

---

### Scene 5: Adaptive Review Engine — Spaced Memory Decay (1:25 – 1:45)

* **Duration:** 20 Seconds (1:25 – 1:45)
* **Camera / Viewport:**
  * Wider field of view showing three cards across the room: Desk Lamp, Bookshelf, Window Sill.
* **Visual Action:**
  * The user moves to the second card on the bookshelf: *"What accelerates conduction speed across myelinated axons?"*
  * User speaks hesitantly: *"The potassium pumps along the membrane?"*
  * Similarity evaluates to **0.34 (`RETRY`)**.
  * The card pulses in soft coral red (`#EF4444`). A targeted hint unfolds: *"Think about the gaps between myelin sheaths named after Ranvier."*
  * Instantly, the Adaptive Scheduler recalculates priorities across all room anchors using the formula:
    $$\text{Priority} = 0.45(1 - R) + 0.25 T + 0.20 H + 0.10 L$$
  * The bookshelf anchor adjusts its spatial beacon: a subtle directional light path on the floor guides the user back to reinforce the concept before session end.
* **On-Screen 3D UI & Spatial Holograms:**
  * Adaptive Priority Indicator: `Priority: 0.82 (High Re-test)` displayed on bookshelf card.
  * Spatial Wayfinding: Faint dotted floor spline connecting user's gaze to high-priority room targets.
* **Exact Voiceover Script:**
  > *"Struggling with a concept? RecallRoom's deterministic adaptive algorithm factors recall accuracy, latency, hint usage, and elapsed time. It dynamically reschedules cards into your room's spatial queue, ensuring difficult ideas are reinforced right at the edge of forgetting."*
* **Sound Effects & Cues:**
  * Gentle error buzz (neutral, non-punitive warm frequency); spatial guide pulse sound directing attention.

---

### Scene 6: Session Summary & Outro — The Spatial Advantage (1:45 – 2:00)

* **Duration:** 15 Seconds (1:45 – 2:00)
* **Camera / Viewport:**
  * User steps back slightly; a sleek 3D holographic summary panel floats comfortably at 0.50m distance.
* **Visual Action:**
  * The session concludes. The entire room glows with completed spatial anchors connected by faint geometric constellation lines.
  * The summary dashboard presents session metrics:
    * 10 Concepts Tested | 90% Mastery Achieved.
    * Average Response Latency: 2.1s (down 38% from baseline).
    * Spatial Retention Index: +42% vs 2D standard flashcards.
  * User taps `Save Anchors & Finish`: Anchors persist locally to Quest's secure storage.
  * Final fade to title card with GitHub repository link and Meta Hackathon badge.
* **On-Screen 3D UI & Spatial Holograms:**
  * Curved 3D Glass Dashboard (`backdrop-blur`, `#0F172A` with 90% opacity).
  * Radial progress rings rendering retention statistics in teal and emerald.
  * Call to Action: `Clone Repo & Sideload on Quest 3`.
* **Exact Voiceover Script:**
  > *"RecallRoom XR bridges millennia-old cognitive science with cutting-edge spatial computing and semantic AI. Turn your room into a living memory palace. Learn faster, remember longer, and master complex subjects in mixed reality."*
* **Sound Effects & Cues:**
  * Uplifting crescendo, subtle spatial acoustic reverb, clean concluding signature chime.

---

## Technical & Ergonomics Compliance Matrix for Video

| Parameter | Specification | Compliance Rationale |
| :--- | :--- | :--- |
| **Field of View** | 110° diagonal (Quest 3 native capture) | Matches viewer perspective without fish-eye distortion |
| **Frame Rate** | 60 FPS minimum / 90 Hz display | Eliminates motion blur and simulation sickness |
| **Eye-Level Offset** | $y = 1.25\text{ m}$ (seated position) | Accessible, comfortable for prolonged desk study |
| **Gaze Tilt** | $15^\circ - 25^\circ$ below horizontal horizon | Ergonomic standard (ISO 9241-410 & Meta XR HIG) |
| **UI Touch / Pinch Distance** | $0.38\text{ m} - 0.55\text{ m}$ | Prevents shoulder fatigue; within forearm reach |
| **Contrast Ratio** | $\ge 7:1$ (Dark slate background `#0F172A` / White text `#F8FAFC`) | Exceeds WCAG 2.1 AAA for spatial visibility against varied passthrough rooms |
| **Audio Latency Budget** | $\le 450\text{ ms}$ (Mic end-of-speech to visual score render) | Maintains conversational flow and user trust |

---

*Production note: All 3D UI elements utilize SDF (Signed Distance Field) text rendering via TextMeshPro with dynamic billboarding to eliminate sub-pixel shimmer in passthrough.*
