using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;

public class KillSeriesManager : MonoBehaviour {

	public CorrectAnswerController WinWindow;


	public int DoubleKillTime,TrippleKillTime,QuadraKillTime,PentaKillTime,HexaKillTime;
    public AudioClip FirstKillSound;
    public AudioClip[] doubleKillSound;
    public AudioClip[] tripleKillSound;
    public AudioClip[] quadraKillSound;
    public AudioClip[] pentaKillSound;
    public AudioClip hexaKillSound;
    public AudioClip shutDownSound;


    public int currentSeries =0;
	float lastAnswerTime=0;
	float timeAvaliableForNext=float.MaxValue;

	public int Answered(Action onComplete){
		
		int IPAddition =1;

		//Debug.Log("Time.time:"+Time.time +"  lastAnswerTime:"+ lastAnswerTime+"  timeAvaliableForNext:"+timeAvaliableForNext);
		if(Time.time - lastAnswerTime<timeAvaliableForNext){
			//Debug.Log("Scored a series "+currentSeries);
			//Scored a series
			switch(currentSeries){
			case 0:		//1st
				timeAvaliableForNext = DoubleKillTime;
				currentSeries = 1;
				IPAddition =0;
                break;
			case 1:		//double
				timeAvaliableForNext = TrippleKillTime;
				currentSeries = 2;
				IPAddition =1;
                int randomDouble = UnityEngine.Random.Range(0, doubleKillSound.Length);
                SoundBase.instance.SoundPlay(doubleKillSound[randomDouble]);
                break;
			case 2:		//tripple
				timeAvaliableForNext = QuadraKillTime;
				currentSeries = 3;
				IPAddition =2;
                int randomTriple = UnityEngine.Random.Range(0, tripleKillSound.Length);
                SoundBase.instance.SoundPlay(tripleKillSound[randomTriple]);
                break;
			case 3:		//quad
				timeAvaliableForNext = PentaKillTime;
				currentSeries = 4;
				IPAddition =3;
                int randomQuadra = UnityEngine.Random.Range(0, quadraKillSound.Length);
                SoundBase.instance.SoundPlay(quadraKillSound[randomQuadra]);
                break;
			case 4:		//penta
				timeAvaliableForNext = HexaKillTime;
				currentSeries = 5;
				IPAddition =4;
                int randomPenta = UnityEngine.Random.Range(0, pentaKillSound.Length);
                SoundBase.instance.SoundPlay(pentaKillSound[randomPenta]);
                break;
			case 5:		//hexa
				timeAvaliableForNext = float.MaxValue;
				currentSeries = 1;
				IPAddition =5;
                SoundBase.instance.SoundPlay(hexaKillSound);
                break;
			}

		}else{
			//Debug.Log("Series reset");
			timeAvaliableForNext = float.MaxValue;
			currentSeries = 1;
            SoundBase.instance.SoundPlay(shutDownSound);
        }
		lastAnswerTime = Time.time;
		WinWindow.PlayAnimation(currentSeries,onComplete);
		return IPAddition;
	}

	public void WrongAnswer(Action onComplete){
		WinWindow.PlayWAnswerAnimation(onComplete);
	}


}
