using System.Collections.Generic;
using UnityEngine;

// Level-up ekranında çıkabilecek tüm silahlar ve pasif itemler.
// Assets/Resources/UpgradePool.asset GameHUD tarafından otomatik bulunur; yeni silah/item eklenince buraya da ekle.
[CreateAssetMenu(fileName = "UpgradePool", menuName = "Waste Survivors/Upgrade Pool")]
public class UpgradePool : ScriptableObject
{
    public const string ResourcePath = "UpgradePool";

    public List<WeaponData> weapons = new();
    public List<PassiveItemData> passiveItems = new();
}
