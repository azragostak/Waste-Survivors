using System.Collections.Generic;
using UnityEngine;

// Güneş Paneli: en yakın düşmanlara panel fırlatır. Panel düştüğü yere kurulur ve
// belirli aralıklarla etrafına elektrik çarpar (bkz. SolarPanelTurret).
//
// Stat kullanımı:
//   cooldown    = fırlatma aralığı       amount   = atış başına panel sayısı
//   range       = hedef arama menzili    speed    = fırlatma hızı
//   damage      = elektrik hasarı        area     = elektrik yarıçapı
//   hitInterval = elektrik aralığı       duration = panelin yerde kalma süresi
public class SolarPanelWeapon : Weapon
{
    [Tooltip("Fırlatılacak panel prefab'ları. Her panel için rastgele biri seçilir.")]
    [SerializeField] private SolarPanelTurret[] panelPrefabs;

    private readonly List<Enemy> targets = new();

    protected override bool TryAttack()
    {
        if (panelPrefabs == null || panelPrefabs.Length == 0) return false;

        var stats = Stats;
        Enemy.GetNearest(Owner.position, stats.range, stats.amount, targets);
        if (targets.Count == 0) return false;

        for (int i = 0; i < stats.amount; i++)
        {
            Vector2 targetPosition = targets[i % targets.Count].transform.position;

            // Panel sayısı düşman sayısından fazlaysa üst üste binmesinler
            if (i >= targets.Count)
                targetPosition += Random.insideUnitCircle * stats.area;

            var prefab = panelPrefabs[Random.Range(0, panelPrefabs.Length)];
            var panel = Instantiate(prefab, Owner.position, Quaternion.identity);
            panel.Launch(targetPosition, stats);
        }

        return true;
    }
}
