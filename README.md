# KCSG Unbound

## Overview
KCSG Unbound is an enhanced version of the KCSG (Krypt's Custom Structure Generation) system for RimWorld. It removes the limitation of 65,535 symbols in the vanilla KCSG implementation, enabling unlimited structure generation capabilities.

## Features
- **Unlimited Symbols**: Removes the 65,535 symbol limit in the original KCSG system
- **Enhanced Structure Generation**: Improves the existing structure generation system
- **Backward Compatibility**: Works alongside the original KCSG system without breaking existing mods
- **RimWorld 1.5 Support**: Fully compatible with RimWorld version 1.5

## Technical Architecture
KCSG Unbound consists of several core components that work together to enhance structure generation:

### Core Components
1. **SymbolRegistry**: Manages unlimited structure generation symbols, bypassing the original 65,535 limit
2. **SymbolResolver_KCSG**: Base class for all KCSG symbol resolvers
3. **KCSGPrepatch**: Provides Harmony patches for integration with the original KCSG system
4. **PrepatcherFields**: Enables integration with Zetrith's Prepatcher for early patching
5. **ResolveParamsExtensions**: Extends RimWorld's ResolveParams with custom properties

### Symbol Resolvers
- **SymbolResolver_Building**: Places specific buildings
- **SymbolResolver_RandomBuilding**: Places randomly selected buildings from a list

## Installation
1. Ensure you have [Zetrith's Prepatcher](https://github.com/Zetrith/Prepatcher) installed
2. Download the latest release of KCSG Unbound
3. Extract to your RimWorld Mods folder
4. Enable in the mod menu, placing after Prepatcher and before any mods that depend on KCSG

## For Developers
### Integration
To use KCSG Unbound in your mod:

1. Add a dependency in your About.xml:
```xml
<modDependencies>
  <li>
    <packageId>zetrith.prepatcher</packageId>
    <displayName>Prepatcher</displayName>
    <steamWorkshopUrl>steam://url/CommunityFilePage/2934420800</steamWorkshopUrl>
  </li>
  <li>
    <packageId>toast7312.kcsg.unbound</packageId>
    <displayName>KCSG Unbound</displayName>
    <steamWorkshopUrl>TBD</steamWorkshopUrl>
  </li>
</modDependencies>
```

2. Register your symbol resolvers during initialization:
```csharp
// Example: Register a custom symbol resolver
SymbolRegistry.Register("MyCustomSymbol", typeof(SymbolResolver_MyCustomThing));
```

### Creating Custom Symbol Resolvers
Custom symbol resolvers should extend `SymbolResolver_KCSG`:

```csharp
using KCSG;
using RimWorld;
using Verse;

public class SymbolResolver_MyCustomThing : SymbolResolver_KCSG
{
    protected override void ResolveInt(ResolveParams rp)
    {
        // Your symbol resolution logic here
        // Use helper methods from SymbolResolver_KCSG
    }
}
```

### Build Instructions
1. Ensure you have .NET SDK 4.7.2+ installed
2. Open the solution in Visual Studio, Rider, or Visual Studio Code
3. Restore NuGet packages
4. Build the solution
5. The compiled assembly will be placed in the 1.5/Assemblies directory

## Technical Implementation Notes

### Prepatcher Integration
KCSG Unbound uses Zetrith's Prepatcher for very early patching capabilities:

```csharp
[PrepatcherField]
public static extern ref bool IsPrepatched(this KCSGPrepatchData data);
```

This pattern allows KCSG Unbound to inject fields into the KCSGPrepatchData class at load time, before standard mods are initialized.

### Extending ResolveParams
To maintain compatibility with RimWorld's BaseGen system while adding custom functionality, we extend ResolveParams with extension methods:

```csharp
public static ThingDef GetThingDef(this ResolveParams rp) => rp.GetCustom<ThingDef>("thingDef");
public static void SetThingDef(this ResolveParams rp, ThingDef value) => rp.SetCustom("thingDef", value);
```

### Symbol Resolution
Symbols are resolved through a three-step process:
1. Check the main VEF's KCSG registry first (for backward compatibility)
2. Check our unlimited SymbolRegistry if not found in the main registry
3. Fall back to RimWorld's default resolution if not found in either

## License
MIT License

## Credits
- Original KCSG system by Oskar Potocki and Vanilla Expanded Framework team
- Prepatcher by Zetrith
- KCSG Unbound by TeH_Dav

## Contributing
Contributions are welcome! Please feel free to submit a pull request.

### Guidelines
- Follow the existing code style
- Add comments to explain complex logic
- Test thoroughly with various mod combinations
- Document any new features in the README

## Contact
GitHub: [TOAST7312](https://github.com/TOAST7312) 