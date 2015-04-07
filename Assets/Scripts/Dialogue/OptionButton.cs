using UnityEngine;
using System.Collections;

public class OptionButton : MonoBehaviour
{
	public enum AnimationType
	{
		Slide,
		Fade,
	}

	public int index;

	public float baseShowTime = .4f;
	public float addShowTime = .2f;
	public float baseHideTime = .2f;
	public float addHideTime = .1f;
	public float selectTime = .6f;
	public Dialogue dialogue;
	public static bool optionSelected;
	Color targetTextColor, originalTextColor;
	UnityEngine.UI.Text uiText;
	//public SpriteRenderer bgSpriteRenderer;
	Vector3 hideOffset = new Vector3(7f, 0f, 0f);
	Vector3 targetScale = Vector3.one;
	Color targetColor, originalColor, fadeOutColor;

	void Awake()
	{
		optionSelected = false;
		uiText = GetComponentInChildren<UnityEngine.UI.Text>();
		//bgSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
		//targetColor = originalColor = bgSpriteRenderer.color;
		fadeOutColor = originalColor;
		fadeOutColor.a = 0f;

		//targetTextColor = originalTextColor = uiText.GetComponent<Renderer>().material.color;


		//bgSpriteRenderer.color = fadeOutColor;
		//uiText.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 0f);
	}

	void Start()
	{
	}

	public void SetText(string text)
	{
		this.uiText.text = text;
	}

	public void Hide()
	{
		adjustSpeed = 8f;
		targetColor = originalColor;
		targetColor.a = 0f;
		targetTextColor = originalTextColor;
		targetTextColor.a = 0f;
	}

	float adjustSpeed = 10f;

	void Update()
	{
		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * adjustSpeed);
		//bgSpriteRenderer.color = Color.Lerp(bgSpriteRenderer.color, targetColor, Time.deltaTime * adjustSpeed);
		//uiText.GetComponent<Renderer>().material.color = Color.Lerp(uiText.GetComponent<Renderer>().material.color, targetTextColor, Time.deltaTime * adjustSpeed);
	}

	void OnMouseDown()
	{
		if (!optionSelected)
		{
			optionSelected = true;
			//Global.twine.optionSelected = true;
			dialogue.SetCurrentOption(index);
			targetColor = originalColor;
			Hide();
			//Tween.ScaleTo(gameObject, Vector3.zero, selectTime, selectEaseType);
		}
	}
}
