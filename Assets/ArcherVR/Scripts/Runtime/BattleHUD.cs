using UnityEngine;
using UnityEngine.UI;

namespace ArcherVR
{
    /// <summary>World-space status board next to the player: wave, enemies left, tower integrity.</summary>
    public class BattleHUD : MonoBehaviour
    {
        public WaveManager waves;
        public TowerHealth tower;
        public Text label;

        float next;

        void Update()
        {
            if (label == null || Time.time < next) return;
            next = Time.time + 0.2f;

            string waveLine;
            if (waves == null) waveLine = "";
            else if (waves.Finished) waveLine = "The invasion is broken!";
            else if (waves.WaitingForBow) waveLine = "Grab your bow to begin!\n(grip to hold, trigger to fire)";
            else if (waves.InPractice) waveLine = "Warm up: hit the target!\nAim with the yellow line, pull the trigger";
            else if (waves.PracticeJustDone) waveLine = $"Good shot! Enemies approaching... {Mathf.CeilToInt(waves.NextWaveCountdown)}";
            else if (waves.CurrentWave == 0) waveLine = $"Enemies approaching... {Mathf.CeilToInt(waves.NextWaveCountdown)}";
            else if (waves.AliveCount == 0 && waves.NextWaveCountdown > 0f) waveLine = $"Wave {waves.CurrentWave} cleared. Next in {Mathf.CeilToInt(waves.NextWaveCountdown)}";
            else waveLine = $"Wave {waves.CurrentWave}/{waves.TotalWaves}   Enemies: {waves.AliveCount}";

            string towerLine = tower != null ? $"Tower: {Mathf.CeilToInt(tower.Normalized * 100f)}%" : "";
            label.text = waveLine + "\n" + towerLine;
            if (tower != null)
                label.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), Color.white, tower.Normalized);
        }
    }
}
