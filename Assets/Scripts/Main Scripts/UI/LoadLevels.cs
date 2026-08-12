using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class LoadLevels : MonoBehaviour
{
   public void LoadLevel()
   {
    if (LevelNaming.TryResolveLoadableSceneName(1, out string sceneName))
    {
        SceneManager.LoadScene(sceneName);
    }
    else
    {
        Debug.LogWarning("Level 1 is not configured in Build Settings.");
    }
   }

}
