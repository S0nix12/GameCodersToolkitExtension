using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase
{
	public enum EDatabseUpdateEvent
	{
		EntriesAdded,
		EntriesRemoved,
		DatabaseCleared
	}

	public class DatabaseUpdatedEventArgs
	{
		public string FilePath { get; set; }
		public EDatabseUpdateEvent UpdateType { get; set; }
	}

	public class Database
	{
		public void AddEntriesForFile(string filePath, HashSet<DataEntry> entries)
		{
			lock (m_mutex)
			{
				if (EntriesPerFile.ContainsKey(filePath))
				{
					ClearEntriesForFile(filePath);
				}

				EntriesPerFile.Add(filePath, entries);
				foreach (DataEntry newEntry in entries)
				{
					foreach (GenericDataIdentifier reference in newEntry.References)
					{
						ReferencedByEntries.GetOrCreate(reference).Add(newEntry);
					}
				}

				DatabaseUpdatedEventArgs updateEventArgs = new DatabaseUpdatedEventArgs();
				updateEventArgs.FilePath = filePath;
				updateEventArgs.UpdateType = EDatabseUpdateEvent.EntriesAdded;
				DatabaseUpdated?.Invoke(this, updateEventArgs);
			}
		}

		public void TrimDatabaseExcess()
		{
			lock (m_mutex)
			{
				foreach (var entry in EntriesPerFile)
				{
					entry.Value.TrimExcess();
				}

				foreach (var refEntry in ReferencedByEntries)
				{
					refEntry.Value.TrimExcess();
				}
			}
		}

		public void ClearDatabase()
		{
			EntriesPerFile.Clear();
			ReferencedByEntries.Clear();
			GC.Collect();

			DatabaseUpdatedEventArgs updateEventArgs = new DatabaseUpdatedEventArgs();
			updateEventArgs.UpdateType = EDatabseUpdateEvent.DatabaseCleared;
			DatabaseUpdated?.Invoke(this, updateEventArgs);
		}

		public void ClearEntriesForDirectory(string directoryPath)
		{
			lock (m_mutex)
			{
				Uri directoryUri = new Uri(Path.GetFullPath(directoryPath));
				List<string> entriesToRemove = new List<string>();
				foreach (var entryPair in EntriesPerFile)
				{
					Uri fileUri = new Uri(Path.GetFullPath(entryPair.Key));
					if (directoryUri.IsBaseOf(fileUri))
					{
						entriesToRemove.Add(entryPair.Key);
					}
				}

				foreach (string filePath in entriesToRemove)
				{
					ClearEntriesForFile(filePath);
				}
			}
		}

		public void ClearEntriesForFile(string filePath)
		{
			lock (m_mutex)
			{
				if (EntriesPerFile.TryGetValue(filePath, out HashSet<DataEntry> fileEntries))
				{
					foreach (DataEntry entry in fileEntries)
					{
						foreach (GenericDataIdentifier reference in entry.References)
						{
							if (ReferencedByEntries.TryGetValue(reference, out HashSet<DataEntry> lookups))
							{
								lookups.Remove(entry);
							}
						}
					}

					EntriesPerFile.Remove(filePath);
					DatabaseUpdatedEventArgs updateEventArgs = new DatabaseUpdatedEventArgs();
					updateEventArgs.FilePath = filePath;
					updateEventArgs.UpdateType = EDatabseUpdateEvent.EntriesRemoved;
					DatabaseUpdated?.Invoke(this, updateEventArgs);
				}
			}
		}

		public EventHandler<DatabaseUpdatedEventArgs> DatabaseUpdated { get; set; }
		public Dictionary<string, HashSet<DataEntry>> EntriesPerFile { get; private set; } = new Dictionary<string, HashSet<DataEntry>>();
		public Dictionary<GenericDataIdentifier, HashSet<DataEntry>> ReferencedByEntries { get; private set; } = new Dictionary<GenericDataIdentifier, HashSet<DataEntry>>();

		private object m_mutex = new object();
	}
}
