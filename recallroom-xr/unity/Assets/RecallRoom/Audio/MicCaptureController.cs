using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RecallRoom.Audio
{
    public class MicCaptureController : MonoBehaviour
    {
        public static MicCaptureController Instance { get; private set; }

        [Header("Microphone Settings")]
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private int maxRecordingDurationSeconds = 30;
        [SerializeField] private string selectedDeviceName = null;

        [Header("Voice Activity / RMS Meter")]
        [SerializeField] private float silenceThreshold = 0.015f;
        [SerializeField] private float autoStopSilenceSeconds = 2.5f;

        [Header("Status Inspection")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private float currentRmsLevel = 0f;
        [SerializeField] private float recordingDuration = 0f;

        private AudioClip recordingClip;
        private float silenceTimer = 0f;
        private bool hasSpoken = false;

        public bool IsRecording => isRecording;
        public float CurrentRmsLevel => currentRmsLevel;
        public float RecordingDuration => recordingDuration;

        public event Action OnRecordingStarted;
        public event Action<byte[], float> OnRecordingStopped;
        public event Action<float> OnVoiceLevelChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Microphone.devices.Length > 0)
            {
                selectedDeviceName = Microphone.devices[0];
                Debug.Log($"[MicCaptureController] Using audio input device: {selectedDeviceName}");
            }
            else
            {
                Debug.LogWarning("[MicCaptureController] No microphone audio devices detected.");
            }
        }

        private void Update()
        {
            if (!isRecording || recordingClip == null) return;

            recordingDuration += Time.deltaTime;
            currentRmsLevel = CalculateRmsLevel();
            OnVoiceLevelChanged?.Invoke(currentRmsLevel);

            // Silence detection
            if (currentRmsLevel > silenceThreshold)
            {
                hasSpoken = true;
                silenceTimer = 0f;
            }
            else if (hasSpoken)
            {
                silenceTimer += Time.deltaTime;
                if (silenceTimer >= autoStopSilenceSeconds)
                {
                    Debug.Log("[MicCaptureController] Silence timeout reached. Stopping recording.");
                    StopRecording();
                    return;
                }
            }

            // Max duration safeguard
            if (recordingDuration >= maxRecordingDurationSeconds)
            {
                Debug.Log("[MicCaptureController] Maximum recording duration reached. Stopping.");
                StopRecording();
            }
        }

        public bool StartRecording()
        {
            if (isRecording) return false;
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[MicCaptureController] Cannot start recording: No microphone device available.");
                return false;
            }

            recordingDuration = 0f;
            silenceTimer = 0f;
            hasSpoken = false;

            recordingClip = Microphone.Start(selectedDeviceName, false, maxRecordingDurationSeconds, sampleRate);
            isRecording = true;

            Debug.Log("[MicCaptureController] Recording started.");
            OnRecordingStarted?.Invoke();
            return true;
        }

        public byte[] StopRecording()
        {
            if (!isRecording) return null;

            int micPosition = Microphone.GetPosition(selectedDeviceName);
            Microphone.End(selectedDeviceName);
            isRecording = false;

            if (micPosition <= 0 || recordingClip == null)
            {
                Debug.LogWarning("[MicCaptureController] Empty recording captured.");
                OnRecordingStopped?.Invoke(null, 0f);
                return null;
            }

            // Extract valid samples
            float[] allSamples = new float[recordingClip.samples * recordingClip.channels];
            recordingClip.GetData(allSamples, 0);

            int validSampleCount = micPosition * recordingClip.channels;
            float[] trimmedSamples = new float[validSampleCount];
            Array.Copy(allSamples, trimmedSamples, validSampleCount);

            byte[] wavBytes = EncodeTo16BitWav(trimmedSamples, recordingClip.channels, sampleRate);
            float duration = (float)micPosition / sampleRate;

            Debug.Log($"[MicCaptureController] Recording stopped. Duration: {duration:F2}s, WAV size: {wavBytes.Length} bytes.");
            OnRecordingStopped?.Invoke(wavBytes, duration);
            return wavBytes;
        }

        private float CalculateRmsLevel()
        {
            int micPosition = Microphone.GetPosition(selectedDeviceName);
            if (micPosition < 128 || recordingClip == null) return 0f;

            int sampleWindow = 256;
            int startSample = Mathf.Max(0, micPosition - sampleWindow);
            float[] waveData = new float[sampleWindow];
            recordingClip.GetData(waveData, startSample);

            float sum = 0f;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += waveData[i] * waveData[i];
            }

            return Mathf.Sqrt(sum / sampleWindow);
        }

        public static byte[] EncodeTo16BitWav(float[] samples, int channels, int sampleRate)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int sampleCount = samples.Length;
                int bytesPerSample = 2; // 16-bit
                int byteRate = sampleRate * channels * bytesPerSample;
                short blockAlign = (short)(channels * bytesPerSample);
                int subChunk2Size = sampleCount * bytesPerSample;
                int chunkSize = 36 + subChunk2Size;

                // RIFF chunk descriptor
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(chunkSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // SubChunk1Size (16 for PCM)
                writer.Write((short)1); // AudioFormat (1 for PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)16); // BitsPerSample

                // data sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(subChunk2Size);

                // Write 16-bit PCM samples
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample16 = (short)(Mathf.Clamp(samples[i], -1.0f, 1.0f) * 32767f);
                    writer.Write(sample16);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
