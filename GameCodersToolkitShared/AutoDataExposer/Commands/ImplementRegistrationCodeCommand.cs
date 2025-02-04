using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.Utils;

namespace GameCodersToolkit.DataReferenceFinderModule
{
    //[Command(PackageGuids.guidMyCmdSetString, PackageIds.ImplementRegistrationCode)]
    //internal sealed class ImplementRegistrationCodeCommand : BaseCommand<ImplementRegistrationCodeCommand>
    //{
    //    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    //    {
    //        var textWriter = await GameCodersToolkitPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

    //        string searchText = await TextUtilFunctions.SearchForGuidUnderCaretAsync();

    //        if (string.IsNullOrEmpty(searchText))
    //        {
    //            await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
    //            await textWriter.WriteLineAsync("No Guid selection found");
    //            return;
    //        }

    //        try
    //        {
    //            await ReferenceDatabaseUtils.ExecuteFindOperationOnDatabaseAsync(new GenericDataIdentifier(Guid.Parse(searchText)), searchText);
    //        }
    //        catch (Exception ex)
    //        {
    //            await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
    //            await DiagnosticUtils.ReportExceptionFromExtensionAsync(
    //                "Expection finding Guid in Database",
    //                ex);
    //        }
    //    }
    //    protected override void BeforeQueryStatus(EventArgs e)
    //    {
    //        Command.Enabled = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.HasPotentialGuidUnderCaretAsync);
    //    }
    //}
}
