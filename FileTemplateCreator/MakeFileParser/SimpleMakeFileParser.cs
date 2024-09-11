using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameCodersToolkit.FileTemplateCreator.MakeFileParser
{
	[DebuggerDisplay("{Name} ({Groups.Count} Groups)")]
	public class CSimpleUberFileNode : IUberFileNode
	{
		public string Name { get; set; }
		public int StartLineNumber { get; set; }
		public int EndLineNumber { get; set; }
		public List<SimpleGroupNode> Groups { get; set; } = new List<SimpleGroupNode>();

		public string GetName()
		{
			return Name;
		}

		public IEnumerable<IGroupNode> GetGroups()
		{
			return Groups;
		}
	}

	[DebuggerDisplay("{Name} ({Files.Count} Entries)")]
	public class SimpleGroupNode : IGroupNode
	{
		public string Name { get; set; }
		public int LineNumber { get; set; }
		public List<SimpleFileNode> Files { get; set; } = new List<SimpleFileNode>();

		public IEnumerable<IFileNode> GetFiles()
		{
			return Files;
		}

		public int GetLineNumber()
		{
			return LineNumber;
		}

		public string GetName()
		{
			return Name;
		}
	}

	[DebuggerDisplay("{Name}")]
	public class SimpleFileNode : IFileNode
	{
		public string Name { get; set; }
		public int LineNumber { get; set; }

		public int GetLineNumber()
		{
			return LineNumber;
		}

		public string GetName()
		{
			return Name;
		}
	}

	public class SimpleMakeFileParserConfig
	{
		public string NewUberFileString { get; set; }
		public string NewGroupString { get; set; }
		public string NewFileString { get; set; }

		public string UberFileRegex { get; set; }
		public string UberFileEndRegex { get; set; }
		public string GroupRegex { get; set; }
		public string FileRegex { get; set; }
	}

	[DebuggerDisplay("SimpleMakeFile ({UberFileEntries.Count} Uber Files)")]
	public class SimpleMakeFile : IMakeFile
	{
		public IEnumerable<IUberFileNode> GetUberFiles()
		{
			return UberFileEntries;
		}

		private void InsertFileNames(int lineNumber, IEnumerable<string> fileNames)
		{
			SimpleMakeFileParserConfig Config = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetParserConfigAs<SimpleMakeFileParserConfig>();

			foreach (string newFileName in fileNames)
			{
				string newLineString = Config.NewFileString.Replace("*", newFileName);
				string[] splitLines = newLineString.Split('\n');

				Lines.InsertRange(lineNumber, splitLines);
				lineNumber += splitLines.Length;
			}
		}

		public IMakeFile AddUberFile(string previousUberFileName, string newUberFileName)
		{
			SimpleMakeFileParserConfig Config = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetParserConfigAs<SimpleMakeFileParserConfig>();
		
			// New uber file
			int newUberFileLine = 0;

			CSimpleUberFileNode previousUberFileEntry = UberFileEntries.Where((Entry) => Entry.Name == previousUberFileName).FirstOrDefault();
			if (previousUberFileEntry != null)
			{
				newUberFileLine = previousUberFileEntry.EndLineNumber + 1;
			}

			string newUberFileString = Config.NewUberFileString;
			newUberFileString = newUberFileString.Replace("*", newUberFileName);
			string[] splitLines = newUberFileString.Split('\n');

			Lines.Insert(newUberFileLine, string.Empty);
			newUberFileLine++;
			Lines.InsertRange(newUberFileLine, splitLines);
			newUberFileLine += splitLines.Length;

			SimpleMakeFileParser parser = new SimpleMakeFileParser();
			return parser.Parse(FilePath, Lines);
		}

		public IMakeFile AddGroup(string uberFileName, string previousGroupName, string newGroupName)
		{
			SimpleMakeFileParserConfig Config = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetParserConfigAs<SimpleMakeFileParserConfig>();
			
			CSimpleUberFileNode uberFileEntry = UberFileEntries.Where((Entry) => Entry.Name == uberFileName).FirstOrDefault();
			if (uberFileEntry == null)
			{
				return this;
			}

			int newGroupLine = uberFileEntry.StartLineNumber + 1;

			SimpleGroupNode previousGroupFileEntry = uberFileEntry.Groups.Where((Group) => Group.Name == previousGroupName).FirstOrDefault();
			if (previousGroupFileEntry != null)
			{
				newGroupLine = previousGroupFileEntry.LineNumber + previousGroupFileEntry.Files.Count + 1;
			}

			string newGroupString = Config.NewGroupString;
			newGroupString = newGroupString.Replace("*", newGroupName);
			string[] splitLines = newGroupString.Split('\n');

			Lines.InsertRange(newGroupLine, splitLines);
			newGroupLine += splitLines.Length;

			SimpleMakeFileParser parser = new SimpleMakeFileParser();
			return parser.Parse(FilePath, Lines);
		}

		public IMakeFile AddFiles(string uberFileName, string groupName, string previousFileName, IEnumerable<string> newFileNames)
		{
			CSimpleUberFileNode uberFileEntry = UberFileEntries.Where((Entry) => Entry.Name == uberFileName).FirstOrDefault();
			if (uberFileEntry == null)
			{
				return this;
			}

			SimpleGroupNode groupEntry = uberFileEntry.Groups.Where((Group) => Group.Name == groupName).FirstOrDefault();
			if (groupEntry == null)
			{
				return this;
			}

			int lineToInsert = groupEntry.LineNumber + 1;
			if (!string.IsNullOrWhiteSpace(previousFileName))
			{
				SimpleFileNode regularFileEntry = groupEntry.Files.Where(Entry => Entry.Name == previousFileName).First();
				if (regularFileEntry != null)
				{
					lineToInsert = regularFileEntry.LineNumber + 1;
				}
			}

			InsertFileNames(lineToInsert, newFileNames);

			SimpleMakeFileParser parser = new SimpleMakeFileParser();
			return parser.Parse(FilePath, Lines);
		}

		public string GetOriginalFilePath()
		{
			return FilePath;
		}

		public void Save()
		{
			if (File.Exists(FilePath))
			{
				File.WriteAllLines(FilePath, Lines);
			}
		}

		public string FilePath { get; set; }
		public List<string> Lines { get; set; }
		public List<CSimpleUberFileNode> UberFileEntries { get; set; } = new List<CSimpleUberFileNode>();
	}

	public class SimpleMakeFileParser : IMakeFileParser
	{
		public IMakeFile Parse(string filePath)
		{
			return Parse(filePath, File.ReadAllLines(filePath));
		}

		public IMakeFile Parse(string originalFilePath, IEnumerable<string> lines)
		{
			try
			{
				if (File.Exists(originalFilePath))
				{
					SimpleMakeFileParserConfig Config = GameCodersToolkitPackage.FileTemplateCreatorConfig.GetParserConfigAs<SimpleMakeFileParserConfig>();
		
					SimpleMakeFile makeFile = new SimpleMakeFile();
					makeFile.FilePath = originalFilePath;
					makeFile.Lines = lines.ToList();

					CSimpleUberFileNode currentUberFile = null;
					SimpleGroupNode currentSourceGroup = null;

					for (int i = 0; i < makeFile.Lines.Count; i++)
					{
						string line = makeFile.Lines[i];

						if (currentUberFile != null)
						{
							// Check if the current line ends the uber file definition
							Match uberFileEndMatch = Regex.Match(line, Config.UberFileEndRegex);
							if (uberFileEndMatch.Success)
							{
								currentUberFile.EndLineNumber = i;
								makeFile.UberFileEntries.Add(currentUberFile);

								currentUberFile = null;
								currentSourceGroup = null;
							}
						}

						Match uberFileMatch = Regex.Match(line, Config.UberFileRegex);
						if (uberFileMatch.Success)
						{
							if (currentUberFile != null || currentSourceGroup != null)
							{
								throw new Exception($"MakeFile {originalFilePath} is malformatted in line {i}: Found nested add_sources call");
							}

							string uberFileName = uberFileMatch.Groups[1].Value;
							currentUberFile = new CSimpleUberFileNode();
							currentUberFile.Name = uberFileName;
							currentUberFile.StartLineNumber = i;

							continue;
						}

						if (currentUberFile != null)
						{
							// Check if the current line is a source group
							Match sourceGroupMatch = Regex.Match(line, Config.GroupRegex);
							if (sourceGroupMatch.Success)
							{
								string sourceGroupName = sourceGroupMatch.Groups[1].Value;

								currentSourceGroup = new SimpleGroupNode();
								currentSourceGroup.Name = sourceGroupName;
								currentSourceGroup.LineNumber = i;
								currentUberFile.Groups.Add(currentSourceGroup);

								continue;
							}

							if (currentSourceGroup != null)
							{
								// Check if it's a regular file
								Match regularFileMatch = Regex.Match(line, Config.FileRegex);
								if (regularFileMatch.Success)
								{
									string fileName = regularFileMatch.Groups[1].Value;

									SimpleFileNode cryGameRegularFileEntry = new SimpleFileNode();
									cryGameRegularFileEntry.Name = fileName;
									cryGameRegularFileEntry.LineNumber = i;

									currentSourceGroup.Files.Add(cryGameRegularFileEntry);

									continue;
								}
							}
						}
					}

					return makeFile;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
				System.Diagnostics.Debug.WriteLine(ex.StackTrace);
				GameCodersToolkitPackage.ExtensionOutput.WriteLine(ex.Message);
				GameCodersToolkitPackage.ExtensionOutput.WriteLine(ex.StackTrace);
			}

			return null;
		}
	}
}
