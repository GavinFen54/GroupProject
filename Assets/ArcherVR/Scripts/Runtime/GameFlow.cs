using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcherVR
{
    /// <summary>
    /// Moves the player through the story-map scenes:
    /// S1 Exposition -> S2 Battle (Rising Action + Climax + Falling Action) -> S4 Resolution.
    /// Losing shows the alternate resolution: start over or quit.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public const string ScenePrologue = "S0_Prologue";
        public const string SceneExposition = "S1_Exposition";
        public const string SceneBattle = "S2_Battle";
        public const string SceneResolution = "S4_Resolution";

        public static GameFlow Instance { get; private set; }

        [Tooltip("Shown when the tower falls (S3-ALT in the story map).")]
        public GameObject defeatPanel;
        [Tooltip("Seconds to linger on the empty battlefield before loading S4.")]
        public float victoryDelay = 4f;

        bool ending;

        void Awake()
        {
            Instance = this;
            if (defeatPanel != null) defeatPanel.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnTowerDestroyed()
        {
            if (ending) return;
            ending = true;
            if (defeatPanel != null) defeatPanel.SetActive(true);
        }

        public void OnVictory()
        {
            if (ending) return;
            ending = true;
            StartCoroutine(LoadAfter(SceneResolution, victoryDelay));
        }

        IEnumerator LoadAfter(string scene, float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(scene);
        }

        // Hooked to UI buttons by the scene builder.
        public void StartBattle() => SceneManager.LoadScene(SceneBattle);
        public void RestartBattle() => SceneManager.LoadScene(SceneBattle);
        public void StartOver() => SceneManager.LoadScene(SceneExposition);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
