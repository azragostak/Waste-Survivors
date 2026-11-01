using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncunun silah envanteri. Player objesine eklenir.
// Level-up ekranı (LevelUpMenu) seçenekleri CanTake ile filtreleyip AddOrUpgrade'i çağırır.
public class PlayerWeapons : MonoBehaviour
{
    [Tooltip("Açıksa oyun başında silah seçim ekranı gelir (GameHUD gerekir) ve Starting Weapons kullanılmaz")]
    [SerializeField] private bool chooseStartingWeapon = true;
    [Tooltip("Silah seçim ekranı kapalıysa ya da sahnede GameHUD yoksa verilen silahlar")]
    [SerializeField] private List<WeaponData> startingWeapons = new();
    [SerializeField] private int maxWeapons = 3;

    [Header("Test")]
    [Tooltip("Editörde L tuşu tüm silahları 1 seviye yükseltir")]
    [SerializeField] private bool debugLevelUpKey = true;

    private readonly List<Weapon> weapons = new();

    public IReadOnlyList<Weapon> Weapons => weapons;
    public int MaxWeapons => maxWeapons;
    // Başlangıç silahını oyuncu seçecek mi? (LevelUpMenu buna bakıp seçim ekranını açar)
    public bool ChoosesStartingWeapon => chooseStartingWeapon && FindObjectOfType<GameHUD>() != null;
    public bool HasFreeSlot => weapons.Count < maxWeapons;

    public event Action<Weapon> WeaponAdded;
    public event Action<Weapon> WeaponLeveledUp;
    public event Action<Weapon> WeaponEvolved;

    // Evrimleşmiş silahların eski halleri: tekrar "yeni silah" olarak sunulmasın
    private readonly HashSet<WeaponData> evolvedFrom = new();

    void Start()
    {
        if (ChoosesStartingWeapon) return;

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

    public bool HasEvolved(WeaponData baseData) => evolvedFrom.Contains(baseData);

    // Silah maksimum seviyede ve evrim için gereken pasif elde mi?
    public bool CanEvolve(Weapon weapon, PlayerPassives passives)
    {
        var data = weapon.Data;
        if (!weapon.IsMaxLevel || data.evolution == null || data.evolution.weaponPrefab == null) return false;
        return data.evolutionRequirement == null || (passives != null && passives.GetItem(data.evolutionRequirement) != null);
    }

    // Silahı aynı slotta evrimleşmiş haliyle değiştirir
    public void Evolve(Weapon weapon)
    {
        int index = weapons.IndexOf(weapon);
        var evolvedData = weapon.Data.evolution;
        if (index < 0 || evolvedData == null || evolvedData.weaponPrefab == null) return;

        var evolved = Instantiate(evolvedData.weaponPrefab, transform);
        evolved.Initialize(evolvedData, transform);
        weapons[index] = evolved;
        evolvedFrom.Add(weapon.Data);
        Destroy(weapon.gameObject);
        WeaponEvolved?.Invoke(evolved);
    }

    // Bu silah level-up seçeneği olarak sunulabilir mi?
    public bool CanTake(WeaponData data)
    {
        if (HasEvolved(data)) return false;
        var weapon = GetWeapon(data);
        return weapon != null ? !weapon.IsMaxLevel : HasFreeSlot;
    }

    public void AddOrUpgrade(WeaponData data)
    {
        if (HasEvolved(data)) return;
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
