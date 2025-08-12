using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

public static class ClipboardHelper
{
    public static bool TrySetText(string text, int retries = 5, int delayMs = 100)
    {
        for (int i = 0; i < retries; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                Thread.Sleep(delayMs); // Wait and retry
            }
        }
        return false;
    }

    public static string TryGetText(int retries = 5, int delayMs = 100)
    {
        for (int i = 0; i < retries; i++)
        {
            try
            {
                if (Clipboard.ContainsText())
                    return Clipboard.GetText();
                return string.Empty;
            }
            catch
            {
                Thread.Sleep(delayMs);
            }
        }
        return string.Empty;
    }
}