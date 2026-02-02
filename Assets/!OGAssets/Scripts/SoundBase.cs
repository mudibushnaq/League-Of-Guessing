using System.Collections;
using System.Collections.Generic;
using UnityEngine;
    [RequireComponent(typeof(AudioSource))]
    public class SoundBase : MonoBehaviour
    {
        public static SoundBase instance;

        [Header("SOUND COMPONENTS")]
        public AudioClip GeneralButtonClick;
        public AudioClip PlayButtonClick;
        public AudioClip ShopButtonClick;

        public AudioClip TypeSound;
        public AudioClip button_locked;
        public AudioClip WinSound;
        public AudioClip TypingSound;

        public AudioClip[] winSound;
        public AudioClip[] loseSound;
        public AudioClip unlockSound;

        public AudioClip coinWinRVSound;

        public AudioClip pingSound;

        internal AudioSource source;

        internal GameController gameController;

        // Start is called before the first frame update
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (instance == null)
            instance = this;
            else
                Destroy(gameObject);

            source = this.GetComponent<AudioSource>();
            //rubberBandSource = this.GetComponentInChildren<AudioSource>();

            gameController = GameObject.FindObjectOfType<GameController>();

            
        }

        public void PlayUnlockSound()
        {
            source.PlayOneShot(unlockSound);
        }

    public void PlayTypingSound()
    {
        source.PlayOneShot(TypingSound);
    }

    public void PlaybuttonLocked()
        {
            source.PlayOneShot(button_locked);
        }

    public void PlayWinSound()
    {
        source.PlayOneShot(WinSound);
    }

    public void SoundPlay( AudioClip audioClip )
        {
            if (audioClip != null)
            {
                if (AudioListener.volume > 0) source.PlayOneShot(audioClip);
                else
                    Debug.Log("Sound Disabled");
            }
            else
            {
                Debug.Log("Please Put an audio cllip");
            }
        }
    }