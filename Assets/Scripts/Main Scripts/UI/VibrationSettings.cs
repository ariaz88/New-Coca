using UnityEngine;
using UnityEngine.UI;


public class VibrationSettings : MonoBehaviour
{
    public static VibrationSettings Instance;

    public bool IsVibrationEnabled = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            IsVibrationEnabled = false;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ToggleVibration(bool isEnabled)
    {
        IsVibrationEnabled = isEnabled;
        Debug.Log($"Vibration is now {(IsVibrationEnabled ? "Enabled" : "Disabled")}");
    }
}

//public class VibrationSettings : MonoBehaviour
//{
//    [SerializeField] private Toggle vibrationToggle;
//    [SerializeField] private GameObject vibrationPanel;

//    // Static property to check if vibration is enabled
//    public static bool IsVibrationEnabled { get;  set; } = true;

//    private void Start()
//    {
//        // Initialize the toggle state
//        vibrationToggle.isOn = IsVibrationEnabled;

//        // Listen for toggle changes
//        vibrationToggle.onValueChanged.AddListener(OnVibrationToggleChanged);
//    }

//    private void OnDestroy()
//    {
//        // Clean up the listener to avoid memory leaks
//        vibrationToggle.onValueChanged.RemoveListener(OnVibrationToggleChanged);
//    }

//    private void OnVibrationToggleChanged(bool isEnabled)
//    {
//        IsVibrationEnabled = isEnabled;
//        Debug.Log($"Vibration Enabled: {IsVibrationEnabled}");
//        SetActivation();
//    }
//    private void SetActivation()
//    {
//        if (IsVibrationEnabled == true)
//        {
//            vibrationPanel.SetActive(true);
//        }
//        else if(IsVibrationEnabled == false)
//        {
//                vibrationPanel.SetActive(false);       
//        }
//    }
//}
