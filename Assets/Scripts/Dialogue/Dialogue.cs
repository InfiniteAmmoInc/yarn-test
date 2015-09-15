using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using SimpleJSON;

public class Dialogue : MonoBehaviour
{
	public class Line
	{
		public Line(string text, int index, int tabs)
		{
			this.index = index;
			this.text = text;
			this.tabs = tabs;
		}
		public int index;
		public int tabs;
		public string text;
	}

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

	public TextAsset textAsset { get; private set; }
	public bool running { get; private set; }
	public int currentOption { get; private set; }

	enum Filetype { Twine, JSON, XML }
	enum ParseMode { None, Text, Title }

	List<Node> nodes = new List<Node>();
	public Node currentNode { get; private set; }
	// glue for extra commands, output, options, characters
	public DialogueImplementation implementation { get; private set; }
	List<string> visitedNodes = new List<string>();
	string filename = "";
	string characterName = "", lastCharacterName = "";
	List<Option> options = new List<Option>();
	public string runningNode { get; private set; }
	TextAsset lastTextAssetParsed;

	string lastNodeTitle;
	int lastLineIndex;

	public string varNodeTitle, varLineIndex;

	// =============================== CONFIG ===============================

	// turn on auto sequential nodes
	// NodeName.1
	// NodeName.2
	const bool autoSequentialNodes = true;

	// turn on parsing of shortcut options
	// e.g.
	// -> option 1
	// -> option 2
	//    -> suboption 1
	//    -> suboption 2 <<if $conditional is 1>>
	//    -> suboption 3
	// -> option 3
	const bool parseShortcutOptions = true;

	void Awake()
	{
		implementation = GetComponent<DialogueImplementation>();
	}

	void Start()
	{
		if (textAsset != null)
			ParseInto(textAsset.text, ref nodes);
	}

	public void SetCurrentOption(int currentOption)
	{
		this.currentOption = currentOption;
	}

	void ParseInto(string text, ref List<Node> nodes)
	{
		Filetype filetype = Filetype.XML;

		if (text.IndexOf("(dp0") == 0)
			filetype = Filetype.Twine;
		else if (text.IndexOf("[") == 0)
			filetype = Filetype.JSON;

		nodes.Clear();

		switch (filetype)
		{
		case Filetype.JSON:
			var N = JSON.Parse(text);
			int c = 0;
			while (N[c] != null)
			{
				if (N[c]["title"] != null && N[c]["body"] != null)
				{
					nodes.Add(new Node(((string)N[c]["title"]).Trim(), ((string)(N[c]["body"])).Replace('\r', '\n')));	
				}
				c++;
			}
			break;

		case Filetype.XML:
			//var xmlParser = new XMLParser(text);
			//var xmlElement = xmlParser.Parse();
			/*
			XDocument xDoc = XDocument.Load("XMLFile1.xml");

            var query = (from x in xDoc.Descendants("quiz").Elements("problem")
                        select new Question
                        {
                            question = x.Element("question").Value,
                            answerA = x.Element("answerA").Value,
                            answerB = x.Element("answerB").Value,
                            answerC = x.Element("answerC").Value,
                            answerD = x.Element("answerD").Value,
                            correct = x.Element("correct").Value
                        }).ToList();
			*/
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
	}

	Node GetNode(string title)
	{
		foreach (Node node in nodes)
		{
			if (IsString(node.title, title))
				return node;
		}

		return null;
	}

	public bool HasNode(TextAsset textAsset, string nodeName)
	{
		string nodeNamePointOne = nodeName + ".1";

		if (textAsset != lastTextAssetParsed)
		{
			Parse(textAsset);
		}

		foreach (var node in nodes)
		{
			if (IsString(node.title, nodeName) || IsString(node.title, nodeNamePointOne))
				return true;
		}
		return false;
	}

	public void Parse(TextAsset textAsset)
	{
		lastTextAssetParsed = textAsset;
		ParseInto(textAsset.text, ref nodes);
	}

	public string GetNodeText(string nodeTitle)
	{
		foreach (var node in nodes)
		{
			if (node.title == nodeTitle)
			{
				return node.text;
			}
		}
		return "";
	}

	// parse the textAsset (if not already parsed) and run 
	public void Run(TextAsset textAsset, string startNode = "Start", int startLineIndex = 0)
	{
		if (textAsset != null)
		{
			this.textAsset = textAsset;
			filename = textAsset.name;
			if (textAsset != lastTextAssetParsed)
			{
				Debug.LogWarning("Re-parsing...");
				Parse(textAsset);
			}

			RunInternal(startNode, startLineIndex);
		}
	}

	// parse the text passed in and run
	public void Run(string text, string startNode = "Start", int startLineIndex = 0)
	{
		this.textAsset = null;
		filename = "";
		ParseInto(text, ref nodes);

		RunInternal(startNode, startLineIndex);
	}

	void RunInternal(string startNode, int startLineIndex)
	{
		Stop();

		Debug.LogWarning("startNode: " + startNode + " at " + startLineIndex);

		this.runningNode = startNode;
		
		if (startNode == "")
			startNode = "Start";

		currentNode = null;

		if (startNode == "Start")
			currentNode = GetNode("Start:" + Application.loadedLevelName);

		if (currentNode == null)
			currentNode = GetNode(startNode);

		if (currentNode == null)
		{
			Debug.LogWarning("Could not find node named: " + startNode);

			if (autoSequentialNodes)
			{
				currentNode = GetNode(startNode + ".1");
				if (currentNode != null)
				{
					// if we can't find a node with the regular name but we can find a node with the .1 after it, then 
					// we're playing a repeated auto node thingy
					// so check continuity
					int nodeProgress = implementation.GetInteger(filename + "_" + startNode);
					if (nodeProgress != -1)
					{
						// try to get the next node in the sequence
						currentNode = GetNode(startNode + "." + (nodeProgress+1));
						if (currentNode == null)
						{
							// if we couldn't get the next node, repeat the last node
							if (nodeProgress != 0)
								currentNode = GetNode(startNode + "." + nodeProgress);
						}
						else
						{
							// commit to moving to the next node, since we found it
							nodeProgress++;
							implementation.SetInteger(filename + "_" + startNode, nodeProgress);
						}
					}
				}
			}
		}
		else
		{
			int nodeProgress = implementation.GetInteger(filename + "_" + startNode);
			if (nodeProgress == -1)
				currentNode = null;
		}

		if (currentNode != null)
		{
			StartCoroutine(DoRunText(currentNode, startLineIndex));
		}
		else
		{
			implementation.NodeFail();
		}
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
		}

		line = line.Trim();
		if (line.Substring(0, 2) == "if")
			line = line.Substring(2, line.Length - 2);
		else if (line.Substring(0, 6) == "elseif")
			line = line.Substring(6, line.Length - 6);
		else if (line.Substring(0, 4) == "else")
			line = line.Substring(4, line.Length - 4);
		
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
        //Debug.LogWarning("result of EvaluateIf: " + eval);
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
		//Debug.LogWarning("EvaluateIfChunk: " + chunk);

		bool result = false;

		if (implementation.EvaluateIfChunk(chunk, ref result))
		{
		}
		else if (chunk.IndexOf("visited(") != -1)
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
			Debug.LogWarning("chunk: " + chunk);
			string[] bits = chunk.Trim().Split(' ');
			string var = ParseVariableName(bits[0]);
			string op = bits[1];
			if (bits[2].IndexOf("\"") != -1)
			{
				/*
				string val = bits[2].Substring(1, bits[2].Length-2);
				bool eval = false;
				if (op == "=" || op == "==" || op == "eq" || op == "is")
					eval = (Global.continuity.GetStringVar(var) == val);
				else if (op == "!=" || op == "neq")
					eval = (Global.continuity.GetStringVar(var) != val);
				else
					Debug.LogError("Comparison operator not defined: " + op);
				
				result = eval;
				*/
			}
			else
			{
				Debug.LogWarning("chunk: " + chunk + " filename: " + filename);
				int val = 0;
				try
				{
					val = int.Parse(bits[2], System.Globalization.CultureInfo.InvariantCulture);
				}
				catch
				{
					Debug.LogError("Error parsing chunk: " + chunk + " in file: " + filename);
				}

				Debug.LogWarning("evaluate if chunk, variable = " + var);
				
				bool eval = false;
				if (op == "=" || op == "==" || op == "eq" || op == "is")
					eval = (implementation.GetInteger(var) == val);
				else if (op == ">" || op == "gt")
					eval = (implementation.GetInteger(var) > val);
				else if (op == ">=" || op == "gte")
					eval = (implementation.GetInteger(var) >= val);
				else if (op == "<" || op == "lt")
					eval = (implementation.GetInteger(var) < val);
				else if (op == "<=" || op == "lte")
					eval = (implementation.GetInteger(var) <= val);
				else if (op == "!=" || op == "neq")
					eval = (implementation.GetInteger(var) != val);
				else
					Debug.LogError("Comparison operator not defined: " + op + " in file: " + filename);
				result = eval;
			}
		}
		//Debug.LogWarning("result of EvaluateIfChunk("+chunk+"): " + result);
		return result;
	}

	public void Stop()
	{
		nestedRunTexts = 0;
		StopAllCoroutines();
		running = false;
	}

	enum ParseIfState
	{
		AddLines,
		SkipToNextElse,
		SkipToEndIf,
	}

	// returns an IF block -> *NOT* including the top line!!! so it'll be shorter than you expect! D:
	List<Line> GetIfBlock(List<Line> lines)
	{
		List<Line> returnLines = new List<Line>();

		// assume first line is <<if or <<elseif
		int ifs = 1;
		for (int i = 1; i < lines.Count; i++)
		{
			var line = lines[i];
			
			returnLines.Add(line);
			//Debug.Log("added return line: " + line + " num lines: " + returnLines.Count);

			if (line.text.IndexOf("<<if") != -1)
			{
				if (i != 0 && ifs == 0)
				{
					// this shouldn't happen
					Debug.LogError("parse error!! check code here...");
				}
				ifs++;
			}
			else if (line.text.IndexOf("<<elseif") != -1)
			{
				if (ifs == 1)
				{
					// we found an elseif on our level, remove it from the list (so we don't skip it) and return
					returnLines.RemoveAt(returnLines.Count-1);
					break;
				}
			}
			else if (line.text.IndexOf("<<else") != -1)
			{
				if (ifs == 1)
				{
					// we found an else on our level, remove it from the list (so we don't skip it) and return
					returnLines.RemoveAt(returnLines.Count-1);
					break;
				}
			}
			else if (line.text.IndexOf("<<endif>>") != -1)
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

	public class LineBlock
	{
		public LineBlock()
		{
			lines = new List<Line>();
		}
		public List<Line> lines;
	}

	IEnumerator RunLines(List<Line> lines, int startLineIndex = 0)
	{
		ParseIfState parseIfState = ParseIfState.AddLines;
		//List<string> newLines = new List<string>();
		int i = 0;
		while (i < lines.Count)
		{
			lastLineIndex = lines[i].index;
			string line = lines[i].text;
			//Debug.Log("line["+i+"]: " + line);

			if (line.IndexOf("<<if") != -1)
			{
				if (EvaluateIf(line))
				{
					List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
					int skipAmount = block.Count+1;
					i += skipAmount;
					//Debug.Log("Passed if: ParseIfState.SkipToEndIf, skipped: " + skipAmount + " line now #" + i + ": " + lines[i]);
					yield return StartCoroutine(RunLines(block, startLineIndex));
					if (i >= lines.Count)
							break;
					parseIfState = ParseIfState.SkipToEndIf;
				}
				else
				{
					parseIfState = ParseIfState.SkipToNextElse;
					List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
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
					List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
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
						List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						int skipAmount = block.Count+1;
						i += skipAmount;
						yield return StartCoroutine(RunLines(block, startLineIndex));
						parseIfState = ParseIfState.SkipToEndIf;
						if (i >= lines.Count)
							break;
						//Debug.Log("Passed elseif: ParseIfState.SkipToEndIf, skipped: " + skipAmount + " line now #" + i + ": " + lines[i]);
					}
					else
					{
						//Debug.Log("Failed elseif... ParseIfState.SkipToNextElse");
						parseIfState = ParseIfState.SkipToNextElse;
						List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
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
					List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
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
						List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						i += block.Count;
						yield return StartCoroutine(RunLines(block, startLineIndex));
						parseIfState = ParseIfState.SkipToEndIf;
					}
					else
					{
						parseIfState = ParseIfState.AddLines;
						List<Line> block = GetIfBlock(lines.GetRange(i, lines.Count - i));
						int skipAmount = block.Count+1;
						i += skipAmount;
						if (i >= lines.Count)
							break;
					}
				}
			}
			else if (line.IndexOf("<<endif>>") != -1)
			{
				parseIfState = ParseIfState.AddLines;
				i++;
			}
			// parse shortcut options with -> 
			else if (parseShortcutOptions && line.Trim().IndexOf("->") == 0)
			{
				List<LineBlock> optionLineBlocks = new List<LineBlock>();
				int usingTabs = lines[i].tabs;
				string optionText = "";
				/*
				var lineTrimmed = line.Trim();
				var optionText = lineTrimmed.Substring(2, lineTrimmed.Length-2);
				optionText = optionText.Trim();
				*/

				options.Clear();
				// if null, skip lines
				LineBlock lineBlock = null;
				while (i < lines.Count)
				{
					var currentLine = lines[i];
					Debug.Log("usingTabs: " + usingTabs + " currentLine.tabs: " + currentLine.tabs);
					
					// starting a new option and line block 
					if (currentLine.tabs == usingTabs && currentLine.text.IndexOf("->") == usingTabs)
					{
						string lineText = currentLine.text;
						bool conditional = true;
						if (currentLine.text.IndexOf("<<") != -1)
						{
							conditional = EvaluateIf(currentLine.text.Substring(usingTabs, currentLine.text.Length - usingTabs));
							int startIndex = lineText.IndexOf("<<");
							int endIndex = lineText.IndexOf(">>");
							lineText = lineText.Substring(0, startIndex) + lineText.Substring(endIndex+2, lineText.Length - (endIndex+2));
							currentLine.text = lineText;
						}

						// end last one
						if (lineBlock != null)
							optionLineBlocks.Add(lineBlock);

						if (conditional)
						{
							// start new one
							optionText = lineText.Substring(usingTabs+2, lineText.Length-(usingTabs+2));
							optionText = optionText.Trim();
							options.Add(new Dialogue.Option(optionText, ""));
							lineBlock = new LineBlock();
						}
						else
						{
							lineBlock = null;
						}
						// keep going
					}
					else if (currentLine.tabs <= usingTabs)
					{
						// end here
						optionLineBlocks.Add(lineBlock);
						break;
					}
					else
					{
						if (lineBlock != null)
						{
							Debug.Log("adding line to block: " + currentLine.text);
							lineBlock.lines.Add(currentLine);
						}
						else
						{
							Debug.Log("skipping line: " + currentLine.text);
						}
					}
					i++;
				}


				optionLineBlocks.Add(lineBlock);

				yield return StartCoroutine(implementation.RunOptions(options));

				options.Clear();

				yield return StartCoroutine(RunLines(optionLineBlocks[currentOption].lines));

			}
			else
			{
				if (parseIfState == ParseIfState.AddLines)
				{
					yield return StartCoroutine(RunLine(lines[i], startLineIndex));
				}
				i++;
			}
		}
	}

	bool ParseOpVal(string text, string op, ref string varName, ref int val)
	{
		int opIndex = text.IndexOf(op);
		if (opIndex != -1)
		{
			int dollarSignIndex = text.IndexOf('$');
			string front = text.Substring(dollarSignIndex, opIndex - dollarSignIndex).Trim();
			varName = ParseVariableName(front);
			val = int.Parse(text.Substring(opIndex+op.Length, text.Length - (opIndex+op.Length)), System.Globalization.CultureInfo.InvariantCulture);
			return true;
		}
		return false;
	}

	IEnumerator RunCommand(string line)
	{
		//Debug.Log("RunCommand: " + line);
		string[] tokens = line.Split(' ');
		bool ranStandardCommand = false;
		if (tokens.Length > 0)
		{
			if (IsString(tokens[0], "set"))
			{
				int val = 0;
				string varName = "";
				if (line.IndexOf("\"") != -1)
				{
					int stringStart = line.IndexOf("\"")+1;
					int stringEnd = line.LastIndexOf("\"");
					string bit = line.Substring(stringStart, (stringEnd - stringStart));
					Debug.Log("read set string value: " + bit);
					implementation.SetString(varName, bit);
				}
				else if (ParseOpVal(line, "+=", ref varName, ref val))
					implementation.AddToInteger(varName, val);
				else if (ParseOpVal(line, "-=", ref varName, ref val))
					implementation.AddToInteger(varName, -val);
				else if (ParseOpVal(line, "*=", ref varName, ref val))
					implementation.SetInteger(varName, implementation.GetInteger(varName) * val);
				else if (ParseOpVal(line, "/=", ref varName, ref val))
					implementation.SetInteger(varName, implementation.GetInteger(varName) / val);
				else if (ParseOpVal(line, "=", ref varName, ref val))
					implementation.SetInteger(varName, val);
				else if (ParseOpVal(line, " to ", ref varName, ref val))
					implementation.SetInteger(varName, val);
				ranStandardCommand = true;
			}
			else if (IsString(tokens[0], "end"))
			{
				implementation.SetInteger(filename + "_" + runningNode, -1);
			}
		}
		if (!ranStandardCommand)
		{
			yield return StartCoroutine(implementation.RunCommand(line));
		}
	}

	public IEnumerator RunLine(Line lineObject, int startLineIndex)
	{
		Debug.LogWarning("Running line: " + lineObject.text);
		while (implementation.IsPaused())
		{
			yield return null;
		}

		if (varLineIndex != "" && varLineIndex != null)
			implementation.SetInteger(varLineIndex, lineObject.index);

		string commandLine = "";
		// run all macros
		// run them
		//line = ProcessLine(line, "<<", ">>", ProcessMacro);
		var line = lineObject.text.Trim();
		// if this line isn't an options line
		if (line.IndexOf("[[") == -1)
		{
			//Debug.Log("the LINE CHECK: " + line);
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
					
					//string name = line.Substring(0, colon);
					line = line.Substring(colon+1, line.Length - (colon + 1));
				}
			}
			else
			{
				//Debug.LogWarning("is command line!");
				int commandEnd = line.IndexOf(">>");
				commandLine = line.Substring(commandStart+2, commandEnd - (commandStart+2));
				line = line.Substring(0, commandStart) + line.Substring(commandEnd+2, line.Length - (commandEnd+2));//line.Replace(commandLine, "");
			}

			//Debug.Log("character name [" + characterName + "]");

			line = line.Trim();

			if (line.Length != 0)
			{
				line = implementation.Parse(characterName, line);
				line = line.Trim();
				if (line.Length > 0)
				{
					if (lineObject.index >= startLineIndex)
					{
						//Debug.LogWarning("lineObject.index: " + lineObject.index + " startLineIndex: " + startLineIndex);
						yield return StartCoroutine(implementation.Say(characterName, line));
					}
				
					if (lastCharacterName != characterName)
						lastCharacterName = characterName;
				}
			}
			
			if (varLineIndex != "" && varLineIndex != null)
				implementation.SetInteger(varLineIndex, lineObject.index+1);
			
			if (lineObject.index >= startLineIndex)
			{
				//Debug.LogWarning("lineObject.index: " + lineObject.index + " startLineIndex: " + startLineIndex);
				yield return StartCoroutine(RunCommand(commandLine));
			}

			//yield return null;
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

	int nestedRunTexts = 0;
	IEnumerator DoRunText(Node currentNode, int startLineIndex = 0)
	{
		lastNodeTitle = currentNode.title;
		if (varNodeTitle != "" && varNodeTitle != null)
			implementation.SetString(varNodeTitle, currentNode.title);

		nestedRunTexts++;
		string text = currentNode.text;
		Visit(currentNode.title);
		Debug.Log("Running node [" + currentNode.title + "] with text [" + text + "]");

		running = true;
		characterName = "";

		string gotoNode = "";
		string[] lines = text.Split('\n');

		options.Clear();

		// edit lines array to put commands on separate lines
		// to make parsing simpler later on
		List<Line> sortedLines = new List<Line>();
		int c = 0;
		int lineIndex = 0;
		for (int i = 0; i < lines.Length; i++)
		{
			string line = lines[i];

			c++;
			if (c > 2560)
				break;

			/*
			while (line.IndexOf("<<") != -1)
			{
				string front = "", center = "";
				int commandLineStart = line.IndexOf("<<");
				int commandLineEnd = line.IndexOf(">>");
				if (commandLineStart == -1 || commandLineEnd == -1)
					break;

				int end = commandLineEnd + 2;

				front = line.Substring(0, commandLineStart);//.Trim();
				if (front.Trim() != "")
				{
					sortedLines.Add(new Line(front, lineIndex, GetNumLeadingTabSpaces(front)));
					lineIndex++;
				}
				
				center = line.Substring(commandLineStart, end - commandLineStart).Trim();
				if (center != "")
				{
					sortedLines.Add(new Line(center, lineIndex, 0));
					lineIndex++;
				}

				line = line.Substring(end, line.Length - end).Trim();
			}
			*/
			
			if (line != "")
			{
				sortedLines.Add(new Line(line, lineIndex, GetNumLeadingTabSpaces(line)));
				lineIndex++;
			}
		}

		// MACROS

		// parse ifs
		yield return StartCoroutine(RunLines(sortedLines, startLineIndex));

		// skip options select if we only have one option and it has no text
		if (options.Count == 1 && options[0].text == "")
		{
			gotoNode = options[0].nodeTitle;
			options.Clear();
		}
		else if (options.Count > 0)
		{
CantFindNodeLoopPoint:

			yield return StartCoroutine(implementation.RunOptions(options));

			currentNode = GetNode(options[currentOption].nodeTitle);
			if (currentNode != null)
			{
				StartCoroutine(DoRunText(currentNode));
			}
			else
			{
				Debug.LogError("Could not find node: " + options[currentOption].nodeTitle);
				goto CantFindNodeLoopPoint;
			}
		}

		if (gotoNode != "")
		{
			this.currentNode = GetNode(gotoNode);
			if (this.currentNode != null)
			{
				gotoNode = "";
				//Debug.LogWarning("Running node: " + currentNode.title);
				yield return StartCoroutine(DoRunText(this.currentNode));
			}
		}

		nestedRunTexts--;

		Debug.LogWarning("nestedRunTexts is now: " + nestedRunTexts);

		if (nestedRunTexts <= 0)
		{
			Debug.LogWarning("Calling EndText...");
        	yield return StartCoroutine(implementation.EndText());

        	Debug.LogWarning("setting running to false");
			running = false;

			if (varLineIndex != "" && varLineIndex != null)
				implementation.SetInteger(varLineIndex, -1);
		}	
	}

	int GetNumLeadingTabSpaces(string s)
	{
		int numTabs = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == ' ')
				numTabs++;
			else
				break;
		}
		return numTabs;
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
			// TODO: parse variables, return result
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
		{
			Debug.LogError("Invalid variable name: " + bit);
		}
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
		return implementation.GetInteger(var);
	}
}
