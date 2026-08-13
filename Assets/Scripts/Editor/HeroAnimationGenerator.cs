#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

public static class HeroAnimationGenerator
{
    [MenuItem("Tools/Hero Animation/Generate Selected Hero")]
    public static void GenerateSelectedHero()
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

        string heroName = hero.heroName;

        string folder =
            $"Assets/Animations/{heroName}";

        CreateFolderIfNeeded("Assets/Animations");
        CreateFolderIfNeeded(folder);

        CreateClip("WalkFront", hero.visual.walkFront, folder);
        CreateClip("WalkFrontLeft", hero.visual.walkFrontLeft, folder);
        CreateClip("WalkFrontRight", hero.visual.walkFrontRight, folder);

        CreateClip("WalkBack", hero.visual.walkBack, folder);
        CreateClip("WalkBackLeft", hero.visual.walkBackLeft, folder);
        CreateClip("WalkBackRight", hero.visual.walkBackRight, folder);

        CreateClip("WalkLeft", hero.visual.walkLeft, folder);
        CreateClip("WalkRight", hero.visual.walkRight, folder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Hero animations generated for {heroName}!"
        );
    }

    private static void CreateClip(
        string clipName,
        Sprite[] sprites,
        string folder)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning(
                $"{clipName}: no sprites assigned."
            );

            return;
        }

        string path =
            $"{folder}/{clipName}.anim";

        AnimationClip clip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        if (clip == null)
        {
            clip = new AnimationClip();

            AssetDatabase.CreateAsset(
                clip,
                path
            );
        }

        clip.frameRate = 8f;

        EditorCurveBinding binding =
            new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

        float frameTime = 1f / clip.frameRate;

        ObjectReferenceKeyframe[] keys =
            new ObjectReferenceKeyframe[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(
            clip,
            binding,
            keys
        );

        EditorUtility.SetDirty(clip);
    }

    private static void CreateFolderIfNeeded(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent =
            System.IO.Path.GetDirectoryName(folder)
                .Replace("\\", "/");

        string folderName =
            System.IO.Path.GetFileName(folder);

        CreateFolderIfNeeded(parent);

        AssetDatabase.CreateFolder(
            parent,
            folderName
        );
    }
}

#endif