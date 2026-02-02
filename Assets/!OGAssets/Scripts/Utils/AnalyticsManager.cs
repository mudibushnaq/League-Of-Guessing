using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnalyticsManager : Singleton<AnalyticsManager>
{
    bool initialized = false;


    public void Initialize()
    {
        if (!initialized)
        {
            initialized = true;
            /*FB.Init(this.OnInitComplete, this.OnHideUnity);
            Debug.Log("FB.Init() called with " + FB.AppId);*/
        }
    }

    private void OnInitComplete()
    {
        Debug.Log("Success - Check log for details");
        Debug.Log("Success Response: OnInitComplete Called\n");
        /*string logMessage = string.Format(
            "OnInitCompleteCalled IsLoggedIn='{0}' IsInitialized='{1}'",
            FB.IsLoggedIn,
            FB.IsInitialized);
        Debug.Log(logMessage);
        if (AccessToken.CurrentAccessToken != null)
        {
            Debug.Log(AccessToken.CurrentAccessToken.ToString());
        }*/
    }

    private void OnHideUnity(bool isGameShown)
    {
        Debug.Log("Success - Check log for details");
        Debug.Log(string.Format("Success Response: OnHideUnity Called {0}\n", isGameShown));
        Debug.Log("Is game shown: " + isGameShown);
    }

    public void logLoginEvent(string username, int icon, int songNum)
    {
        /*FB.LogAppEvent("Login", null, new Dictionary<string, object>()
        {
            {"user_name",username },
            {"selected_icon",icon },
            {"current_song",songNum}
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/"+ FB.AppId);*/


    }

    public void logCurrentLangEvent(string lang)
    {
        /*FB.LogAppEvent("Language", null, new Dictionary<string, object>()
        {
            {"user_lang",lang }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logShopWindowEvent(int keys,int points)
    {
        /*FB.LogAppEvent("Open Shop", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_points",points }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logSoundSettingsEvent(int sound)
    {
        bool SoundEnabled;
        if (sound == 0)
        {
            SoundEnabled = false;
        }
        else
        {
            SoundEnabled = true;
        }
        /*FB.LogAppEvent("Sound Settings", null, new Dictionary<string, object>()
        {
            {"user_sound",SoundEnabled }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logIconBuyEvent(int keys,int ip,int iconID)
    {
        /*FB.LogAppEvent("Buy Icon", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"icon_id",iconID }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logPlayEvent(int keys, int ip)
    {
        /*FB.LogAppEvent("Play", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logUnlockEvent(int keys, int ip,string skill)
    {
        /*FB.LogAppEvent("Unlock Skill", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"unlocked_skill",skill }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logBackButtonEvent(int keys,int ip, int currentLevel, string champion)
    {
        /*FB.LogAppEvent("Back Button", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"user_level",currentLevel },
            {"level_champion",champion }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logSkipButtonEvent(int keys, int ip, int currentLevel, string champion)
    {
        /*FB.LogAppEvent("Skip Button", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"user_level",currentLevel },
            {"level_champion",champion }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logOnDoneWinEvent(int keys, int ip, int currentLevel, string champion)
    {
        /*FB.LogAppEvent("Done Win", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"user_level",currentLevel },
            {"level_champion",champion }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logOnDoneLoseEvent(int keys, int ip, int currentLevel, string champion)
    {
        /*FB.LogAppEvent("Done Lose", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip },
            {"user_level",currentLevel },
            {"level_champion",champion }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logEventClickEvent(int keys,int ip)
    {
        /*FB.LogAppEvent("Watch AD", null, new Dictionary<string, object>()
        {
            {"user_keys",keys },
            {"user_ip",ip }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }

    public void logEventBuyEvent(int ip,int keys)
    {
        /*FB.LogAppEvent("Buy Key", null, new Dictionary<string, object>()
        {
            {"user_ip",ip },
            {"keys_bought",keys }
        });
        Debug.Log("You may see results showing up at https://www.facebook.com/analytics/" + FB.AppId);*/
    }
}
