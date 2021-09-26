namespace QuakePatches.Patches
{
    public class PatchDefinition
    {
        public string Id { get; set; }
        public string[] Pattern { get; set; }
        public PatchReplacement[] Replacements { get; set; }
    }
}
