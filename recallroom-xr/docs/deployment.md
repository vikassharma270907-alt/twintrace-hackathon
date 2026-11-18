# RecallRoom XR — Production Deployment & Build Pipeline

This document details the production build, packaging, containerization, and hosting processes for both the **Meta Quest Android APK** and the **FastAPI AI Backend**.

---

## 1. Meta Quest Android APK Production Pipeline

### 1.1 Android Keystore Generation
For production or sideload distribution, sign the APK with a dedicated release keystore rather than Unity's default debug key:
```powershell
keytool -genkey -v -keystore recallroom-release.keystore -alias recallroom -keyalg RSA -keysize 2048 -validity 10000
```
Store `recallroom-release.keystore` securely and configure Unity Player Settings:
* **Project Settings > Player > Publishing Settings**
* Check **Custom Release Keystore** and enter keystore password and alias credentials.

### 1.2 Automated Headless Build via Unity Batch Mode
To trigger an automated, clean build from CI/CD or PowerShell without opening the Unity Editor GUI:
```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.28f1\Editor\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "d:\codex antigravity\recallroom-xr" `
  -buildTarget Android `
  -executeMethod RecallRoom.Editor.BuildPipeline.BuildQuestApk `
  -logFile "build.log"
```

### 1.3 Android Manifest & Permissions Checklist
The resulting `AndroidManifest.xml` must include the following permissions:
```xml
<!-- Audio Recording for Voice Evaluation -->
<uses-permission android:name="android.permission.RECORD_AUDIO" />

<!-- Internet for Backend API Calls -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />

<!-- Meta Spatial Anchors & Scene API -->
<uses-permission android:name="com.oculus.permission.USE_ANCHOR_API" />
<uses-permission android:name="com.oculus.permission.USE_SCENE" />

<!-- Quest Passthrough Hardware Requirements -->
<uses-feature android:name="android.hardware.vr.headtracking" android:required="true" android:version="1" />
<uses-feature android:name="oculus.software.handtracking" android:required="false" />
```

---

## 2. Docker Containerization for Backend

The AI backend is packaged into a production-grade Docker container utilizing an official Python 3.10 slim base image.

### 2.1 Multi-Stage Dockerfile (`backend/Dockerfile`)
```dockerfile
# Stage 1: Build & Dependencies
FROM python:3.10-slim AS builder

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir --user -r requirements.txt

# Stage 2: Runtime Container
FROM python:3.10-slim AS runner

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Copy installed Python packages from builder stage
COPY --from=builder /root/.local /root/.local
ENV PATH=/root/.local/bin:$PATH

# Pre-cache models to image to avoid cold-start runtime downloads
ENV MODEL_CACHE_DIR=/app/model_cache
RUN python -c "import whisper; whisper.load_model('base.en', download_root='/app/model_cache')"
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('all-MiniLM-L6-v2', cache_folder='/app/model_cache')"

COPY . /app

EXPOSE 8000

ENV PYTHONUNBUFFERED=1
ENV HOST=0.0.0.0
ENV PORT=8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "2"]
```

### 2.2 Building & Running the Docker Container Locally
```powershell
cd "d:\codex antigravity\recallroom-xr\backend"

# Build Image
docker build -t recallroom-backend:latest .

# Run Container with 2GB Memory Limit
docker run -d --name recallroom-api -p 8000:8000 --memory=2g recallroom-backend:latest
```

---

## 3. Cloud Deployment: Render / Fly.io / AWS

### 3.1 Deployment on Render (One-Click / Web Service)
1. Link your GitHub repository in the **Render Dashboard**.
2. Create a new **Web Service**:
   * **Runtime:** Docker
   * **Plan:** Standard ($25/mo, 2 GB RAM, 1 CPU) or Starter (for development testing).
   * **Health Check Path:** `/health`
3. Configure Environment Variables in Render:
   | Key | Value | Description |
   | :--- | :--- | :--- |
   | `ASR_MODEL_NAME` | `base.en` | Whisper model size (`tiny.en` or `base.en`) |
   | `EMBEDDING_MODEL_NAME` | `all-MiniLM-L6-v2` | Sentence Transformer model name |
   | `CORS_ORIGINS` | `*` | Allowed client origins |
   | `LOG_LEVEL` | `INFO` | Logging level (`DEBUG`, `INFO`, `WARNING`) |
   | `MAX_AUDIO_SIZE_MB` | `10` | Maximum audio payload limit |

### 3.2 Deployment on Fly.io
Deploy to Fly.io with a single configuration file (`fly.toml`):
```toml
app = "recallroom-backend"
primary_region = "iad"

[build]
  dockerfile = "Dockerfile"

[http_service]
  internal_port = 8000
  force_https = true
  auto_stop_machines = false
  auto_start_machines = true
  min_machines_running = 1

[[vm]]
  cpu_kind = "shared"
  cpus = 2
  memory_mb = 2048
```
Deploy command:
```powershell
fly deploy
```

---

## 4. HTTPS, SSL & WebXR Security Requirements

Meta Quest browser and OpenXR native Android networking strictly require **HTTPS with TLS 1.2/1.3** when communicating across public networks.

### Caddy Reverse Proxy Configuration (Self-Hosted Option)
If self-hosting on a dedicated VPS (e.g. Hetzner / DigitalOcean):
```caddy
api.recallroom.org {
    reverse_proxy localhost:8000 {
        header_up Host {host}
        header_up X-Real-IP {remote}
    }
    encode zstd gzip
    tls admin@recallroom.org
}
```
Caddy automatically provisions and renews Let's Encrypt SSL/TLS certificates.

---

## 5. Capacity Sizing & Latency Budgets

| Component | Minimum Spec | Recommended Production Spec | Peak Throughput |
| :--- | :--- | :--- | :--- |
| **CPU** | 1 vCPU (x86_64 or ARM64) | 2–4 Dedicated vCPUs | 40 evaluations / minute / worker |
| **Memory** | 1.5 GB RAM | 4 GB RAM | Supports simultaneous Whisper + MiniLM in RAM |
| **Storage** | 4 GB Disk | 10 GB NVMe Disk | Includes cached model weights and OS dependencies |
| **Network Egress** | 10 Mbps | 100 Mbps | Payload per eval is $< 100\text{ KB}$ |

### End-to-End Latency Target Breakdown
* Client Audio Encoding (WAV 16kHz): **$15\text{ ms}$**
* Network Transport to Cloud (Fiber / 5G): **$35\text{ ms}$**
* Whisper `base.en` Transcription: **$190\text{ ms}$**
* MiniLM Embedding & Cosine Dot Product: **$22\text{ ms}$**
* Adaptive Queue Re-calculation: **$3\text{ ms}$**
* Network Return Transport: **$35\text{ ms}$**
* Client Hologram State Update & VFX: **$16\text{ ms}$**
* **Total Roundtrip Latency:** **$\approx 316\text{ ms}$** *(Well within the $< 450\text{ ms}$ conversational UX budget).*
