using System;
using UnityEngine;

// Oyun dili yönetimi: Varsayılan İngilizce (English), isteğe bağlı Türkçe (Turkish).
// Seçim PlayerPrefs'te saklanır ve sahneler arası korunur.
public enum Language
{
    English = 0,
    Turkish = 1,
}

public static class GameLanguage
{
    private const string PrefKey = "GameLanguage_v1";

    private static bool initialized;
    private static Language current = Language.English;

    public static Language Current
    {
        get
        {
            EnsureInitialized();
            return current;
        }
    }

    public static bool IsTurkish => Current == Language.Turkish;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        initialized = false;
        current = Language.English;
        Changed = null;
    }

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        // Varsayılan dil her zaman İngilizce (0)
        int saved = PlayerPrefs.GetInt(PrefKey, (int)Language.English);
        current = saved == (int)Language.Turkish ? Language.Turkish : Language.English;
    }

    public static void SetLanguage(Language language)
    {
        EnsureInitialized();
        if (current == language) return;

        current = language;
        PlayerPrefs.SetInt(PrefKey, (int)current);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static string Tr(string english, string turkish) => IsTurkish ? turkish : english;

    public static string WeaponName(WeaponData data)
    {
        if (data == null) return "";
        if (!IsTurkish) return data.displayName;

        return data.name switch
        {
            "WindTurbine" => "Rüzgar Türbini",
            "StormTurbine" => "Fırtına Türbini",
            "SeedShooter" => "Tohum Atıcı",
            "BrambleShooter" => "Dikenli Tohum Atıcı",
            "HydraulicPress" => "Hidrolik Pres",
            "MagneticPress" => "Manyetik Pres",
            _ => data.displayName,
        };
    }

    public static string WeaponDescription(WeaponData data)
    {
        if (data == null) return "";
        if (!IsTurkish) return data.description;

        return data.name switch
        {
            "WindTurbine" => "Türbin kanatları etrafında dönerek çarptığı her düşmana hasar verir. Seviye atladıkça kanat sayısı, hasar ve dönüş hızı artar.",
            "StormTurbine" => "Elektrik yüklü kanatlar etrafında döner. Çarptıkları her düşmanı elektrikle çarpar ve kısa süreliğine sersemletir.",
            "SeedShooter" => "En yakın düşmana filizlenen tohumlar fırlatır. Seviye atladıkça saldırı hızı ve hasar artar.",
            "BrambleShooter" => "Gübrelenmiş tohumlar seker: hedefini vurduktan sonra başka bir tohumun yönelmediği en yakın 2 düşmana daha sıçrar. Her vuruşta biraz can çalar.",
            "HydraulicPress" => "Gökyüzünden düşmanların üzerine ağır bir geri dönüşüm presi indirerek alan hasarı verir.",
            "MagneticPress" => "Düşeceği noktaya yakındaki düşmanları kısa süreliğine çeker ve hepsini tek seferde ezer. Ezdiği düşmanların XP'si doğrudan sana uçar.",
            _ => data.description,
        };
    }

    public static string WeaponUpgradeDescription(WeaponData data, int level)
    {
        if (data == null) return "";
        string fallback = data.GetUpgradeDescription(level);
        if (!IsTurkish) return fallback;

        return data.name switch
        {
            "WindTurbine" => level switch
            {
                1 => "Etrafında dönen 2 türbin kanadı çarptığı her düşmana hasar verir.",
                2 => "Dönüş hızı +%20",
                3 => "+1 kanat",
                4 => "Hasar +%29",
                5 => "Hasar +%22",
                6 => "Kanatlar %15 daha uzun, hasar +%5",
                7 => "Dönüş hızı +%20, hasar +%13",
                8 => "+1 kanat, hasar +%4",
                9 => "Hasar +%15, aynı düşmana daha sık vurur",
                10 => "MAKS: +1 kanat, hasar +%19, dönüş hızı +%20",
                _ => fallback,
            },
            "StormTurbine" => "Rüzgar Türbini + Güneş Paneli evrimi",
            "SeedShooter" => level switch
            {
                1 => "En yakın düşmana hızla tohumlar fırlatır.",
                2 => "Hasar +6, saldırı hızı +%10",
                3 => "Hasar +6, saldırı hızı +%9",
                4 => "Hasar +9, saldırı hızı +%10",
                5 => "Hasar +9, saldırı hızı +%11",
                6 => "+1 tohum, hasar +9, saldırı hızı +%9",
                7 => "Hasar +12, saldırı hızı +%9",
                8 => "Hasar +12, saldırı hızı +%10",
                9 => "Hasar +15, saldırı hızı +%12",
                10 => "MAKS: +1 tohum, hasar +24, saldırı hızı +%13!",
                _ => fallback,
            },
            "BrambleShooter" => "Tohum Atıcı + Gübre evrimi",
            "HydraulicPress" => level switch
            {
                1 => "Gökyüzünden düşmanları ezmek için bir hidrolik pres indirir.",
                2 => "+1 hidrolik pres",
                3 => "Hasar +15, ezme alanı +%25",
                4 => "Daha hızlı ve daha sık iner",
                5 => "+1 pres, hasar +18, daha geniş ezme alanı",
                6 => "Hasar +24",
                7 => "Ezme alanı +%15",
                8 => "Daha sık iner",
                9 => "+1 pres",
                10 => "MAKS: Hasar +36, daha geniş alan, daha hızlı ve sık iner",
                _ => fallback,
            },
            "MagneticPress" => "Hidrolik Pres + Mıknatıs evrimi",
            _ => fallback,
        };
    }

    public static string ItemName(PassiveItemData data)
    {
        if (data == null) return "";
        if (!IsTurkish) return data.displayName;

        return data.name switch
        {
            "Fertilizer" => "Gübre",
            "GasMask" => "Gaz Maskesi",
            "Magnet" => "Mıknatıs",
            "SolarPanel" => "Güneş Paneli",
            _ => data.displayName,
        };
    }

    public static string ItemDescription(PassiveItemData data)
    {
        if (data == null) return "";
        if (!IsTurkish) return data.description;

        return data.name switch
        {
            "Fertilizer" => "Tüm silahlarının etki alanını büyütür.",
            "GasMask" => "Seni kirli havadan korur: düşmanlardan daha az hasar alırsın.",
            "Magnet" => "Geri dönüşüm coinlerini daha uzaktan kendine çeker.",
            "SolarPanel" => "Güneş enerjisi tüm silahlarını daha hızlı şarj eder, böylece daha sık saldırırlar.",
            _ => data.description,
        };
    }
}

