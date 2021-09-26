using QuakePatches.Patches;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace QuakePatches
{
    class Program
    {
        private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        const string BinaryName = "Quake_x64_steam.exe";

        private static List<LoadedPatchFile> _patches;
        private static PatchedBinary _originalBinary;
        private static string _ownHash;
        private static string _binaryPath;

        static void Main(string[] args)
        {
            // Set default colors
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;

            // Get target binary and check if it exists
            _binaryPath = GetTargetBinaryPath(args);
            if (!File.Exists(_binaryPath))
            {
                Console.WriteLine($"ERROR: Cannot find '{BinaryName}'. Put this in the same folder or drag & drop the exe into this program");
                return;
            }

            PrintWarningAndWaitConfirmation();

            Console.WriteLine();


            // Calculate Quake Patch hash
            Console.Write("Calculating Quake Patch hash... ");
            _ownHash = GetOwnHash();
            Console.WriteLine(_ownHash);

            Console.Write($"Loading '{_binaryPath}'... ");
            var binary = new PatchedBinary(File.ReadAllBytes(_binaryPath), _ownHash);
            Console.WriteLine("Loaded");

            // Make sure the backup exists and that it's fine
            var originalPath = _binaryPath + ".original";
            if (!File.Exists(originalPath))
            {
                Console.WriteLine("No original backup found");
                Console.WriteLine("Making a backup of the original executable...");

                if (binary.Patched != PatchedBinary.PatchStatus.Unpatched)
                {
                    Console.WriteLine("FAILURE: The binary that was provided is not an original unpatched binary.");
                    return;
                }

                File.Copy(_binaryPath, originalPath);
                Console.WriteLine($"Copied '{_binaryPath}' to '{originalPath}'");
            }

            // Load the original binary now
            Console.Write($"Loading original binary '{originalPath}'... ");
            _originalBinary = new PatchedBinary(File.ReadAllBytes(originalPath), _ownHash);
            Console.WriteLine("Loaded");

            if (_originalBinary.Patched != PatchedBinary.PatchStatus.Unpatched)
            {
                Console.WriteLine("FAILURE: The original binary is not an unpatched version. Please delete it and make sure you make a new copy");
                return;
            }

            // Do some version checks between original and backup
            Console.Write("Doing version checks... ");

            if (binary.Patched == PatchedBinary.PatchStatus.PatchedVersionMismatch)
            {
                Console.WriteLine("WARNING: The binary is patched, but it was done with a different Quake Patch program.");
            }
            else if (binary.Patched == PatchedBinary.PatchStatus.Patched)
            {
                if (_originalBinary.GetBinaryHash() != binary.OriginalHash)
                {
                    Console.WriteLine("FAILURE: The binary is patched, but the backup is for a different game version.");
                    return;
                }
            }

            if (_originalBinary.GetBinaryHash() != (binary.OriginalHash ?? binary.GetBinaryHash()))
            {
                Console.WriteLine("FAILURE: The binary and backup are for a different game version.");
                return;
            }

            Console.WriteLine("OK");

            // Load patches
            Console.WriteLine("Loading patches... ");
            LoadPatches();
            Console.WriteLine($"Loaded {_patches.Count} patches");

            // Select applied patches
            if (binary.Patched == PatchedBinary.PatchStatus.Patched)
            {
                Console.Write("Selecting existing patches... ");

                SetSelectedPatchesFromBinary(binary);

                Console.WriteLine("DONE");
            }

            //Console.WriteLine("Continuing in 4s...");
            //Thread.Sleep(TimeSpan.FromSeconds(4));



            MenuPatches();

        }

        /// <summary>
        /// Given the provided patched binary, it will select all the variants that are present in the binary.
        /// </summary>
        static void SetSelectedPatchesFromBinary(PatchedBinary binary)
        {
            foreach (var appliedPatch in binary.AppliedPatches)
            {
                // Find the LoadedPatchFile
                var loadedPatchFile = _patches.FirstOrDefault(p => p.Patch.Id.Equals(appliedPatch.Patch, StringComparison.OrdinalIgnoreCase));
                if (loadedPatchFile == null)
                {
                    Console.WriteLine($"WARNING: Unable to find patch with id '{appliedPatch.Patch}'");
                    continue;
                }

                // Find the variant
                var variant = loadedPatchFile.Patch.Variants.FirstOrDefault(v => v.Id.Equals(appliedPatch.Variant, StringComparison.OrdinalIgnoreCase));
                if (variant == null)
                {
                    Console.WriteLine($"WARNING: Unable to find patch variant with id '{appliedPatch.Patch}' in patch '{loadedPatchFile.Patch.Id}'");
                    continue;
                }

                // Set it selected as with this variant
                loadedPatchFile.SelectedVariant = variant;
            }
        }

        /// <summary>
        /// Returns the target binary path.
        /// By default uses <see cref="BinaryName"/> unless specified in the <paramref name="args"/>.
        /// </summary>
        static string GetTargetBinaryPath(string[] args)
        {
            var path = BinaryName;
            if (args.Length > 0)
                path = args[^1];

            return path;
        }

        static void MenuPatches()
        {
            const int ItemsPerPage = 6;

            int selectedPatch = 0;
            int page = 0;
            var pagesCount = Math.Ceiling(_patches.Count / (float)ItemsPerPage);


            while (true)
            {
                Console.Clear();
                Console.WriteLine("Choose the patches you wish to apply");

                // Do pages calculation
                var start = page * ItemsPerPage;
                var end = Math.Min(_patches.Count, start + ItemsPerPage);

                // Print pagination
                Console.WriteLine($"Page {page + 1}/{pagesCount}");

                // Print patches menu
                for (var i = start; i < end; i++)
                {
                    var filePatch = _patches[i];

                    Console.ResetColor();

                    if (filePatch.IsSelected)
                    {
                        Console.Write("[");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("X");
                        Console.ResetColor();
                        Console.Write("] ");
                    }
                    else
                        Console.Write($"[ ] ");

                    if (selectedPatch == i)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.BackgroundColor = ConsoleColor.Gray;
                    }

                    Console.WriteLine(filePatch.Patch.Name);

                    Console.ResetColor();

                    // If selected and there's more than 1 variant, display it
                    if (filePatch.SelectedVariant != null && filePatch.Patch.Variants.Length > 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"    Selected variant: {filePatch.SelectedVariant.Name}");
                    }

                    Console.ResetColor();

                    Console.WriteLine($"    {filePatch.Patch.Description}");
                    Console.WriteLine();
                }

                Console.ResetColor();

                // Print keys
                Console.WriteLine();
                Console.WriteLine("Left: Previous page\t\tUp: Move up\t\tDown: Move down\t\tRight: Next page");
                Console.WriteLine("ENTER: Toggle patch\t\tF10: Apply patch...\tESQ: Exit");


                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.DownArrow: selectedPatch = Math.Min(end - 1, selectedPatch + 1); break;
                    case ConsoleKey.UpArrow: selectedPatch = Math.Max(start, selectedPatch - 1); break;
                    case ConsoleKey.LeftArrow:
                        {
                            if (page > 0)
                            {
                                page--;
                                selectedPatch -= ItemsPerPage;
                            }
                            break;
                        }
                    case ConsoleKey.RightArrow:
                        {
                            if (page < pagesCount - 1)
                            {
                                page++;
                                selectedPatch = Math.Min(_patches.Count, selectedPatch + ItemsPerPage);
                            }
                            break;
                        }
                    case ConsoleKey.Enter:
                        {
                            var filePatch = _patches[selectedPatch];

                            // If selected, unselect it!
                            if (filePatch.IsSelected)
                            {
                                filePatch.SelectedVariant = null;
                                break;
                            }

                            // If only one variant, select that one
                            if(filePatch.Patch.Variants.Length == 1)
                            {
                                filePatch.SelectedVariant = filePatch.Patch.Variants[0];
                                break;
                            }

                            // More than 1 variant, need to show the variant select menu
                            filePatch.SelectedVariant = MenuSelectPatchVariant(filePatch.Patch);

                            break;
                        }
                    case ConsoleKey.F10: ApplyPatch(); break;
                    case ConsoleKey.Escape: return;
                }
            }
        }

        static PatchVariant MenuSelectPatchVariant(PatchFile patch)
        {
            int variant = 0;


            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Select which variant to apply for patch '{patch.Name}'");
                Console.WriteLine();

                for (var i = 0; i < patch.Variants.Length; i++)
                {
                    Console.Write($"{i + 1}. ");
                    if (i == variant)
                    {
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                    }

                    Console.WriteLine(patch.Variants[i].Name);

                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.WriteLine("Up Arrow: Move up\t\tDown arrow: Move down");
                Console.WriteLine("ENTER: Select\t\tESC: Cancel");

                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.UpArrow: variant = Math.Max(0, variant - 1); break;
                    case ConsoleKey.DownArrow: variant = Math.Min(patch.Variants.Length - 1, variant + 1); break;
                    case ConsoleKey.Enter: return patch.Variants[variant];
                    case ConsoleKey.Escape: return null;
                }
            }
        }

        static bool ReadYesOrNo()
        {
            do
            {
                var key = Console.ReadKey(true).Key;

                if (key == ConsoleKey.N)
                    return false;
                else if (key == ConsoleKey.Y)
                    return true;
            }
            while (true);
        }

        static void ApplyPatch()
        {
            Console.Clear();
            Console.WriteLine("You are about to apply the patches. Are you sure you want to continue? (Y/N)");

            if (!ReadYesOrNo())
                return;

            Console.WriteLine("Starting patching...");

            var binary = new PatchedBinary(_originalBinary.FullBinary, _originalBinary.PatchProgramHash);

            bool success = false;
            int count = 0;
            foreach (var filePatch in _patches.Where(p => p.IsSelected))
            {
                var patch = filePatch.Patch;
                var variant = filePatch.SelectedVariant;

                Console.WriteLine($"Applying patch '{patch.Name}' variant '{variant.Name}'...");

                try
                {
                    success = binary.ApplyPatch(patch, variant);
                }
                catch (Exception ex)
                {
                    success = false;
                    Console.WriteLine($"FAILED: Failed to apply patch '{patch}'. Exception: {ex}");
                    break;
                }

                if (!success)
                {
                    Console.WriteLine($"FAILED: Failed to apply patch '{patch}'. Too many matches");
                    break;
                }

                count++;
            }

            Console.WriteLine();
            Console.WriteLine($"{count} patches applied");

            if (!success)
            {
                Console.WriteLine($"Unable to apply all the patches. Do you still want to save the patched binary? (Y/N)");
                if (!ReadYesOrNo())
                    return;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Binary was patched successfully!");
                Console.ResetColor();
            }

            Console.Write($"Writing patched binary to '{_binaryPath}'");
            File.WriteAllBytes(_binaryPath, binary.FullBinary);
            Console.WriteLine(" DONE");

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        static bool LoadPatches()
        {
            _patches = new List<LoadedPatchFile>();

            // Load patches from the Patches folder
            var patchesFolder = Path.Combine(Environment.CurrentDirectory, "Patches");
            if (Directory.Exists(patchesFolder))
            {
                Console.WriteLine("Patches folder found");
                foreach (var file in new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "Patches")).GetFiles("*.json"))
                {
                    var patch = JsonSerializer.Deserialize<PatchFile>(File.ReadAllText(file.FullName), _jsonSerializerOptions);
                    
                    _patches.Add(new LoadedPatchFile()
                    {
                        Patch = patch,
                        SelectedVariant = null
                    });
                }
            }

            return true;
        }

        static void PrintWarningAndWaitConfirmation()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Quake Enhanced Patches");
            Console.ResetColor();
            Console.WriteLine("----------------------");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("You'll be able to select which patches you want to do.");
            Console.WriteLine("This program will patch the game binary directly, there's no guarantee that nothing will break. A backup will be performed.");
            Console.WriteLine("Patching might be considered cheating! USE AT YOUR OWN RISK.");
            Console.WriteLine("Press Y if you understand all of this.");

            Console.ResetColor();

            while (Console.ReadKey(true).Key != ConsoleKey.Y)
                continue;
        }

        /// <summary>
        /// Returns the SHA512 hash of the QuakePatches program .exe
        /// </summary>
        static string GetOwnHash()
        {
            var processName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName) + ".exe";

            using (var sha512 = SHA512.Create())
                return Convert.ToHexString(sha512.ComputeHash(File.ReadAllBytes(processName)));
        }

    }
}
