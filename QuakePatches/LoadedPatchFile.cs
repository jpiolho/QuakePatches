using QuakePatches.Patching;

namespace QuakePatches;

public class LoadedPatchFile
{
    public PatchFile Patch { get; set; }
    public PatchVariant SelectedVariant { get; set; }


    public bool IsSelected => SelectedVariant != null;
}
