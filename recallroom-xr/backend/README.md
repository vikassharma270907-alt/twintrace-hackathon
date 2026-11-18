# RecallRoom XR — AI Backend Microservice

The AI engine powering the **RecallRoom XR** Meta Quest 3 spatial learning system. It delivers real-time speech-to-text transcription, semantic recall evaluation with a calibrated 3-tier policy, and deterministic adaptive spaced repetition scheduling.

---

## Architecture Overview

```
recallroom-xr/backend/
├── app/
│   ├── api/
│   │   ├── __init__.py
│   │   └── endpoints.py         # REST routes (ASR, Semantic Eval, Adaptive Scheduling)
│   ├── schemas/
│   │   ├── requests.py          # Pydantic request models
│   │   └── responses.py         # Pydantic response models
│   ├── services/
│   │   ├── asr_service.py       # Whisper ASR with graceful mock/fallback mode
│   │   ├── semantic_service.py  # all-MiniLM-L6-v2 + sublinear TF-IDF fallback
│   │   └── adaptive_service.py  # Deterministic priority queue scheduler
│   ├── config.py                # Environment-driven settings
│   └── main.py                  # FastAPI app with CORS, logging & error handlers
├── tests/
│   ├── conftest.py              # Shared fixtures (TestClient, synthetic WAVs)
│   ├── test_api.py              # End-to-end HTTP route tests
│   ├── test_semantic.py         # 3-tier calibration & keyword matching tests
│   └── test_adaptive.py         # Mathematical verification & edge cases
├── Dockerfile                   # Production Python 3.11 slim container
└── requirements.txt             # Pinned package dependencies
```

---

## 3-Tier Semantic Evaluation Policy

Student verbal explanations are compared against canonical definitions and acceptable alternatives:

| Tier | Cosine Similarity Range | Feedback Guidance | Headset Color Token |
| :--- | :--- | :--- | :--- |
| **`CORRECT`** | $\ge 0.75$ | High-fidelity recall; core principles verified | Emerald Green (`#10B981`) |
| **`PARTIAL`** | $0.50 \le s < 0.75$ | Partial grasp; highlights missing keywords | Amber Warning (`#F59E0B`) |
| **`RETRY`** | $< 0.50$ | Inaccurate or absent recall; resets card | Crimson Rose (`#EF4444`) |

Includes both Sentence Transformers (`all-MiniLM-L6-v2`) and calibrated sublinear TF-IDF n-gram vector cosine similarity for CPU and offline environments.

---

## Adaptive Spaced Repetition Formula

Determines the study queue order using 4 research-calibrated parameters:

$$\text{Priority} = 0.45 \times (1 - \text{recall\_score}) + 0.25 \times \text{time\_since\_review} + 0.20 \times \text{hint\_dependency} + 0.10 \times \text{response\_latency}$$

- **`recall_score` ($45\%$)**: Lower historical recall creates the strongest urgency to prevent forgetting.
- **`time_since_review` ($25\%$)**: Elapsed time since the card was last tested.
- **`hint_dependency` ($20\%$)**: Reliance on spatial hints indicates incomplete retrieval.
- **`response_latency` ($10\%$)**: Hesitation or cognitive stall before answering.

Queue items are sorted descending by priority with 1-based ranks and explanatory rationales.

---

## API Reference

### Health & Metadata
- **`GET /health`**: Microservice health, model readiness, and version details.
- **`GET /api/version`**: Semantic and ASR engine versions.

### Speech & Evaluation
- **`POST /api/transcribe`**: Accepts multipart WAV file or base64 audio. Returns text transcript.
- **`POST /api/evaluate`**: Accepts JSON `{concept_id, concept_title, expected_answer, student_transcript, keywords}`. Returns semantic score, 3-tier status, matched/missed keywords, and feedback.
- **`POST /api/evaluate-audio`**: End-to-end endpoint accepting WAV audio file + concept metadata. Returns combined transcript and evaluation.

### Spaced Repetition
- **`POST /api/review/next`**: Accepts `{concepts: [...], top_k: 5}`. Returns prioritized review queue with ranks and rationales.

---

## Local Development & Testing

```bash
# Navigate to backend directory
cd "recallroom-xr/backend"

# Install dependencies
pip install -r requirements.txt

# Run full test suite
pytest tests/ -v

# Start development server
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```
