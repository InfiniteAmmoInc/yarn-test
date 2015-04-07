using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class Continuity : MonoBehaviour
{
	[HideInInspector]
	public Dictionary<string,int> vars = new Dictionary<string,int>();
	[HideInInspector]
	public Dictionary<string,string> stringVars = new Dictionary<string,string>();

	public bool showDebug;
	public static Continuity instance;

	void Awake()
	{
		if (instance == null)
		{
			instance = this;
			Reset();
		}
	}

	public void Reset()
	{
		vars.Clear();
		stringVars.Clear();
	}

	// Integer Variables
	public int GetVar(string var)
	{
		if (!vars.ContainsKey(var))
			vars[var] = 0;
		return vars[var];
	}

	public bool IsVar(string var, int val)
	{
		if (!vars.ContainsKey(var))
			vars[var] = 0;
		return vars[var] == val;
	}

	public void SetVar(string var, int val)
	{
		vars[var] = val;
		Debug.LogWarning("var: " + var + " is now " + vars[var]);
	}

	public void ChangeVar(string var, int val)
	{
		if (!vars.ContainsKey(var))
			vars[var] = 0;
		vars[var] += val;
		Debug.LogWarning("var: " + var + " is now " + vars[var] + " after adding: "+val);
	}
	
	// Saving and loading
	public void Save()
	{
		// save all flags
		System.Text.StringBuilder stringBuilder = new StringBuilder();

		// TODO: write all vars and stringVars
		
		foreach (KeyValuePair<string,int> keyValuePair in vars)
		{
			stringBuilder.Append(keyValuePair.Key);
			stringBuilder.Append(",");
			stringBuilder.Append(keyValuePair.Value);
			stringBuilder.Append(",");
		}

		PlayerPrefs.SetString("data", stringBuilder.ToString());
	}

	public void Load()
	{
		
		vars.Clear();
		string[] split = PlayerPrefs.GetString("data").Split(',');
		for (int i = 0; i < split.Length; i+=2)
		{
			string v1 = split[i];//int.Parse(split[i], System.Globalization.CultureInfo.InvariantCulture);
			int v2 = int.Parse(split[i+1], System.Globalization.CultureInfo.InvariantCulture);
			vars[v1] = v2;
		}
		
	}

	void OnGUI()
	{
		if (showDebug)
		{
			//GUI.Box(new Rect(0f, 0f, 400f, 700f), "");
			string debugText = "";
			foreach (KeyValuePair<string,int> keyValuePair in vars)
				debugText += "" + keyValuePair.Key + " = " + keyValuePair.Value + "\n";
			GUI.Box(new Rect(0f, 0f, 150f, 700f), debugText);
		}
	}
}
