using System;
using UnityEngine;

namespace RecallRoom.Data
{
    [Serializable]
    public class ConceptCardData
    {
        [SerializeField] private string id;
        [SerializeField] private string deckId;
        [SerializeField] private string title;
        [SerializeField] private string summary;
        [SerializeField] private string mnemonic;
        [SerializeField] private string[] keywords;
        [SerializeField] private string[] acceptedAnswers;
        [SerializeField] private int difficulty = 1;
        [SerializeField] private string category;
        [SerializeField] private string hint;

        public string Id { get => id; set => id = value; }
        public string DeckId { get => deckId; set => deckId = value; }
        public string Title { get => title; set => title = value; }
        public string Summary { get => summary; set => summary = value; }
        public string Mnemonic { get => mnemonic; set => mnemonic = value; }
        public string[] Keywords { get => keywords ?? Array.Empty<string>(); set => keywords = value; }
        public string[] AcceptedAnswers { get => acceptedAnswers ?? Array.Empty<string>(); set => acceptedAnswers = value; }
        public int Difficulty { get => Mathf.Clamp(difficulty, 1, 5); set => difficulty = Mathf.Clamp(value, 1, 5); }
        public string Category { get => category; set => category = value; }
        public string Hint { get => hint; set => hint = value; }

        public ConceptCardData() { }

        public ConceptCardData(string id, string deckId, string title, string summary, string mnemonic,
            string[] keywords, string[] acceptedAnswers, int difficulty, string category, string hint)
        {
            this.id = id;
            this.deckId = deckId;
            this.title = title;
            this.summary = summary;
            this.mnemonic = mnemonic;
            this.keywords = keywords;
            this.acceptedAnswers = acceptedAnswers;
            this.difficulty = Mathf.Clamp(difficulty, 1, 5);
            this.category = category;
            this.hint = hint;
        }

        public bool ContainsKeyword(string text)
        {
            if (string.IsNullOrEmpty(text) || keywords == null) return false;
            string lower = text.ToLowerInvariant();
            foreach (var kw in keywords)
            {
                if (!string.IsNullOrEmpty(kw) && lower.Contains(kw.ToLowerInvariant()))
                    return true;
            }
            return false;
        }
    }
}
