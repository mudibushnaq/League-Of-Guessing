#if(FACEBOOK_ENABLED)
using UnityEngine;
using Facebook.Unity;

namespace ObscureGames
{
    public class FacebookManager : MonoBehaviour
    {
        public static FacebookManager _instance;

        private void Awake()
        {
            if (!FB.IsInitialized)

                FB.Init(() =>
                {
                    if (FB.IsInitialized)

                        FB.ActivateApp();
                    else
                        Debug.Log("Failed to Initialize the Facebook SDK");
                }, (bool isGameShown) =>
                {
                    if (!isGameShown)
                        Time.timeScale = 0;
                    else
                        Time.timeScale = 1;
                });
            else
                FB.ActivateApp();

            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
                Destroy(gameObject);
        }
    }
}
#endif
