namespace QuakePatches.Patches
{
    public class PatchFile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public PatchDefinition[] Patches { get; set; }
        public PatchVariant[] Variants { get; set; }
    }
}
