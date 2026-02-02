using DG.Tweening;
using I2.Loc;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{

    public static ShopManager instance;

    public GameObject errorWindow;

    public TextMeshProUGUI messageText;


    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
        messageText = errorWindow.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>();
        errorWindow.SetActive(false);
    }

    public static void ShowMessage(string text)
    {
        if (!instance.errorWindow) return;

        if (instance.messageText) instance.messageText.text = text;
        instance.errorWindow.SetActive(true);
    }

    public void ShowNotification(string term)
    {
        //messageText.text = ScriptLocalization.Get(term);
        errorWindow.SetActive(true);
    }


    public void ShowTransiantNotification(string term, float timeSec = 2f, Action completed = null)
    {
        ShowTransiantNotification(term, "", timeSec, completed);
    }

    public void ShowTransiantNotification(string term, string message, float timeSec = 2f, Action completed = null)
    {
        //string txt = ScriptLocalization.Get(term) + message;
        //messageText.text = txt;
        //errorWindow.SetActive(true);
        StartCoroutine(hideAfterDelay(timeSec, completed));
    }

    public void ShowTransiantNotification_Text(string message, float timeSec = 2f, Action completed = null)
    {
        string txt = message;
        messageText.text = txt;
        errorWindow.SetActive(true);
        StartCoroutine(hideAfterDelay(timeSec, completed));
    }

    public void HideNotification()
    {
        messageText.text = (" ");
        errorWindow.SetActive(false);
    }

    IEnumerator hideAfterDelay(float time, Action completed = null)
    {
        yield return new WaitForSeconds(time);
        errorWindow.transform.GetChild(0).GetComponent<Image>().DOColor(Color.clear, 0.1f);
        yield return new WaitForSeconds(0.1f);
        errorWindow.SetActive(false);
        errorWindow.transform.GetChild(0).GetComponent<Image>().color = Color.white;
        if (completed != null)
        {
            completed();
        }
    }
}
