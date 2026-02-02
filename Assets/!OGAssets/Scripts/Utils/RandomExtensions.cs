using UnityEngine;
using System.Collections;

static class RandomExtensions
{
	public static void Shuffle<T> ( T[] array)
	{
		int n = array.Length;
		while (n > 1) 
		{

 			int k = (int)(Random.value * n--);
			T temp = array[n];
			array[n] = array[k];
			array[k] = temp;
		}
	}
}
