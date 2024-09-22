using EnvDTE80;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace GameCodersToolkit.QuickAttach
{
	[Command(PackageGuids.QuickAttachCommandSet_GuidString, PackageIds.QuickAttachCommand)]
	internal sealed class QuickAttachCommand : BaseCommand<QuickAttachCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			Process2 selectedProcess = await GetSelectedDebugProcessAsync();
			if (selectedProcess != null)
			{
				selectedProcess.Attach();
			}
		}

		protected override void BeforeQueryStatus(EventArgs e)
		{
			var quickAttachService = Package.GetService<QuickAttachService, QuickAttachService>();

			int selectedProcessId = quickAttachService.GetMatchingProcessIdForSelection();
			if (selectedProcessId == -1)
			{
				Command.Enabled = false;
				return;
			}

			var dte = Package.GetService<EnvDTE.DTE, DTE2>();
			var debugger = dte.Debugger as Debugger2;
			var debuggedProcesses = debugger.DebuggedProcesses;

			foreach (Process2 process in debuggedProcesses)
			{
				if (process.ProcessID == selectedProcessId)
				{
					Command.Enabled = false;
					return;
				}
			}

			Command.Enabled = true;
		}

		private async Task<Process2> GetSelectedDebugProcessAsync()
		{
			var quickAttachService = await Package.GetServiceAsync<QuickAttachService, QuickAttachService>();
			int processId = quickAttachService.GetMatchingProcessIdForSelection();
			if (processId == -1)
				return null;

			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			var dte = await Package.GetServiceAsync<EnvDTE.DTE, DTE2>();
			var debugger = dte.Debugger as Debugger2;

			var processes = debugger.LocalProcesses;
			foreach (Process2 process in processes)
			{
				if (process.ProcessID == processId)
				{
					return process;
				}
			}

			return null;
		}
	}
}
