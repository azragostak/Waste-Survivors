using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncunun silah envanteri. Player objesine eklenir.
// İleride level-up sistemi seçenekleri CanTake ile filtreleyip AddOrUpgrade'i çağıracak.
public class PlayerWeapons : MonoBehaviour
{
    [SerializeField] private List<WeaponData> startingWeapons = new();
    [SerializeField] private int maxWeapons = 6;

    [Header("Test")]
    [Tooltip("Editörde L tuşu tüm silahları 1 seviye yükseltir")]
    [SerializeField] private bool debugLevelUpKey = true;

    private readonly List<Weapon> weapons = new();

    public IReadOnlyList<Weapon> Weapons => weapons;
    public bool HasFreeSlot => weapons.Count < maxWeapons;

    public event Action<Weapon> WeaponAdded;
    public event Action<Weapon> WeaponLeveledUp;

    void Start()
    {
        foreach (var data in startingWeapons)
            AddOrUpgrade(data);
    }

    void Update()
    {
        if (debugLevelUpKey && Application.isEditor && Input.GetKeyDown(KeyCode.L))
        {
            foreach (var weapon in weapons)
            {
                Upgrade(weapon);
                Debug.Log($"{weapon.Data.displayName} -> Level {weapon.Level}/{weapon.Data.MaxLevel}");
            }
        }
    }

    public Weapon GetWeapon(WeaponData data) => weapons.Find(w => w.Data == data);

    // Bu silah level-up seçeneği olarak sunulabilir mi?
    public bool CanTake(WeaponData data)
    {
        var weapon = GetWeapon(data);
        return weapon != null ? !weapon.IsMaxLevel : HasFreeSlot;
    }

    public void AddOrUpgrade(WeaponData data)
    {
        var weapon = GetWeapon(data);
        if (weapon != null)
            Upgrade(weapon);
        else
            AddWeapon(data);
    }

    private void AddWeapon(WeaponData data)
    {
        if (!HasFreeSlot) return;
        if (data.weaponPrefab == null)
        {
            Debug.LogWarning($"{data.name} için Weapon Prefab atanmamış.", data);
            return;
        }

        var weapon = Instantiate(data.weaponPrefab, transform);
        weapon.Initialize(data, transform);
        weapons.Add(weapon);
        WeaponAdded?.Invoke(weapon);
    }

    private void Upgrade(Weapon weapon)
    {
        if (weapon.IsMaxLevel) return;
        weapon.LevelUp();
        WeaponLeveledUp?.Invoke(weapon);
    }
}
