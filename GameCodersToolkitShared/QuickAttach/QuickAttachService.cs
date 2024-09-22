using GameCodersToolkit.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace GameCodersToolkit.QuickAttach
{
	public class QuickAttachProcessEntry
	{
		public string GetFormatedEntryName()
		{
			if (string.IsNullOrEmpty(WindowTitle))
			{
				return string.Format("{0} (ID: {1})", ProcessName, ProcessId);
			}
			else
			{
				return string.Format("{0}/{1} (ID: {2})", ProcessName, WindowTitle, ProcessId);
			}
		}

		public string ProcessName { get; set; }
		public string WindowTitle { get; set; }
		public int ProcessId { get; set; }
	}

	public class QuickAttachService
	{
		public void UpdateProcessList()
		{
			ProcessEntries.Clear();

			char[] separators = new char[] { ',' };
			string[] validModuleNames = QuickAttachOptions.Instance.ProcessFilters.Split(separators, StringSplitOptions.RemoveEmptyEntries);
			if (validModuleNames.Length == 0)
			{
				return;
			}

			var processes = Process.GetProcesses();

			try
			{
				foreach (var process in processes)
				{
					try
					{
						if (validModuleNames.Any(checkName => process.ProcessName.IndexOf(checkName, StringComparison.OrdinalIgnoreCase) != -1))
						{
							ProcessEntries.Add(new QuickAttachProcessEntry { ProcessName = process.ProcessName, ProcessId = process.Id, WindowTitle = process.MainWindowTitle });
							continue;
						}
						
						// Checking Modules would be nice but is very slow and often access to processes is denied so we can't even check them
						//foreach (ProcessModule module in process.Modules)
						//{
						//	if (validModuleNames.Any(checkName => module.ModuleName.IndexOf(checkName, StringComparison.OrdinalIgnoreCase) != -1))
						//	{
						//		ProcessEntries.Add(new QuickAttachProcessEntry { ProcessName = process.ProcessName, ProcessId = process.Id });
						//		break;
						//	}
						//}
					}
					catch (Win32Exception ex)
					{
						Debug.WriteLine("Process: " + process.ProcessName + ex.Message);
					}
				}
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory.Run(async delegate
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"QuickAttch: Exception updating process list",
					ex);
				});
			}
			finally
			{
				foreach (var process in processes)
					process.Dispose();
			}
		}

		public int GetMatchingProcessIdForSelection()
		{
			if (SelectedProcess == null)
				return -1;

			var processes = Process.GetProcesses();

			try
			{
				int backupId = -1;
				foreach (var process in processes)
				{
					if (process.Id == SelectedProcess.ProcessId)
						return process.Id;

					if (process.MainWindowTitle != null && process.MainWindowTitle == SelectedProcess.WindowTitle)
						return process.Id;

					if (backupId == -1 && process.ProcessName == SelectedProcess.ProcessName)
						backupId = process.Id;
				}

				return backupId;
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory.Run(async delegate
				{
					await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"QuickAttch: Exception getting matching ProcessId",
					ex);
				});
			}
			finally
			{
				foreach (var process in processes)
					process.Dispose();
			}

			return -1;
		}

		public void SelectProcessEntry(int entryIndex)
		{
			if (entryIndex < ProcessEntries.Count)
			{
				SelectedProcess = ProcessEntries[entryIndex];
			}
			else
			{
				SelectedProcess = null;
			}
		}

		public QuickAttachProcessEntry SelectedProcess { get; private set; }

		public List<QuickAttachProcessEntry> ProcessEntries { get; private set; } = new List<QuickAttachProcessEntry>();
	}
}
