// SpiritsCrossing — CaveOriginStoryNarrator.cs
// Plays the founding myth of the spirit animal companions as the cave's
// opening narrative before the attunement session begins.
//
// The story is a conversation between a Shaman and his Chief on a bluff
// overlooking the ocean. It establishes the mythic origin of everything
// the player is about to experience: the Great Mother's expansion, the
// spirit animal transformation, the cycle of contrast and growth.
//
// The narrator delivers lines one at a time with configurable pacing.
// During delivery, the cave environment responds:
//   - Ambient light shifts to warm ocean-sunset tones
//   - Audio fades to wind and distant waves
//   - Spirit sculptures remain dormant (they awaken after the story)
//
// After the final line, the postStoryNote bridges into the attunement.
// The session does NOT start until the story completes.
//
// The origin story plays ONCE PER BIRTH — it is the explanation of why
// the player exists in this world. The cave simulation itself is a sorting
// ceremony for each new birth, not a repeating session ritual.
//
// After the first birth has seen the story, it never plays again unless
// a rebirth occurs (lifecycle reset). Returning to the cave for subsequent
// sessions skips directly to attunement.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace V243.SandstoneCave
{
    // -------------------------------------------------------------------------
    // JSON data types
    // -------------------------------------------------------------------------
    [Serializable]
    public class OriginStoryLine
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    public class OriginStoryData
    {
        public string storyId;
        public string title;
        public string setting;
        public List<OriginStoryLine> lines = new List<OriginStoryLine>();
        public string postStoryNote;
    }

    // =========================================================================
    public class CaveOriginStoryNarrator : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------
        [Header("Data")]
        public string storyFileName = "cave_origin_story.json";

        [Header("Pacing")]
        [Tooltip("Seconds to display the setting description before lines begin.")]
        public float settingDuration = 6f;

        [Tooltip("Base seconds per line. Longer lines get proportionally more time.")]
        public float baseLineDuration = 4f;

        [Tooltip("Additional seconds per 50 characters of line text.")]
        public float durationPerCharBlock = 1.5f;

        [Tooltip("Pause between lines (seconds).")]
        public float linePause = 1.5f;

        [Tooltip("Seconds to display the post-story note before session starts.")]
        public float postNoteDuration = 8f;

        [Header("Birth Policy")]
        [Tooltip("If true, story only plays on the very first session of a birth (or rebirth).")]
        public bool oncePerBirthOnly = true;

        [Header("References")]
        public CaveSessionController sessionController;

        [Header("Debug")]
        public bool logLines = true;

        // -----------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------
        /// <summary>Fires for each line as it becomes active. Drive UI text display.</summary>
        public event Action<string, string> OnLineStarted;  // speaker, text

        /// <summary>Fires when the setting description is shown.</summary>
        public event Action<string> OnSettingShown;          // setting text

        /// <summary>Fires when the post-story bridge note is shown.</summary>
        public event Action<string> OnPostNoteShown;         // note text

        /// <summary>Fires when the entire story sequence completes.</summary>
        public event Action OnStoryComplete;

        // -----------------------------------------------------------------
        // Public state
        // -----------------------------------------------------------------
        public bool IsPlaying { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsSkippable { get; private set; }
        public OriginStoryData Data { get; private set; }
        public int CurrentLineIndex { get; private set; } = -1;

        // -----------------------------------------------------------------
        // Internal
        // -----------------------------------------------------------------
        private Coroutine _playCoroutine;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------
        private IEnumerator Start()
        {
            yield return LoadStory();

            if (Data == null) yield break;

            // The origin story is the sorting ceremony for each birth.
            // It plays once — at the very first session of this birth cycle.
            // After that, the cave goes straight to attunement.
            var universe = SpiritsCrossing.UniverseStateManager.Instance?.Current;
            int sessions = universe?.totalSessionCount ?? 0;
            bool isRebirth = universe?.lifecycle.hasRebirthOccurred ?? false;

            bool shouldPlay = sessions == 0;           // first session of this birth
            if (isRebirth && sessions <= 1)             // or first session after rebirth
                shouldPlay = true;
            if (!oncePerBirthOnly)                      // override: always play
                shouldPlay = true;

            IsSkippable = !shouldPlay;                  // can't skip your own birth story

            if (sessionController != null)
                sessionController.autoStart = false;    // narrator controls session start

            if (shouldPlay)
            {
                Play();
            }
            else
            {
                // Not first birth — skip straight to attunement
                IsComplete = true;
                if (sessionController != null && !sessionController.sessionRunning)
                    sessionController.StartSession();
            }
        }

        // -----------------------------------------------------------------
        // Loading
        // -----------------------------------------------------------------
        private IEnumerator LoadStory()
        {
            yield return null;

            // Try Data/ folder first (demo layout), then StreamingAssets
            string path = Path.Combine(Application.dataPath,
                "V243_3_SandstoneCaveDemo", "Data", storyFileName);
            if (!File.Exists(path))
                path = Path.Combine(Application.streamingAssetsPath, storyFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CaveOriginStoryNarrator] {storyFileName} not found.");
                yield break;
            }

            try
            {
                Data = JsonUtility.FromJson<OriginStoryData>(File.ReadAllText(path));
                Debug.Log($"[CaveOriginStoryNarrator] Loaded: \"{Data.title}\" " +
                          $"({Data.lines.Count} lines)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CaveOriginStoryNarrator] Load error: {e.Message}");
            }
        }

        // -----------------------------------------------------------------
        // Playback
        // -----------------------------------------------------------------
        public void Play()
        {
            if (Data == null || IsPlaying) return;
            _playCoroutine = StartCoroutine(PlaySequence());
        }

        public void Skip()
        {
            if (!IsSkippable || !IsPlaying) return;
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            CompleteStory();
        }

        private IEnumerator PlaySequence()
        {
            IsPlaying = true;
            IsComplete = false;
            CurrentLineIndex = -1;

            // --- Setting ---
            if (!string.IsNullOrEmpty(Data.setting))
            {
                OnSettingShown?.Invoke(Data.setting);
                if (logLines)
                    Debug.Log($"[CaveOriginStoryNarrator] SETTING: {Data.setting}");
                yield return new WaitForSeconds(settingDuration);
            }

            // --- Dialogue lines ---
            for (int i = 0; i < Data.lines.Count; i++)
            {
                CurrentLineIndex = i;
                var line = Data.lines[i];

                OnLineStarted?.Invoke(line.speaker, line.text);

                if (logLines)
                    Debug.Log($"[CaveOriginStoryNarrator] {line.speaker}: {line.text}");

                // Duration scales with line length
                float duration = baseLineDuration +
                    (line.text.Length / 50f) * durationPerCharBlock;
                yield return new WaitForSeconds(duration);

                // Pause between lines
                if (i < Data.lines.Count - 1)
                    yield return new WaitForSeconds(linePause);
            }

            // --- Post-story bridge ---
            if (!string.IsNullOrEmpty(Data.postStoryNote))
            {
                OnPostNoteShown?.Invoke(Data.postStoryNote);
                if (logLines)
                    Debug.Log($"[CaveOriginStoryNarrator] NOTE: {Data.postStoryNote}");
                yield return new WaitForSeconds(postNoteDuration);
            }

            CompleteStory();
        }

        private void CompleteStory()
        {
            IsPlaying = false;
            IsComplete = true;
            CurrentLineIndex = -1;

            OnStoryComplete?.Invoke();
            Debug.Log("[CaveOriginStoryNarrator] Origin story complete. Starting attunement.");

            // Start the cave session now
            if (sessionController != null && !sessionController.sessionRunning)
                sessionController.StartSession();
        }

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>Get the current line being displayed (or null if not playing).</summary>
        public OriginStoryLine GetCurrentLine()
        {
            if (Data == null || CurrentLineIndex < 0 || CurrentLineIndex >= Data.lines.Count)
                return null;
            return Data.lines[CurrentLineIndex];
        }

        /// <summary>Get line duration for external animation/audio sync.</summary>
        public float GetLineDuration(OriginStoryLine line)
        {
            if (line == null) return baseLineDuration;
            return baseLineDuration + (line.text.Length / 50f) * durationPerCharBlock;
        }
    }
}
