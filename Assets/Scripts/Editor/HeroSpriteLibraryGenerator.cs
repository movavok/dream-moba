#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.U2D.Animation;

public static class HeroSpriteLibraryGenerator
{
    [MenuItem("Tools/Hero Animation/Generate Sprite Library")]
    public static void Generate()
    {
        HeroDefinition hero =
            Selection.activeObject as HeroDefinition;

        if (hero == null)
        {
            Debug.LogError(
                "Select a HeroDefinition asset first!"
            );

            return;
        }

        if (hero.visual == null)
        {
            Debug.LogError(
                $"Hero '{hero.heroName}' has no VisualDefinition!"
            );

            return;
        }

        string path =
            $"Assets/Animations/{hero.heroName}SpriteLibrary.asset";

        SpriteLibraryAsset library =
            AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(path);

        if (library == null)
        {
            library = new SpriteLibraryAsset();

            AssetDatabase.CreateAsset(
                library,
                path
            );
        }

        AddSprites(
            library,
            "Front",
            hero.visual.walkFront
        );

        AddSprites(
            library,
            "FrontLeft",
            hero.visual.walkFrontLeft
        );

        AddSprites(
            library,
            "FrontRight",
            hero.visual.walkFrontRight
        );

        AddSprites(
            library,
            "Back",
            hero.visual.walkBack
        );

        AddSprites(
            library,
            "BackLeft",
            hero.visual.walkBackLeft
        );

        AddSprites(
            library,
            "BackRight",
            hero.visual.walkBackRight
        );

        AddSprites(
            library,
            "Left",
            hero.visual.walkLeft
        );

        AddSprites(
            library,
            "Right",
            hero.visual.walkRight
        );

        EditorUtility.SetDirty(library);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Sprite Library generated for {hero.heroName}!"
        );
    }

    private static void AddSprites(
        SpriteLibraryAsset library,
        string category,
        Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning(
                $"Category '{category}' has no sprites."
            );

            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            string label = $"Frame{i}";

            library.AddCategoryLabel(
                sprites[i],
                category,
                label
            );
        }
    }
}

#endif