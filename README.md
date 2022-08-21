# QuakePatches

A utility that provides easy management of binary patches for Quake Enhanced (2021 re-release).

This tool is made specifically for Quake Enhanced but can be easily adapted to any other binary.

## How to use

**This tool requires [.NET 6.0 Runtime](https://dotnet.microsoft.com/en-us/download) in order to run.**

1. Extract the tool in any folder
2. Either copy Quake_x64_steam.exe to the same folder or drag & drop it into the QuakePatches executable
3. Follow the instructions on screen.

## FAQ

### **Is this considered cheating?**
Officially we're not going to support cheating of any kind. However, it's possible that some patches might be considered cheating by other people, so use at your own risk and ask the community beforehand if you're in doubt.

### **How to restore the original executable?**
You can find a backup in the same folder as QuakePatches. A file named Quake_x64_steam.exe.original file will be present. This is a backup of the original unpatched quake executable.

### **Can I repatch multiple times?**
Yes. QuakePatches uses the backup as a source when applying patches so you don't need to provide it a clean executable every time.

### **QuakePatches stopped working after an update**
If the tool doesn't start, make sure to copy the updated executable and delete Quake_x64_steam.exe.original. This will ensure the tool gets a new backup of the newly updated binary.

Also patches are not guaranteed to work across updates. However some might!

### **How does it know which patches are applied?**
This info gets appended to the end of the game executable as json.

## Patches

Patches are defined in .json files. They contain a binary search pattern and a binary replacement. Each patch can also contain multiple variants allowing the user to choose different types of patching. An early example of this was to choose which type of crosshair the user wanted by patching the ascii code corresponding to what the user choose.
