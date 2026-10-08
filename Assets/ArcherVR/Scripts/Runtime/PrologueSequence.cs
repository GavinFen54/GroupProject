using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcherVR
{
    /// <summary>
    /// S0 Prologue: story text fades in line by line on a panel in front of the player,
    /// then the game moves on to S1 (walk to the tower). "Skip" jumps straight there.
    /// Edit the lines in the Inspector to change the story.
    /// </summary>
    public class PrologueSequence : MonoBehaviour
    {
        public Text body;
        public CanvasGroup textGroup;
        [TextArea(2, 4)]
        public string[] lines =
        {
            "Late autumn. The harvest is in, and the village sleeps.",
            "At dawn the scouts rode back with grim news:\nan army is marching on our lands.",
            "Our walls are old and our soldiers are few.\nBut the watchtower still stands.",
            "From its top, one steady archer\ncan hold the line until help arrives.",
            "You are that archer.\nCheck your bowstring. Steady your breath.",
            "When the horns sound, every arrow counts.\nIf the tower falls, the village falls with it.",
            "Walk to the tower, archer.\nTake your post.",
        };
        public float fadeSeconds = 1f;
        public float holdSeconds = 4.5f;
        public float startDelay = 1.5f;
        public string nextScene = GameFlow.SceneExposition;

        bool leaving;

        IEnumerator Start()
        {
            if (textGroup != null) textGroup.alpha = 0f;
            yield return new WaitForSeconds(startDelay);
            foreach (var line in lines)
            {
                if (leaving) yield break;
                if (body != null) body.text = line;
                yield return Fade(0f, 1f);
                yield return new WaitForSeconds(holdSeconds + line.Length * 0.02f);
                yield return Fade(1f, 0f);
            }
            Continue();
        }

        IEnumerator Fade(float from, float to)
        {
            if (textGroup == null) yield break;
            for (float t = 0; t < 1f; t += Time.deltaTime / fadeSeconds)
            {
                textGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            textGroup.alpha = to;
        }

        /// <summary>Hooked to the Skip button and called when the story ends.</summary>
        public void Continue()
        {
            if (leaving) return;
            leaving = true;
            SceneManager.LoadScene(nextScene);
        }
    }
}
