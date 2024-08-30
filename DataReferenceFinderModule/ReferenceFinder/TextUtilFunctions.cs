using Microsoft.VisualStudio.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameCodersToolkit.ReferenceFinder
{
	public static class TextUtilFunctions
	{
		public static async Task<string> FindGuidUnderCaretAsync()
		{
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return "";

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			ITextSnapshotLine lineSnapshot = caretSnapshotPoint.GetContainingLine();
			int positionInLine = lineSnapshot.Start.Difference(caretSnapshotPoint);
			string lineText = lineSnapshot.GetText();
			int literalEndIndex = lineText.IndexOf('"', positionInLine);
			int literalStartIndex = literalEndIndex >= 0 ? lineText.LastIndexOf('"', positionInLine) : -1;

			if (literalStartIndex >= 0 && literalEndIndex >= 0 && literalStartIndex != literalEndIndex)
			{
				string literalString = lineText.Substring(literalStartIndex, literalEndIndex - literalStartIndex);
				Regex guidRegex = new Regex("[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", RegexOptions.IgnoreCase);
				Match regexMatch = guidRegex.Match(literalString);
				if (regexMatch.Success)
				{
					return regexMatch.Value;
				}
			}

			return "";
		}
	}
}
