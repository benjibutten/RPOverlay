# AI Agent Guidelines for RPOverlay

## Versioning Rules

### Version Format
Use **three-digit semantic versioning**: `MAJOR.MINOR.PATCH`

Examples:
- ✅ `0.10.1`, `1.0.0`, `2.5.3`
- ❌ `0.10`, `1.0` (two digits not allowed)

### Where Versions Are Defined
Update versions in: `RPOverlay.WPF/RPOverlay.WPF.csproj`
- `<Version>` tag: Use three digits (e.g., `0.10.1`)
- `<AssemblyVersion>` tag: Use four digits (e.g., `0.10.1`)
- `<FileVersion>` tag: Use four digits (e.g., `0.10.1`)

### When to Update Version
- **Minor bump**: Bug fixes, small features, improvements
- **Major bump**: Breaking changes, significant rewrites

### Example
```xml
<Version>0.10.1</Version>
<AssemblyVersion>0.10.1</AssemblyVersion>
<FileVersion>0.10.1</FileVersion>
```

---

## General Guidelines

### Code Quality
- Always run the build before committing changes
- Check for compilation errors using the build terminal
- Add debug logging for significant fixes

### User Communication
- Clearly explain what bug was fixed or feature was added
- Provide file paths when referencing code changes
