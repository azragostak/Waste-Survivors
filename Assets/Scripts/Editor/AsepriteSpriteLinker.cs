using System.Linq;
using UnityEditor;
using UnityEngine;

// Aseprite dosyalarındaki sprite'ları prefab ve asset alanlarına bağlar.
// Aseprite sprite'larının ID'si Unity import edince belli olduğu için dosyalara elle yazılamıyor;
// aşağıdaki alanlar boşsa editör açılışında bir kez doldurur, doluysa hiçbir şey yapmaz.
// Yeni bir bağlantı gerekirse Links listesine eklemek yeterli.
[InitializeOnLoad]
public static class AsepriteSpriteLinker
{
    private class Link
    {
        public string targetPath;    // .prefab ya da .asset
        public string propertyName;  // prefab ise kök objedeki component'in alanı
        public string[] spritePaths; // tek sprite alanı için 1, dizi alanı için birden fazla
    }

    private static readonly Link[] Links =
    {
        new Link
        {
            targetPath = "Assets/Scripts/Weapons/WindTurbine/WindTurbineWeapon.prefab",
            propertyName = "bladeSprite",
            spritePaths = new[] { "Assets/Sprites/ruzgar_turbini_kanat.aseprite" },
        },
        new Link
        {
            targetPath = "Assets/Enemies/BossEnemy.prefab",
            propertyName = "bodySprite",
            spritePaths = new[] { "Assets/Sprites/boss_cop_tepesi.aseprite" },
        },
        new Link
        {
            targetPath = "Assets/Items/GasMask.asset",
            propertyName = "icon",
            spritePaths = new[] { "Assets/Sprites/Items/gaz_maskesi.aseprite" },
        },
        new Link
        {
            targetPath = "Assets/Enemies/BottleEnemy.prefab",
            propertyName = "variants",
            spritePaths = new[]
            {
                "Assets/Sprites/cam_sise_kirik.aseprite",
                "Assets/Sprites/pet_sise_gazoz_ezik.aseprite",
                "Assets/Sprites/pet_sise_su_ezik.aseprite",
            },
        },
    };

    static AsepriteSpriteLinker()
    {
        EditorApplication.delayCall += LinkAll;
    }

    private static void LinkAll()
    {
        foreach (var link in Links)
            Apply(link);
    }

    private static void Apply(Link link)
    {
        var sprites = link.spritePaths.Select(LoadSprite).ToArray();
        if (sprites.Any(s => s == null)) return; // henüz import edilmemiş

        var target = AssetDatabase.LoadMainAssetAtPath(link.targetPath);
        if (target == null) return;

        var prefab = target as GameObject;
        foreach (var obj in prefab != null ? prefab.GetComponents<Component>() : new[] { target })
        {
            if (obj == null) continue;

            var serialized = new SerializedObject(obj);
            var property = serialized.FindProperty(link.propertyName);
            if (property == null) continue;

            if (!Fill(property, sprites)) return; // zaten dolu

            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (prefab != null)
                PrefabUtility.SavePrefabAsset(prefab);
            else
                AssetDatabase.SaveAssetIfDirty(target);

            Debug.Log($"Aseprite sprite'ları bağlandı: {link.targetPath} → {link.propertyName}", target);
            return;
        }
    }

    // Alan boşsa doldurur ve true döner
    private static bool Fill(SerializedProperty property, Sprite[] sprites)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            if (property.arraySize > 0) return false;

            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            return true;
        }

        if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null)
            return false;

        property.objectReferenceValue = sprites[0];
        return true;
    }

    private static Sprite LoadSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
}
