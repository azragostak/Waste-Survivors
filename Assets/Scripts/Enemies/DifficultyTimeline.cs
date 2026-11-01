using System;
using System.Collections.Generic;
using UnityEngine;

// 10 dakikalık oyunun zorluk eğrisi. EnemySpawner bunu okur.
// Assets/Resources/DifficultyTimeline.asset otomatik bulunur; değerleri Inspector'dan ayarlanabilir.
//
// Fazlar başlangıç dakikasına göre sıralıdır. Spawn hızı, can ve hasar çarpanları bir fazdan sonrakine
// yumuşakça geçer (aniden zıplamaz); düşman karışımı ve sürüler faz başladığında değişir.
[CreateAssetMenu(fileName = "DifficultyTimeline", menuName = "Waste Survivors/Difficulty Timeline")]
public class DifficultyTimeline : ScriptableObject
{
    public const string ResourcePath = "DifficultyTimeline";

    [Serializable]
    public class EnemyWeight
    {
        public Enemy prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Serializable]
    public class Swarm
    {
        [Tooltip("Boşsa bu fazda sürü gelmez")]
        public Enemy prefab;
        [Min(1)] public int count = 15;
        [Tooltip("İki sürü arası süre (sn). İlk sürü fazın başından bu sürenin yarısı kadar sonra gelir.")]
        [Min(1f)] public float interval = 30f;
    }

    [Serializable]
    public class Phase
    {
        [Tooltip("Bu fazın başladığı dakika (0 = oyun başı)")]
        [Min(0f)] public float startMinute;
        [Tooltip("Saniyede doğan tekli düşman sayısı")]
        [Min(0f)] public float spawnsPerSecond = 1f;
        [Tooltip("Düşman canı çarpanı")]
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Tooltip("Düşmanların oyuncuya temas hasarı çarpanı")]
        [Min(0.1f)] public float damageMultiplier = 1f;
        [Tooltip("Aynı anda en fazla kaç düşman olabilir (sürüler dahil)")]
        [Min(1)] public int maxEnemies = 100;
        [Tooltip("Tekli spawn'larda hangi düşmanın ne sıklıkla çıkacağı")]
        public List<EnemyWeight> enemies = new();
        public Swarm swarm = new();
    }

    [Serializable]
    public class RingEvent
    {
        [Tooltip("Hangi dakikada (ör. 5 = 05:00, 9.5 = 09:30)")]
        [Min(0f)] public float minute;
        public Enemy prefab;
        [Tooltip("Oyuncunun etrafını çember gibi saran düşman sayısı")]
        [Min(1)] public int count = 24;
    }

    public List<Phase> phases = new();
    [Tooltip("Belirli anlarda oyuncunun etrafını saran çemberler (gerilim zirveleri)")]
    public List<RingEvent> ringEvents = new();

    public int GetPhaseIndex(float seconds)
    {
        float minute = seconds / 60f;
        int index = 0;
        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i].startMinute <= minute) index = i;
            else break;
        }
        return index;
    }

    // Faz içinde bir sonraki faza doğru ilerleme (0-1); sayısal değerlerin yumuşak geçişi için
    public float GetPhaseProgress(float seconds, int index)
    {
        if (index + 1 >= phases.Count) return 0f;
        float start = phases[index].startMinute * 60f;
        float end = phases[index + 1].startMinute * 60f;
        return end > start ? Mathf.Clamp01((seconds - start) / (end - start)) : 0f;
    }

    public float GetSpawnsPerSecond(float seconds) => Interpolate(seconds, p => p.spawnsPerSecond);
    public float GetHealthMultiplier(float seconds) => Interpolate(seconds, p => p.healthMultiplier);
    public float GetDamageMultiplier(float seconds) => Interpolate(seconds, p => p.damageMultiplier);
    public int GetMaxEnemies(float seconds) => Mathf.RoundToInt(Interpolate(seconds, p => p.maxEnemies));

    private float Interpolate(float seconds, Func<Phase, float> value)
    {
        if (phases.Count == 0) return 0f;
        int index = GetPhaseIndex(seconds);
        float t = GetPhaseProgress(seconds, index);
        float current = value(phases[index]);
        return index + 1 < phases.Count ? Mathf.Lerp(current, value(phases[index + 1]), t) : current;
    }
}
