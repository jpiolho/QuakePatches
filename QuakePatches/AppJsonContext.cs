using QuakePatches.Patching;
using System.Text.Json.Serialization;

namespace QuakePatches;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PatchedBinarySection))]
[JsonSerializable(typeof(PatchFile))]
public partial class AppJsonContext : JsonSerializerContext;
