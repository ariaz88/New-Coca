using UnityEngine;
using UnityEngine.UI;


public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    private bool isMuted = false; // For sound effects
    private bool isMusicMuted = false; // For music

    [Header("Audio Sources")]
    public AudioSource soundEffectSource; // Reusable AudioSource for SFX
    public AudioSource musicSource;       // AudioSource for music

    //[Header("UI References")]
    //public Image soundIcon;
    //public Sprite soundUnmuteSprite;
    //public Sprite soundMuteSprite;

    //public Image musicIcon;
    //public Sprite musicUnmuteSprite;
    //public Sprite musicMuteSprite;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    private void Start()
    {
        isMuted = false; 
        isMusicMuted = false;
    }

    // Play sound effects using the reusable AudioSource
    public void PlaySound(AudioClip audioClip, float volume = 1f)
    {
        if (!isMuted && soundEffectSource != null)
        {
            soundEffectSource.PlayOneShot(audioClip, volume);
        }
    }

    // Play music
    public void PlayMusic(bool loop = true)
    {
        if (musicSource != null)
        {
            musicSource.loop = loop;
            if (!isMusicMuted)
            {
                musicSource.Play();
            }
        }
    }

    // Mute/unmute sound effects
    public void ToggleSoundMute(bool toggleSound)
    {
        isMuted = !toggleSound;
        //isMuted = !isMuted;

        // Update the AudioSource mute property
        if (soundEffectSource != null)
        {
            soundEffectSource.mute = isMuted;
            Debug.Log($"Sound is now {(isMuted ? "Disabled " : "Enabled")}");

        }

        //// Update the icon
        //if (soundIcon != null)
        //{
        //    soundIcon.sprite = isMuted ? soundMuteSprite : soundUnmuteSprite;
        //}
    }

    // Mute/unmute music
    public void ToggleMusicMute(bool toggleMusic)
    {
        isMusicMuted = !toggleMusic;
        //isMusicMuted = !isMusicMuted;

        // Update the AudioSource mute property
        if (musicSource != null)
        {
            musicSource.mute = isMusicMuted;
            Debug.Log($"Music is now {(isMusicMuted ? "Disabled " : "Enabled")}");
            PlayMusic();
        }

        // Update the icon
        //    if (musicIcon != null)
        //    {
        //        musicIcon.sprite = isMusicMuted ? musicMuteSprite : musicUnmuteSprite;
        //    }
        //}
    }
}


//public class SoundManager : MonoBehaviour
//{
//    public static SoundManager instance;

//    private bool isMuted = false; // For sound effects
//    private bool isMusicMuted = false; // For music
//    private AudioSource musicSource;

//    [Header("UI References")]
//    public Image soundIcon; // Icon for sound effects
//    public Sprite soundUnmuteSprite; // Unmute icon
//    public Sprite soundMuteSprite; // Mute icon

//    public Image musicIcon; // Icon for music
//    public Sprite musicUnmuteSprite; // Unmute icon
//    public Sprite musicMuteSprite; // Mute icon

//    private void Awake()
//    {
//        if (instance == null)
//        {
//            instance = this;
//        }
//    }

//    // Play sound effects
//    public void PlaySound(AudioClip audioClip, Vector3 position, float volume = 1f)
//    {
//        if (!isMuted) // Play only if sound is not muted
//        {
//            AudioSource.PlayClipAtPoint(audioClip, position, volume);
//        }
//    }

//    // Play game music
//    public void PlayMusic(AudioSource musicSource, bool loop = true)
//    {
//        this.musicSource = musicSource;
//        musicSource.loop = loop;
//        if (!isMusicMuted)
//        {
//            musicSource.Play();
//        }
//    }

//    // Mute or unmute all sounds
//    public void ToggleSoundMute()
//    {
//        isMuted = !isMuted;

//        // Update the icon
//        if (soundIcon != null)
//        {
//            soundIcon.sprite = isMuted ? soundMuteSprite : soundUnmuteSprite;
//        }
//    }

//    // Mute or unmute music
//    public void ToggleMusicMute()
//    {
//        isMusicMuted = !isMusicMuted;

//        // Update the icon
//        if (musicIcon != null)
//        {
//            musicIcon.sprite = isMusicMuted ? musicMuteSprite : musicUnmuteSprite;
//        }

//        if (musicSource != null)
//        {
//            if (isMusicMuted)
//            {
//                musicSource.Pause();
//            }
//            else
//            {
//                musicSource.Play();
//            }
//        }
//    }
//}
