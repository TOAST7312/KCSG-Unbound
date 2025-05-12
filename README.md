# KCSG Unbound - Extended Def Limit

KCSG Unbound removes the 65,535 definition limitation in RimWorld, specifically targeting SymbolDefs used in procedural structure generation.

## What This Mod Does

RimWorld has a hard-coded limit of 65,535 definitions (Defs) per type, including SymbolDefs used in structure generation. This is due to the game using a 16-bit unsigned integer (ushort) for indexing. When you have many mods that add new structures, settlements, and custom symbols, you can hit this limit, causing errors or broken functionality.

KCSG Unbound provides an alternative registration system that:
1. Intercepts SymbolDef registration
2. Stores symbols in an unlimited dictionary instead of the vanilla array
3. Handles lookups transparently for both old and new defs

## Features

- **Unlimited SymbolDefs**: No more hitting the 65,535 limit for structure generation
- **Compatible with existing mods**: Works with Vanilla Expanded Framework and other KCSG-based mods
- **Monitoring system**: Includes a debug window (in dev mode) to track symbol usage
- **Early patching**: Uses Harmony in the mod constructor to patch before def loading begins
- **Improved error handling**: Robust exception handling for better stability
- **Rendering detection**: Smart detection of rendering contexts to prevent UI errors

## Technical Details

The mod employs two primary strategies to bypass the 65,535 def limit:

1. **Custom Registry**: All SymbolDefs are registered in a parallel dictionary-based system with unlimited capacity
2. **Transparent Lookup**: Intercepts DefDatabase.GetNamed and GetByShortHash calls to check both registries
3. **Early Harmony Patching**: Applies patches during mod initialization before def loading begins

### Implementation Details

- The SymbolRegistry class maintains high-capacity dictionaries for both symbols and defs
- Harmony patches intercept critical methods to ensure our custom registry is checked
- Error handling includes fallbacks and recovery mechanisms
- SymbolDefMonitor provides real-time statistics and debugging information

## For Modders

If you're a mod author adding many SymbolDefs, you don't need to change anything in your code. This mod will automatically handle the registration and resolution of your defs.

For more advanced usage, you can directly use the SymbolRegistry API:

```csharp
// Register a symbol resolver
SymbolRegistry.Register("MyCustomSymbol", typeof(MyCustomSymbolResolver));

// Register a symbol def directly
SymbolRegistry.RegisterDef("MyCustomSymbolDef", mySymbolDefInstance);

// Check if a symbol is registered
bool exists = SymbolRegistry.IsRegistered("MyCustomSymbol");

// Get monitoring information
string status = SymbolRegistry.GetStatusReport();
```

## For Players

1. Subscribe to this mod and its dependencies:
   - Harmony
2. Make sure it loads after Harmony and before any mods that add many structure generation symbols
3. Enjoy your heavily modded game without the 65,535 symbol limit!

To monitor your symbol usage (in Dev Mode):
1. Enable Dev Mode in the options
2. Click the "Symbol Monitor" button in the top-right corner of the screen
3. View detailed statistics about your registered symbols

## Compatibility

- Compatible with RimWorld 1.4 and 1.5
- Works with Vanilla Expanded Framework
- Compatible with mods adding custom structure generation

## Troubleshooting

If you encounter issues:
1. Check the KCSGUnbound_runtime_log.txt and KCSGUnbound_basegen_analysis.txt files in your RimWorld directory
   - These logs are generated when you click "Copy statistics to log" in the Symbol Monitor
   - They contain detailed information about registered symbols and errors
2. Enable Dev Mode and use the Symbol Monitor to check for any errors
3. Make sure this mod loads after Harmony but before content mods
4. If you see red "KCSG Errors!" button, click it for details about what's going wrong

## Credits

- Original KCSG system by Vanilla Expanded Framework team
- Extended and maintained by community contributors

## License

GNU General Public License v3.0 - See LICENSE file for details 