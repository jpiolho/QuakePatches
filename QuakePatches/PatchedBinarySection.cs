using System.Collections.Generic;

namespace QuakePatches;

public class PatchedBinarySection
{
    public string HashPatchingProgram { get; set; }
    public string HashOriginalProgram { get; set; }
    public List<AppliedPatch> AppliedPatches { get; set; } = new List<AppliedPatch>();
}
