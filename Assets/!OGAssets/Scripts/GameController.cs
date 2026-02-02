using I2.Loc;
using ObscureGames;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class GameController : MonoBehaviour {

    [Header("CANVASES")]
    public GameObject MenuCanvas;
    public GameObject SettingsCanvas;
    public GameObject NotificationCanvas;
    public GameObject ShopCanvas;
    public GameObject GameCanvas;
    public GameObject GameTutorialCanvas;
    public GameObject FinishCanvas;
    public GameObject OnBoardingCanvas;
    public GameObject OnBoardingLoginWindow;
    public GameObject OnBoardingIconWindow;
    public GameObject RVShopCanvas;

    [Header("PLAYER")]
    public TextMeshProUGUI MenuIPText;
    public TextMeshProUGUI MenuKeysText;
    public TextMeshProUGUI GameKeysText;
    public TextMeshProUGUI GameIPText;
    public TextMeshProUGUI PlayerNameText;
    public TextMeshProUGUI GamePlayerNameText;
    public TextMeshProUGUI currentLevelText;

    public Image playerIcon;

    internal int FirstTimeLogin = 0;
    internal int playerIP;
    internal int playerKeys;
    public int FirstLoginIP;
    public int FirstLoginKeys;


    //public GameObject PlayButton;
    //public GameObject LoadingPlayIcon;

    [Header("GAME")]
    public Transform ControlsHolder;

    public int NumberOfTiles;
    public int keysToSkip = 1;
    public int SkillUnlockPrice = 1;
    public int CurrentLevel = 1;
    public int ChampionIndex = 0;
    public int TotalUnlockedSkills = 0;
    public int BoosterCounter = 2;

    bool gameStarted = false;
    public GameObject unlockEffect;
    public GameObject BoosterIcon;
    KillSeriesManager killSeriesManager;
    public TMP_InputField PlayerAnswerText;
    public Image CurrentLevelImage;

    public string[] CurrentAnswer = new string[10];

    public static int MyFirstUp = 0;

    public CorrectAnswerController WinWindow;

    public bool isGameFinished;
    public bool RemoveAll = false;
    public bool SkipEnabled = false;
    public bool isIPBooster = false;

    public Button[] GameButtons;

    public GameObject[] Skills;

    [Header("PLAYER PREFS")]
    public string ref_playerIP = "CurrentIP";
    public string ref_playerKeys = "CurrentKeys";
    public string ref_skill = "Skill_";
    public string ref_answersFile = "MyData";
    public string ref_currentLevel = "CurrentLevel";
    public string ref_championIndex = "ChampionIndex_";
    public string ref_firstGame = "FirstGame";
    public string ref_championsCount = "ChampionsCount";
    public string ref_isFirstUnlock = "isFirstUnlock";
    public string ref_totalUnlockedSkills = "TotalUnlockedSkills";
    public string ref_isGameFinished = "isGameFinished";
    public string ref_isOnBoarding = "isOnBoarding";
    public string ref_PlayerName = "PlayerName";
    string EnglishName, PortugueseName, PolishName, SpanishName, TurkishName, HungarianName, GreekName, KoreanName, ChineseName, JapaneseName;

    [Header("ADS VARS")]
    public int InterstitialCount;
    public int showInterstitialAfter;
    public int SkipCount;
    public int showSkipAfter;
    public int rewardCountMax = 30;
    public float RewardCount;
    public int randomIP;
    public float randomTimer;

    public GameObject[] Boosters;

    public int[] shopIPRVArray;
    public float[] timerGivenArray;
    public bool isShopRVOpen = false;

    public bool isIPBoosterPowerUp = false;
    public bool isRVShopPowerUp = false;
    public bool isFreeKeysPowerUp = false;
    public bool isFreeIPPowerUp = false;
    public bool isChampSkip = false;

    public TextMeshProUGUI shopRVIPText;
    public TextMeshProUGUI shopRVKeysText;
    public TextMeshProUGUI shopRVTimerText;

    [Header("ONBOARDING")]
    public TMP_InputField usernameField;
    public TextMeshProUGUI LangText;
    public TextMeshProUGUI LanguageBtnText;

    // Use this for initialization
    void Awake () {
        
        Application.targetFrameRate = 60;
        InitPlayer();
    }
	
    public void InitPlayer(){
        if(RemoveAll == true)
        {
            PlayerPrefs.DeleteAll();
        }

        //Debug.Log(PlayerPrefs.HasKey(ref_isOnBoarding));

        if (!PlayerPrefs.HasKey(ref_isOnBoarding)){
            //Debug.Log("Start Onboarding");
            OnBoardingCanvas.SetActive(true);
            //checkLang();
        }
        else{
            PlayerNameText.SetText(PlayerPrefs.GetString(ref_PlayerName));
            GamePlayerNameText.SetText(PlayerNameText.text);
            playerIP = PlayerPrefs.GetInt(ref_playerIP, FirstLoginIP);
            playerKeys = PlayerPrefs.GetInt(ref_playerKeys, FirstLoginKeys);
            killSeriesManager = GameObject.FindObjectOfType<KillSeriesManager>();
            ChangeKeys(0); ChangeIP(0);
            ShopCanvas.SetActive(true);
            OnBoardingCanvas.SetActive(false);
            MenuCanvas.SetActive(true);
        }
    }

    private void Start()
    {
        //RefreshPoints();
    }

    public void LateUpdate()
    {
        if(gameStarted == true){
            RewardCount += Time.deltaTime;
        }
        if(isShopRVOpen == true)
        {
            randomTimer -= Time.deltaTime;
            //shopRVTimerText.SetText(.ToString());
            TimeSpan timer = TimeSpan.FromSeconds((double)(new decimal(randomTimer)));
            shopRVTimerText.SetText(timer.ToString("ss") + " Seconds");
            if (randomTimer <= 0)
            {
                RVShopCanvas.SetActive(false);
                randomTimer = 0;
            }
        }
    }

    public void LoadGame()
    {
        gameStarted = true;
        if (PlayerPrefs.GetInt(ref_firstGame) == 0)
        {
            int temp, iRand;
            int[] arr = new int[NumberOfTiles];
            for (int i = 0; i < NumberOfTiles; i++)
            {
                arr[i] = i;
            }

            // Rest From bundles
            for (int i = 0; i < NumberOfTiles; i++)
            {
                iRand = UnityEngine.Random.Range(0, NumberOfTiles);
                temp = arr[i];
                arr[i] = arr[iRand];
                arr[iRand] = temp;
            }

            for (int i = 0; i < NumberOfTiles; i++)
            {
                PlayerPrefs.SetInt(ref_championIndex + i, arr[i]);
            }
            PlayerPrefs.SetInt(ref_firstGame, NumberOfTiles);
            PlayerPrefs.SetInt(ref_championsCount, NumberOfTiles);
        }
        else
        {
            if (!PlayerPrefs.HasKey(ref_championsCount) || PlayerPrefs.GetInt(ref_championsCount) < NumberOfTiles)
            {
                PlayerPrefs.SetInt(ref_championIndex + (NumberOfTiles - 1), (NumberOfTiles - 1));
                PlayerPrefs.SetInt(ref_championsCount, NumberOfTiles);
            }
        }

        CurrentLevel = PlayerPrefs.GetInt(ref_currentLevel,1);
        ChampionIndex = PlayerPrefs.GetInt(ref_championIndex + CurrentLevel);
        currentLevelText.SetText("LEVEL " + CurrentLevel);
        //GetUnlockedSkill();
        LoadLevel(ChampionIndex, false);

        if (CurrentLevel > NumberOfTiles)
        {
            Debug.Log("Game Is Done");
        }
    }

    public void LoadLevel(int levelIndex,bool resetSkills)
    {
        if (resetSkills)
        {
            ResetUnlockedSkill();
            if (PlayerPrefs.GetInt(ref_isFirstUnlock) == 0)
            {
                //Debug.Log("Unlock Random Skill");
                var random = UnityEngine.Random.Range(0,Skills.Length);
                PlayerPrefs.SetInt(ref_isFirstUnlock,1);
                PlayerPrefs.SetInt(ref_skill+random,1);
                TotalUnlockedSkills++;
                PlayerPrefs.SetInt(ref_totalUnlockedSkills, TotalUnlockedSkills);
            }
        }
        else
        {
            if (PlayerPrefs.GetInt(ref_isFirstUnlock) == 0)
            {
                var random = UnityEngine.Random.Range(0, Skills.Length);
                PlayerPrefs.SetInt(ref_isFirstUnlock, 1);
                PlayerPrefs.SetInt(ref_skill + random, 1);
                //print("Unlock Skill " + ref_skill + random);
                TotalUnlockedSkills++;
                PlayerPrefs.SetInt(ref_totalUnlockedSkills, TotalUnlockedSkills);
                //print("dont enter");
            }   
        }

        GetUnlockedSkill();
        CurrentAnswer = DataReader.GetLineStr(DataReader.GetLine(ref_answersFile, levelIndex));

        if (isChampSkillsSpriteAvailable(CurrentAnswer[0]))
        {
            //loadSkillImages(MyQues[0], MyPictureImage, AnswersLoadingImg);

            CurrentLevelImage.sprite = (Sprite)Resources.Load("ChampSkills_NEW/" + CurrentAnswer[0], typeof(Sprite));

            EnglishName = CurrentAnswer[1];
            PortugueseName = CurrentAnswer[2];
            PolishName = CurrentAnswer[3];
            SpanishName = CurrentAnswer[4];
            TurkishName = CurrentAnswer[5];
            HungarianName = CurrentAnswer[6];
            GreekName = CurrentAnswer[7];
            KoreanName = CurrentAnswer[8];
            ChineseName = CurrentAnswer[9];
            JapaneseName = CurrentAnswer[10];
        }
        else
        {
            //Debug.Log ("Level not ready");
            PlayerPrefs.SetInt("needDownloadNotify", 1);
            SceneManager.LoadScene("MainMenu");
        }

        //EventsManager._instance.LogLevelStartEvent(Int32.Parse(CurrentAnswer[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), CurrentAnswer[1]);
    }

    public bool isChampSkillsSpriteAvailable(string champIndexStr)
    {
        return true;
    }

    public void RemoveButton()
    {
        PlayerAnswerText.text = "";
    }

    public void BackButton()
    {
        gameStarted = false;
        GameCanvas.gameObject.SetActive(false);
        MenuCanvas.gameObject.SetActive(true);
    }

    IEnumerator loadPlayScene(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        SceneManager.LoadScene("GameScene");
    }

    public void RefreshPoints(){
        MenuKeysText.SetText(playerKeys.ToString());
        GameKeysText.SetText(playerKeys.ToString());
        MenuIPText.SetText(playerIP.ToString());
        GameIPText.SetText(playerIP.ToString());
    }

    public void ChangeKeys(int amount)
    {
        
        playerKeys = PlayerPrefs.GetInt(ref_playerKeys, FirstLoginKeys);
        Debug.Log("playerKeys" + playerKeys);
        playerKeys += amount;
        MenuKeysText.SetText(playerKeys.ToString());
        GameKeysText.SetText(playerKeys.ToString());
        PlayerPrefs.SetInt(ref_playerKeys, playerKeys);
    }

    public void ChangeIP(int amount)
    {
        playerIP = PlayerPrefs.GetInt(ref_playerIP, FirstLoginIP);
        Debug.Log("playerIP" + playerIP);
        playerIP += amount;
        MenuIPText.SetText(playerIP.ToString());
        GameIPText.SetText(playerIP.ToString());
        PlayerPrefs.SetInt(ref_playerIP, playerIP);
    }

    public void HideSkill(GameObject skillObject)
    {
        playerKeys = PlayerPrefs.GetInt(ref_playerKeys);
        //Debug.Log("playerKeys" + playerKeys);
        if (playerKeys >= SkillUnlockPrice)
        {
            var skill = skillObject.GetComponent<CanvasGroup>();
            skill.interactable = false;
            //skill.GetComponent<Animator>().Play("Unlocker");
            skill.alpha = 0;
            skillObject.transform.GetChild(0).GetChild(0).gameObject.SetActive(false);
            ChangeKeys(-SkillUnlockPrice);
            PlayerPrefs.SetInt(ref_skill + skillObject.transform.GetSiblingIndex(), 1);
            Debug.Log("Skill: " + ref_skill + skillObject.transform.GetSiblingIndex());
            //EventsManager._instance.LogSkillUnlockEvent(Int32.Parse(CurrentAnswer[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), CurrentAnswer[1], skillObject.transform.name);
            SoundBase.instance.PlayUnlockSound();
            TotalUnlockedSkills++;
            PlayerPrefs.SetInt(ref_totalUnlockedSkills, TotalUnlockedSkills);
            Instantiate(unlockEffect,skillObject.transform.position,Quaternion.identity);
            //Save UnlockedSkill
            //Debug.Log(skillObject.transform.GetSiblingIndex() + 1);
        }
        else
        {
            //Debug.Log("Not Enough Keys");
            SoundBase.instance.PlaybuttonLocked();
        }

    }


    public void GetUnlockedSkill()
    {
        TotalUnlockedSkills = PlayerPrefs.GetInt(ref_totalUnlockedSkills);
        Debug.Log("TotalUnlockedSkills" + TotalUnlockedSkills);
        for (int i = 0; i < Skills.Length; i++)
        {
            print(PlayerPrefs.GetInt(ref_skill + i));
            if (PlayerPrefs.GetInt(ref_skill+i) == 1)
            {
                
                var skill = Skills[i].GetComponent<CanvasGroup>();
                Skills[i].transform.GetChild(0).GetChild(0).gameObject.SetActive(false);
                skill.interactable = false;
                skill.alpha = 0;
                print("Unlock Skill " + i);
            }
            else
            {
                var skill = Skills[i].GetComponent<CanvasGroup>();
                skill.interactable = true;
                skill.alpha = 1;
            }
        }
    }

    public void ResetUnlockedSkill()
    {
        for (int i = 0; i < Skills.Length; i++)
        {
            PlayerPrefs.SetInt(ref_skill + i, 0);
        }
        TotalUnlockedSkills = 0;
        PlayerPrefs.SetInt(ref_totalUnlockedSkills, TotalUnlockedSkills);
    }

    public void DoneButton()
    {
        /*for (int i = 0; i < GameButtons.Length; i++)
        {
            GameButtons[i].interactable = false;
        }*/

        /*InterstitialCount++;
        if(InterstitialCount == 4)
        {
            //IronSourceManager.instance.PrepareInterstitial();
            //Debug.Log("PREPARE INTERSTITIAL");
        }
        if (InterstitialCount >= 5)
        {
            InterstitialCount = 0;
            if (UnityEngine.Random.value <= 0.5f)
            {
                //IronSourceManager.instance.ShowInterstitial();
                //Debug.Log("SHOW INTERSTITIAL");
            }
        }*/

        SoundBase.instance.PlayTypingSound();

        if (PlayerAnswerText.text.ToUpper().Equals(EnglishName) || PlayerAnswerText.text.ToUpper().Equals(PortugueseName) || PlayerAnswerText.text.ToUpper().Equals(PolishName)
            || PlayerAnswerText.text.ToUpper().Equals(SpanishName) || PlayerAnswerText.text.ToUpper().Equals(TurkishName) || PlayerAnswerText.text.ToUpper().Equals(HungarianName)
            || PlayerAnswerText.text.Equals(GreekName, StringComparison.CurrentCultureIgnoreCase) || PlayerAnswerText.text.ToUpper().Equals(KoreanName) || PlayerAnswerText.text.ToUpper().Equals(ChineseName)
            || PlayerAnswerText.text.ToUpper().Equals(JapaneseName))
        {
            //int seriesAddition = killSeriesManager.Answered(() => { });
            //int seriesAddition = killSeriesManager.Answered();
            //EventsManager._instance.LogLevelFinishEvent(CurrentLevel.ToString());
            //EventsManager._instance.LogLevelWinEvent(Int32.Parse(CurrentAnswer[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), CurrentAnswer[1]);
            PlayWinAnimation();
            //Play Win Effect
            if (isIPBooster)
            {
                if(BoosterCounter > 0)
                {
                    int IPInc = ((4 - TotalUnlockedSkills)) * 2;
                    ChangeIP(IPInc);
                    BoosterIcon.SetActive(true);
                    BoosterCounter--;
                }
                else
                {
                    BoosterIcon.SetActive(false);
                }
                
            }
            else
            {
                int IPInc = ((4 - TotalUnlockedSkills));
                ChangeIP(IPInc);
                BoosterIcon.SetActive(false);
            }
            
            //print("Right Answer");
            PlayerAnswerText.text = "";
            //HideSkip();
            SoundBase.instance.PlayWinSound();
            StartCoroutine(MoveToNextLevel(2.0F));
            //OpenShopRV();
            /*if (RewardCount >= rewardCountMax)
            {
                RewardCount = 0;
                ShuffleIP();
                if (randomIP <= PlayerPrefs.GetInt(ref_playerIP))
                {
                    OpenShopRV();
                }
            }*/
        }
        else
        {
            Debug.Log("Wrong");
            
        }
        
        /*else
        {
            //killSeriesManager.WrongAnswer(() => { });
            //killSeriesManager.WrongAnswer(action);
            //EventsManager._instance.LogLevelFailEvent(Int32.Parse(CurrentAnswer[0]), PlayerPrefs.GetInt("CurrentKeys"), PlayerPrefs.GetInt("CurrentIP"), CurrentAnswer[1]);
            //PlayLoseAnimation();

            //print("Wrong Answer");
            for (int i = 0; i < GameButtons.Length; i++)
            {
                GameButtons[i].interactable = true;
            }
            if(RewardCount >= rewardCountMax)
            {
                RewardCount = 0;
                if (UnityEngine.Random.value <= 0.5f)
                {
                    var random = UnityEngine.Random.Range(0, Boosters.Length);
                    Instantiate(Boosters[random], new Vector3(0, 0, 0), Quaternion.identity);
                }
            }
            SkipCount++;
            if (SkipCount >= showSkipAfter && SkipEnabled == false)
            {
                SkipCount = 0;
                if (UnityEngine.Random.value <= 0.4f)
                {
                    //Debug.Log("Allow Skip");
                    GameButtons[1].GetComponent<CanvasGroup>().alpha = 1;
                    SkipEnabled = true;
                    
                }
            }

        }*/
    }

    void PlayWinAnimation()
    {
        WinWindow.PlayAnimation_New(1);
    }

    void PlayLoseAnimation()
    {
        WinWindow.PlayWAnswerAnimation_new();
    }

    public void HideSkip()
    {
        GameButtons[1].GetComponent<CanvasGroup>().alpha = 0;
        SkipEnabled = false;
    }

    public void Skip()
    {
        bool canReplace = false;
        int lvlIndex = CurrentLevel - 1;

        int lvlChampIndex = PlayerPrefs.GetInt(ref_championIndex + lvlIndex);
        int replacementLvl = 0;

        canReplace = true;
        replacementLvl = UnityEngine.Random.Range(CurrentLevel, NumberOfTiles);

        if (canReplace){

            int replacemenChampIndex = PlayerPrefs.GetInt(ref_championIndex + replacementLvl);

            //PlayerPrefs.SetInt(ref_championIndex + lvlIndex, replacemenChampIndex);
            PlayerPrefs.SetInt(ref_championIndex + replacementLvl, lvlChampIndex);
                
            PlayerPrefs.SetInt(ref_isFirstUnlock, 0);
            ChampionIndex = PlayerPrefs.GetInt(ref_championIndex + replacementLvl);
            LoadLevel(replacemenChampIndex, true);
            ShopManager.instance.ShowTransiantNotification("SKIP_MSG_TEXT");
        }
        else
        {
            ShopManager.instance.ShowTransiantNotification("SKIP_DOWNLOAD_NEEDED_ERR");
        }
    }

    IEnumerator MoveToNextLevel(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        CurrentLevel++;
        ChampionIndex = PlayerPrefs.GetInt(ref_championIndex + CurrentLevel.ToString());
        PlayerPrefs.SetInt(ref_currentLevel, CurrentLevel);
        PlayerPrefs.SetInt(ref_isFirstUnlock,0);
        CheckGameFinished();
        currentLevelText.SetText("LEVEL " + CurrentLevel);
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < GameButtons.Length; i++)
        {
            GameButtons[i].interactable = true;
        }
        //PlayerPrefs.SetInt("PriceToUnlock", 0);
    }

    private void CheckGameFinished()
    {
        if ( CurrentLevel > NumberOfTiles)
        {
            isGameFinished = true;
            PlayerPrefs.SetInt(ref_isGameFinished,1);
            //print("Game Done");
        }
        else
        {
            LoadLevel(ChampionIndex, true);
        }
        if (CurrentLevel == NumberOfTiles - 1){
            DisableSkip();
        }

    }

    public void ShuffleIP()
    {
        randomIP = shopIPRVArray[UnityEngine.Random.Range(0, shopIPRVArray.Length)];
        for (int i = 0; i < shopIPRVArray.Length; i++)
        {
            if (shopIPRVArray[i] > PlayerPrefs.GetInt(ref_playerIP))
            {
                randomIP = shopIPRVArray[UnityEngine.Random.Range(0, shopIPRVArray.Length)];
            }
        }
        randomTimer = timerGivenArray[UnityEngine.Random.Range(0, timerGivenArray.Length)];
    }

    public void OpenShopRV()
    {
        isShopRVOpen = true;
        //if(isShopRVOpen == true)
        //{
            shopRVIPText.SetText("-"+randomIP);
            shopRVKeysText.SetText("+"+ randomIP/5);
        //}
        RVShopCanvas.SetActive(true);
    }

    public void CloseShopRV()
    {
        RVShopCanvas.SetActive(false);
    }

    public void AcceptDeal()
    {
        if( PlayerPrefs.GetInt(ref_playerIP) >= randomIP)
        {
            CloseShopRV();
            ChangeIP(-randomIP);
            ChangeKeys(randomIP / 5);
        }
        else
        {
            ShopManager.instance.ShowTransiantNotification_Text("Not Enough IP");
        }
        
    }

    private void DisableSkip(){
        GameButtons[1].interactable = false;
    }

    public void OnBoardingLogin()
    {
        if(usernameField.text.Length > 3)
        {
            //print("Login");
            PlayerPrefs.SetString(ref_PlayerName, usernameField.text);
            OnBoardingLoginWindow.SetActive(false);
            OnBoardingCanvas.SetActive(false);
            PlayerPrefs.SetInt(ref_isOnBoarding, 1);
            MenuCanvas.SetActive(true);

            ChangeKeys(0); ChangeIP(0);
            PlayerNameText.SetText(PlayerPrefs.GetString(ref_PlayerName));
            GamePlayerNameText.SetText(PlayerNameText.text);
            //OnBoardingIconWindow.SetActive(true);
        }
        else
        {
            //print("Name is not 3");
            ShopManager.instance.ShowTransiantNotification_Text("Please write a name more than 3 letters");
        }
    }

    public void SelectItem(int icon)
    {
        OnBoardingCanvas.SetActive(false);

        PlayerPrefs.SetInt("CurrentIcon",icon);
        PlayerPrefs.SetInt(ref_isOnBoarding,1);
        MenuCanvas.SetActive(true);

        ChangeKeys(0); ChangeIP(0);
        PlayerNameText.SetText(PlayerPrefs.GetString(ref_PlayerName));
        GamePlayerNameText.SetText(PlayerNameText.text);
        //playerIcon.sprite = 
    }
}
