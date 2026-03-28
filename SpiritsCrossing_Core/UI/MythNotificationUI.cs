// SpiritsCrossing — MythNotificationUI.cs
// Queue-based notification display for in-world events.
//
// NOTIFICATION SOURCES
//   - Dryad whispers (DryadSystem.OnDryadWhisper)
//   - Stone ring discovery (ForestWorldSpawner.OnStoneRingDiscovered)
//   - Temple outer discovery (ForestWorldSpawner.OnTempleOuterDiscovered)
//   - Temple sanctum unlock (ForestWorldSpawner.OnTempleSanctumUnlocked)
//   - Chamber discovery (ForestWorldSpawner.OnTempleChamberDiscovered)
//   - River first contact (ForestWorldSpawner.OnRiverFirstContact)
//   - Source pool reached (ForestWorldSpawner.OnSourcePoolReached)
//   - World ruin discovery (WorldSystem.OnRuinDiscovered)
//   - Realm complete (via GameBootstrapper/PortalTransitionController)
//
// DISPLAY BEHAVIOUR
//   Notifications queue. Each one: fade in (0.4s), hold (displayDuration), fade out (0.6s).
//   Maximum 3 queued at once — oldest are dropped if overflow occurs.
//   Dryad whispers use a different visual style (softer, smaller, italic).
//
// WIRING
//   Assign notificationText (TMP_Text or legacy Text), notificationGroup (CanvasGroup),
//   and optionally dryadWhisperText for the whisper style.
//   Subscribe events in OnEnable, unsubscribe in OnDisable.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SpiritsCrossing.ForestWorld;
using SpiritsCrossing.World;

namespace SpiritsCrossing.UI
{
    public enum NotificationType
    {
        Discovery,    // ruin, ring, temple, river — white/gold
        DryadWhisper, // tree spirit speech — soft green, italic
        Sanctum,      // temple sanctum — violet/bright
        Source,       // source pool — violet/sacred
        RealmEvent,   // realm complete / begin — full brightness
    }

    [Serializable]
    public class NotificationEntry
    {
        public string           text;
        public NotificationType type;
        public float            duration;
    }

    public class MythNotificationUI : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector
        // -------------------------------------------------------------------------
        [Header("UI Components")]
        public CanvasGroup notificationGroup;   // controls alpha for fade
        public Text        notificationText;    // or assign TMP_Text via script
        public CanvasGroup whisperGroup;        // separate group for dryad whispers
        public Text        whisperText;

        [Header("Timing")]
        public float fadeInDuration   = 0.40f;
        public float displayDuration  = 3.50f;  // default hold time
        public float whisperDuration  = 5.00f;  // whispers linger longer
        public float fadeOutDuration  = 0.60f;

        [Header("Style")]
        public Color colorDiscovery  = new Color(1.00f, 0.90f, 0.65f); // warm gold
        public Color colorWhisper    = new Color(0.72f, 0.95f, 0.72f); // soft green
        public Color colorSanctum    = new Color(0.80f, 0.65f, 1.00f); // violet
        public Color colorSource     = new Color(0.92f, 0.80f, 1.00f); // bright violet
        public Color colorRealmEvent = Color.white;

        [Header("Queue")]
        public int maxQueueSize = 3;

        // -------------------------------------------------------------------------
        // Internal state
        // -------------------------------------------------------------------------
        private readonly Queue<NotificationEntry> _queue = new Queue<NotificationEntry>();
        private bool _isShowing;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------
        private void OnEnable()
        {
            // Dryad whispers
            if (DryadSystem.Instance != null)
                DryadSystem.Instance.OnDryadWhisper += HandleDryadWhisper;

            // Forest world events
            if (ForestWorldSpawner.Instance != null)
            {
                ForestWorldSpawner.Instance.OnStoneRingDiscovered    += r  => Enqueue($"Stone ring awakens: {r.era}", NotificationType.Discovery);
                ForestWorldSpawner.Instance.OnTempleOuterDiscovered  += t  => Enqueue($"Temple found: {t.templeId.Replace("_"," ")}", NotificationType.Discovery);
                ForestWorldSpawner.Instance.OnTempleSanctumUnlocked  += t  => Enqueue($"Sanctum opened", NotificationType.Sanctum, displayDuration * 1.3f);
                ForestWorldSpawner.Instance.OnTempleChamberDiscovered += c  => Enqueue(c.chamberName, NotificationType.Discovery);
                ForestWorldSpawner.Instance.OnRiverFirstContact      += rv => Enqueue($"{rv.riverName}", NotificationType.Discovery);
                ForestWorldSpawner.Instance.OnSourcePoolReached       += rv => Enqueue($"Source pool: {rv.riverName}", NotificationType.Source, displayDuration * 1.5f);
            }

            // World system ruins
            if (WorldSystem.Instance != null)
                WorldSystem.Instance.OnRuinDiscovered += HandleRuinDiscovered;
        }

        private void OnDisable()
        {
            if (DryadSystem.Instance != null)
                DryadSystem.Instance.OnDryadWhisper -= HandleDryadWhisper;

            if (WorldSystem.Instance != null)
                WorldSystem.Instance.OnRuinDiscovered -= HandleRuinDiscovered;
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------
        private void HandleDryadWhisper(string dryadId, string line)
        {
            // Whispers show in the whisper panel if available, else main panel
            EnqueueWhisper(line);
        }

        private void HandleRuinDiscovered(RuinRecord ruin)
        {
            string label = ruin.Layer == WorldLayer.Ancient
                ? $"Ancient ruin: {ruin.era}"
                : $"Ruin: {ruin.era}";
            Enqueue(label, NotificationType.Discovery);
        }

        // -------------------------------------------------------------------------
        // External API — other systems can push notifications directly
        // -------------------------------------------------------------------------
        public void ShowRealmEvent(string message)
            => Enqueue(message, NotificationType.RealmEvent, displayDuration * 1.5f);

        // -------------------------------------------------------------------------
        // Queue management
        // -------------------------------------------------------------------------
        private void Enqueue(string text, NotificationType type,
                             float duration = -1f)
        {
            if (_queue.Count >= maxQueueSize) return; // drop if overflowing

            _queue.Enqueue(new NotificationEntry
            {
                text     = text,
                type     = type,
                duration = duration < 0f ? displayDuration : duration,
            });

            if (!_isShowing)
                StartCoroutine(ShowNext());
        }

        private void EnqueueWhisper(string text)
        {
            // Whispers always go to the whisper panel
            if (whisperGroup != null && whisperText != null)
                StartCoroutine(ShowWhisper(text));
            else
                Enqueue(text, NotificationType.DryadWhisper, whisperDuration);
        }

        // -------------------------------------------------------------------------
        // Display coroutine
        // -------------------------------------------------------------------------
        private IEnumerator ShowNext()
        {
            _isShowing = true;

            while (_queue.Count > 0)
            {
                var entry = _queue.Dequeue();
                yield return StartCoroutine(DisplayEntry(entry));
            }

            _isShowing = false;
        }

        private IEnumerator DisplayEntry(NotificationEntry entry)
        {
            if (notificationGroup == null || notificationText == null) yield break;

            notificationText.text  = entry.text;
            notificationText.color = ColorForType(entry.type);

            // Fade in
            yield return StartCoroutine(FadeGroup(notificationGroup, 0f, 1f, fadeInDuration));

            // Hold
            yield return new WaitForSeconds(entry.duration);

            // Fade out
            yield return StartCoroutine(FadeGroup(notificationGroup, 1f, 0f, fadeOutDuration));
        }

        private IEnumerator ShowWhisper(string text)
        {
            if (whisperGroup == null || whisperText == null) yield break;

            whisperText.text  = text;
            whisperText.color = colorWhisper;

            yield return StartCoroutine(FadeGroup(whisperGroup, 0f, 1f, fadeInDuration));
            yield return new WaitForSeconds(whisperDuration);
            yield return StartCoroutine(FadeGroup(whisperGroup, 1f, 0f, fadeOutDuration * 1.5f));
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            float t = 0f;
            group.alpha = from;
            while (t < duration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        // -------------------------------------------------------------------------
        // Color mapping
        // -------------------------------------------------------------------------
        private Color ColorForType(NotificationType type) => type switch
        {
            NotificationType.DryadWhisper => colorWhisper,
            NotificationType.Sanctum      => colorSanctum,
            NotificationType.Source       => colorSource,
            NotificationType.RealmEvent   => colorRealmEvent,
            _                             => colorDiscovery,
        };
    }
}
