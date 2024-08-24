using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReferenceFinder
{
	internal class LeftOverFunctions
	{
		StreamReader CreateStreamReader(string path)
		{
			FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
			//FileOptions combinedOption = (FileOptions)0x20000000;
			var readStream = new FileStream(
				path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 512, combinedOption);

			return new StreamReader(readStream);
		}

		async Task ScanFileFullAsync(string path, string guidString, TextWriter writer)
		{
			using StreamReader reader = CreateStreamReader(path);
			string fileString = reader.ReadToEnd();

			int lineCounter = 0;
			int spanStartIndex = 0;
			int lineBreakIndex = 0;

			while ((lineBreakIndex = fileString.IndexOf('\n', lineBreakIndex)) != -1)
			{
				lineCounter++;

				if (fileString.IndexOf(guidString, spanStartIndex, lineBreakIndex - spanStartIndex, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					string outputString = string.Format("Reference Found in: {0}. Line: {1}. {2}", path, lineCounter, fileString.Substring(spanStartIndex, lineBreakIndex - spanStartIndex));
					await writer.WriteLineAsync(outputString);
				}

				lineBreakIndex++;
				spanStartIndex = lineBreakIndex;
			}
		}

		void ScanFileSyncFull(string path, string guidString, TextWriter writer)
		{
			using StreamReader reader = CreateStreamReader(path);

			ReadOnlySpan<char> fileText = reader.ReadToEnd().AsSpan();
			ReadOnlySpan<char> guidSpan = guidString.AsSpan();

			int lineBreakIndex = 0;
			int lineCounter = 0;
			while ((lineBreakIndex = fileText.IndexOf('\n')) != -1)
			{
				lineCounter++;
				ReadOnlySpan<char> subString = fileText.Slice(0, lineBreakIndex);
				fileText = fileText.Slice(lineBreakIndex + 1);

				if (subString.Contains(guidSpan, StringComparison.OrdinalIgnoreCase))
				{
					string outputString = string.Format("Reference Found in: {0}. Line: {1}. {2}", path, lineCounter, subString.ToString());
					writer.WriteLine(outputString);
				}
			}
		}
	}
}
