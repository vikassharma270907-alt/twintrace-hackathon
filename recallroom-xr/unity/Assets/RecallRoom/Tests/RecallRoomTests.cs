using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RecallRoom.Core;
using RecallRoom.Data;
using RecallRoom.Learning;
using RecallRoom.API;
using RecallRoom.Spatial;

namespace RecallRoom.Tests
{
    [TestFixture]
    public class RecallRoomTests
    {
        private GameObject testRunnerGo;
        private RecallStateManager stateManager;

        [SetUp]
        public void Setup()
        {
            testRunnerGo = new GameObject("Test_StateManager");
            stateManager = testRunnerGo.AddComponent<RecallStateManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testRunnerGo != null)
            {
                Object.DestroyImmediate(testRunnerGo);
            }
        }

        #region FSM State Machine Tests

        [Test]
        public void FSM_InitialState_TransitionsToHome()
        {
            stateManager.Initialize();
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);
        }

        [Test]
        public void FSM_ValidTransitionFlow_ProgressesCorrectly()
        {
            stateManager.Initialize();
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.SELECTING_DECK));
            Assert.AreEqual(RecallState.SELECTING_DECK, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.PLACING_CARDS));
            Assert.AreEqual(RecallState.PLACING_CARDS, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.READY_TO_RECALL));
            Assert.AreEqual(RecallState.READY_TO_RECALL, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.RECALLING));
            Assert.AreEqual(RecallState.RECALLING, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.EVALUATING));
            Assert.AreEqual(RecallState.EVALUATING, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.SHOWING_FEEDBACK));
            Assert.AreEqual(RecallState.SHOWING_FEEDBACK, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.SESSION_COMPLETE));
            Assert.AreEqual(RecallState.SESSION_COMPLETE, stateManager.CurrentState);

            Assert.IsTrue(stateManager.TransitionTo(RecallState.HOME));
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);
        }

        [Test]
        public void FSM_InvalidTransition_IsRejected()
        {
            stateManager.Initialize();
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);

            // Cannot jump directly from HOME to SHOWING_FEEDBACK
            bool canJump = stateManager.CanTransitionTo(RecallState.SHOWING_FEEDBACK);
            Assert.IsFalse(canJump);

            bool transitioned = stateManager.TransitionTo(RecallState.SHOWING_FEEDBACK);
            Assert.IsFalse(transitioned);
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);
        }

        [Test]
        public void FSM_ErrorState_ReachableFromAnyState_AndCanRecover()
        {
            stateManager.Initialize();
            stateManager.TriggerError("Test error triggered");
            Assert.AreEqual(RecallState.ERROR, stateManager.CurrentState);
            Assert.AreEqual("Test error triggered", stateManager.LastErrorMessage);

            // Recover back to HOME
            Assert.IsTrue(stateManager.TransitionTo(RecallState.HOME));
            Assert.AreEqual(RecallState.HOME, stateManager.CurrentState);
        }

        #endregion

        #region Data & Deck Deserialization Tests

        [Test]
        public void Data_StudyDeck_JsonSerializationAndDeserialization()
        {
            var card = new ConceptCardData(
                "net_test", "deck_test", "TCP", "Reliable transport", "SYN-ACK",
                new string[] { "SYN", "ACK" },
                new string[] { "SYN SYN-ACK ACK" },
                2, "Protocols", "Think 3-way"
            );

            var deck = new StudyDeck("deck_test", "Networking", "Protocols test", "CS", new List<ConceptCardData> { card });
            string json = deck.ToJson();

            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("deck_test"));
            Assert.IsTrue(json.Contains("net_test"));

            var deserialized = StudyDeck.FromJson(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("deck_test", deserialized.Id);
            Assert.AreEqual(1, deserialized.CardCount);
            Assert.AreEqual("TCP", deserialized.Cards[0].Title);
            Assert.AreEqual("SYN-ACK", deserialized.Cards[0].Mnemonic);
            Assert.IsTrue(deserialized.Cards[0].ContainsKeyword("syn"));
        }

        #endregion

        #region Adaptive Scheduler Priority Formula Tests

        [Test]
        public void AdaptiveScheduler_CalculatePriority_PerfectMastery_YieldsZero()
        {
            // recall=1.0 (perfect), time=0h, hint=0, latency=0s
            float priority = AdaptiveScheduler.CalculatePriority(1.0f, 0f, 0f, 0f);
            Assert.AreEqual(0.0f, priority, 0.001f);
        }

        [Test]
        public void AdaptiveScheduler_CalculatePriority_WorstPerformance_YieldsOne()
        {
            // recall=0.0 (failed), time=24h (max), hint=1.0 (used), latency=20s (max)
            float priority = AdaptiveScheduler.CalculatePriority(0.0f, 24.0f, 1.0f, 20.0f, 24.0f, 20.0f);
            Assert.AreEqual(1.0f, priority, 0.001f);
        }

        [Test]
        public void AdaptiveScheduler_CalculatePriority_CalibratedIntermediateValues()
        {
            // recall=0.5 -> term = 0.45 * (1 - 0.5) = 0.225
            // time=12h / 24h = 0.5 -> term = 0.25 * 0.5 = 0.125
            // hint=1.0 -> term = 0.20 * 1.0 = 0.200
            // latency=10s / 20s = 0.5 -> term = 0.10 * 0.5 = 0.050
            // Total = 0.225 + 0.125 + 0.200 + 0.050 = 0.600
            float priority = AdaptiveScheduler.CalculatePriority(0.5f, 12.0f, 1.0f, 10.0f, 24.0f, 20.0f);
            Assert.AreEqual(0.60f, priority, 0.001f);
        }

        [Test]
        public void AdaptiveScheduler_ScheduleDeck_SortsWeakestFirst()
        {
            var cardMastered = new ConceptCardData("c1", "deck", "Mastered", "", "", new string[0], new string[0], 1, "", "");
            var cardStruggling = new ConceptCardData("c2", "deck", "Struggling", "", "", new string[0], new string[0], 1, "", "");

            var history = new List<CardReviewResult>
            {
                new CardReviewResult("c1", 0.95f, ReviewStatus.CORRECT, 2.0f, false, "", "", 1.0f),
                new CardReviewResult("c2", 0.20f, ReviewStatus.RETRY, 18.0f, true, "", "", 20.0f)
            };

            var cards = new List<ConceptCardData> { cardMastered, cardStruggling };
            var scheduled = AdaptiveScheduler.ScheduleDeck(cards, history);

            Assert.AreEqual(2, scheduled.Count);
            // Struggling card should have higher priority and be ranked #1
            Assert.AreEqual("c2", scheduled[0].Card.Id);
            Assert.AreEqual("c1", scheduled[1].Card.Id);
            Assert.Greater(scheduled[0].Priority, scheduled[1].Priority);
        }

        #endregion

        #region Offline Deterministic Scoring Tests

        [Test]
        public void API_OfflineDeterministicEvaluation_AccurateRecall_ReturnsCorrect()
        {
            var card = new ConceptCardData(
                "net_tcp", "deck", "TCP Handshake", "Three-way handshake", "SYN SYN-ACK ACK",
                new string[] { "SYN", "ACK", "handshake" },
                new string[] { "Client sends SYN, server sends SYN-ACK, client replies ACK to establish handshake" },
                2, "Protocols", ""
            );

            string studentTranscript = "The client sends a SYN packet, the server responds with SYN-ACK, and finally ACK completes the handshake.";
            var response = RecallRoomApiClient.EvaluateOfflineDeterministic(card, studentTranscript, 4.5f, false);

            Assert.AreEqual("CORRECT", response.status);
            Assert.GreaterOrEqual(response.score, 0.75f);
            Assert.AreEqual(0, response.missing_keywords.Length);
        }

        [Test]
        public void API_OfflineDeterministicEvaluation_EmptyOrIrrelevant_ReturnsRetry()
        {
            var card = new ConceptCardData(
                "net_tcp", "deck", "TCP Handshake", "", "",
                new string[] { "SYN", "ACK", "handshake" },
                new string[] { "SYN SYN-ACK ACK" },
                2, "", ""
            );

            var response = RecallRoomApiClient.EvaluateOfflineDeterministic(card, "", 0f, false);
            Assert.AreEqual("RETRY", response.status);
            Assert.AreEqual(0f, response.score);
        }

        #endregion

        #region Spatial Fallback Arc Tests

        [Test]
        public void Spatial_TabletopFallback_GeneratesCorrectSlotCount()
        {
            var fallbackGo = new GameObject("FallbackManager");
            var manager = fallbackGo.AddComponent<TabletopFallbackManager>();

            var placements = manager.GenerateFallbackPlacements(5);
            Assert.AreEqual(5, placements.Count);

            // All placements must have valid positions and not overlap
            for (int i = 0; i < placements.Count; i++)
            {
                Assert.AreEqual(SurfaceType.TabletopFallback, placements[i].SurfaceType);
                Assert.IsTrue(placements[i].Position != Vector3.zero);
                if (i > 0)
                {
                    float dist = Vector3.Distance(placements[i].Position, placements[i - 1].Position);
                    Assert.Greater(dist, 0.1f, "Adjacent cards should not overlap");
                }
            }

            Object.DestroyImmediate(fallbackGo);
        }

        #endregion
    }
}
