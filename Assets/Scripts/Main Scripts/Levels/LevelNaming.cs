using System;
using UnityEngine;

/// <summary>
/// Single source of truth for translating between a logical level number and the
/// scene that hosts it.
///
/// Before this existed, six separate files each re-implemented the same
/// "StartsWith(\"Level\") + int.TryParse" pair, and each of them formatted the next
/// scene name with its own string interpolation. Renaming the level scenes to a
/// zero-padded form would have needed six independent, individually-wrong edits.
///
/// The campaign scenes are named <c>Level01</c>..<c>Level25</c>, but the shipped
/// build previously used the unpadded <c>Level1</c>..<c>Level5</c>. Parsing accepts
/// both forms and <see cref="TryResolveLoadableSceneName"/> probes padded first and
/// unpadded second, so a half-renamed project still loads every level it has.
/// </summary>
public static class LevelNaming
{
    public const string Prefix = "Level";
    public const int PaddedDigits = 2;
    public const string SceneFolder = "Assets/Scenes/MainLevels";
    public const string TutorialSceneName = "TUTORIAL";
    public const string MainMenuSceneName = "MainMenu";

    /// <summary>Highest level number the campaign is authored up to.</summary>
    public const int CampaignLevelCount = 25;

    /// <summary>
    /// Parses a scene name into its logical level number. Accepts both "Level7"
    /// and "Level07"; rejects anything that is not the prefix followed purely by
    /// digits, so "LevelSelect" and "Level2-test" do not read as levels.
    /// </summary>
    public static bool TryGetLevelNumber(string sceneName, out int levelNumber)
    {
        levelNumber = 0;

        if (string.IsNullOrEmpty(sceneName) ||
            !sceneName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = sceneName.Substring(Prefix.Length);
        if (suffix.Length == 0)
        {
            return false;
        }

        // int.TryParse would accept " 7", "+7" and "-7". A scene name is not a
        // user-entered number, so anything but plain digits is a different scene.
        foreach (char character in suffix)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        return int.TryParse(suffix, out levelNumber) && levelNumber >= 1;
    }

    /// <summary>True when the scene is a campaign level scene.</summary>
    public static bool IsLevelScene(string sceneName)
    {
        return TryGetLevelNumber(sceneName, out _);
    }

    /// <summary>The canonical, zero-padded scene name: 7 -> "Level07".</summary>
    public static string GetSceneName(int levelNumber)
    {
        return Prefix + Mathf.Max(1, levelNumber).ToString("D" + PaddedDigits);
    }

    /// <summary>The pre-rename scene name: 7 -> "Level7".</summary>
    public static string GetLegacySceneName(int levelNumber)
    {
        return Prefix + Mathf.Max(1, levelNumber).ToString();
    }

    /// <summary>Asset path of the canonical scene: 7 -> "Assets/Scenes/MainLevels/Level07.unity".</summary>
    public static string GetScenePath(int levelNumber)
    {
        return SceneFolder + "/" + GetSceneName(levelNumber) + ".unity";
    }

    public static string GetLegacyScenePath(int levelNumber)
    {
        return SceneFolder + "/" + GetLegacySceneName(levelNumber) + ".unity";
    }

    /// <summary>
    /// Returns the name of a scene for this level that is actually present in
    /// Build Settings, preferring the padded form. This is what makes the
    /// Level1 -> Level01 rename safe to land incrementally: whichever form is in
    /// the build is the one that gets loaded.
    /// </summary>
    public static bool TryResolveLoadableSceneName(int levelNumber, out string sceneName)
    {
        sceneName = GetSceneName(levelNumber);
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return true;
        }

        sceneName = GetLegacySceneName(levelNumber);
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return true;
        }

        sceneName = null;
        return false;
    }
}
