using GameCodersToolkit.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameCodersToolkit.FileTemplateCreator.MakeFileParser
{
	[DebuggerDisplay("{Name} ({FileGroups.Count} Groups)")]
	public class CryGameUberFileEntry : IUberFileEntry
	{
		public string Name { get; set; }
		public int StartLineNumber { get; set; }
		public int EndLineNumber { get; set; }
		public List<CryGameSourceGroupEntry> FileGroups { get; set; } = new List<CryGameSourceGroupEntry>();

		public string GetName()
		{
			return Name;
		}

		public IEnumerable<ISourceGroupEntry> GetSourceGroups()
		{
			return FileGroups;
		}
	}

	[DebuggerDisplay("{Name} ({RegularFiles.Count} Entries)")]
	public class CryGameSourceGroupEntry : ISourceGroupEntry
	{
		public string Name { get; set; }
		public int LineNumber { get; set; }
		public List<CryGameRegularFileEntry> RegularFiles { get; set; } = new List<CryGameRegularFileEntry>();

		public IEnumerable<IRegularFileEntry> GetFiles()
		{
			return RegularFiles;
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
	public class CryGameRegularFileEntry : IRegularFileEntry
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

	[DebuggerDisplay("CryGameMakeFile ({UberFileEntries.Count} Uber Files)")]
	public class CryGameMakeFile : IMakeFile
	{
		public bool AddFilesAndSave(string previousUberFileName, string uberFileName, string previousGroupName, string groupName, string previousFileName, IEnumerable<string> newFileNames)
		{
			if (!HasBeenModified && AddFilesAndSave_Internal(previousUberFileName, uberFileName, previousGroupName, groupName, previousFileName, newFileNames))
			{
				HasBeenModified = true;
				return true;
			}

			return false;
		}

		public IEnumerable<IUberFileEntry> GetUberFileEntries()
		{
			return UberFileEntries;
		}

		public bool IsValid()
		{
			return !HasBeenModified;
		}

		private void SaveLinesToFile()
		{
			if (File.Exists(FilePath))
			{
				File.WriteAllLines(FilePath, Lines);
			}
		}

		private bool AddFilesAndSave_Internal(string previousUberFileName, string uberFileName, string previousGroupName, string groupName, string previousFileName, IEnumerable<string> newFileNames)
		{
			CryGameUberFileEntry uberFileEntry = UberFileEntries.Where((Entry) => Entry.Name == uberFileName).FirstOrDefault();

			if (uberFileEntry == null)
			{
				// New uber file
				int newUberFileLine = 0;

				CryGameUberFileEntry previousUberFileEntry = UberFileEntries.Where((Entry) => Entry.Name == previousUberFileName).FirstOrDefault();
				if (previousUberFileEntry != null)
				{
					newUberFileLine = previousUberFileEntry.EndLineNumber + 1;
				}

				List<string> newUberFileContent = new List<string>()
				{
					"add_sources(\"UBER_TOKEN.cpp\"",
					"\tSOURCE_GROUP \"GROUP_TOKEN\"",
					")"
				};

				newUberFileContent[0] = newUberFileContent[0].Replace("UBER_TOKEN", uberFileName);
				newUberFileContent[1] = newUberFileContent[1].Replace("GROUP_TOKEN", groupName);

				Lines.Insert(newUberFileLine, string.Empty);
				newUberFileLine++;
				Lines.Insert(newUberFileLine, newUberFileContent[0]);
				newUberFileLine++;
				Lines.Insert(newUberFileLine, newUberFileContent[1]);
				newUberFileLine++;

				InsertFileNames(newUberFileLine, newFileNames);
				newUberFileLine += newFileNames.Count();

				Lines.Insert(newUberFileLine, newUberFileContent[2]);
				newUberFileLine++;

				SaveLinesToFile();

				return true;
			}
			else
			{
				CryGameSourceGroupEntry sourceGroupEntry = uberFileEntry.FileGroups.Where(Group => Group.Name == groupName).FirstOrDefault();
				if (sourceGroupEntry == null)
				{
					// Existing uber file, new group

					int newGroupLine = uberFileEntry.StartLineNumber + 1;

					CryGameSourceGroupEntry previousGroupFileEntry = uberFileEntry.FileGroups.Where((Group) => Group.Name == previousGroupName).FirstOrDefault();
					if (previousGroupFileEntry != null)
					{
						newGroupLine = previousGroupFileEntry.LineNumber + previousGroupFileEntry.RegularFiles.Count;
					}

					string newGroupContent = "\tSOURCE_GROUP \"GROUP_TOKEN\"";
					newGroupContent = newGroupContent.Replace("GROUP_TOKEN", groupName);

					Lines.Insert(newGroupLine, newGroupContent);
					newGroupLine++;

					InsertFileNames(newGroupLine, newFileNames);
					SaveLinesToFile();

					return true;
				}

				// Existing uber file, existing group
				int lineToInsert = sourceGroupEntry.LineNumber + 1;
				if (!string.IsNullOrWhiteSpace(previousFileName))
				{
					CryGameRegularFileEntry regularFileEntry = sourceGroupEntry.RegularFiles.Where(Entry => Entry.Name == previousFileName).First();
					if (regularFileEntry != null)
					{
						lineToInsert = regularFileEntry.LineNumber + 1;
					}
				}

				InsertFileNames(lineToInsert, newFileNames);
				SaveLinesToFile();

				return true;
			}
		}

		private void InsertFileNames(int lineNumber, IEnumerable<string> fileNames)
		{
			foreach (string newFileName in fileNames)
			{
				string newLine = $"\t\t\"{newFileName}\"";
				Lines.Insert(lineNumber, newLine);
				lineNumber++;
			}
		}

		public string FilePath { get; set; }
		public List<string> Lines { get; set; }
		public List<CryGameUberFileEntry> UberFileEntries { get; set; } = new List<CryGameUberFileEntry>();
		public bool HasBeenModified { get; set; }
	}

	public class CryGameParser : IMakeFileParser
	{
		public IMakeFile Parse(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					CryGameMakeFile makeFile = new CryGameMakeFile();
					makeFile.FilePath = filePath;
					makeFile.Lines = File.ReadAllLines(filePath).ToList();

					CryGameUberFileEntry currentUberFile = null;
					CryGameSourceGroupEntry currentSourceGroup = null;

					for (int i = 0; i < makeFile.Lines.Count; i++)
					{
						string line = makeFile.Lines[i];

						if (currentUberFile != null)
						{
							// Check if the current line ends the uber file definition
							Match uberFileEndMatch = Regex.Match(line, @"\)");
							if (uberFileEndMatch.Success)
							{
								currentUberFile.EndLineNumber = i;
								makeFile.UberFileEntries.Add(currentUberFile);

								currentUberFile = null;
								currentSourceGroup = null;
							}
						}

						// Check if the current line begins a new uber file definition
						Match uberFileMatch = Regex.Match(line, @"add_sources");
						if (uberFileMatch.Success)
						{
							if (currentUberFile != null || currentSourceGroup != null)
							{
								throw new Exception($"MakeFile {filePath} is malformatted in line {i}: Found nested add_sources call");
							}

							Match uberFileNameMatch = Regex.Match(line, "\"([^\"]*)\"", RegexOptions.IgnoreCase);
							if (uberFileNameMatch.Success)
							{
								string uberFileName = uberFileNameMatch.Value;
								uberFileName = uberFileName.Trim('\"', '\\');

								currentUberFile = new CryGameUberFileEntry();
								currentUberFile.Name = uberFileName;
								currentUberFile.StartLineNumber = i;

								continue;
							}
						}

						if (currentUberFile != null)
						{
							// Check if the current line is a source group
							Match sourceGroupMatch = Regex.Match(line, @"SOURCE_GROUP");
							if (sourceGroupMatch.Success)
							{
								Match sourceGroupNameMatch = Regex.Match(line, "\"([^\"]*)\"", RegexOptions.IgnoreCase);
								if (sourceGroupNameMatch.Success)
								{
									string sourceGroupName = sourceGroupNameMatch.Value;
									sourceGroupName = sourceGroupName.Trim('\"', '\\');

									currentSourceGroup = new CryGameSourceGroupEntry();
									currentSourceGroup.Name = sourceGroupName;
									currentSourceGroup.LineNumber = i;
									currentUberFile.FileGroups.Add(currentSourceGroup);

									continue;
								}
							}

							if (currentSourceGroup != null)
							{
								// Check if it's a regular file
								Match regularFileMatch = Regex.Match(line, "\"([^\"]*)\"", RegexOptions.IgnoreCase);
								if (regularFileMatch.Success)
								{
									string regularFileName = regularFileMatch.Value;
									regularFileName = regularFileName.Trim('\"', '\\');

									CryGameRegularFileEntry cryGameRegularFileEntry = new CryGameRegularFileEntry();
									cryGameRegularFileEntry.Name = regularFileName;
									cryGameRegularFileEntry.LineNumber = i;

									currentSourceGroup.RegularFiles.Add(cryGameRegularFileEntry);

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
