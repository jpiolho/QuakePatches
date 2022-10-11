using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuakePatches;

public class PatchedBinarySection
{
    public string HashPatchingProgram { get; set; }
    public string HashOriginalProgram { get; set; }
    public List<AppliedPatch> AppliedPatches { get; set; } = new List<AppliedPatch>();
}
