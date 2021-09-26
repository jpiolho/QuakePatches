using QuakePatches.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuakePatches
{
    public class LoadedPatchFile
    {
        public PatchFile Patch { get; set; }
        public PatchVariant SelectedVariant { get; set; }


        public bool IsSelected => SelectedVariant != null;
    }
}
