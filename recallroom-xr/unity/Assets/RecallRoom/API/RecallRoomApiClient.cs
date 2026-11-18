using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using RecallRoom.Data;

namespace RecallRoom.API
{
    [Serializable]
    public class TranscribeResponse
    {
        public string transcript;
        public float duration;
        public float confidence;
    }

    [Serializable]
    public class EvaluateRequest
    {
        public string concept_id;
        public string concept_title;
        public string summary;
        public string[] keywords;
        public string[] accepted_answers;
        public string transcript;
        public float latency_seconds;
        public bool hint_used;
    }

    [Serializable]
    public class EvaluateResponse
    {
        public string concept_id;
        public float score;
        public string status; // CORRECT, PARTIAL, RETRY
        public string feedback;
        public string[] missing_keywords;
        public string[] matched_keywords;
    }

    public class RecallRoomApiClient : MonoBehaviour
    {
        public static RecallRoomApiClient Instance { get; private set; }

        [Header("Backend Configuration")]
        [SerializeField] private string baseUrl = "http://127.0.0.1:8000";
        [SerializeField] private float requestTimeoutSeconds = 8.0f;
        [SerializeField] private bool forceOfflineMode = false;

        public string BaseUrl { get => baseUrl; set => baseUrl = value; }
        public bool ForceOfflineMode { get => forceOfflineMode; set => forceOfflineMode = value; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public async Task<bool> CheckHealthAsync()
        {
            if (forceOfflineMode) return false;
            string url = $"{baseUrl.TrimEnd('/')}/health";
            try
            {
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.timeout = (int)requestTimeoutSeconds;
                    var op = req.SendWebRequest();
                    while (!op.isDone)
                    {
                        await Task.Yield();
                    }
                    return req.result == UnityWebRequest.Result.Success;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RecallRoomApiClient] Health check failed: {ex.Message}");
                return false;
            }
        }

        #region Transcribe Audio

        public async Task<string> TranscribeAudioAsync(byte[] wavBytes)
        {
            if (forceOfflineMode || wavBytes == null || wavBytes.Length == 0)
            {
                Debug.Log("[RecallRoomApiClient] Transcribe using offline mock speech recognition.");
                return "[Offline Mode: Mock audio transcription placeholder]";
            }

            string url = $"{baseUrl.TrimEnd('/')}/api/transcribe";
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavBytes, "recording.wav", "audio/wav");

            using (UnityWebRequest req = UnityWebRequest.Post(url, form))
            {
                req.timeout = (int)requestTimeoutSeconds;
                var op = req.SendWebRequest();

                while (!op.isDone)
                {
                    await Task.Yield();
                }

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var res = JsonUtility.FromJson<TranscribeResponse>(req.downloadHandler.text);
                        return res?.transcript ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RecallRoomApiClient] Failed to parse transcribe response: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[RecallRoomApiClient] Transcribe endpoint failed: {req.error}. Using offline fallback.");
                }
            }

            return "[Offline: Audio received, fallback transcript]";
        }

        #endregion

        #region Evaluate Recall

        public async Task<EvaluateResponse> EvaluateRecallAsync(ConceptCardData card, string transcript, float latencySeconds, bool hintUsed)
        {
            if (forceOfflineMode)
            {
                return EvaluateOfflineDeterministic(card, transcript, latencySeconds, hintUsed);
            }

            string url = $"{baseUrl.TrimEnd('/')}/api/evaluate";
            var requestData = new EvaluateRequest
            {
                concept_id = card.Id,
                concept_title = card.Title,
                summary = card.Summary,
                keywords = card.Keywords,
                accepted_answers = card.AcceptedAnswers,
                transcript = transcript,
                latency_seconds = latencySeconds,
                hint_used = hintUsed
            };

            string jsonBody = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)requestTimeoutSeconds;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    await Task.Yield();
                }

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<EvaluateResponse>(req.downloadHandler.text);
                        if (response != null)
                        {
                            return response;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RecallRoomApiClient] Failed to parse evaluate JSON: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[RecallRoomApiClient] Evaluate API failed: {req.error}. Switching to local evaluation.");
                }
            }

            // Fallback to local scoring
            return EvaluateOfflineDeterministic(card, transcript, latencySeconds, hintUsed);
        }

        #endregion

        #region Offline Deterministic Scoring

        public static EvaluateResponse EvaluateOfflineDeterministic(ConceptCardData card, string transcript, float latencySeconds, bool hintUsed)
        {
            if (card == null)
            {
                return new EvaluateResponse
                {
                    concept_id = "",
                    score = 0f,
                    status = "RETRY",
                    feedback = "Invalid card data.",
                    missing_keywords = Array.Empty<string>(),
                    matched_keywords = Array.Empty<string>()
                };
            }

            if (string.IsNullOrWhiteSpace(transcript))
            {
                return new EvaluateResponse
                {
                    concept_id = card.Id,
                    score = 0f,
                    status = "RETRY",
                    feedback = "No speech detected. Speak clearly into the microphone.",
                    missing_keywords = card.Keywords,
                    matched_keywords = Array.Empty<string>()
                };
            }

            string lowerTranscript = transcript.ToLowerInvariant();
            var matchedList = new List<string>();
            var missingList = new List<string>();

            // 1. Keyword coverage
            if (card.Keywords != null && card.Keywords.Length > 0)
            {
                foreach (var kw in card.Keywords)
                {
                    if (string.IsNullOrEmpty(kw)) continue;
                    if (lowerTranscript.Contains(kw.ToLowerInvariant()))
                    {
                        matchedList.Add(kw);
                    }
                    else
                    {
                        missingList.Add(kw);
                    }
                }
            }

            float keywordRatio = card.Keywords != null && card.Keywords.Length > 0
                ? (float)matchedList.Count / card.Keywords.Length
                : 0.5f;

            // 2. Accepted answers overlap check
            float answerOverlap = 0f;
            if (card.AcceptedAnswers != null && card.AcceptedAnswers.Length > 0)
            {
                foreach (var ans in card.AcceptedAnswers)
                {
                    float sim = ComputeWordOverlapSimilarity(lowerTranscript, ans.ToLowerInvariant());
                    if (sim > answerOverlap) answerOverlap = sim;
                }
            }

            // Combine keyword match (60%) and answer overlap (40%)
            float baseScore = (keywordRatio * 0.6f) + (answerOverlap * 0.4f);

            // Small penalty if hint was used
            if (hintUsed)
            {
                baseScore *= 0.9f;
            }

            float finalScore = Mathf.Clamp01(baseScore);
            string statusStr;
            string feedbackStr;

            if (finalScore >= 0.75f)
            {
                statusStr = "CORRECT";
                feedbackStr = $"Excellent recall! Captured key concepts: {string.Join(", ", matchedList)}.";
            }
            else if (finalScore >= 0.50f)
            {
                statusStr = "PARTIAL";
                feedbackStr = $"Good effort. Missing key concepts: {(missingList.Count > 0 ? string.Join(", ", missingList) : "detail accuracy")}.";
            }
            else
            {
                statusStr = "RETRY";
                feedbackStr = $"Needs review. Mnemonic reminder: {card.Mnemonic}";
            }

            return new EvaluateResponse
            {
                concept_id = card.Id,
                score = finalScore,
                status = statusStr,
                feedback = feedbackStr,
                matched_keywords = matchedList.ToArray(),
                missing_keywords = missingList.ToArray()
            };
        }

        private static float ComputeWordOverlapSimilarity(string textA, string textB)
        {
            if (string.IsNullOrEmpty(textA) || string.IsNullOrEmpty(textB)) return 0f;

            char[] sep = { ' ', ',', '.', ';', ':', '-', '!', '?' };
            var wordsA = new HashSet<string>(textA.Split(sep, StringSplitOptions.RemoveEmptyEntries));
            var wordsB = new HashSet<string>(textB.Split(sep, StringSplitOptions.RemoveEmptyEntries));

            if (wordsA.Count == 0 || wordsB.Count == 0) return 0f;

            int intersectCount = 0;
            foreach (var w in wordsA)
            {
                if (w.Length > 2 && wordsB.Contains(w))
                {
                    intersectCount++;
                }
            }

            return (float)intersectCount / Mathf.Max(wordsA.Count, wordsB.Count);
        }

        #endregion
    }
}
