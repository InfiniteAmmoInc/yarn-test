using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class DialogueImplementation : MonoBehaviour
{
	[HideInInspector]
	public int currentOption;
	Dialogue dialogue;
	public UnityEngine.UI.Text uiText;
	public GameObject[] optionButtons;
	public TextAsset defaultDialogue;
	bool scrolling;

	void Awake()
	{
		dialogue = GetComponent<Dialogue>();

		foreach (var gameObject in optionButtons)
		{
			gameObject.SetActive(false);
		}

		if (defaultDialogue != null)
		{
			textToRun = defaultDialogue.text;
		}
	}

	public string Parse(string characterName, string line)
	{
		return line;
	}

	public IEnumerator Say(string characterName, string text)
	{
		uiText.text = "";
		string textToScroll = characterName + ": " + text;
		//CharacterData characterData = Global.constants.GetCharacterData(characterName);
		//Global.textbox.Say(characterData, text);
		const float timePerChar = .05f;
		float accumTime = 0f;
		int c = 0;
		while (!InputNext() && c < textToScroll.Length)
		{
			yield return null;
			accumTime += Time.deltaTime;
			while (accumTime > timePerChar)
			{
				accumTime -= timePerChar;
				if (c < textToScroll.Length)
					uiText.text += textToScroll[c];
				c++;
			}
		}
		uiText.text = textToScroll;

		while (InputNext()) yield return null;

		while (!InputNext()) yield return null;
	}

	public bool InputNext()
	{
		return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
	}

	public IEnumerator EndText()
	{
		//Global.textbox.Hide();
		uiText.text = "";
		yield break;
	}

	public void SelectOption00()
	{
		currentOption = 0;
	}

	public void SelectOption01()
	{
		currentOption = 1;
	}

	public void SelectOption02()
	{
		currentOption = 2;
	}

	public void SelectOption03()
	{
		currentOption = 3;
	}

	public IEnumerator RunOptions(List<Dialogue.Option> options)
	{
		dialogue.SetCurrentOption(0);

		yield return null;

		int index = 0;
		foreach (var option in options)
		{
			optionButtons[index].SetActive(true);
			optionButtons[index].GetComponentInChildren<UnityEngine.UI.Text>().text = option.text;
			index++;
		}
		
		/*
		List<OptionButton> optionButtons = new List<OptionButton>();
		int index = 0;
		foreach (var option in options)
		{
			var optionButton = (OptionButton)Instantiate(prefabOptionButton);
			optionButton.index = index;
			optionButton.transform.position = new Vector3(3.375f, 4f, 0f) + Vector3.down * index * 1.5f;
			optionButton.SetText(option.text);
			optionButtons.Add(optionButton);
			index++;
		}
		*/

		currentOption = -1;
		do { yield return null; } while (currentOption == -1);

		//Global.textbox.Say(null, "");

		/*
		for (int i = 0; i < optionButtons.Count; i++)
		{
			if (i != currentOption)
				optionButtons[i].Hide();
		}
		*/

		//yield return new WaitForSeconds(.71f);

		foreach (var gameObject in optionButtons)
		{
			gameObject.SetActive(false);
		}

		dialogue.SetCurrentOption(currentOption);
	}

	public IEnumerator RunCommand(string line)
	{
		string[] tokens = line.Split(' ');
		if (tokens.Length > 0)
		{
			if (IsString(tokens[0], "wait"))
			{
				float timeToWait = (float)Convert.ToDouble(tokens[1]);
				yield return new WaitForSeconds(timeToWait);
			}
			else if (IsString(tokens[0], "tell"))
			{
				GameObject gameObject = GameObject.Find(tokens[1]);
				if (gameObject != null)
				{
					int methodToken = 2;
					if (IsString(tokens[2], "to"))
						methodToken = 3;
					
					string sendData = "";
					if (tokens.Length > methodToken+1)
						sendData = tokens[methodToken+1];
					
					gameObject.SendMessage(tokens[3], sendData, SendMessageOptions.DontRequireReceiver);
				}
			}

		}
		yield break;
	}

	bool ReadBool(string token)
	{
		return IsString(token, "on") || IsString(token, "1");
	}

	bool IsString(string strA, string strB)
	{
		return string.Compare(strA, strB, System.StringComparison.InvariantCultureIgnoreCase) == 0;
	}

	public void SetInteger(string varName, int varValue)
	{
		Continuity.instance.SetVar(varName, varValue);
	}

	public int GetInteger(string varName)
	{
		return Continuity.instance.GetVar(varName);
	}

	public void AddToInteger(string varName, int addAmount)
	{
		Continuity.instance.SetVar(varName, Continuity.instance.GetVar(varName) + addAmount);
	}

	public void SetString(string varName, string varValue)
	{
		// TODO: write this!
	}

	// called when node not found
	public void NodeFail()
	{

	}

	public bool IsPaused()
	{
		return false;
	}

	public bool EvaluateIfChunk(string chunk, ref bool result)
	{
		return false;
	}

	string textToRun = "";
	void OnGUI()
	{
		if (!dialogue.running)
		{
			textToRun = GUI.TextArea(new Rect(0, 0, 600, 350), textToRun);
			if (GUI.Button(new Rect(610, 0, 100, 50), "Test Run"))
			{
				dialogue.Run(textToRun);
			}
			if (GUI.Button(new Rect(610, 60, 100, 50), "Clear"))
			{
				textToRun = "";
			}
		}
	}
}
