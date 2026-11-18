using System.Collections;
using UnityEngine;
using TMPro;
using RecallRoom.Data;
using RecallRoom.API;

namespace RecallRoom.UI
{
    public class SpatialCardView : MonoBehaviour
    {
        [Header("Root Canvases / Faces")]
        [SerializeField] private GameObject frontFace;
        [SerializeField] private GameObject backFace;
        [SerializeField] private Transform cardBodyTransform;

        [Header("Front Face Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private TMP_Text mnemonicText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private GameObject hintContainer;

        [Header("Interactive Choice Fallback (Accessible Non-Speech)")]
        [SerializeField] private GameObject choiceContainer;
        [SerializeField] private UnityEngine.UI.Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceButtonTexts;

        [Header("Back Face Elements")]
        [SerializeField] private TMP_Text scoreBadgeText;
        [SerializeField] private TMP_Text transcriptText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text missingKeywordsText;

        [Header("Border & Visual Styling")]
        [SerializeField] private Renderer borderRenderer;
        [SerializeField] private Color defaultBorderColor = new Color(0.2f, 0.5f, 0.9f);
        [SerializeField] private Color correctBorderColor = new Color(0.13f, 0.77f, 0.36f); // #22c55e
        [SerializeField] private Color partialBorderColor = new Color(0.92f, 0.70f, 0.03f); // #eab308
        [SerializeField] private Color retryBorderColor = new Color(0.94f, 0.27f, 0.27f);   // #ef4444

        [Header("Flip Animation")]
        [SerializeField] private float flipDuration = 0.4f;

        private ConceptCardData boundCard;
        private bool isShowingBack = false;
        private bool isHintRevealed = false;
        private Coroutine flipCoroutine;

        public ConceptCardData BoundCard => boundCard;
        public bool IsShowingBack => isShowingBack;
        public bool IsHintRevealed => isHintRevealed;

        private void Awake()
        {
            if (cardBodyTransform == null) cardBodyTransform = transform;
            ShowFrontImmediate();
        }

        public void Bind(ConceptCardData card)
        {
            boundCard = card;
            isHintRevealed = false;
            if (card == null) return;

            if (titleText != null) titleText.text = card.Title;
            if (categoryText != null) categoryText.text = card.Category;
            if (summaryText != null) summaryText.text = card.Summary;
            if (mnemonicText != null) mnemonicText.text = $"<b>Mnemonic:</b> {card.Mnemonic}";
            if (hintText != null) hintText.text = card.Hint;
            if (hintContainer != null) hintContainer.SetActive(false);

            SetBorderColor(defaultBorderColor);
            ShowFrontImmediate();
        }

        public void ToggleHint()
        {
            isHintRevealed = !isHintRevealed;
            if (hintContainer != null)
            {
                hintContainer.SetActive(isHintRevealed);
            }
        }

        public void ShowRecallPrompt(bool hideAnswers, bool showChoices = false, string[] choiceOptions = null, System.Action<string> onChoiceSelected = null)
        {
            if (boundCard == null) return;

            if (hideAnswers)
            {
                if (titleText != null) titleText.text = $"[Recall Target: {boundCard.Category}]";
                if (summaryText != null) summaryText.text = "??? (Describe this spatial concept aloud or choose an option)";
                if (mnemonicText != null) mnemonicText.text = $"<b>Cue:</b> {boundCard.Mnemonic}";
            }
            else
            {
                if (titleText != null) titleText.text = boundCard.Title;
                if (summaryText != null) summaryText.text = boundCard.Summary;
                if (mnemonicText != null) mnemonicText.text = $"<b>Mnemonic:</b> {boundCard.Mnemonic}";
            }

            if (choiceContainer != null)
            {
                choiceContainer.SetActive(showChoices && choiceOptions != null && choiceOptions.Length > 0);
                if (showChoices && choiceButtons != null && choiceOptions != null)
                {
                    for (int i = 0; i < choiceButtons.Length; i++)
                    {
                        if (i < choiceOptions.Length)
                        {
                            choiceButtons[i].gameObject.SetActive(true);
                            if (choiceButtonTexts != null && i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                            {
                                choiceButtonTexts[i].text = choiceOptions[i];
                            }
                            string selectedOption = choiceOptions[i];
                            choiceButtons[i].onClick.RemoveAllListeners();
                            choiceButtons[i].onClick.AddListener(() => onChoiceSelected?.Invoke(selectedOption));
                        }
                        else
                        {
                            choiceButtons[i].gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        public void DisplayFeedback(EvaluateResponse response, string transcript)
        {
            if (response == null) return;

            if (scoreBadgeText != null)
            {
                scoreBadgeText.text = $"{response.status} ({(response.score * 100f):F0}%)";
            }
            if (transcriptText != null)
            {
                transcriptText.text = $"\"{transcript}\"";
            }
            if (feedbackText != null)
            {
                feedbackText.text = response.feedback;
            }
            if (missingKeywordsText != null)
            {
                if (response.missing_keywords != null && response.missing_keywords.Length > 0)
                {
                    missingKeywordsText.text = $"<b>Missing:</b> {string.Join(", ", response.missing_keywords)}";
                    missingKeywordsText.gameObject.SetActive(true);
                }
                else
                {
                    missingKeywordsText.gameObject.SetActive(false);
                }
            }

            // Update border color
            switch (response.status?.ToUpperInvariant())
            {
                case "CORRECT":
                    SetBorderColor(correctBorderColor);
                    break;
                case "PARTIAL":
                    SetBorderColor(partialBorderColor);
                    break;
                case "RETRY":
                    SetBorderColor(retryBorderColor);
                    break;
                default:
                    SetBorderColor(defaultBorderColor);
                    break;
            }

            FlipToBack();
        }

        public void FlipToBack()
        {
            if (isShowingBack) return;
            StartFlip(true);
        }

        public void FlipToFront()
        {
            if (!isShowingBack) return;
            StartFlip(false);
        }

        private void StartFlip(bool toBack)
        {
            if (flipCoroutine != null) StopCoroutine(flipCoroutine);
            flipCoroutine = StartCoroutine(AnimateFlip(toBack));
        }

        private IEnumerator AnimateFlip(bool toBack)
        {
            float elapsed = 0f;
            Quaternion startRot = cardBodyTransform.localRotation;
            Quaternion endRot = toBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            bool swappedVisibility = false;

            while (elapsed < flipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flipDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                cardBodyTransform.localRotation = Quaternion.Slerp(startRot, endRot, ease);

                // Swap faces midway through flip
                if (!swappedVisibility && t >= 0.5f)
                {
                    swappedVisibility = true;
                    if (frontFace != null) frontFace.SetActive(!toBack);
                    if (backFace != null) backFace.SetActive(toBack);
                }

                yield return null;
            }

            cardBodyTransform.localRotation = endRot;
            isShowingBack = toBack;
            if (frontFace != null) frontFace.SetActive(!toBack);
            if (backFace != null) backFace.SetActive(toBack);
            flipCoroutine = null;
        }

        private void ShowFrontImmediate()
        {
            isShowingBack = false;
            if (frontFace != null) frontFace.SetActive(true);
            if (backFace != null) backFace.SetActive(false);
            if (cardBodyTransform != null) cardBodyTransform.localRotation = Quaternion.identity;
        }

        private void SetBorderColor(Color color)
        {
            if (borderRenderer != null && borderRenderer.material != null)
            {
                borderRenderer.material.color = color;
            }
        }
    }
}
