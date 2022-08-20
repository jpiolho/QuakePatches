namespace QuakePatches.Patching;

public class PatchVariant
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string[] Patches { get; set; }
    public PatchVariantVariable[] Variables { get; set; }
}
