using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using SimpleJSON;

public class Dialogue : MonoBehaviour
{
	public class Node
	{
		public Node(string title, string text)
		{
			this.title = title; this.text = text;
		}
		public string title;
		public string text;
	}

	public class Option
	{
		public Option(string text, string nodeTitle)
		{
			this.text = text;
			this.nodeTitle = nodeTitle;
		}

		public string text;
		public string nodeTitle;
	}

	TextAsset textAsset;
	public bool running { get; private set; }
	public int currentOption { get; private set; }

	List<Node> nodes = new List<Node>();
	Node currentNode;
	// glue for extra commands, output, options, characters
	DialogueImplementation implementation;
	List<string> visitedNodes = new List<string>();
	string filename = "";
	string characterName = "", lastCharacterName = "";
	List<Option> options = new List<Option>();

	void Awake()
	{
		implementation = GetComponent<DialogueImplementation>();
	}

	void Start()
	{
		if (textAsset != null)
			Parse(textAsset.text);
	}

	public void SetCurrentOption(int currentOption)
	{
		this.currentOption = currentOption;
	}

	enum Filetype { Twine, JSON }
	private enum ParseMode { None, Text, Title }

	void Parse(string text)
	{
		Filetype filetype = Filetype.JSON;

		if (text.IndexOf("(dp0") == 0)
			filetype = Filetype.Twine;

		nodes.Clear();

		switch (filetype)
		{
		case Filetype.JSON:
			var N = JSON.Parse(text);
			int c = 0;
			while (N[c] != null)
			{
				nodes.Add(new Node(((string)N[c]["title"]).Trim(), ((string)(N[c]["body"])).Replace('\r', '\n')));	
				c++;
			}
			break;
		case Filetype.Twine:
			string[] lines = text.Split('\n');

			// get rid of tabs!
			for (int i = 0; i < lines.Length; i++)
				lines[i] = Regex.Replace(lines[i], @"\t", "");

			ParseMode parseMode = ParseMode.None;
			string nodeTitle = "";
			string nodeText = "";
			foreach (string line in lines) 
			{
				if (line.IndexOf("Tiddler") != -1)
				{
					parseMode = ParseMode.Text;	
				}

				if (parseMode != ParseMode.None)
				{
					/*
					if (line[0] == 'F')
					{
						nodeX = float.Parse(line.Substring(1, line.Length-1), System.Globalization.CultureInfo.InvariantCulture);
					}
					else if (line.Substring(0, 2) == "aF")
					{

					}
					else
					*/
					if (line[0] == 'V')
					{
						if (parseMode == ParseMode.Text)
						{
							nodeText = line.Substring(1, line.Length-1);
							nodeText = nodeText.Replace("\\u000a", "\n");
							nodeText = nodeText.Replace("\\u2013", "-");
							parseMode = ParseMode.Title;
						}
						else if (parseMode == ParseMode.Title)
						{
							nodeTitle = line.Substring(1, line.Length-1);
						}
						if (nodeTitle != "" && nodeText != "")
						{
							nodes.Add(new Node(nodeTitle, nodeText));
							nodeText = "";
							nodeTitle = "";
							parseMode = ParseMode.Text;
						}
					}
				}
			}
			break;
		}

		/*
		foreach (var node in nodes)
		{
			Debug.Log("------------------------------------");
			Debug.Log("Title: " + node.title);
			Debug.Log("Text: " + node.text);
		}
		*/
	}

	Node GetNode(string title)
	{
		foreach (Node node in nodes)
		{
			if (node.title == title)
				return node;
		}
		Debug.LogWarning("Could not find node of title: [" + title + "]");
		return null;
	}

	public void Run(TextAsset textAsset, string startNode = "Start")
	{
		filename = textAsset.name;
		Run(textAsset.text);
	}

	public void Run(string text, string startNode = "Start")
	{
		
		if (startNode == "")
			startNode = "Start";

		currentNode = null;

		Parse(text);
		if (startNode == "Start")
		{
			currentNode = GetNode("Start:" + Application.loadedLevelName);
		}
		if (currentNode == null)
			currentNode = GetNode(startNode);

		if (currentNode != null)
			StartCoroutine(DoRunText(currentNode));
	}

	public string ParseFunction(string name)
	{
		return "";
	}

	bool EvaluateIf(string line)
	{
		//Debug.LogWarning("Evaluating IF: " + line);
		bool eval = true;
		int start = line.IndexOf("<<");
		int end = line.IndexOf(">>");
		//Debug.LogWarning("start: " + start + " + end: " + end);
		if (start != -1 && end != -1)
		{
			line = line.Substring(start+2, end-(start+2)); 
			//Debug.LogWarning("after SUBSTRING: " + line);
		}

		line = line.Replace("if", "");
		line = line.Replace("elseif", "");
		line = line.Replace("else", "");

		// split the line into chunks and operators
		List<string> chunks = new List<string>();
		List<string> operators = new List<string>();
		string chunk = "";
		foreach (var word in line.Split(' '))
		{
			if (word == "or" || word == "and")
			{
				chunks.Add(chunk);
				operators.Add(word);
				chunk = "";
			}
			else
			{
				chunk += word + " ";
			}
		}

		if (chunk != "")
			chunks.Add(chunk);

		if (chunks.Count == 0)
		{
			eval = EvaluateIfChunk(line);
		}
		else
		{
			//Debug.LogWarning("chunks: " + chunks.Count + " operators: " + operators);
			eval = EvaluateIfChunk(chunks[0]);
			for (int i = 0; i < operators.Count; i++)
			{
				if (operators[i] == "or")
					eval = (eval || EvaluateIfChunk(chunks[i+1]));
				else if (operators[i] == "and")
					eval = (eval && EvaluateIfChunk(chunks[i+1]));
            }
        }
        Debug.LogWarning("result of EvaluateIf: " + eval);
		return eval;
	}

	string GetVisitedNodeName(string chunk)
	{
		int visitedStart = chunk.IndexOf("visited(");
		return chunk.Substring(visitedStart+9, chunk.IndexOf("\")") - (visitedStart+9));
	}

	void Visit(string nodeName)
	{
		string key = filename+":"+nodeName;
		if (!visitedNodes.Contains(key))
			visitedNodes.Add(key);
	}

	bool HasVisited(string nodeName)
	{
		return visitedNodes.Contains(filename+":"+nodeName);
	}

	bool EvaluateIfChunk(string chunk)
	{
		Debug.LogWarning("EvaluateIfChunk: " + chunk);

		bool result = false;
		if (chunk.IndexOf("visited(") != -1)
		{

			bool not = false;
			not = (chunk.IndexOf("not") != -1);
			string nodeName = GetVisitedNodeName(chunk);
			if (HasVisited(nodeName))
			{
				if (not)
					result = false;
				else
					result = true;
			}
			else
			{
				if (not)
					result = true;
				else
					result = false;
			}
		}
		else
		{

			string[] bits = chunk.Trim().Split(' ');
			string var = ParseVariableName(bits[0]);
			string op = bits[1];
			/*
			if (bits[2].IndexOf("\"") != -1)
			{
				string val = bits[2].Substring(1, bits[2].Length-2);
				bool eval = false;
				if (op == "=" || op == "==" || op == "eq" || op == "is")
					eval = (Global.continuity.GetStringVar(var) == val);
				else if (op == "!=" || op == "neq")
					eval = (Global.continuity.GetStringVar(var) != val);
				else
					Debug.LogError("Comparison operator not defined: " + op);
				
				result = eval;
			}
			*/
			if (true)
			{
				int val = int.Parse(bits[2], System.Globalization.CultureInfo.InvariantCulture);

				Debug.LogWarning("evaluate if chunk, variable = " + var);
				
				bool eval = false;
				if (op == "=" || op == "==" || op == "eq" || op == "is")
					eval = (Continuity.instance.GetVar(var) == val);
				else if (op == ">" || op == "gt")
					eval = (Continuity.instance.GetVar(var) > val);
				else if (op == ">=" || op == "gte")
					eval = (Continuity.instance.GetVar(var) >= val);
				else if (op == "<" || op == "lt")
					eval = (Continuity.instance.GetVar(var) < val);
				else if (op == "<=" || op == "lte")
					eval = (Continuity.instance.GetVar(var) <= val);
				else if (op == "!=" || op == "neq")
					eval = (Continuity.instance.GetVar(var) != val);
				else
					Debug.LogError("Comparison operator not defined: " + op);
				result = eval;
			}
		}
		Debug.LogWarning("result of EvaluateIfChunk("+chunk+"): " + result);
		return result;
	}

	public void Stop()
	{
		StopAllCoroutines();
	}

	enum ParseIfState
	{
		AddLines,
		SkipToNextElse,
		SkipToEndIf,
	}

	// returns an IF block -> *NOT* including the top line!!! so it'll be shorter than you expect! D:
	List<string> GetIfBlock(List<string> lines)
	{
		List<string> returnLines = new List<string>();

		// assume first line is <<if or <<elseif
		int ifs = 1;
		for (int i = 1; i < lines.Count; i++)
		{
			string line = lines[i];
			
			returnLines.Add(line);
			//Debug.Log("added return line: " + line + " num lines: " + returnLines.Count);

			if (line.IndexOf("<<if") != -1)
			{
				if (i != 0 && ifs == 0)
				{
					// this shouldn't happen
					Debug.LogError("parse error!! check code here...");
				}
				ifs++;
			}
			else if (line.IndexOf("<<elseif") != -1)
			{
				if (ifs == 1)
				{
					// we found an elseif on our level, remove it from the list (so we don't skip it) and return
					returnLines.RemoveAt(returnLines.Count-1);
					break;
				}
			}
			else if (line.IndexOf("<<else") != -1)
			{
				if (ifs == 1)
				{
					// we found an else on our level, remove it from the list (so we don't skip it) and return
					returnLines.RemoveAt(returnLines.Count-1);
					break;
				}
			}
			else if (line.IndexOf("<<endif>>") != -1)
			{
				ifs--;
				if (ifs == 0)
				{
					returnLines.RemoveAt(returnLines.Count-1);
					break;
				}
			}
			else
			{

			}
		}

		Debug.Log("final returnLines count: " + returnLines.Count);

		return returnLines;
	}

	IEnumerator ParseIf(List<string> lines)
	{
		Debug.LogWarning("Entered ParseIf!");
		ParseIfState parseIfState = ParseIfState.AddLines;
		List<string> newLines = new List<string>();
		int i = 0;
		while (i < lines.Count)
		{
			string line = lines[i];
			Debug.Log("line["+i+"]: " + line);

			if (line.IndexOf("<<if") != -1)
			{
				if (EvaluateIf(line))
				{
					List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
					int skipAmount = block.Count+1;
					i += skipAmount;
					//Debug.Log("Passed if: ParseIfState.SkipToEndIf, skipped: " + skipAmount + " line now #" + i + ": " + lines[i]);
					yield return StartCoroutine(ParseIf(block));
					if (i >= lines.Count)
							break;
					parseIfState = ParseIfState.SkipToEndIf;
				}
				else
				{
					parseIfState = ParseIfState.SkipToNextElse;
					List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
					int skipAmount = block.Count+1;
					i += skipAmount;
					if (i >= lines.Count)
						break;
					//Debug.Log("Failed if: ParseIfState.SkipToNextElse, skipped: " + skipAmount + " line now #" + i + ": " + lines[i]);
				}
			}
			else if (line.IndexOf("<<elseif") != -1)
			{
				if (parseIfState == ParseIfState.SkipToEndIf)
				{
					List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
					int skipAmount = block.Count+1;
					i += skipAmount;
					if (i >= lines.Count)
						break;
					//Debug.LogWarning("Skipped: " + skipAmount + " due to ParseIfState.SkipToEndIf line now #" + i + ": " + lines[i]);
				}
				else
				{
					if (EvaluateIf(line))
					{
						List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						int skipAmount = block.Count+1;
						i += skipAmount;
						yield return StartCoroutine(ParseIf(block));
						parseIfState = ParseIfState.SkipToEndIf;
						if (i >= lines.Count)
							break;
						//Debug.Log("Passed elseif: ParseIfState.SkipToEndIf, skipped: " + skipAmount + " line now #" + i + ": " + lines[i]);
					}
					else
					{
						//Debug.Log("Failed elseif... ParseIfState.SkipToNextElse");
						parseIfState = ParseIfState.SkipToNextElse;
						List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						int skipAmount = block.Count+1;
						i += skipAmount;
						if (i >= lines.Count)
							break;
						//Debug.LogWarning("Skipped: " + skipAmount + " due to failed IF check line now #" + i + ": " + lines[i]);
					}
				}
			}
			else if (line.IndexOf("<<else") != -1)
			{
				if (parseIfState == ParseIfState.SkipToEndIf)
				{
					List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
					int skipAmount = block.Count+1;
					i += skipAmount;
					if (i >= lines.Count)
							break;
					//Debug.LogWarning("Skipped: " + skipAmount + " due to ParseIfState.SkipToEndIf");
				}
				else
				{
					if (parseIfState == ParseIfState.SkipToNextElse)
					{
						List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						i += block.Count;
						yield return StartCoroutine(ParseIf(block));
						parseIfState = ParseIfState.SkipToEndIf;
					}
					else
					{
						parseIfState = ParseIfState.AddLines;
						List<string> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						int skipAmount = block.Count+1;
						i += skipAmount;
						if (i >= lines.Count)
							break;
						//Debug.LogWarning("Skipped: " + skipAmount + " due to failed SOME WEIRDNESS line now #" + i + ": " + lines[i]);
					}
				}
			}
			else if (line.IndexOf("<<endif>>") != -1)
			{
				//Debug.Log("Found endif... ParseIfState.AddLines");
				parseIfState = ParseIfState.AddLines;
				i++;
			}
			else
			{
				if (parseIfState == ParseIfState.AddLines)
				{
					yield return StartCoroutine(RunLine(line));
					newLines.Add(line);

				}
				i++;
			}
		}
		//Debug.LogWarning ("Returning from ParseIf ----------------");
		//return newLines;
	}

	bool ParseOpVal(string text, string op, ref string varName, ref int val)
	{
		int opIndex = text.IndexOf(op);
		if (opIndex != -1)
		{
			int dollarSignIndex = text.IndexOf('$');
			string front = text.Substring(dollarSignIndex, opIndex - dollarSignIndex).Trim();
			varName = ParseVariableName(front);
			//Debug.LogError("front: " + front + " varName: " + varName);
			val = int.Parse(text.Substring(opIndex+op.Length, text.Length - (opIndex+op.Length)), System.Globalization.CultureInfo.InvariantCulture);
			return true;
		}
		return false;
	}

	IEnumerator RunCommand(string line)
	{
		Debug.Log("RunCommand: " + line);
		string[] tokens = line.Split(' ');
		bool ranStandardCommand = false;
		if (tokens.Length > 0)
		{
			if (IsString(tokens[0], "set"))
			{
				int val = 0;
				string varName = "";
				if (ParseOpVal(line, "+=", ref varName, ref val))
					Continuity.instance.ChangeVar(varName, val);
				else if (ParseOpVal(line, "-=", ref varName, ref val))
					Continuity.instance.ChangeVar(varName, -val);
				else if (ParseOpVal(line, "*=", ref varName, ref val))
					Continuity.instance.SetVar(varName, Continuity.instance.GetVar(varName) * val);
				else if (ParseOpVal(line, "/=", ref varName, ref val))
					Continuity.instance.SetVar(varName, Continuity.instance.GetVar(varName) / val);
				else if (ParseOpVal(line, "=", ref varName, ref val))
					Continuity.instance.SetVar(varName, val);
				else if (ParseOpVal(line, "to", ref varName, ref val))
					Continuity.instance.SetVar(varName, val);
				ranStandardCommand = true;
			}
		}
		if (!ranStandardCommand)
		{
			yield return StartCoroutine(implementation.RunCommand(line));
		}
	}

	IEnumerator RunLine(string line)
	{
		string commandLine = "";
		// run all macros
		// run them
		//line = ProcessLine(line, "<<", ">>", ProcessMacro);

		// if this line isn't an options line
		if (line.IndexOf("[[") == -1)
		{
			Debug.Log("the LINE CHECK: " + line);
			// check to see if this is a command line
			int commandStart = line.IndexOf("<<");
			if (commandStart == -1)
			{
				// if it's not a command line, it's assumed to be dialogue
				int colon = line.IndexOf(':');
				int space = line.IndexOf(' ');
				if (colon != -1 && (space == -1 || space >= colon))
				{
					characterName = line.Substring(0, colon);
					
					string name = line.Substring(0, colon);
					line = line.Substring(colon+1, line.Length - (colon + 1));
				}
			}
			else
			{
				Debug.LogWarning("is command line!");
				int commandEnd = line.IndexOf(">>");
				commandLine = line.Substring(commandStart+2, commandEnd - (commandStart+2));
				line = line.Substring(0, commandStart) + line.Substring(commandEnd+2, line.Length - (commandEnd+2));//line.Replace(commandLine, "");
			}

			line = line.Trim();

			if (line.Length != 0)
			{
				line = implementation.Parse(characterName, line);
				yield return StartCoroutine(implementation.Say(characterName, line));
				
				if (lastCharacterName != characterName)
					lastCharacterName = characterName;
			}

			yield return StartCoroutine(RunCommand(commandLine));

			yield return null;
		}
		else
		{
			// add option
			int squareStart = line.IndexOf("[[");
			int squareEnd = line.IndexOf("]]");
			int bar = line.IndexOf("|");
			if (squareStart != -1 && bar != -1 && squareEnd != -1)
			{
				AddOption(new Option(line.Substring(squareStart+2, bar - (squareStart+2)),
				                       line.Substring(bar+1, squareEnd - (bar+1))));
			}
			else if (squareStart != -1 && squareEnd != -1)
			{
				AddOption(new Option("", line.Substring(squareStart+2, squareEnd - (squareStart+2))));
			}
		}
	}

	void AddOption(Option option)
	{
		options.Add(option);
	}

	IEnumerator DoRunText(Node currentNode)
	{
		string text = currentNode.text;
		Visit(currentNode.title);
		Debug.Log("Running node with text: " + text);

		running = true;
		characterName = "";

		string gotoNode = "";
		string[] lines = text.Split('\n');

		options.Clear();

		// edit lines array to put commands on separate lines
		// to make parsing simpler later on
		List<string> sortedLines = new List<string>();
		int c = 0;
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];

			c++;
			if (c > 2560)
				break;

			while (line.IndexOf("<<") != -1)
			{
				string front = "", center = "";
				int commandLineStart = line.IndexOf("<<");
				int commandLineEnd = line.IndexOf(">>");
				if (commandLineStart == -1 || commandLineEnd == -1)
					break;

				int end = commandLineEnd + 2;

				front = line.Substring(0, commandLineStart).Trim();
				if (front != "")
					sortedLines.Add(front);
				
				center = line.Substring(commandLineStart, end - commandLineStart).Trim();
				if (center != "")
					sortedLines.Add(center);

				line = line.Substring(end, line.Length - end).Trim();
			}
			
			if (line != "")
				sortedLines.Add(line);
		}

		// MACROS

		// parse ifs
		yield return StartCoroutine(ParseIf(sortedLines));

		// skip options select if we only have one option and it has no text
		if (options.Count == 1 && options[0].text == "")
		{
			gotoNode = options[0].nodeTitle;
			options.Clear();
		}

		if (options.Count > 0)
		{
CantFindNodeLoopPoint:

			yield return StartCoroutine(implementation.RunOptions(options));

			currentNode = GetNode(options[currentOption].nodeTitle);
			if (currentNode != null)
			{
				yield return null;
				yield return StartCoroutine(DoRunText(currentNode));
			}
			else
			{
				Debug.LogError("Could not find node: " + options[currentOption].nodeTitle);
				goto CantFindNodeLoopPoint;
			}
		}

		if (gotoNode != "")
		{
			currentNode = GetNode(gotoNode);
			if (currentNode != null)
			{
				gotoNode = "";
				yield return null;
				Debug.LogWarning("Running node: " + currentNode.title);
				yield return StartCoroutine(DoRunText(currentNode));
			}
		}

        yield return StartCoroutine(implementation.EndText());

		running = false;

		yield break;
	}

	delegate string ProcessMethod(string chunk);

	string ProcessLine(string line, string startSequence, string endSequence, ProcessMethod processMethod)
	{
		int startIndex = 0;
		bool done = false;
		while (!done)
		{
			int start = line.IndexOf(startSequence, startIndex);
			if (start != -1)
			{
				int end = line.IndexOf(endSequence);
				if (end != -1)
				{
					int startChunk = start + startSequence.Length;
					int endChunk = end;
					string chunk = line.Substring(startChunk, endChunk - startChunk);

					int endEnd = end + endSequence.Length;
					string replace = processMethod(chunk);
					line = line.Substring(0, start) + replace + line.Substring(endEnd, line.Length - endEnd);
					startIndex = start + replace.Length;
				}
				else
				{
					done = true;
				}
			}
			else
			{
				done = true;
			}
		}
		return line;
	}

	string ProcessMacro(string macro)
	{
		string[] bits = macro.Split(' ');
		if (IsString(bits[0], "print"))
		{
			// parse variables, return result
		}
		return macro;
	}

	bool IsString(string value1, string value2)
	{
		return string.Equals(value1, value2, StringComparison.InvariantCultureIgnoreCase);
	}

	public string ParseVariableName(string bit)
	{
		if (bit.Length < 2)
			Debug.LogError("Invalid variable name: " + bit);
		else
		{
			if (bit[0] == '$')
			{
				return bit.Substring(1, bit.Length - 1);
            }
        }
        return "";
    }

	public int GetVariable(string var)
	{
		return Continuity.instance.GetVar(var);
	}
}
