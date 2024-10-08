using GameCodersToolkit;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GameCodersToolkitShared.DataReferenceFinderModule.CodeLensTagging
{
	[Flags]
	enum CustomCodeElementKinds
	{
		DataReference = 1 << 24
	}

	class ChangedLineTracker
	{
		public void UpdateLine(int lineNumber)
		{
			FirstChangedLine = Math.Min(FirstChangedLine, lineNumber);
			LastChangedLine = Math.Max(LastChangedLine, lineNumber);
		}

		public int FirstChangedLine { get; private set; } = int.MaxValue;
		public int LastChangedLine { get; private set; } = -1;
	}

	struct ParseResultLine
	{
		public int LineNumber;
		public string GuidString;
		public string Identifier;
	}

	class ParseResultLineComparer : IComparer<ParseResultLine>
	{
		public int Compare(ParseResultLine a, ParseResultLine b)
		{
			return a.LineNumber - b.LineNumber;
		}
	}

	[Export(typeof(ITaggerProvider))]
	[TagType(typeof(ICodeLensTag))]
	[ContentType("code")]
	internal sealed class CodeLensTaggerProvider : ITaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
		{
			//create a single tagger for each buffer.
			Func<ITagger<T>> sc = delegate () { return new CodeLensTagger(buffer) as ITagger<T>; };
			return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
		}
	}

	public class DataReferenceCodeLensTag : ICodeLensTag3, ICodeLensDescriptorContextProvider
	{
		public DataReferenceCodeLensTag()
		{
			Properties = new CodeLensTagProperties(true);
		}

		public void DisconnectCodeLensTag()
		{
			Disconnected?.Invoke(this, new EventArgs());
		}

		public ICodeLensDescriptor Descriptor { get; set; }

		public CodeLensTagProperties Properties { get; set; }

		public ICodeLensDescriptorContextProvider DescriptorContextProvider => this;

		public event EventHandler Disconnected;

		public string DataReferenceIdentifier { get; set; }

		public Task<CodeLensDescriptorContext> GetCurrentContextAsync()
		{
			var descriptor = new CodeLensDescriptorContext(Descriptor.ApplicableSpan);
			descriptor.Properties.Add("DataReferenceIdentifier", DataReferenceIdentifier);
			return Task.FromResult(descriptor);
		}
	}

	public class DataReferenceCodeLensDescriptor : ICodeLensDescriptor
	{
		public string FilePath { get; set; }

		public Guid ProjectGuid { get; set; }

		public string ElementDescription { get; set; }

		public Span? ApplicableSpan { get; set; }

		public CodeElementKinds Kind { get; set; }
	}

	internal sealed class CodeLensTagger : ITagger<ICodeLensTag>
	{
		public CodeLensTagger(ITextBuffer inTextBuffer)
		{
			m_textBuffer = inTextBuffer;
			m_textBuffer.Changed += OnTextBufferChanged;
			m_currentSnapshot = m_textBuffer.CurrentSnapshot;

			GameCodersToolkitPackage.PackageLoaded += OnPackageLoaded;
			if (GameCodersToolkitPackage.IsLoaded)
			{
				ParseEntireBuffer();
			}
		}

		private void OnPackageLoaded(object sender, EventArgs e)
		{
			ParseEntireBuffer();;
		}

		private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			foreach (var change in e.Changes)
			{
				ParseBufferSpan(e.Before, e.After, change.OldSpan, change.NewSpan, change.LineCountDelta);
			}
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		private DataReferenceCodeLensDescriptor GetCodeLensDescriptorForSpan(SnapshotSpan snapshotSpan)
		{
			return new DataReferenceCodeLensDescriptor
			{
				ElementDescription = "DataReference",
				FilePath = m_textBuffer.GetFileName(),
				Kind = (CodeElementKinds)CustomCodeElementKinds.DataReference,
				ProjectGuid = Guid.Empty,
				ApplicableSpan = snapshotSpan
			};
		}

		private SnapshotSpan GetLineSpanWithoutLeadingWhitespace(ITextSnapshotLine lineSnapshot)
		{
			if (lineSnapshot.Extent.IsEmpty)
			{
				return lineSnapshot.Extent;
			}

			string lineText = lineSnapshot.GetText();
			int startOffset = 0;
			while (startOffset < lineSnapshot.Length && char.IsWhiteSpace(lineText[startOffset]))
			{
				startOffset++;
			}

			SnapshotPoint startPoint = lineSnapshot.Start + startOffset;
			return new SnapshotSpan(startPoint, lineSnapshot.End);
		}

		public IEnumerable<ITagSpan<ICodeLensTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0)
				yield break;

			SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(m_currentSnapshot, SpanTrackingMode.EdgeExclusive);
			int startLineNumber = entire.Start.GetContainingLine().LineNumber;
			int endLineNumber = entire.End.GetContainingLine().LineNumber;

			foreach (var guidResultLine in m_guidResultLines)
			{
				if (guidResultLine.LineNumber > endLineNumber)
					yield break;

				if (guidResultLine.LineNumber < startLineNumber)
					continue;

				TagSpan<ICodeLensTag> yieldResult = null;
				try
				{
					ITextSnapshotLine lineSnapshot = entire.Snapshot.GetLineFromLineNumber(guidResultLine.LineNumber);
					SnapshotSpan tagSpan = GetLineSpanWithoutLeadingWhitespace(lineSnapshot);
					if (tagSpan.IsEmpty)
						continue;

					DataReferenceCodeLensTag codeLensTag = new DataReferenceCodeLensTag();
					codeLensTag.DataReferenceIdentifier = guidResultLine.GuidString;
					codeLensTag.Descriptor = GetCodeLensDescriptorForSpan(tagSpan);

					yieldResult = new TagSpan<ICodeLensTag>(tagSpan, codeLensTag);
				}
				catch (Exception ex)
				{
					ThreadHelper.JoinableTaskFactory.Run(async delegate { await DiagnosticUtils.ReportExceptionFromExtensionAsync("[CodeLensTagger] Exception Getting Tags", ex); });
					yield break;
				}

				if (yieldResult != null)
				{
					yield return yieldResult;
				}
				else
				{
					yield break;
				}
			}

			yield break;
		}

		private void ParseEntireBuffer()
		{
			ITextSnapshot snapshot = m_textBuffer.CurrentSnapshot;
			m_guidResultLines.Clear();
			m_identifierDefinitionLines.Clear();
			ParseBufferSpan(snapshot, snapshot, new Span(), new Span(0, snapshot.Length), 0);
		}

		private void ParseBufferSpan(ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot, Span oldSpan, Span newSpan, int lineDelta)
		{
			if (!GameCodersToolkitPackage.IsLoaded)
				return;

			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();

				ChangedLineTracker changeTracker = new ChangedLineTracker();

				// Remove all lines we found in the old span
				List<string> identifierToReparse = RemoveChangedLineEntries(oldSnapshot, oldSpan, changeTracker);

				int guidResultCountBefore = m_guidResultLines.Count;
				int identifierResultCountBefore = m_identifierDefinitionLines.Count;

				SnapshotSpan newSnapshotSpan = new SnapshotSpan(newSnapshot, newSpan);
				ParseLinesInSnapshotSpan(newSnapshotSpan, changeTracker, out Dictionary<string, List<int>> identifiersToResolve);

				// Offset all lines we did not change but come after the new text span by the line delta of the change so our results stay attached to the correct line
				ApplyLineChangeDelta(lineDelta, newSnapshotSpan.Start.GetContainingLine().LineNumber, m_guidResultLines, guidResultCountBefore, changeTracker);
				ApplyLineChangeDelta(lineDelta, newSnapshotSpan.Start.GetContainingLine().LineNumber, m_identifierDefinitionLines, identifierResultCountBefore, changeTracker);

				ReparseChangedIdentifier(identifierToReparse, guidResultCountBefore, identifiersToResolve, changeTracker);

				// TODO resolve remaining guid identifiers with corresponding file

				// Sort all results by their line number again.
				ParseResultLineComparer resultLineComparer = new ParseResultLineComparer();
				m_guidResultLines.Sort(resultLineComparer);
				m_identifierDefinitionLines.Sort(resultLineComparer);

				stopwatch.Stop();
#if DEBUG
				GameCodersToolkitPackage.ExtensionOutput?.WriteLine($"Parsing Text Buffer took {stopwatch.ElapsedMilliseconds}ms");
#endif

				m_currentSnapshot = newSnapshot;
				// We changed atleast one line. Make sure to notify about it
				if (changeTracker.FirstChangedLine != int.MaxValue)
				{
					ITextSnapshotLine firstSnapshotLine = newSnapshot.GetLineFromLineNumber(changeTracker.FirstChangedLine);
					ITextSnapshotLine lastSnapshotLine = newSnapshot.GetLineFromLineNumber(changeTracker.LastChangedLine);
					TagsChanged?.Invoke(this,
						new SnapshotSpanEventArgs(
							new SnapshotSpan(firstSnapshotLine.Start, lastSnapshotLine.End)));
				}
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory.Run(async delegate { await DiagnosticUtils.ReportExceptionFromExtensionAsync($"[CodeLensTagger] Exception parsing TextBufferSpan: {oldSnapshot.TextBuffer.GetFileName()}", ex); });
			}
		}

		// Apply the line delta of a change to current results
		private void ApplyLineChangeDelta(int lineDelta, int changeStartLine, List<ParseResultLine> lineResults, int resultCountBefore, ChangedLineTracker changeTracker)
		{
			if (lineDelta != 0)
			{
				for (int i = 0; i < resultCountBefore; ++i)
				{
					if (lineResults[i].LineNumber >= changeStartLine)
					{
						ParseResultLine parseResultLine = lineResults[i];
						changeTracker.UpdateLine(parseResultLine.LineNumber);

						parseResultLine.LineNumber += lineDelta;
						lineResults[i] = parseResultLine;

						changeTracker.UpdateLine(parseResultLine.LineNumber);
					}
				}
			}
		}

		// Adjust all old guid results for which the identifier was potentially modified with a potentially changed guid
		// Remove all old guid results for which the identifier no longer exists
		private void ReparseChangedIdentifier(List<string> identifierToReparse, int guidResultCountBefore, Dictionary<string, List<int>> identifiersToResolve, ChangedLineTracker changeTracker)
		{
			if (identifierToReparse.Count == 0)
				return;

			for (int i = guidResultCountBefore - 1; i >= 0; --i)
			{
				ParseResultLine guidResult = m_guidResultLines[i];
				if (identifierToReparse.Contains(guidResult.Identifier))
				{
					ParseResultLine identifierDef = m_identifierDefinitionLines.Find((element) => element.Identifier == guidResult.Identifier);
					if (!string.IsNullOrEmpty(identifierDef.Identifier))
					{
						if (identifierDef.GuidString != guidResult.GuidString)
						{
							guidResult.GuidString = identifierDef.GuidString;
							m_guidResultLines[i] = guidResult;
							changeTracker.UpdateLine(guidResult.LineNumber);
						}
					}
					else
					{
						identifiersToResolve.GetOrCreate(guidResult.Identifier).Add(guidResult.LineNumber);
						changeTracker.UpdateLine(guidResult.LineNumber);
						m_guidResultLines.RemoveAtSwap(i);
					}
				}
			}
		}

		// Parse all lines in the given snapshot span and search for guids and guid identifiers in them. Results are added to m_guidLineResults and m_identifierDefinitionLines
		private void ParseLinesInSnapshotSpan(SnapshotSpan snapshotSpan, ChangedLineTracker changeTracker, out Dictionary<string, List<int>> identifiersToResolve)
		{
			identifiersToResolve = new Dictionary<string, List<int>>();
			List<string> guidFieldIdentifiers = GameCodersToolkitPackage.DataLocationsConfig.GetGuidFieldIdentifiers();
			ITextSnapshot snapshot = snapshotSpan.Snapshot;
			int newStartLine = snapshotSpan.Start.GetContainingLine().LineNumber;
			int newEndLine = snapshotSpan.End.GetContainingLine().LineNumber;

			for (int lineToParse = newStartLine; lineToParse <= newEndLine; lineToParse++)
			{
				ITextSnapshotLine lineSnapshot = snapshot.GetLineFromLineNumber(lineToParse);

				string identifierWord = TextUtilFunctions.GetGuidIdentifierWordInLine(lineSnapshot, guidFieldIdentifiers);
				string lineGuidString = TextUtilFunctions.FindGuidInLineSnapshot(lineSnapshot);
				if (!string.IsNullOrEmpty(lineGuidString))
				{
					if (guidFieldIdentifiers.Count != 0)
					{
						if (!string.IsNullOrWhiteSpace(identifierWord))
						{
							m_identifierDefinitionLines.Add(new ParseResultLine { GuidString = lineGuidString, Identifier = identifierWord, LineNumber = lineToParse });
						}
					}
					m_guidResultLines.Add(new ParseResultLine { GuidString = lineGuidString, LineNumber = lineToParse, Identifier = identifierWord });
					changeTracker.UpdateLine(lineToParse);

					if (identifiersToResolve.TryGetValue(identifierWord, out List<int> linesToResolve))
					{
						foreach (int i in linesToResolve)
						{
							m_guidResultLines.Add(new ParseResultLine { GuidString = lineGuidString, LineNumber = i, Identifier = identifierWord });
							changeTracker.UpdateLine(i);
						}
						identifiersToResolve.Remove(identifierWord);
					}
				}
				else if (!string.IsNullOrWhiteSpace(identifierWord))
				{
					ParseResultLine identifierDef = m_identifierDefinitionLines.Find((element) => element.Identifier == identifierWord);
					if (!string.IsNullOrEmpty(identifierDef.Identifier))
					{
						m_guidResultLines.Add(new ParseResultLine { GuidString = identifierDef.GuidString, LineNumber = lineToParse, Identifier = identifierWord });
						changeTracker.UpdateLine(lineToParse);
					}
					else
					{
						identifiersToResolve.GetOrCreate(identifierWord).Add(lineToParse);
					}
				}
			}
		}

		// Remove all current guid line results and identifier definitions for all lines in a change
		private List<string> RemoveChangedLineEntries(ITextSnapshot oldSnapshot, Span oldSpan, ChangedLineTracker changeTracker)
		{
			SnapshotSpan oldSnapshotSpan = new SnapshotSpan(oldSnapshot, oldSpan);
			int oldStartLine = oldSnapshotSpan.Start.GetContainingLine().LineNumber;
			int oldEndLine = oldSnapshotSpan.End.GetContainingLine().LineNumber;

			Span guidResultsToRemove = GetRangeToRemove(oldStartLine, oldEndLine, ref m_guidResultLines);
			Span removedGuidLinesSpan = new Span();
			if (!guidResultsToRemove.IsEmpty)
			{
				int startLineToRemove = m_guidResultLines[guidResultsToRemove.Start].LineNumber;
				int endLineToRemove = m_guidResultLines[guidResultsToRemove.End - 1].LineNumber;
				removedGuidLinesSpan = new Span(startLineToRemove, endLineToRemove - startLineToRemove);
				m_guidResultLines.RemoveRange(guidResultsToRemove.Start, guidResultsToRemove.Length);
			}

			Span identifierDefsToRemove = GetRangeToRemove(oldStartLine, oldEndLine, ref m_identifierDefinitionLines);
			List<string> identifierToReparse = new List<string>();
			for (int i = identifierDefsToRemove.Start; i < identifierDefsToRemove.End; ++i)
			{
				identifierToReparse.Add(m_identifierDefinitionLines[i].Identifier);
			}
			m_identifierDefinitionLines.RemoveRange(identifierDefsToRemove.Start, identifierDefsToRemove.Length);

			if (!removedGuidLinesSpan.IsEmpty)
			{
				changeTracker.UpdateLine(removedGuidLinesSpan.Start);
				changeTracker.UpdateLine(removedGuidLinesSpan.End);
			}

			return identifierToReparse;
		}

		// Returns the index based span to remove from the given lines. The first index is inclusive while the second is exclusive
		// If no elements should get removed returns an empty span
		private Span GetRangeToRemove(int startLine, int endLine, ref List<ParseResultLine> resultLines)
		{
			if (startLine > endLine)
				return new Span();

			var resultsComparer = new ParseResultLineComparer();
			if (startLine == endLine)
			{
				ParseResultLine removeLineResult = new ParseResultLine { LineNumber = startLine };
				int indexToRemove = resultLines.BinarySearch(removeLineResult, resultsComparer);
				if (indexToRemove >= 0)
					return new Span(indexToRemove, 1);

				return new Span();
			}

			ParseResultLine startSearchLine = new ParseResultLine { LineNumber = startLine };
			int indexFirstLine = resultLines.BinarySearch(startSearchLine, resultsComparer);
			if (indexFirstLine < 0)
				indexFirstLine = ~indexFirstLine;

			if (indexFirstLine < resultLines.Count)
			{
				ParseResultLine endSearchLine = new ParseResultLine { LineNumber = endLine };
				int indexLastLine = resultLines.BinarySearch(endSearchLine, resultsComparer);

				// If the binary search hits an element we want to remove this element aswell
				if (indexLastLine >= 0)
				{
					indexLastLine += 1;
				}
				else
				{
					indexLastLine = ~indexLastLine;
				}

				return new Span(indexFirstLine, indexLastLine - indexFirstLine);
			}

			return new Span();
		}

		List<ParseResultLine> m_guidResultLines = new List<ParseResultLine>();
		List<ParseResultLine> m_identifierDefinitionLines = new List<ParseResultLine>();

		ITextBuffer m_textBuffer;
		ITextSnapshot m_currentSnapshot;
	}
}
