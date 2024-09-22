namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.ClearReferenceDatabase)]
	internal sealed class ClearReferenceDatabase : BaseCommand<ClearReferenceDatabase>
	{
		protected override void Execute(object sender, EventArgs e)
		{
			GameCodersToolkitPackage.ReferenceDatabase.ClearDatabase();
		}
	}
}
