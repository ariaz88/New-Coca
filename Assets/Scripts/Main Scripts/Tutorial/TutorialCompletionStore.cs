using UnityEngine;

/// <summary>
/// Small persistence boundary used by TutorialManager. A different save backend
/// can replace this implementation without changing any feature Tutorial.
/// </summary>
public interface ITutorialCompletionStore
{
    bool IsCompleted(string tutorialId);
    void MarkCompleted(string tutorialId);
    void Reset(string tutorialId);
}

/// <summary>PlayerPrefs-backed completion storage used by the current project.</summary>
public sealed class PlayerPrefsTutorialCompletionStore : ITutorialCompletionStore
{
    private const string KeyPrefix = "Tutorial.Completed.";

    public bool IsCompleted(string tutorialId)
    {
        return !string.IsNullOrWhiteSpace(tutorialId) &&
               PlayerPrefs.GetInt(BuildKey(tutorialId), 0) == 1;
    }

    public void MarkCompleted(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
        {
            return;
        }

        PlayerPrefs.SetInt(BuildKey(tutorialId), 1);
        PlayerPrefs.Save();
    }

    public void Reset(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
        {
            return;
        }

        PlayerPrefs.DeleteKey(BuildKey(tutorialId));
        PlayerPrefs.Save();
    }

    private static string BuildKey(string tutorialId)
    {
        return KeyPrefix + tutorialId.Trim();
    }
}
