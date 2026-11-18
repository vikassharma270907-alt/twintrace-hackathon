# RecallRoom XR — Comprehensive Developer Setup Guide

This guide provides end-to-end setup instructions for building, testing, and running both the **Unity Mixed Reality Client** and the **Python FastAPI AI Backend**.

---

## 1. System Requirements & Prerequisites

### Hardware
* **VR/MR Headset:** Meta Quest 3 (recommended), Meta Quest Pro, or Meta Quest 2.
* **Host PC:** 
  * OS: Windows 10/11 (64-bit) or macOS (Apple Silicon / Intel) / Linux (Ubuntu 22.04+).
  * RAM: 16 GB minimum (32 GB recommended).
  * Storage: At least 25 GB free disk space.
  * USB: High-speed USB-C 3.0 data cable for Quest Link / ADB sideloading.

### Software & SDKs
* **Unity Hub:** Version 3.8.0 or later.
* **Unity Editor:** **Unity 2022.3 LTS** (e.g. `2022.3.28f1`+) or **Unity 6 (6000.0 LTS)**.
  * *Required Modules:* **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK**.
* **Meta Quest Developer Hub (MQDH):** Version 4.0+.
* **Python Runtime:** Python **3.10** or **3.11** (64-bit).
* **Package Managers:** `pip` and `git` with Git LFS enabled.

---

## 2. Python AI Backend Setup

The backend serves the Whisper ASR model, MiniLM sentence embedding model, and the adaptive spaced repetition scheduler.

### 2.1 Clone Repository & Navigate to Backend
```powershell
cd "d:\codex antigravity\recallroom-xr\backend"
```

### 2.2 Create & Activate Virtual Environment
```powershell
# On Windows (PowerShell)
python -m venv venv
.\venv\Scripts\Activate.ps1

# On Linux/macOS
python3 -m venv venv
source venv/bin/activate
```

### 2.3 Install Python Dependencies
```powershell
pip install --upgrade pip
pip install -r requirements.txt
```

> **Note on PyTorch / Torchvision:** For standard CPU inference, the default PyTorch wheels are installed. If you have an NVIDIA CUDA GPU, install the CUDA-accelerated wheel:
> ```powershell
> pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121
> ```

### 2.4 Pre-Cache AI Models
Pre-download the Whisper and Sentence Transformers weights to avoid runtime download delays:
```powershell
python -c "import whisper; whisper.load_model('base.en')"
python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('all-MiniLM-L6-v2')"
```

### 2.5 Run Backend Locally
```powershell
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```
Verify the server is running by opening:
* Health Endpoint: `http://localhost:8000/health`
* Interactive OpenAPI Swagger Docs: `http://localhost:8000/docs`

### 2.6 Run Automated Backend Test Suite
```powershell
pytest tests/ -v --tb=short
```
Expected output: All unit tests (`test_api.py`, `test_semantic.py`, `test_adaptive.py`) pass with 100% green status.

---

## 3. Unity Client Project Setup

### 3.1 Open Project in Unity
1. Launch **Unity Hub**.
2. Click **Add > Add project from disk**.
3. Select `d:\codex antigravity\recallroom-xr` (or the Unity project directory).
4. Select Unity Editor version `2022.3.x LTS` or `6000.x LTS`.

### 3.2 Switch Platform to Android
1. Go to **File > Build Settings**.
2. Select **Android** in the Platform list.
3. Set **Texture Compression** to **ASTC**.
4. Click **Switch Platform**.

### 3.3 Configure Project & Player Settings
Go to **Edit > Project Settings**:

#### Player > Other Settings
* **Color Space:** `Linear`
* **Auto Graphics API:** Uncheck. Ensure **Vulkan** is at the top of the list (fallback: `OpenGLES3`).
* **Package Name:** `com.antigravity.recallroom`
* **Minimum API Level:** `Android 10.0 (API Level 29)`
* **Target API Level:** `Android 12.0 (API Level 32)` or higher.
* **Scripting Backend:** `IL2CPP`
* **Target Architectures:** Check **ARM64** (Uncheck ARMv7).

#### XR Plug-in Management
* Select the **Android** tab.
* Check **OpenXR**.
* In the OpenXR settings, under **Enabled Interaction Profiles**, add:
  * *Oculus Touch Controller Profile*
  * *Hand Interaction Profile*
* Under **OpenXR Feature Groups**, check:
  * *Meta Quest Support*
  * *Meta XR Feature*
  * *Passthrough Feature*
  * *Spatial Anchors*
  * *Scene Support*

#### Quality Settings
* **Texture Quality:** Full Res
* **Anti-Aliasing:** `4x Multi Sampling`
* **VSync Count:** `Don't Sync` (Meta runtime handles refresh rate synchronization at 90Hz/120Hz).

### 3.4 Package Dependencies (Manifest Check)
Ensure `Packages/manifest.json` contains:
```json
{
  "dependencies": {
    "com.unity.xr.openxr": "1.10.0",
    "com.meta.xr.sdk.core": "65.0.0",
    "com.meta.xr.sdk.interaction": "65.0.0",
    "com.meta.xr.sdk.interaction.ovr": "65.0.0",
    "com.meta.xr.mruk": "65.0.0",
    "com.unity.textmeshpro": "3.0.6"
  }
}
```

---

## 4. Connecting Meta Quest 3 & Sideloading

### 4.1 Enable Developer Mode on Quest
1. Open the **Meta Horizon Mobile App** on your smartphone.
2. Navigate to **Devices > Headset Settings > Developer Mode** and toggle **ON**.
3. Connect your Quest 3 to your host computer via a high-speed USB-C cable.
4. Put on the headset and click **Always allow from this computer** when prompted for USB Debugging.

### 4.2 Verify ADB Connection
In a terminal, run:
```powershell
adb devices
```
You should see:
```text
List of devices attached
2G0YC1JF8300XX    device
```

### 4.3 Configure Client Backend URL
Open `Assets/Resources/RecallRoomConfig.json` (or inspect `RecallRoomApiClient.cs`):
* For local development over Wi-Fi, set:
  ```json
  {
    "backendBaseUrl": "http://192.168.1.50:8000",
    "useLocalOfflineFallback": true,
    "requestTimeoutSeconds": 5.0
  }
  ```
  *(Replace `192.168.1.50` with your host PC's local LAN IP address).*
* For remote cloud deployment, set:
  ```json
  {
    "backendBaseUrl": "https://recallroom-api.onrender.com",
    "useLocalOfflineFallback": true,
    "requestTimeoutSeconds": 5.0
  }
  ```

### 4.4 Build & Run APK
1. In Unity, go to **File > Build Settings**.
2. Select scenes:
   * `Scenes/RecallRoomMain.unity`
3. Click **Build and Run**.
4. Unity will compile C# via IL2CPP, package the APK, and stream it to your headset via ADB.

---

## 5. Verification & Health Check Checklist

- [ ] **Passthrough Active:** When putting on the headset, your physical room is clearly visible in high-resolution color.
- [ ] **MRUK Surface Mesh:** Pointing hands at your desk shows subtle surface highlight contours.
- [ ] **Hand Pinch Tracking:** Pinching index finger and thumb successfully picks up floating concept cards.
- [ ] **Voice Capture:** Pinching the mic pill and speaking displays real-time waveform modulation.
- [ ] **Backend Latency:** The response pill renders in $< 450\text{ ms}$ with semantic score and color-coded feedback.
- [ ] **Persistence Test:** Placing 3 cards, exiting the app, and re-launching restores the cards at identical physical coordinates.
