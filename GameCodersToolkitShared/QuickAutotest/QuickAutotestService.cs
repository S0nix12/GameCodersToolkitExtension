using GameCodersToolkit.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;


public class QuickAutotestService
{
	public void SelectAutotestEntry(int entryIndex)
	{
		if (entryIndex < Autotests.Count)
		{
			string batFilePath = Autotests[entryIndex].FilePath;

			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = batFilePath,
				UseShellExecute = true,
				RedirectStandardOutput = false,
				RedirectStandardError = false,
				CreateNoWindow = false
			};

			using (Process process = new Process())
			{
				process.StartInfo = psi;
				process.Start();
			}
		}
	}

	public List<QuickAutotestEntry> Autotests { get; private set; } = new List<QuickAutotestEntry>();
}