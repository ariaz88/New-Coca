using UnityEngine;
using UnityEngine.UI;

//using CandyCoded.HapticFeedback;

public class ButtonVibrationHandler : MonoBehaviour
{
    [SerializeField] private Button defaultVibrationButton;
    [SerializeField] private Button lightVibrationButton;
    [SerializeField] private Button mediumVibrationButton;
    [SerializeField] private Button heavyVibrationButton;

    private void OnEnable()
    {
        defaultVibrationButton.onClick.AddListener(DefaultVibration);
        lightVibrationButton.onClick.AddListener(LightVibration);
        mediumVibrationButton.onClick.AddListener(MediumVibration);
        heavyVibrationButton.onClick.AddListener(HeavyVibration);
    }

    private void OnDisable()
    {
        defaultVibrationButton.onClick.RemoveListener(DefaultVibration);
        lightVibrationButton.onClick.RemoveListener(LightVibration);
        mediumVibrationButton.onClick.RemoveListener(MediumVibration);
        heavyVibrationButton.onClick.RemoveListener(HeavyVibration);
    }

    private void DefaultVibration()
    {
        if (VibrationSettings.Instance.IsVibrationEnabled)
        {
            Debug.Log("Default Vibration Performed");
            Handheld.Vibrate();
        }
        else
        {
            Debug.Log("Vibration Disabled");
        }
    }

    private void LightVibration()
    {
        if (VibrationSettings.Instance.IsVibrationEnabled)
        {
            Debug.Log("Light Vibration Performed!");
            //HapticFeedback.LightFeedback();
        }
        else
        {
            Debug.Log("Vibration Disabled");
        }
    }

    private void MediumVibration()
    {
        if (VibrationSettings.Instance.IsVibrationEnabled)
        {
            Debug.Log("Medium Vibration Performed!");
            //HapticFeedback.MediumFeedback();
        }
        else
        {
            Debug.Log("Vibration Disabled");
        }
    }

    private void HeavyVibration()
    {
        if (VibrationSettings.Instance.IsVibrationEnabled)
        {
            Debug.Log("Heavy Vibration Performed!");
            //HapticFeedback.HeavyFeedback();
        }
        else
        {
            Debug.Log("Vibration Disabled");
        }
    }
}
