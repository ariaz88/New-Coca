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
///
/// THREE-digit padded names (<c>Level001</c>) are a separate thing: sandbox levels.
/// They are deliberately NOT campaign levels. Plain digit parsing would read
/// "001" as 1 and a sandbox scene would silently masquerade as Level01 -
/// recording campaign progress against it and, on a win, advancing the player to
/// Level02. See <see cref="TryGetSandboxNumber"/>.
/// </summary>
public static class LevelNaming
{
    public const string Prefix = "Level";
    public const int PaddedDigits = 2;
    public const string SceneFolder = "Assets/Scenes/MainLevels";
    public const string TutorialSceneName = "TUTORIAL";
    public const string MainMenuSceneName = "MainMenu";

    /// <summary>Zero-padded width that marks a scene as a sandbox, not campaign.</summary>
    public const int SandboxDigits = 3;

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

        if (!TryGetDigits(sceneName, out string suffix))
        {
            return false;
        }

        // Over-padded forms ("001") are sandbox scenes, not campaign levels.
        // Without this test they parse as 1 and impersonate Level01.
        if (IsOverPadded(suffix))
        {
            return false;
        }

        return int.TryParse(suffix, out levelNumber) && levelNumber >= 1;
    }

    /// <summary>
    /// Parses a sandbox scene name into its sandbox number: "Level001" -> 1.
    /// Sandbox levels are hand-built test scenes. They are playable, but they are
    /// not part of the campaign: no progress is recorded against them and a win
    /// does not advance to the next campaign level.
    /// </summary>
    public static bool TryGetSandboxNumber(string sceneName, out int sandboxNumber)
    {
        sandboxNumber = 0;

        if (!TryGetDigits(sceneName, out string suffix) || !IsOverPadded(suffix))
        {
            return false;
        }

        return int.TryParse(suffix, out sandboxNumber) && sandboxNumber >= 1;
    }

    /// <summary>The canonical sandbox scene name: 1 -> "Level001".</summary>
    public static string GetSandboxSceneName(int sandboxNumber)
    {
        return Prefix + Mathf.Max(1, sandboxNumber).ToString("D" + SandboxDigits);
    }

    /// <summary>Asset path of a sandbox scene: 1 -> ".../MainLevels/Level001.unity".</summary>
    public static string GetSandboxScenePath(int sandboxNumber)
    {
        return SceneFolder + "/" + GetSandboxSceneName(sandboxNumber) + ".unity";
    }

    /// <summary>True when the scene is a campaign level scene.</summary>
    public static bool IsLevelScene(string sceneName)
    {
        return TryGetLevelNumber(sceneName, out _);
    }

    /// <summary>True when the scene is a sandbox test level.</summary>
    public static bool IsSandboxLevelScene(string sceneName)
    {
        return TryGetSandboxNumber(sceneName, out _);
    }

    /// <summary>
    /// True for anything the player actually plays a board in - campaign or
    /// sandbox. Use this for presentation (music, HUD); use
    /// <see cref="TryGetLevelNumber"/> for anything that writes progress.
    /// </summary>
    public static bool IsPlayableLevelScene(string sceneName)
    {
        return IsLevelScene(sceneName) || IsSandboxLevelScene(sceneName);
    }

    /// <summary>
    /// Extracts the pure-digit suffix after the prefix. int.TryParse would accept
    /// " 7", "+7" and "-7"; a scene name is not a user-entered number, so anything
    /// but plain digits is a different scene ("LevelSelect", "Level2-test").
    /// </summary>
    private static bool TryGetDigits(string sceneName, out string suffix)
    {
        suffix = null;

        if (string.IsNullOrEmpty(sceneName) ||
            !sceneName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string candidate = sceneName.Substring(Prefix.Length);
        if (candidate.Length == 0)
        {
            return false;
        }

        foreach (char character in candidate)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        suffix = candidate;
        return true;
    }

    /// <summary>
    /// True for a suffix padded wider than the campaign uses - "001", not "100".
    /// The leading zero is what marks it: a three-digit number with no leading
    /// zero is just a large level number.
    /// </summary>
    private static bool IsOverPadded(string suffix)
    {
        return suffix.Length > PaddedDigits && suffix[0] == '0';
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
