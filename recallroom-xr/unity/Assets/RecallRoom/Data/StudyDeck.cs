using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecallRoom.Data
{
    [Serializable]
    public class StudyDeck
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string description;
        [SerializeField] private string category;
        [SerializeField] private List<ConceptCardData> cards = new List<ConceptCardData>();
        [SerializeField] private string iconName;
        [SerializeField] private int targetTimeSeconds = 300;

        public string Id { get => id; set => id = value; }
        public string Title { get => title; set => title = value; }
        public string Description { get => description; set => description = value; }
        public string Category { get => category; set => category = value; }
        public List<ConceptCardData> Cards { get => cards ?? (cards = new List<ConceptCardData>()); set => cards = value; }
        public string IconName { get => iconName; set => iconName = value; }
        public int TargetTimeSeconds { get => targetTimeSeconds; set => targetTimeSeconds = value; }

        public int CardCount => cards != null ? cards.Count : 0;

        public StudyDeck() { }

        public StudyDeck(string id, string title, string description, string category, List<ConceptCardData> cards, string iconName = "", int targetTimeSeconds = 300)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.category = category;
            this.cards = cards ?? new List<ConceptCardData>();
            this.iconName = iconName;
            this.targetTimeSeconds = targetTimeSeconds;
        }

        public ConceptCardData GetCardById(string cardId)
        {
            if (cards == null) return null;
            return cards.Find(c => c.Id == cardId);
        }

        public static StudyDeck FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<StudyDeck>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StudyDeck] Failed to deserialize deck from JSON: {ex.Message}");
                return null;
            }
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }
    }
}
