using GameCodersToolkit.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;


public class QuickAutotestService
{
	public void ExecuteAutotestEntry(int entryIndex)
    {
        if (entryIndex < Autotests.Count)
        {
            ExecuteAutotestEntry(Autotests[entryIndex]);
        }
    }

    public void ExecuteAutotestEntry(QuickAutotestEntry autotestEntry)
    {
        if (autotestEntry == null)
            return;

        string batFilePath = autotestEntry.FilePath;

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

    public void SelectAutotestEntry(int entryIndex)
	{
        if (entryIndex >= 0 && entryIndex < Autotests.Count)
        {
            SelectedAutotest = Autotests[entryIndex];
        }
        else
        {
            SelectedAutotest = null;
        }
    }

    public QuickAutotestEntry SelectedAutotest {  get; set; }
	public List<QuickAutotestEntry> Autotests { get; private set; } = new List<QuickAutotestEntry>();
}