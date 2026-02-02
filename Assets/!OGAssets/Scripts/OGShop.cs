using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ObscureGames
{
    public class OGShop : MonoBehaviour
    {

        public shopName ShopName;
        public shopType ShopType;

        public ShopGroup[] shopGroups;

        public GameController gameController;
        //public FBPlayerControls playerControls;
        public Image lockedIcon;

        //public Sprite emptyDot;
        //public Sprite selectedDot;

        public Text titleText;
        public Text RVIPButtonText;

        public int rewardedVideoIP = 30;
        public int groupIPPrice = 30;
        internal int scrollGroupIndex = 0;

        public Transform shopGroupObject;
        public Transform shopItemObject;
        public Transform shopGroupsObj;
        //public Transform shopDot;
        //public Transform shopPages;

        public RectTransform contentArea;

        internal float contentAreaSize = 0;
        internal float groupSize = 0;
        internal float scrollTarget;
        internal float currentScroll = 0;
        internal float previousScroll = 0;
        
        public ScrollRect scrollRect;
        public Scrollbar scrollBar;

        internal bool isUnlocking = false;
        internal bool isRV = false;

        public GameObject ipEffect;

        public AudioSource audioSource;

        public AudioClip audioUnlockSequence;
        public AudioClip audioUnlock;
        public AudioClip audioSelect;

        [System.Serializable]
        public class ShopGroup
        {
            internal Transform shopGroupTransform;

            public string title = "Trails";

            public string playerPrefs = "Trail";

            internal int selectIndex = 0;

            public int pricePerUnlock = 1;

            public int totalUnlocks = 1;

            public string currencyPlayerPrefs = "Gems";

            internal int currencyLeft = 0;

            public Sprite currencyIcon;

            internal int unlocked = 1;

            public ShopItem[] shopItems;
        }

        

        [System.Serializable]
        public class ShopItem
        {
            internal OGShopItem shopItemComponent;

            public int lockState = 0;
            public string name = "";
            public Sprite icon;
            //public ParticleSystem trail;
            public Sprite gameIcon;
            public string playerPrefs = "Item0001";
        }

        public enum shopName{
            Icons
        }

        public enum shopType{
            RandomUnlock
        }

        // Start is called before the first frame update
        void Awake()
        {
            CreateShop();
            CloseShop();
        }

        // Update is called once per frame
        void Update()
        {
            if (scrollBar && contentArea)
            {
                if (Input.GetButtonDown("Fire1")) currentScroll = scrollBar.value;

                if (Input.GetButton("Fire1")) scrollTarget = scrollBar.value;

                if (Input.GetButtonUp("Fire1"))
                {
                    previousScroll = currentScroll;

                    currentScroll = scrollBar.value;

                    if (currentScroll - previousScroll > 0.1f)
                    {
                        if (scrollGroupIndex < shopGroups.Length - 1) ChangeScrollGroup(1);
                    }
                    else if (currentScroll - previousScroll < -0.1f)
                    {
                        if (scrollGroupIndex > 0) ChangeScrollGroup(-1);
                    }

                    //scrollTarget = Mathf.Round(-scrollGroupIndex * (contentArea.localPosition.x / contentArea.sizeDelta.x)) / (shopGroups.Length - 1);
                    scrollTarget = 1.0f * scrollGroupIndex / (shopGroups.Length - 1);

                    scrollTarget = Mathf.Clamp(scrollTarget, 0, 1);


                    /*scrollTarget = Mathf.Round(-shopGroups.Length * (contentArea.localPosition.x / contentArea.sizeDelta.x)) / (shopGroups.Length - 1);

                    scrollTarget = Mathf.Clamp(scrollTarget, 0, 1);*/
                }



                scrollBar.value = Mathf.Lerp(scrollBar.value, scrollTarget, Time.deltaTime * 5);

            }

            if (isUnlocking == true) scrollRect.enabled = false;
            else scrollRect.enabled = true;

        }

        public void ChangeScrollGroup(int changeValue)
        {
            scrollGroupIndex += changeValue;

            scrollGroupIndex = Mathf.Clamp(scrollGroupIndex, 0, shopGroups.Length - 1);
        }

        public void SetContentArea()
        {
            contentAreaSize = 0;

            contentAreaSize += (shopGroups.Length - 1) * contentArea.GetComponentInChildren<HorizontalLayoutGroup>().spacing;

            contentAreaSize += shopGroups.Length * contentArea.GetComponentInChildren<HorizontalLayoutGroup>().transform.GetChild(0).GetComponent<RectTransform>().sizeDelta.x;

            contentArea.sizeDelta = new Vector2(contentAreaSize, contentArea.sizeDelta.y);

            groupSize = contentArea.GetComponentInChildren<HorizontalLayoutGroup>().spacing + contentArea.GetComponentInChildren<HorizontalLayoutGroup>().transform.GetChild(0).GetComponent<RectTransform>().sizeDelta.x;
        }

        public List<Transform> shopButtons = new List<Transform>();

        void CreateShop()
        {
            SetContentArea();
            //shopGroups[1].pricePerUnlock = groupBackgroundsPrice;
            //RVGemsButtonText.text = "+ "+ rewardedVideoGems + " gems";
            // Go through all the shop groups and create them
            for (int index = 0; index < shopGroups.Length; index++)
            {
                shopGroups[index].totalUnlocks = 0;

                // Set the title of the shop group
                titleText.text = shopGroups[index].title;

                var newShopGroup = Instantiate(shopGroupObject, shopGroupObject.parent);
                //var newDots = Instantiate(shopDot, shopPages.parent);
                //shopDot.GetComponent<Image>().enabled = true;
                //newDots.SetParent(shopPages);
                //newDots.localScale = new Vector3(1, 1, 1);
                shopGroups[index].shopGroupTransform = newShopGroup;

                for (int itemIndex = 0; itemIndex < shopGroups[index].shopItems.Length; itemIndex++)
                {
                    var newShopItem = Instantiate(shopItemObject, newShopGroup.Find("ItemsGrid"));

                    shopGroups[index].shopItems[itemIndex].shopItemComponent = newShopItem.gameObject.AddComponent<OGShopItem>();

                    //shopGroups[index].shopItems[itemIndex].shopItemComponent.button = newShopItem.GetComponent<Button>();

                    shopGroups[index].shopItems[itemIndex].lockState = PlayerPrefs.GetInt(shopGroups[index].shopItems[itemIndex].playerPrefs, shopGroups[index].shopItems[itemIndex].lockState);

                    if (ShopName == shopName.Icons && ShopType == shopType.RandomUnlock){
                        SetItemsInfo(0);
                        shopGroups[index].pricePerUnlock = groupIPPrice;
                    }

                    /*if (ShopType == shopType.SingleUnlock){
                        SetItemsInfo(0);
                        Invoke("InvokeCheckItemsStatus",0.1f);
                        newShopItem.Find("UnlockAtLevel").GetComponent<TextMeshProUGUI>().text = "Unlocks at Level " + shopGroups[index].shopItems[itemIndex].UnlockAtLevel + "!";
                        newShopItem.Find("LockGems/Text").GetComponent<TextMeshProUGUI>().text = shopGroups[index].shopItems[itemIndex].UnlockPrice + " Gems";
                        //newShopItem.Find("LockCoins/CurrencyIcon/CurrencyText").GetComponent<TextMeshProUGUI>().text = (shopGroups[index].shopItems[itemIndex].UnlockPrice).ToString();
                    }*/
                    /*if(ShopType == shopType.Free)
                    {
                        SetItemsInfo(0);
                        Invoke("InvokeCheckItemsStatus", 0.1f);
                        newShopItem.Find("UnlockAtLevel").GetComponent<TextMeshProUGUI>().text = "Unlocks at Level " + shopGroups[index].shopItems[itemIndex].UnlockAtLevel + "!";
                        //newShopItem.Find("LockGems/Text").GetComponent<TextMeshProUGUI>().text = shopGroups[index].shopItems[itemIndex].UnlockPrice + " Gems";
                    }
                    else
                    {
                        
                        newShopItem.Find("UnlockAtLevel").gameObject.SetActive(false);
                        newShopItem.Find("LockRV").gameObject.SetActive(false);
                        newShopItem.Find("LockGems").gameObject.SetActive(false);
                    }*/

                    newShopItem.GetComponent<OGShopItem>().button = newShopItem.GetComponent<Button>();
                    newShopItem.GetComponent<OGShopItem>().shopGroupIndex = index;
                    newShopItem.GetComponent<OGShopItem>().shopItemIndex = itemIndex;
                    newShopItem.GetComponent<OGShopItem>().lockState = shopGroups[index].shopItems[itemIndex].lockState;
                    newShopItem.GetComponent<OGShopItem>().playerPrefs = shopGroups[index].shopItems[itemIndex].playerPrefs;
                    newShopItem.GetComponent<OGShopItem>().gameIcon = shopGroups[index].shopItems[itemIndex].gameIcon;
                    newShopItem.name = "Item_" + itemIndex;
                    //shopGroups[index].shopItems[itemIndex].shopItemComponent.button.transform.Find("Selected").GetComponent<Image>().enabled = false;
                    newShopItem.transform.Find("Select").gameObject.SetActive(false);
                    /*if (ShopType == shopType.SingleUnlock)
                    {
                        if (shopGroups[index].shopItems[itemIndex].lockState > 0)
                        {
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = true;
                            newShopItem.Find("Mask/Item").GetComponent<Image>().sprite = shopGroups[index].shopItems[itemIndex].icon;

                            shopGroups[index].totalUnlocks++;
                        }
                        else
                        {
                            newShopItem.Find("Mask/Item").GetComponent<Image>().sprite = shopGroups[index].shopItems[itemIndex].icon;
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = false;
                        }
                    }*/
                    /*if(ShopType == shopType.Free)
                    {
                        if (shopGroups[index].shopItems[itemIndex].lockState > 0)
                        {
                            //newShopItem.Find("Text").GetComponent<TextMeshProUGUI>().text = shopGroups[index].shopItems[itemIndex].name;
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = true;
                            newShopItem.Find("Mask/Item").GetComponent<Image>().sprite = shopGroups[index].shopItems[itemIndex].icon;

                            shopGroups[index].totalUnlocks++;
                        }
                        else
                        {
                            newShopItem.Find("Mask/Item").GetComponent<Image>().sprite = shopGroups[index].shopItems[itemIndex].icon;
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = false;
                        }
                    }*/
                    if (ShopType == shopType.RandomUnlock)
                    {
                        if (shopGroups[index].shopItems[itemIndex].lockState > 0)
                        {
                            //newShopItem.Find("Text").GetComponent<TextMeshProUGUI>().text = shopGroups[index].shopItems[itemIndex].name;
                            newShopItem.Find("Text").GetComponent<Text>().text = "";
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = true;
                            newShopItem.Find("Item").GetComponent<Image>().sprite = shopGroups[index].shopItems[itemIndex].icon;

                            shopGroups[index].totalUnlocks++;
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.transform.Find("Select").gameObject.SetActive(true);
                            newShopItem.Find("Select").gameObject.SetActive(true);
                        }
                        else
                        {
                            //newShopItem.Find("UnlockAtLevel").gameObject.SetActive(false);
                            newShopItem.Find("Text").GetComponent<Text>().text = "?";
                            newShopItem.Find("Item").gameObject.SetActive(false);
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.button.interactable = false;
                            shopGroups[index].shopItems[itemIndex].shopItemComponent.transform.Find("Select").gameObject.SetActive(false);
                        }
                    }
                }

                shopGroups[index].currencyLeft = PlayerPrefs.GetInt(shopGroups[index].currencyPlayerPrefs, 0);
                if (ShopName == shopName.Icons) {
                    shopGroups[index].pricePerUnlock = groupIPPrice;
                    //gameController.CheckTrailsBadge(0);
                    if (ShopType == shopType.RandomUnlock)
                    {
                        newShopGroup.Find("ButtonUnlock/CurrencyIcon/CurrencyText").GetComponent<Text>().text = (shopGroups[index].pricePerUnlock).ToString();

                        newShopGroup.Find("ButtonUnlock/CurrencyIcon").GetComponent<Image>().sprite = shopGroups[index].currencyIcon;

                        newShopGroup.Find("ButtonUnlock/Text").GetComponent<Text>().text = "Unlock Random";

                        shopGroups[index].selectIndex = PlayerPrefs.GetInt(shopGroups[index].playerPrefs, 0);
                        if(shopGroups[index].shopItems[shopGroups[index].selectIndex].lockState > 0)
                        {
                            SelectItem(shopGroups[index].shopItems[shopGroups[index].selectIndex].shopItemComponent.button.gameObject);
                        }
                    }
                    else
                    {
                        print("Single unlock functions for Trails Shop");
                    }
                }           
                // Hide the original shop group object
                Destroy(newShopGroup.Find("ItemsGrid").GetChild(0).gameObject);

                // Hide the original shop item object
                //shopItemObject.gameObject.SetActive(false);
            }

            // Hide the original shop group object
            Destroy(shopGroupObject.gameObject);

            foreach (Transform child in shopGroupsObj)
            {
                if (child != shopGroupsObj && child != shopGroupObject)
                {
                    shopButtons.Add(child);
                }
            }
            //shopGroupObject.gameObject.SetActive(false);
        }

        public void SetItemsInfo(int group)
        {
            if (ShopName == shopName.Icons && ShopType == shopType.RandomUnlock)
            {
                for (int i = 0; i < shopGroups.Length; i++)
                {
                    //groupTrailsPrice = gameController.TrailUnlockPrices[i];
                    Debug.Log("groupTrailsPrice");
                    break;
                }
            }
        }

        /*public void InvokeCheckItemsStatus()
        {
            CheckItemsStatus(0);

        }

        public void CheckItemsStatus(int group)
        {
            for (int i = 0; i < shopGroups[group].shopItems.Length; i++)
            {
                shopGroups[group].shopItems[i].lockState = PlayerPrefs.GetInt(shopGroups[group].shopItems[i].playerPrefs, shopGroups[group].shopItems[i].lockState);
                if(50 >= shopGroups[group].shopItems[i].UnlockAtLevel &&
                    shopGroups[group].shopItems[i].UnlockAtLevel != -1 &&
                    shopGroups[group].shopItems[i].lockState < 1)
                {
                    if (shopGroups[group].shopItems[i].UnlockPrice == -1){
                        //Debug.Log(shopGroups[group].shopItems[i].shopItemComponent.name);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").gameObject.SetActive(true);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockGems").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockFree").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("UnlockAtLevel").gameObject.SetActive(false);
                        //gameController.BackgroundsBadge.SetActive(true);
                        break;
                    }
                    if (shopGroups[group].shopItems[i].UnlockPrice == 0)
                    {
                        //Debug.Log(shopGroups[group].shopItems[i].shopItemComponent.name);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockGems").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockFree").gameObject.SetActive(true);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("UnlockAtLevel").gameObject.SetActive(false);
                        //gameController.BackgroundsBadge.SetActive(true);
                        //gameController.BackgroundsBadge.SetActive(true);
                        break;
                    }
                    else
                    {
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockGems").gameObject.SetActive(true);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockFree").gameObject.SetActive(false);
                        shopGroups[group].shopItems[i].shopItemComponent.transform.Find("UnlockAtLevel").gameObject.SetActive(false);
                        //gameController.BackgroundsBadge.SetActive(true);
                        //Debug.Log(shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").name);
                        break;
                    }
                }
                
                


                if (shopGroups[group].shopItems[i].lockState > 0)
                {
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("UnlockAtLevel").gameObject.SetActive(false);
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("Mask/Cover").gameObject.SetActive(false);
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").gameObject.SetActive(false);
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockGems").gameObject.SetActive(false);
                    //gameController.BackgroundsBadge.SetActive(false);
                    //gameController.BackgroundsBadge.SetActive(false);
                }
                else
                {
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockRV").gameObject.SetActive(false);
                    shopGroups[group].shopItems[i].shopItemComponent.transform.Find("LockGems").gameObject.SetActive(false);
                    //Disable Shop Badge
                }
            }
            if (group + 1 < shopGroups.Length) CheckItemsStatus(group + 1);

            shopButtons[group].transform.Find("ButtonUnlock").gameObject.SetActive(false);
            shopButtons[group].transform.Find("ButtonRV").gameObject.SetActive(false);
        }*/

        public GameObject currentShopItem;

        public void SelectItem(GameObject shopItem)
        {
            if(currentShopItem) currentShopItem.transform.Find("Select").gameObject.SetActive(false);
            currentShopItem = shopItem;
            //currentShopItem.transform.Find("Select").gameObject.SetActive(true);
            if (ShopType == shopType.RandomUnlock)
            {
                if (gameController.playerIP - shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock >= 0 && shopItem.GetComponent<OGShopItem>().lockState <= 0)
                {
                    Debug.Log("Random Unlock");

                    shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].currencyLeft -= shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock;

                    PlayerPrefs.SetInt(shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].currencyPlayerPrefs, shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].currencyLeft);

                    //if (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].currencyPlayerPrefs == "Coins") gameController.ChangeCoins((-shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock).ToString());
                    if (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].currencyPlayerPrefs == "CurrentIP") gameController.ChangeIP(-shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock);

                    shopItem.GetComponent<OGShopItem>().lockState = 1;

                    //shopItem.GetComponent<OGShopItem>().button.transform.Find("Text").GetComponent<TextMeshProUGUI>().text = shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].name;
                    shopItem.GetComponent<OGShopItem>().button.transform.Find("Item").gameObject.SetActive(true);
                    shopItem.GetComponent<OGShopItem>().button.transform.Find("Item").GetComponent<Image>().sprite = shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].icon;
                    
                    shopItem.GetComponent<OGShopItem>().button.transform.Find("Text").GetComponent<Text>().text = "";
                    shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].totalUnlocks++;

                    if (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].totalUnlocks < shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems.Length)
                    {
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonUnlock/CurrencyIcon/CurrencyText").GetComponent<Text>().text = (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock).ToString();
                    }
                    else
                    {
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonUnlock").gameObject.SetActive(false);
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonRV").gameObject.SetActive(false);
                        
                    }

                    //shopItem.GetComponent<OGShopItem>().button.transform.Find("Mask/Item").gameObject.SetActive(true);// shopGroups[index].shopItems[itemIndex].icon;
                    //shopItem.GetComponent<OGShopItem>().button.transform.Find("Mask/Cover").gameObject.SetActive(false);

                    //shopItem.GetComponent<OGShopItem>().button.transform.Find("Mask/UnlockEffect").GetComponent<Animation>().Play();

                    shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.button.interactable = true;
                    PlayerPrefs.SetInt(shopItem.GetComponent<OGShopItem>().playerPrefs, 1);

                    if (audioSource && audioUnlock) audioSource.PlayOneShot(audioUnlock);

                    //gameController.CheckTrailsBadge(0);
                }
                else
                {
                    if (audioSource && audioSelect) audioSource.PlayOneShot(audioSelect);
                    if (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].totalUnlocks < shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems.Length)
                    {
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonUnlock/CurrencyIcon/CurrencyText").GetComponent<Text>().text = (shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].pricePerUnlock).ToString();
                    }
                    else
                    {
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonUnlock").gameObject.SetActive(false);
                        shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopGroupTransform.Find("ButtonRV").gameObject.SetActive(false);
                    }
                }

                if(shopItem.GetComponent<OGShopItem>().lockState > 0)
                {
                    //shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.button.Select();
                    //shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.transform.Find("Select").gameObject.SetActive(true);
                    if (shopItem.GetComponent<OGShopItem>().gameIcon)
                    {
                        //playerControls.weapon.trailEffect = shopItem.GetComponent<OGShopItem>().trail;

                        //playerControls.weapon.UpdateTrail();

                        gameController.playerIcon.sprite = currentShopItem.GetComponent<Image>().sprite;
                        
                        PlayerPrefs.SetInt(shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].playerPrefs, shopItem.GetComponent<OGShopItem>().shopItemIndex);
                    }
                }
            }
            if (shopItem.GetComponent<OGShopItem>().lockState > 0)
            {
                //shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.button.Select();
                //shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[shopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.transform.Find("Select").gameObject.SetActive(true);
                //currentShopItem.SetActive(true);
                if (shopItem.GetComponent<OGShopItem>().gameIcon)
                {
                    //playerControls.weapon.trailEffect = shopItem.GetComponent<OGShopItem>().trail;
                    gameController.playerIcon.sprite = currentShopItem.transform.Find("Item").GetComponent<Image>().sprite;
                    //playerControls.weapon.UpdateTrail();
                    currentShopItem.transform.Find("Select").gameObject.SetActive(true);
                    PlayerPrefs.SetInt(shopGroups[shopItem.GetComponent<OGShopItem>().shopGroupIndex].playerPrefs, shopItem.GetComponent<OGShopItem>().shopItemIndex);
                }
            }
        }

        public void UnlockGems(GameObject item)
        {
            isRV = false;
            SelectItem(item);
        }

        public void UnlockRV(GameObject item)
        {
            /*ServiceProvider.Ads.ShowRV("unlockFingazRV", (bool watchedAd) => {
                if (watchedAd)
                {
                    print("Unlocked Avatar with RV");
                    //SoundBase.instance.unlockedPlay();
                    isRV = true;
                    isFree = false;
                    SelectItem(item);
                }
            });*/
            Debug.Log("RV_Unlock");
        }

        private List<OGShopItem> shopItemsList = new List<OGShopItem>();
        private int shopGroupIndex = 0;

        public void UnlockRandom(Transform shopGroupTransform)
        {
            if (isUnlocking == true) return;
            
            shopGroupIndex = shopGroupTransform.GetSiblingIndex();
            
            if (gameController.playerIP >= shopGroups[shopGroupIndex].pricePerUnlock)
            {
                shopItemsList.Clear();
                
                // Get all locked items in the group
                for (int index = 0; index < shopGroups[shopGroupIndex].shopItems.Length; index++)
                {
                    if (shopGroups[shopGroupIndex].shopItems[index].shopItemComponent.lockState <= 0) shopItemsList.Add(shopGroups[shopGroupIndex].shopItems[index].shopItemComponent);
                }

                if (shopItemsList.Count > 1)
                {
                    StartCoroutine(UnlockSequence());
                    
                }
                else if (shopItemsList.Count > 0)
                {
                    int randomItem = Random.Range(0, shopItemsList.Count);

                    SelectItem(shopItemsList[randomItem].button.gameObject);
                }

            }
            else
            {
                //shopGroups[shopGroupIndex].shopGroupTransform.Find("ButtonUnlock/PopupText").GetComponent<Animation>().Play("Upgrade");
                ShopManager.instance.ShowTransiantNotification_Text("Not enough IP");
            }


        }

        IEnumerator UnlockSequence()
        {
            isUnlocking = true;

            int randomItem = 0;// Random.Range(0, shopItemsList.Count);

            int moves = Random.Range(15, 20);

            while (moves > 0)
            {
                //shopGroups[currentShopItem.GetComponent<OGShopItem>().shopGroupIndex].shopItems[currentShopItem.GetComponent<OGShopItem>().shopItemIndex].shopItemComponent.button.Select();

                shopItemsList[randomItem].button.transform.Find("Select").gameObject.SetActive(false);

                if (randomItem < shopItemsList.Count - 1) randomItem++;
                else randomItem = 0;

                shopItemsList[randomItem].button.transform.Find("Select").gameObject.SetActive(true);

                if (audioSource && audioUnlockSequence) audioSource.PlayOneShot(audioUnlockSequence);

                moves--;

                yield return new WaitForSecondsRealtime(0.08f);

                //Move the onboarding One more stage
                //gameController.onboardingStage = 7;

            }

            if (randomItem < shopItemsList.Count)
            {
                shopItemsList[randomItem].button.transform.Find("Select").gameObject.SetActive(false);

                SelectItem(shopItemsList[randomItem].button.gameObject);
                // Unlock Event Here
                //AnalyticsManager.instance.random_unlock(randomItem, shopGroups[shopGroupIndex].pricePerUnlock);
                isUnlocking = false;
            }
            else
            {
                StartCoroutine(UnlockSequence());
            }

            //checkButtonsAnimation();

        }


        public void CloseShop()
        {
            if (isUnlocking == true) return;

            transform.root.gameObject.SetActive(false);
        }


        private void OnEnable()
        {
            // Go through all the shop groups and create them
            for (int index = 0; index < shopGroups.Length; index++)
            {
                shopGroups[index].currencyLeft = PlayerPrefs.GetInt(shopGroups[index].currencyPlayerPrefs, 0);
            }
        }

        public void InvokeCheckBackgroundsBadge()
        {
            //gameController.CheckBackgroundsBadge(0);
        }
    }

}