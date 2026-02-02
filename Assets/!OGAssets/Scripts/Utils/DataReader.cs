using UnityEngine;
using System.Collections;
using System.IO;

public class DataReader : MonoBehaviour
{
	string content;
	public static int LenSize = 0;

	public static string GetLine (string FileName, int index)
	{

		TextAsset txt = (TextAsset)Resources.Load (FileName, typeof(TextAsset));

		string[] lines = txt.text.Split ('\n');
		LenSize = lines.Length;
		if (index < lines.Length)
			return lines [index];
		
		return null;
	}

	public static string[][] GetLines (string FileName)
	{

		TextAsset txt = (TextAsset)Resources.Load (FileName, typeof(TextAsset));

		string[] lines = txt.text.Split ('\n');
		string[][] allData = new string[lines.Length][];
		int i = 0;
		foreach (string line in lines) {
			allData [i] = GetLineStr (line);
			i++;
		}
		return allData;
	}

	public static string[] GetLineStr (string line)
	{
		if (line == null)
			return null;

		string[] lines = line.Split ('/');

		return lines;
	}
}
