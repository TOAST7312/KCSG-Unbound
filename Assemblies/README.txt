KCSG Unbound - Compilation Instructions
=======================================

To compile this mod:

1. Open the KCSGUnbound.sln file in Visual Studio (2019 or later recommended)

2. Make sure you have:
   - .NET Framework 4.7.2 development tools
   - NuGet Package Manager 

3. The project should automatically restore NuGet packages, including:
   - Krafs.Rimworld.Ref (RimWorld assemblies)
   - Lib.Harmony
   - Zetrith.Prepatcher

4. Build the solution (F6 or Build > Build Solution)
   - The compiled DLL will automatically be placed in the Assemblies folder

5. If you encounter reference errors, make sure the following DLLs are present in the Source folder:
   - Assembly-CSharp.dll
   - UnityEngine.dll
   - UnityEngine.CoreModule.dll
   - UnityEngine.IMGUIModule.dll
   - UnityEngine.TextRenderingModule.dll
   
   These can be copied from your RimWorld installation's RimWorldWin64_Data/Managed folder.

6. The project is configured to build directly to the Assemblies folder of the mod.

7. After successful compilation, you can run RimWorld with the mod enabled to test it.

Troubleshooting:
- If you get "Could not find or load a specific file" errors, make sure all reference paths are correct.
- If you get "Type or namespace not found" errors, make sure all required assemblies are referenced.
- For Prepatcher issues, ensure you have the 0PrepatcherAPI.dll in the Assemblies folder. 