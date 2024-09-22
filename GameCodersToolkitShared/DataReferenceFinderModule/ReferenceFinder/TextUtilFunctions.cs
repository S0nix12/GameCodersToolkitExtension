using Microsoft.VisualStudio.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Community.VisualStudio.Toolkit;
using System.IO;
using System.Linq;
using GameCodersToolkit.Utils;

namespace GameCodersToolkit.ReferenceFinder
{
	public static class TextUtilFunctions
	{
		public static readonly Regex FindGuidRegex = new("[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", RegexOptions.IgnoreCase);

		public static async Task<string> FindGuidUnderCaretAsync()
		{
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return string.Empty;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			return FindGuidAtSnapshotPoint(caretSnapshotPoint);
		}

		private static string FindGuidAtSnapshotPoint(in SnapshotPoint snapshotPoint)
		{
			ITextSnapshotLine lineSnapshot = snapshotPoint.GetContainingLine();
			int positionInLine = lineSnapshot.Start.Difference(snapshotPoint);
			string lineText = lineSnapshot.GetText();
			int literalEndIndex = lineText.IndexOf('"', positionInLine);
			int literalStartIndex = literalEndIndex >= 0 ? lineText.LastIndexOf('"', positionInLine) : -1;

			if (literalStartIndex >= 0 && literalEndIndex >= 0 && literalStartIndex != literalEndIndex)
			{
				string literalString = lineText.Substring(literalStartIndex, literalEndIndex - literalStartIndex);
				Match regexMatch = FindGuidRegex.Match(literalString);
				if (regexMatch.Success)
				{
					return regexMatch.Value;
				}
			}

			return string.Empty;
		}

		public static async Task<string> FindWordUnderCaretAsync()
		{
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return string.Empty;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			return FindWordAtSnapshotPoint(in caretSnapshotPoint);
		}

		private static string FindWordAtSnapshotPoint(in SnapshotPoint snapshotPoint)
		{
			ITextSnapshotLine lineSnapshot = snapshotPoint.GetContainingLine();
			int positionInLine = lineSnapshot.Start.Difference(snapshotPoint);
			string lineText = lineSnapshot.GetText();

			string foundWord = string.Empty;
			Regex findWordRegex = new(@"\w+");
			Match matchAfterCaret = findWordRegex.Match(lineText, positionInLine);
			if (matchAfterCaret.Success)
			{
				Regex findLastWord = new(@"\w+$");
				Match matchBeforeCaret = findLastWord.Match(lineText.Substring(0, positionInLine));
				if (matchBeforeCaret.Success)
				{
					foundWord = matchBeforeCaret.Value;
				}
				foundWord += matchAfterCaret.Value;
			}

			return foundWord;
		}

		private static List<string> FindWordsInLineSnapshot(ITextSnapshotLine lineSnapshot)
		{
			string lineText = lineSnapshot.GetText();
			Regex findWordsRegex = new(@"(\w+)");
			MatchCollection lineMatches = findWordsRegex.Matches(lineText);
			List<string> outWords = new List<string>();
			foreach (Match lineMatch in lineMatches)
			{
				if (lineMatch.Success)
				{
					outWords.Add(lineMatch.Value);
				}
			}
			return outWords;
		}

		public static string FindGuidInLineSnapshot(ITextSnapshotLine lineSnapshot)
		{
			string lineText = lineSnapshot.GetText();
			Match guidMatch = FindGuidRegex.Match(lineText);
			if (guidMatch.Success)
			{
				return guidMatch.Value;
			}

			return string.Empty;
		}

		public static async Task<string> SearchForGuidUnderCaretAsync()
		{
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return string.Empty;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();

			string guidString = FindGuidAtSnapshotPoint(in caretSnapshotPoint);
			if (!string.IsNullOrEmpty(guidString))
				return guidString;

			guidString = FindGuidInLineSnapshot(caretLineSnapshot);
			if (!string.IsNullOrEmpty(guidString))
				return guidString;

			List<string> guidFieldIdentifiers = GameCodersToolkitPackage.DataLocationsConfig.GetGuidFieldIdentifiers();
			if (guidFieldIdentifiers.Count == 0)
				return string.Empty;

			// First try to find the word at the carret position
			string identifierWord = FindWordAtSnapshotPoint(in caretSnapshotPoint);

			// If we did not find a word or it does not match an identifier try all words in the current line
			if (string.IsNullOrEmpty(identifierWord) 
				|| !guidFieldIdentifiers.Any(identifier => identifierWord.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) != -1))
			{
				List<string> wordsInLine = FindWordsInLineSnapshot(caretSnapshotPoint.GetContainingLine());
				if (wordsInLine.Count == 0)
					return string.Empty;

				foreach (string word in wordsInLine)
				{
					bool foundIdentifier = guidFieldIdentifiers.Any(identifier => word.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) != -1);
					if (foundIdentifier)
					{
						identifierWord = word;
						break;
					}
				}
			}

			if (string.IsNullOrEmpty(identifierWord))
				return string.Empty;

			// Find the first line in the document that contains this identifier
			foreach (var lineSnapshot in caretLineSnapshot.Snapshot.Lines)
			{
				// We reached the line under the caret no identifier can come before this and we already checked this line
				if (lineSnapshot.LineNumber == caretLineSnapshot.LineNumber)
					break;

				string lineText = lineSnapshot.GetText();
				if (lineText.Contains(identifierWord))
				{
					Match lineMatch = FindGuidRegex.Match(lineText);
					if (lineMatch.Success)
					{
						return lineMatch.Value;
					}
					break;
				}
			}

			if (string.IsNullOrEmpty(documentView.FilePath))
				return string.Empty;

			try
			{

				string activeDocumentPath = documentView.FilePath;
				string activeDocumentDir = Path.GetDirectoryName(activeDocumentPath);
				string activeDocumentPathWithoutExtension = activeDocumentDir + "\\" + Path.GetFileNameWithoutExtension(activeDocumentPath);

				string[] filePathsInDir = Directory.GetFiles(activeDocumentDir);
				foreach (var filePathInSameDir in filePathsInDir.Where(path => path.StartsWith(activeDocumentPathWithoutExtension) && path != activeDocumentPath))
				{
					string[] linesText = File.ReadAllLines(filePathInSameDir);
					foreach (string lineText in linesText)
					{
						if (lineText.Contains(identifierWord))
						{
							Match lineMatch = FindGuidRegex.Match(lineText);
							if (lineMatch.Success)
							{
								return lineMatch.Value;
							}
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception searching GUID in corresponding file",
					ex);
			}

			return string.Empty;
		}

		public static async Task<bool> HasPotentialGuidUnderCaretAsync()
		{
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return false;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();

			string guidString = FindGuidAtSnapshotPoint(in caretSnapshotPoint);
			if (!string.IsNullOrEmpty(guidString))
				return true;

			guidString = FindGuidInLineSnapshot(caretLineSnapshot);
			if (!string.IsNullOrEmpty(guidString))
				return true;

			List<string> guidFieldIdentifiers = GameCodersToolkitPackage.DataLocationsConfig.GetGuidFieldIdentifiers();
			if (guidFieldIdentifiers.Count == 0)
				return false;

			List<string> wordsInLine = FindWordsInLineSnapshot(caretSnapshotPoint.GetContainingLine());
			foreach (string word in wordsInLine)
			{
				if (guidFieldIdentifiers.Any(identifier => word.IndexOf(identifier, StringComparison.Ordinal) != -1))
					return true;
			}

			return false;
		}
	}
}
