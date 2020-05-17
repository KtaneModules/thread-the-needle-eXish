using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Random = UnityEngine.Random;

public class ThreadTheNeedle : MonoBehaviour
{
	#region Types

	/// <summary>
	/// What kind of hole is at a point on the wheel
	/// </summary>
	private enum Hole {
		None,    // empty
		Circle,  // good hole!
		Triangle // bad hole
	}

	/// <summary>
	/// The logical representation of a wheel's pattern.
	/// So, where the holes are and what characters are on it.
	/// </summary>
	private class WheelPattern {
		public static int BIGGEST_SIZE = 8;

		public char[] Chars { get; private set; }
		public Hole[] Holes { get; private set; }
		public int Size { get; private set; }

		public WheelPattern(char[] chars, Hole[] holes) {
			if (chars.Length != holes.Length)
				throw new ArgumentException("Chars and Holes have different lengths!");
			if (BIGGEST_SIZE % chars.Length != 0)
				throw new ArgumentException(string.Format("The character count ought to evenly go into {0} but length {1} found!", BIGGEST_SIZE, chars.Length));
			if (BIGGEST_SIZE % holes.Length != 0)
				throw new ArgumentException(string.Format("The hole count ought to evenly go into {0} but length {1} found!", BIGGEST_SIZE, holes.Length));

			Chars = chars;
			Holes = holes;
			Size = chars.Length; // it doesn't matter which i pull from really...
		}
		public WheelPattern(string charString, string holeString) : this(charString.ToCharArray(), setupHoleStringForConstructor(holeString)) {}

		private static Hole[] setupHoleStringForConstructor(string holeString) {
			Hole[] holes = new Hole[holeString.Length];
			for (int c = 0; c < holeString.Length; c++) {
				char ch = holeString[c];
				if (ch == '.')
					holes[c] = Hole.None;
				else if (ch == 'O')
					holes[c] = Hole.Circle;
				else if (ch == '^')
					holes[c] = Hole.Triangle;
				else
					throw new ArgumentException("Unknown character when making hole string: " + ch);
			}
			return holes;
		}
	}

	/// <summary>
	/// A logical representation of a Wheel.
	/// </summary>
	private class Wheel {
		public WheelPattern Pattern { get; private set; }
		public int Index { get; private set; }
		private GameObject myWheel;

		public Wheel(WheelPattern pattern, int index, GameObject wheel) {
			Pattern = pattern;
			Index = index;
			myWheel = wheel;

			UpdateLabel();
		}

		// Incrementing == pressing down == turning counter-clockwise 
		public void SpinUp() {
			Index++;
			if (Index >= Pattern.Size) Index = 0;
		}

		// Decrementing == pressing up == turning clockwise
		public void SpinDown() {
			Index--;
			if (Index < 0) Index = Pattern.Size - 1;
		}

		public void UpdateLabel() {
			if (myWheel != null)
				myWheel.GetComponentInChildren<TextMesh>().text = Pattern.Chars[Index].ToString();
		}

		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			sb.Append('[');
			for (int i = 0; i < Pattern.Chars.Length; i++) {
				sb.Append(Pattern.Chars[(i + Index) % Pattern.Chars.Length]);
			}
			sb.Append("] [");
			for (int i = 0; i < Pattern.Chars.Length; i++) {
				var h = Pattern.Holes[(i + Index) % Pattern.Holes.Length];
				if (h == Hole.None) sb.Append(' ');
				else if (h == Hole.Circle) sb.Append('O');
				else sb.Append('^');
			}
			sb.Append(']');
			return sb.ToString();
		}
	}

	#endregion Types

	private static WheelPattern[] POSSIBLE_WHEELS = {
		new WheelPattern("+-]!<0#<", "O.^^^.OO"),
        new WheelPattern("$)2=*/>!", "^.^.O^O."),
        new WheelPattern("78/?(-7~", "O.O..^.^"),
        new WheelPattern("96394?#!", "O^..^^OO"),

        new WheelPattern("+!2#!@*@", "^^O..OO."),
        new WheelPattern("%*&50$52", "^..O^O.."),
        new WheelPattern("%*!%~*+$", "O^.^O.^^"),
        new WheelPattern("[%?/1{]}", "^.^..O^^"),

        new WheelPattern("1@3!2312", "O^..^^.O"),
        new WheelPattern("%^O<#>^(", ".^.^OOO."),
        new WheelPattern("->-~>@<%", ".O...^.^"),
        new WheelPattern("{]}[%?/$", ".^..OOO.")
	};

	// The bonus wheels!
	private static WheelPattern[,] BONUS_WHEELS = {
		// [w, u]
		{ new WheelPattern("12345678", "..O..^^O"), new WheelPattern("12345678", "..^O..^^"), new WheelPattern("12345678", "..^^.^.O") },
		{ new WheelPattern("12345678", "^^.O.^^^"), new WheelPattern("12345678", "....OOOO"), new WheelPattern("12345678", "..^OO...") },
		{ new WheelPattern("12345678", "^...OOOO"), new WheelPattern("12345678", ".^^O..^^"), new WheelPattern("12345678", "...^O^^.") },
		{ new WheelPattern("12345678", ".^O^O^.O"), new WheelPattern("12345678", ".O^.^.O^"), new WheelPattern("12345678", "..^^.OOO") }
	};

	// Instance data

	bool isActive = false;
	bool moduleSolved = false;
	public GameObject[] wheelBodies;
	public KMSelectable[] upButtons;
	public KMSelectable[] downButtons;
	public KMSelectable submitButton;
	private KMBombInfo bombInfo;

	private Wheel[] wheels;

	// Use this for initialization
	void Start () {
		bombInfo = GetComponent<KMBombInfo>();
		wheels = new Wheel[wheelBodies.Length];

		var sb = new StringBuilder();

		// Generate the wheels
		var bonusPattern = GetBonusWheel().Pattern;
		var possibleSolution = false;
		var patterns = new WheelPattern[wheelBodies.Length + 1];
		var oops_help_infinite_loop = false;
		do {
			Debug.Log("Making the wheel patterns...");
			for (int c = 0; c < wheelBodies.Length; c++) {
				var premadeWheelIdx = Random.Range(0, POSSIBLE_WHEELS.Length);
				WheelPattern pattern = POSSIBLE_WHEELS[premadeWheelIdx];
				patterns[c] = pattern;
			}
			patterns[patterns.Length - 1] = bonusPattern;
			possibleSolution = TestIfWheelComboIsPossible(patterns);
		} while (!possibleSolution && !oops_help_infinite_loop);
		// Nice, now make them into real wheels
		for (int c = 0; c < wheelBodies.Length; c++) {
			Wheel wheel = new Wheel(patterns[c], Random.Range(0, patterns[c].Size), wheelBodies[c]);

			// Make the buttons spin the wheel
			int saveIndex = c; // Save the value of c for the closure
			upButtons[c].OnInteract += delegate () {
				PressSpinnyButton(saveIndex, true);
				return false; // don't bubble down to children
			};
			downButtons[c].OnInteract += delegate () {
				PressSpinnyButton(saveIndex, false);
				return false;
			};

			wheels[c] = wheel;

			sb.Append("Wheel #"); sb.Append(c); sb.Append(" : "); sb.Append(wheel.ToString()); sb.Append('\n');
		}

		// Make the submit button do a thing
		submitButton.OnInteract += delegate () {
			GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			GetComponent<KMSelectable>().AddInteractionPunch();
			if (moduleSolved) return false; // done

			var testTheseWheels = new Wheel[wheels.Length + 1];
			wheels.CopyTo(testTheseWheels, 0);
			testTheseWheels[wheels.Length] = GetBonusWheel();
			var _sb = new StringBuilder();
			foreach (Wheel w in testTheseWheels) {
				_sb.Append(w.ToString());
				_sb.Append('\n');
			}
			Debug.Log(_sb.ToString());
			var success = TestWheelCombo(testTheseWheels);
			if (success)
				GetComponent<KMBombModule>().HandlePass(); // yay
			else
				GetComponent<KMBombModule>().HandleStrike(); // boo
			return false;
		};

		// Debug info!
		Debug.Log("Thread the Needle:\n" + sb.ToString());

		GetComponent<KMBombModule>().OnActivate += () => isActive = true;
	}

	private Wheel GetBonusWheel() {
		int u = 0;
		var serial = bombInfo.GetSerialNumber();
		foreach (char c in serial) {
			if ("13579".Contains(c.ToString()))
				u++;
			else if ("AEIOU".Contains(c.ToString()))
				u += 2;
		}
			
		int w = bombInfo.GetBatteryCount();
		var inds = bombInfo.GetOnIndicators();
		foreach (var _ in inds) w--;

		int uIndex, wIndex;
		if (u == 1 || u == 3)
			uIndex = 0;
		else if (u == 2 || u == 4 || u == 5)
			uIndex = 1;
		else
			uIndex = 2;
		if (w <= 0)
			wIndex = 0;
		else if (w == 1 || w == 2)
			wIndex = 1;
		else if (w == 3)
			wIndex = 2;
		else
			wIndex = 3;

		// Get the wheel
		WheelPattern pattern = BONUS_WHEELS[wIndex, uIndex]; // No mating

		// Spinny amount
		int spin = bombInfo.GetStrikes() - bombInfo.GetSolvedModuleNames().Count;

		// Wheelio
		return new Wheel(pattern, spin, null);
	}

	bool TestWheelCombo(Wheel[] wheels) {
		Debug.Log("Testing wheels...");
		for (int absoluteRoundPos = 0; absoluteRoundPos < WheelPattern.BIGGEST_SIZE; absoluteRoundPos++) {
			// RoundPos represents like numbers on a clock (except this increases counter-clockwise)
			bool allCircle = true;
			bool allTriangle = true;
			foreach(var wheel in wheels) {
				var roundPos = absoluteRoundPos + wheel.Index;
				// Get the hole at this position in the wheel.
				// Remember things might not have all the holes.
				// In that case it's assumed they're evenly spaced.
				Hole hole = wheel.Pattern.Holes[roundPos % WheelPattern.BIGGEST_SIZE];

				if (hole == Hole.None) {
					allCircle = false;
					allTriangle = false;
					break; // No need to continue
				}
				if (hole != Hole.Circle) allCircle = false;
				if (hole != Hole.Triangle) allTriangle = false;
			}

			if (allCircle) {
				Debug.Log("Beat the module!");
				return true;
			}
			if (allTriangle) {
				Debug.Log("Failed due to a straight line of triangles.");
				return false;
			}
			// Otherwise, there was neither.
		}
		Debug.Log("Failed due to no straight line of circles.");
		return false; // We never found all circles.
	}


	bool TestIfWheelComboIsPossible(WheelPattern[] patterns) {
		// This combination of wheels has to have at least one solution

		// Each index has a list of all the positions with holes on that wheel
		List<List<int>> holes = new List<List<int>>();
		foreach (var pat in patterns) {
			List<int> thisHoles = new List<int>();
			for (var c = 0; c < pat.Size; c++)
				if (pat.Holes[c] == Hole.Circle) thisHoles.Add(c);
			holes.Add(thisHoles);
		}

		const int MAX_TRIES = 100;
		for (int tries = 0; tries < MAX_TRIES; tries++) {
			// Pick a random set of OK holes
			Wheel[] test = new Wheel[patterns.Length];
			for (int c = 0; c < patterns.Length; c++) {
				var spin = holes[c].PickRandom();
				var wheel = new Wheel(patterns[c], spin, null);
				test[c] = wheel;
			}
			var success = TestWheelCombo(test);
			if (success) return true;
		}

		return false; // Nope, didn't find anything
		
	}

	void PressSpinnyButton(int idx, bool up) {
		GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
		GetComponent<KMSelectable>().AddInteractionPunch();

		Debug.Log(string.Format("Pressed spinny button #{0} with up?: {1}", idx, up));

		if (moduleSolved)
			return; // don't worry!

		if (!isActive) {
			// No! Don't play with the buttons before-hand!
			GetComponent<KMBombModule>().HandleStrike();
		} else {
			if (up)
				wheels[idx].SpinUp();
			else
				wheels[idx].SpinDown();

			wheels[idx].UpdateLabel();
		}
	}
	
	//twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"To cycle down a wheel in the module, use the command !{0} cycle [1-5] | To press the down button on a wheel, use the command !{0} press [1-5] [1-8] | To submit your answer, use the command !{0} submit";
    #pragma warning restore 414
	
	string[] ValidKeys = {"1", "2", "3", "4", "5"};
	string[] ValidSpins = {"1", "2", "3", "4", "5", "6", "7", "8"};
	
	IEnumerator ProcessTwitchCommand(string command)
	{
		string[] parameters = command.Split(' ');
		if (Regex.IsMatch(parameters[0], @"^\s*cycle\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			yield return null;
			if (parameters.Length != 2)
			{
				yield return "sendtochaterror Invalid parameter length.";
				yield break;
			}
			
			if (!parameters[1].EqualsAny(ValidKeys))
			{
				yield return "sendtochaterror Given text was not 1-5.";
				yield break;
			}
			
			for (int x = 0; x < 8; x++)
			{
				yield return "trycancel The cycle was cancelled due to a cancel request.";
				downButtons[Int32.Parse(parameters[1])-1].OnInteract();
				yield return new WaitForSecondsRealtime(1.5f);
			}
		}
		
		if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			yield return null;
			if (parameters.Length != 3)
			{
				yield return "sendtochaterror Invalid parameter length.";
				yield break;
			}
			
			if (!parameters[1].EqualsAny(ValidKeys) || !parameters[2].EqualsAny(ValidSpins))
			{
				yield return "sendtochaterror Invalid number/format on the wheel / amount of presses.";
				yield break;
			}
			
			for (int x = 0; x < Int32.Parse(parameters[2]); x++)
			{
				downButtons[Int32.Parse(parameters[1])-1].OnInteract();
				yield return new WaitForSecondsRealtime(0.1f);
			}
		}
		
		if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
		{
			yield return null;
			submitButton.OnInteract();
		}
	}
}
