using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerUp : MonoBehaviour {

    public float removeAfter = 10;

    GameController gameController;

    public Type powerUpType;

    public enum Type
    {
        IPBooster, FreeKey, FreeIP, Exchanger
    }

    // Use this for initialization
    void Start () {
        gameController = FindObjectOfType<GameController>();
        Invoke("Remove", removeAfter);
    }

    private void Remove()
    {
        Destroy(gameObject, 0.5f);
    }

    public void Pickup()
    {
        if (powerUpType == Type.FreeIP)
        {
            //gameController.isFreeIPPowerUp = true;
            //IronSource.Agent.showRewardedVideo("FreeIPReward");
            Debug.Log("SHOW AD FOR FREE IP");
        }
        if (powerUpType == Type.FreeKey)
        {
            //gameController.isFreeKeysPowerUp = true;
            //IronSource.Agent.showRewardedVideo("FreeKeysReward");
            Debug.Log("SHOW AD FOR FREE KEYS");
        }
        if (powerUpType == Type.IPBooster)
        {
            //gameController.isIPBoosterPowerUp = true;
            //IronSource.Agent.showRewardedVideo("RVShopReward");
            Debug.Log("SHOW AD FOR FREE BOOSTER");
        }
        if (powerUpType == Type.Exchanger)
        {
            //gameController.isRVShopPowerUp = true;
            //IronSource.Agent.showRewardedVideo("IPBoosterReward");
            Debug.Log("SHOW AD FOR FREE BOOSTER");
        }
        
        Destroy(gameObject,0.1f);
    }
}
