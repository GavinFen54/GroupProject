using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace ArcherVR
{
    [System.Serializable]
    public class WaveEntry
    {
        public GameObject enemyPrefab;
        public int count = 5;
    }

    [System.Serializable]
    public class Wave
    {
        public string name = "Wave";
        public WaveEntry[] entries;
        public float spawnInterval = 1.2f;
        [Tooltip("S3 Climax: switches music and fires onBossWaveStarted.")]
        public bool isBossWave;
    }

    /// <summary>
    /// Runs S2 (Rising Action) and S3 (Climax): spawns waves that get bigger and stronger,
    /// a boss wave, then the stragglers (Falling Action). When everything is dead it hands
    /// off to GameFlow, which loads S4 (Resolution).
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public TowerHealth tower;
        public Transform[] spawnPoints;
        public Wave[] waves;

        [Header("Timing")]
        [Tooltip("Hold the first wave until the player picks up the bow.")]
        public bool waitForBowGrab = true;
        public float firstWaveDelay = 6f;
        [Tooltip("Breather between waves. This is where the upgrade step from the story map will go.")]
        public float timeBetweenWaves = 8f;

        [Header("Difficulty")]
        [Tooltip("Each wave adds this fraction of health to every enemy (0.15 = +15% per wave).")]
        public float healthGrowthPerWave = 0.15f;
        [Tooltip("Enemies stop this far out from the tower centre and start hitting it.")]
        public float attackRingRadius = 6f;

        [Header("Audio (optional)")]
        public AudioSource musicSource;
        public AudioClip waveHornSfx;
        public AudioClip battleMusic;
        public AudioClip bossMusic;

        [Header("Events")]
        public UnityEvent<int> onWaveStarted = new UnityEvent<int>();
        public UnityEvent onBossWaveStarted = new UnityEvent();
        public UnityEvent onAllWavesCleared = new UnityEvent();

        readonly List<EnemyController> alive = new List<EnemyController>();

        public int CurrentWave { get; private set; }
        public int TotalWaves => waves != null ? waves.Length : 0;
        public int AliveCount => alive.Count;
        public float NextWaveCountdown { get; private set; }
        public bool Finished { get; private set; }
        public bool WaitingForBow => waitForBowGrab && !Bow.EverGrabbed && CurrentWave == 0;

        IEnumerator Start()
        {
            // Give RuntimeNavMesh a frame to bake.
            yield return null;
            while (waitForBowGrab && !Bow.EverGrabbed) yield return null;
            yield return Countdown(firstWaveDelay);

            for (int i = 0; i < waves.Length; i++)
            {
                if (tower != null && tower.IsDestroyed) yield break;
                var wave = waves[i];
                CurrentWave = i + 1;
                onWaveStarted.Invoke(CurrentWave);
                if (waveHornSfx != null && musicSource != null) musicSource.PlayOneShot(waveHornSfx);
                if (wave.isBossWave)
                {
                    onBossWaveStarted.Invoke();
                    SwitchMusic(bossMusic);
                }
                else if (i == 0)
                {
                    SwitchMusic(battleMusic);
                }

                yield return SpawnWave(wave, i);

                while (alive.Count > 0)
                {
                    if (tower != null && tower.IsDestroyed) yield break;
                    yield return null;
                }

                if (i < waves.Length - 1) yield return Countdown(timeBetweenWaves);
            }

            Finished = true;
            SwitchMusic(null);
            onAllWavesCleared.Invoke();
            if (GameFlow.Instance != null) GameFlow.Instance.OnVictory();
        }

        IEnumerator Countdown(float seconds)
        {
            NextWaveCountdown = seconds;
            while (NextWaveCountdown > 0f)
            {
                NextWaveCountdown -= Time.deltaTime;
                yield return null;
            }
            NextWaveCountdown = 0f;
        }

        IEnumerator SpawnWave(Wave wave, int waveIndex)
        {
            // Interleave entry types so advanced enemies / the boss arrive inside the crowd.
            var queue = new List<GameObject>();
            int max = 0;
            foreach (var e in wave.entries) max = Mathf.Max(max, e.count);
            for (int n = 0; n < max; n++)
                foreach (var e in wave.entries)
                    if (n < e.count && e.enemyPrefab != null) queue.Add(e.enemyPrefab);

            foreach (var prefab in queue)
            {
                if (tower != null && tower.IsDestroyed) yield break;
                Spawn(prefab, waveIndex);
                yield return new WaitForSeconds(wave.spawnInterval);
            }
        }

        void Spawn(GameObject prefab, int waveIndex)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return;
            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            var pos = sp.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            if (NavMesh.SamplePosition(pos, out var hit, 10f, NavMesh.AllAreas)) pos = hit.position;

            var towerPos = tower != null ? tower.transform.position : Vector3.zero;
            var look = towerPos - pos; look.y = 0f;
            var go = Instantiate(prefab, pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);

            var health = go.GetComponent<Health>();
            if (health != null) health.ResetHealth(health.maxHealth * (1f + healthGrowthPerWave * waveIndex));

            var enemy = go.GetComponent<EnemyController>();
            if (enemy == null) return;
            alive.Add(enemy);
            enemy.Died += e => alive.Remove(e);
            enemy.Init(tower, AttackPoint(towerPos, pos), true);
        }

        // A point on a ring around the tower base, on the side the enemy is coming from.
        Vector3 AttackPoint(Vector3 towerPos, Vector3 from)
        {
            var dir = from - towerPos; dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * dir;
            var p = towerPos + dir * attackRingRadius;
            p.y = from.y;
            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) p = hit.position;
            return p;
        }

        void SwitchMusic(AudioClip clip)
        {
            if (musicSource == null) return;
            if (clip == null) { musicSource.Stop(); return; }
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }
    }
}
