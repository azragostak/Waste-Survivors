using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncunun pasif item envanteri. Player objesine eklenir (yoksa GameHUD otomatik ekler).
// Diğer sistemler bonusları GetBonus ile okur: silahlar hasar/alan/bekleme süresi, PlayerController hız vs.
public class PlayerPassives : MonoBehaviour
{
    [SerializeField, Min(0)] private int maxItems = 3;

    private readonly List<PassiveItem> items = new();

    public IReadOnlyList<PassiveItem> Items => items;
    public int MaxItems => maxItems;
    public bool HasFreeSlot => items.Count < maxItems;

    public event Action<PassiveItem> ItemAdded;
    public event Action<PassiveItem> ItemLeveledUp;

    // Owner'da PlayerPassives yoksa 0 döner; çağıran tarafın null kontrolü yapmasına gerek kalmaz
    public static float GetBonus(Component owner, PassiveStat stat) =>
        owner != null && owner.TryGetComponent(out PlayerPassives passives) ? passives.GetBonus(stat) : 0f;

    public float GetBonus(PassiveStat stat)
    {
        float total = 0f;
        foreach (var item in items)
        {
            if (item.Data.stat == stat)
                total += item.Data.GetTotalValue(item.Level);
        }
        return total;
    }

    public PassiveItem GetItem(PassiveItemData data) => items.Find(i => i.Data == data);

    // Bu item level-up seçeneği olarak sunulabilir mi?
    public bool CanTake(PassiveItemData data)
    {
        var item = GetItem(data);
        return item != null ? !item.IsMaxLevel : HasFreeSlot;
    }

    public void AddOrUpgrade(PassiveItemData data)
    {
        var item = GetItem(data);
        if (item != null)
        {
            if (item.IsMaxLevel) return;
            item.Level++;
            OnGained(data);
            ItemLeveledUp?.Invoke(item);
        }
        else
        {
            if (!HasFreeSlot) return;
            item = new PassiveItem(data);
            items.Add(item);
            OnGained(data);
            ItemAdded?.Invoke(item);
        }
    }

    // Maksimum can artınca artan kısım kadar iyileş (yoksa bar boş görünür)
    private void OnGained(PassiveItemData data)
    {
        if (data.stat == PassiveStat.MaxHealth && TryGetComponent(out PlayerHealth health))
            health.Heal(data.valuePerLevel);
    }
}

public class PassiveItem
{
    public PassiveItemData Data { get; }
    public int Level { get; internal set; } = 1;
    public bool IsMaxLevel => Level >= Data.maxLevel;

    public PassiveItem(PassiveItemData data) => Data = data;
}
