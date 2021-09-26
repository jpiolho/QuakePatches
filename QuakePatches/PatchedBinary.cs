using QuakePatches.Exceptions;
using QuakePatches.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

        private const string PatchMarker = "[QPATCH_MARKER]";

        private MemoryStream _stream;
        private BinaryReader _reader;
        private BinaryWriter _writer;
        private int _markerOffset = -1;
        private List<AppliedPatch> _appliedPatches;

        public IReadOnlyList<AppliedPatch> AppliedPatches => _appliedPatches.AsReadOnly();
        public string PatchProgramHash { get; private set; }
        public string OriginalHash { get; private set; }

        public byte[] FullBinary => _stream.ToArray();
        public byte[] Binary
        {
            get
            {
                // If there's no marker, this is identical to FullBinary
                if (_markerOffset == -1)
                    return FullBinary;

                var pos = _stream.Position;
                try
                {
                    _stream.Position = 0;
                    return _reader.ReadBytes(_markerOffset);
                }
                finally
                {
                    _stream.Position = pos;
                }
            }
        }

        public PatchStatus Patched { get; private set; }

        public PatchedBinary()
        {
            _appliedPatches = new List<AppliedPatch>();
            _stream = new MemoryStream();

            _reader = new BinaryReader(_stream);
            _writer = new BinaryWriter(_stream);
        }

        public PatchedBinary(byte[] binary, string patchProgramHash) : this()
        {
            Load(binary, patchProgramHash);
        }

        public PatchStatus Load(byte[] binary, string patchProgramHash)
        {
            PatchProgramHash = patchProgramHash;

            _stream.Write(binary);
            _stream.Position = 0;

            // Find marker index
            var markerBytes = Encoding.UTF8.GetBytes(PatchMarker);
            var patchIndexes = IndexOf(markerBytes);
            _markerOffset = patchIndexes.Length == 1 ? patchIndexes[0] : -1;

            // Check if not patched
            if (_markerOffset == -1)
                return Patched = PatchStatus.Unpatched;

            // Patched?
            _stream.Position = _markerOffset;

            if (!_reader.ReadBytes(PatchMarker.Length).SequenceEqual(markerBytes))
                return Patched = PatchStatus.Corrupted;

            try
            {
                // Read the hashes
                PatchProgramHash = Encoding.UTF8.GetString(_reader.ReadBytes(128));
                OriginalHash = Encoding.UTF8.GetString(_reader.ReadBytes(128));

                // Read json
                var jsonLength = _reader.ReadInt32();
                var json = Encoding.UTF8.GetString(_reader.ReadBytes(jsonLength));

                // Copy the applied patches list
                var patches = JsonSerializer.Deserialize<AppliedPatch[]>(json);
                _appliedPatches.AddRange(patches);

                // Check if there's a patch program version mismatch
                if (!string.Equals(this.PatchProgramHash, patchProgramHash, StringComparison.OrdinalIgnoreCase))
                    return Patched = PatchStatus.PatchedVersionMismatch;

                return Patched = PatchStatus.Patched;
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                return Patched = PatchStatus.Corrupted;
            }
        }

        public void ApplyPatch(PatchFile patchFile, PatchVariant variant)
        {
            if (_markerOffset == -1)
            {
                var hash = GetBinaryHash();

                // Need to create marker
                _stream.Position = _stream.Length;

                _markerOffset = (int)_stream.Position;

                _writer.Write(PatchMarker.ToCharArray());
                _writer.Write(PatchProgramHash.ToCharArray());
                _writer.Write(hash.ToCharArray());
                _writer.Write((int)0); // JSON Length
            }

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
                var pattern = Convert.FromHexString(DoVariantVariableReplacement(string.Join(" ", patch.Pattern).Trim(), variant).Replace(" ", ""));
                var matches = IndexOf(pattern);

                if (matches.Length != 1)
                    throw new PatchingException("Could not find a match for the pattern");

                // Do all the replacements
                foreach (var replacement in patch.Replacements)
                {
                    var index = matches[0];

                    index += replacement.Index;
                    var bytes = Convert.FromHexString(DoVariantVariableReplacement(replacement.Bytes, variant).Trim().Replace(" ", ""));

                    _stream.Position = index;
                    _stream.Write(bytes);
                }
            }


            _appliedPatches.Add(new AppliedPatch() { Patch = patchFile.Id, Variant = variant.Id });

            // Write json
            _stream.Position = _markerOffset + PatchMarker.Length + 128 + 128;

            var json = JsonSerializer.Serialize(_appliedPatches.ToArray());
            _writer.Write((int)json.Length);
            _writer.Write(json.ToCharArray());
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

        private int[] IndexOf(byte[] pattern)
        {
            _stream.Position = 0;

            var startIndex = -1;
            var end = _markerOffset == -1 ? _stream.Length : _markerOffset;
            var indexes = new List<int>();
            for (int i = 0, pi = 0; i < end; i++)
            {
                var b = _reader.ReadByte();

                if (b != pattern[pi])
                {
                    // Failed match, reset
                    pi = 0;
                    startIndex = -1;
                }

                if (b == pattern[pi])
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
                return Convert.ToHexString(sha512.ComputeHash(Binary));
        }

        public void Dispose()
        {
            _writer.Dispose();
            _reader.Dispose();
            _stream.Dispose();
        }
    }
}
