using AsmResolver;
using AsmResolver.PE.File;
using AsmResolver.PE.File.Headers;
using QuakePatches.Exceptions;
using QuakePatches.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuakePatches
{
    public class PatchedBinary : IDisposable
    {
        public enum PatchStatus
        {
            Unpatched,
            Patched,
            PatchedVersionMismatch,
            Corrupted
        }

        private const string PatchSection = "QPATCH.I";

        private byte[] _originalBinary;
        private PatchedBinarySection _patchedInfo;
        private Dictionary<string, (PESection,MemoryStream)> _sectionStreams;

        public IReadOnlyList<AppliedPatch> AppliedPatches => _patchedInfo.AppliedPatches.AsReadOnly();
        public string PatchProgramHash => _patchedInfo.HashPatchingProgram;
        public string OriginalHash => _patchedInfo.HashOriginalProgram;


        public byte[] FullBinary => _originalBinary;

        public PatchStatus Patched { get; private set; }

        public PEFile PE { get; private set; }

        public PatchedBinary()
        {
            _patchedInfo = new PatchedBinarySection();
            _originalBinary = null;
        }

        public PatchedBinary(byte[] binary, string patchProgramHash) : this()
        {
            Load(binary, patchProgramHash);
        }

        public PatchStatus Load(byte[] binary, string patchProgramHash)
        {
            _originalBinary = binary;


            // Read PEHeaders
            PE = PEFile.FromBytes(binary);


            // Set the patching program hash
            _patchedInfo.HashPatchingProgram = patchProgramHash;

            // Load all sections into streams
            _sectionStreams = new Dictionary<string, (PESection,MemoryStream)>();
            foreach(var section in PE.Sections)
            {
                if (!section.IsContentCode && !section.IsContentInitializedData)
                    continue;

                var segment = section.Contents as VirtualSegment;
                var arr = segment.ToArray();
                _sectionStreams.Add(section.Name, (section,new MemoryStream(arr,0,arr.Length,true,true)));
            }

            var patchesSection = GetQuakePatchesSection();

            // Check if not patched
            if (patchesSection is null)
                return Patched = PatchStatus.Unpatched;

            var sectionContents = patchesSection.Contents;
            if (sectionContents is null)
                return Patched = PatchStatus.Corrupted;

            byte[] sectionData = null;
            if (sectionContents is DataSegment dataSegment)
                sectionData = dataSegment.Data;
            else if (sectionContents is VirtualSegment virtualSegment)
                sectionData = virtualSegment.ToArray();

            if (sectionData is null || sectionData.Length == 0)
                return Patched = PatchStatus.Corrupted;

            try
            {
                var json = Encoding.UTF8.GetString(sectionData).Trim((char)0);
                var patchedBinarySection = JsonSerializer.Deserialize<PatchedBinarySection>(json);

                _patchedInfo = patchedBinarySection;

                // Check if there's a patch program version mismatch
                if (!string.Equals(_patchedInfo.HashPatchingProgram, patchProgramHash, StringComparison.OrdinalIgnoreCase))
                    return Patched = PatchStatus.PatchedVersionMismatch;

                return Patched = PatchStatus.Patched;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                return Patched = PatchStatus.Corrupted;
            }
        }

        public void BeginPatches()
        {
            _patchedInfo.HashOriginalProgram = GetBinaryHash();
        }

        public void ApplyPatch(PatchFile patchFile, PatchVariant variant)
        {
            // For now, only do it for .text
            var stream = _sectionStreams[".text"];

            // Load in all the patches
            var patches = new List<PatchDefinition>(variant.Patches.Length);
            foreach (var patchId in variant.Patches)
            {
                var patch = patchFile.Patches.FirstOrDefault(p => p.Id.Equals(patchId, StringComparison.OrdinalIgnoreCase));

                if (patch == null)
                    throw new PatchingException($"Could not find patch with id '{patchId}'");

                patches.Add(patch);
            }


            // Apply all the patches from this variant
            foreach (var patch in patches)
            {
                // Find the pattern
                var pattern = PatternToByteArray(patch.Pattern, variant);
                var matches = IndexOf(stream.Item2.GetBuffer(),pattern);

                // Check if there was at least 1 match
                if (matches.Length < 1)
                    throw new PatchingException("Could not find a match for the pattern");

                // Throw exception if there's more than 1 match
                if (matches.Length > 1)
                    throw new PatchingException("More than 1 match was found. Only 1 match is supported");


                // Do all the replacements
                foreach (var replacement in patch.Replacements)
                {
                    var index = matches[0];

                    index += replacement.Index;

                    byte[] bytes = null;

                    if (replacement.Bytes != null)
                        bytes = Convert.FromHexString(DoVariantVariableReplacement(replacement.Bytes, variant).Trim().Replace(" ", ""));

                    stream.Item2.Position = index;
                    stream.Item2.Write(bytes);
                }
            }


            _patchedInfo.AppliedPatches.Add(new AppliedPatch() { Patch = patchFile.Id, Variant = variant.Id });

            // Write json into PE section
            var section = GetQuakePatchesSection(true);
            var json = JsonSerializer.Serialize(_patchedInfo);
            section.Contents = new DataSegment(Encoding.UTF8.GetBytes(json));
        }


        private PESection GetQuakePatchesSection(bool create=false)
        {
            var section = PE.Sections.FirstOrDefault(s => s.Name == PatchSection);

            if (section is null && create) {
                section = new PESection(PatchSection, SectionFlags.MemoryLocked | SectionFlags.ContentInitializedData);
                section.Contents = new DataSegment(new byte[] { });
                PE.Sections.Add(section);
            }

            return section;
        }

        private byte?[] PatternToByteArray(string[] patchPattern, PatchVariant variant)
        {
            var pattern = DoVariantVariableReplacement(string.Join(" ", patchPattern).Trim(), variant).Replace(" ", "");

            if (pattern.Length % 2 != 0)
                throw new PatchingException("Pattern is not divisble by 2");

            byte?[] array = new byte?[pattern.Length / 2];

            // Go byte by byte in the hex pattern
            for (int i = 0, x = 0; i < array.Length; i++, x += 2)
            {
                var b = pattern.Substring(x, 2);

                if (b == "**")
                    array[i] = null;
                else
                    array[i] = Convert.FromHexString(b)[0];
            }

            return array;
        }

        private string DoVariantVariableReplacement(string text, PatchVariant variant)
        {
            // If no variables specified, just return the original text
            if (variant.Variables == null || variant.Variables.Length == 0)
                return text;

            var sb = new StringBuilder(text);

            foreach (var variable in variant.Variables)
                sb.Replace($"%{variable.Variable}%", variable.Value);

            return sb.ToString();
        }

        private byte?[] ByteArrayToNullableByteArray(byte[] array)
        {
            var newArray = new byte?[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                newArray[i] = array[i];
            }

            return newArray;
        }

        private int[] IndexOf(byte[] source, byte[] pattern) => IndexOf(source, ByteArrayToNullableByteArray(pattern));
        private int[] IndexOf(byte[] source, byte?[] pattern)
        {
            var arr = source;

            var startIndex = -1;
            var indexes = new List<int>(1);
            for (int i = 0, pi = 0; i < arr.Length; i++)
            {
                var b = arr[i];

                var pb = pattern[pi];


                if (pb != null && b != pb)
                {
                    // Failed match, reset
                    pi = 0;
                    startIndex = -1;
                }

                if (pb == null || b == pattern[pi])
                {
                    // First match?
                    if (startIndex == -1)
                        startIndex = i;

                    pi++;

                    // Did we reach the end of the pattern?
                    if (pi >= pattern.Length)
                    {
                        indexes.Add(startIndex);

                        pi = 0;
                        startIndex = -1;
                    }
                }
            }

            return indexes.ToArray();
        }

        public string GetBinaryHash()
        {
            using (var sha512 = SHA512.Create())
                return Convert.ToHexString(sha512.ComputeHash(FullBinary));
        }

        public void Save(string filePath)
        {
            // Write data into sections
            foreach(var kv in _sectionStreams)
            {
                var section = PE.Sections.Single(s => s.Name == kv.Key);
                var buffer = new DataSegment(kv.Value.Item2.GetBuffer());
                section.Contents = new VirtualSegment(buffer,kv.Value.Item1.GetVirtualSize());
            }

            PE.Write(filePath);
        }

        public void Dispose()
        {
            if(_sectionStreams is not null)
            {
                foreach(var kv in _sectionStreams)
                    kv.Value.Item2.Dispose();
            }

            _originalBinary = null;
        }
    }
}
