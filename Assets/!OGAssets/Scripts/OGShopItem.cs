using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ObscureGames
{

    public class OGShopItem : MonoBehaviour
    {
        internal Button button;
        internal int shopGroupIndex;
        internal int shopItemIndex;

        public int lockState = 0;
        public Sprite icon;
        public Sprite gameIcon;
        public string playerPrefs = "Item0001";
    }
}
