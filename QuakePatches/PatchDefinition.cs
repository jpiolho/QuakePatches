using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuakePatches
{
    public class PatchDefinition
    {
        public class PatchReplacement
        {
            public int Index { get; set; }
            public string Bytes { get; set; }
        }

        public class PatchInformation
        {
            public string[] Pattern { get; set; }
            public PatchReplacement[] Replacements { get; set; }
        }

        public class PatchVariant
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public PatchInformation Patch { get; set; }
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PatchVariant[] Variants { get; set; }
    }
}
