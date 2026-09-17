# Zongsoft .NET/C# rule catalog

[English](RULES.md) | [简体中文](RULES.zh-Hans.md)

This page documents diagnostic severity, triggers, exceptions and fixes in `Zongsoft.CodeAnalysis`. The [development guidelines](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) retain coding principles and design requirements; see the [README](https://github.com/Zongsoft/Guidelines/blob/main/README.md#code-analysis) for setup and validation commands.

[Index](#index) · [Custom rules](#custom-rules) · [SDK rules](#sdk-rules) · [Exceptions](#suppressions) · [Configuration](#configuration) · [Review](#review) · [References](#references)

> 💡 **Open a rule from VS:** Custom diagnostics `ZS0005`, `ZS1304` and `ZS2003` carry help links to stable anchors in the Chinese catalog. Use the diagnostic help link or the rule code in the Error List; selecting an entry and pressing `F1` also opens help. SDK IDE/CA and compiler diagnostics retain their official links. Opening help and applying a `Ctrl+.` code fix are separate actions.

This catalog describes the current main branch; actual behavior depends on the installed package and SDK versions. Merge new documentation to GitHub's `main` branch, publish the updated package and upgrade consuming projects for both online links and new behavior to become available. Help links open the Chinese page by default; its language switch opens this English version. Both versions retain identical rule anchors.

<a id="index"></a>

## Rule index

Severities below come from the packaged [Global AnalyzerConfig](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) and [build settings](https://github.com/Zongsoft/Guidelines/blob/main/analysis/Zongsoft.CodeAnalysis.targets). Project settings may override them. Strict mode means `ZongsoftCodeStyleStrict=true`. “SDK” denotes host-provided fixes; available actions depend on the diagnostic and SDK version.

| ID | Rule | Default / strict build | Fix |
| --- | --- | --- | --- |
| [ZS0005](#zs0005) | Remove unused imports, allowing ordinary `using System;` | Warning / error | ✅ Remove unused using |
| [ZS1304](#zs1304) | Access fixed resource entries through generated properties | Warning / error | Manual resource-access migration |
| [ZS2003](#zs2003) | Separate specified statement groups | Warning / error | ✅ Insert blank line |
| [IDE0009](#ide0009) | Qualify instance member access with `this.` | Warning / error | SDK |
| [IDE0049](#ide0049) | Use C# type keywords | Warning / not run during builds | SDK |
| [IDE0055](#ide0055) | Whitespace, indentation and line breaks | Warning / error | SDK; review exceptions |
| [IDE0065](#ide0065) | Place using directives outside namespaces | Warning / error | SDK |
| [IDE0161](#ide0161) | Use file-scoped namespaces | Warning / error | SDK |
| [IDE1006](#ide1006) | Symbol naming | Warning / error | SDK; review public contracts |
| [IDE2001](#ide2001) | Put embedded statements on separate lines | Warning / error | SDK; review exceptions |
| [CA1303](#ca1303) | Localize user-visible text | Warning / error | No custom resource-migration fix |
| [CA2012](#ca2012) | Consume ValueTask correctly | Error / error | Follow the async contract |
| [CS4014](#cs4014) | Observe asynchronous task completion | Error / error | Follow task ownership |

[Intentionally disabled diagnostics](#disabled-rules), [SDK suggestions](#suggestions), [ZSS exceptions](#suppressions) and [ZSCFG configuration errors](#configuration) are listed separately from source warnings. A rule without a custom fix is not a promise of safe one-click conversion.

<a id="custom-rules"></a>

## Custom rules

<a id="zs0005"></a>

### ZS0005 · Remove unused imports

- **Trigger:** Compiler diagnostic CS8019 identifies an unused import; the analyzer reports each directive separately.
- **Exception:** Only ordinary `using System;` resolving to the global System namespace is allowed. `System.*`, aliases, `using static` and `global using` are not exempt.
- **Build requirement:** Enable `GenerateDocumentationFile=true`; this package does not override a project's choice to disable XML documentation.
- **Fix:** `Ctrl+.` → **Remove unused using**, including Fix All in a document, project or solution. Comments and directives are retained.
- **Configuration:** `dotnet_diagnostic.ZS0005.severity`. The original [IDE0005](#ide0005) is disabled because its grouping of consecutive imports conflicts with the System exception.

File-header fragment, assuming the body does not use any System.Text types:

```csharp
using System;      //Allowed
using System.Text; //ZS0005: remove this directive
```

This rule only checks usage. Alias restrictions, global imports, dependency grouping and namespace-length ordering remain review requirements under guideline 3.2.

<a id="zs1304"></a>

### ZS1304 · Use generated resource properties

- **Trigger:** A direct `ResourceManager.GetString`, `GetObject` or `GetStream` call whose first argument is a compile-time nonempty string key, including calls through ResourceManager-derived types.
- **Exceptions:** Dynamic keys and generated code are excluded. Unrelated methods with the same names are not reported.
- **Fix:** Maintain the entry in the neutral `.resx` and access the property generated by `ResXFileCodeGenerator`. No automatic fix is provided: replacing a string with an assumed property can be unsafe.
- **Configuration:** `dotnet_diagnostic.ZS1304.severity`. [CA1303](#ca1303) helps find hardcoded text that needs translation.

Method-body fragments, assuming `manager` is a ResourceManager and the neutral resource defines `InvalidValue`:

```csharp
//Before: fixed key bypasses the generated property
var message = manager.GetString("InvalidValue");
```

```csharp
//After: property generated by the custom tool
var message = Properties.Resources.InvalidValue;
```

These are separate before/after fragments. Parameter names, protocol fields, paths and machine-readable text are not translation content. Custom lookup wrappers, resource synchronization and text purpose still require review.

<a id="resource-generation"></a>

#### Resource generation

Generate and commit `*.Designer.cs` for neutral resources; culture-specific files contain translations only. SDK-style C# project fragment:

```xml
<ItemGroup>
	<EmbeddedResource Update="Properties/Resources.resx">
		<Generator>ResXFileCodeGenerator</Generator>
		<LastGenOutput>Resources.Designer.cs</LastGenOutput>
	</EmbeddedResource>
	<Compile Update="Properties/Resources.Designer.cs">
		<DesignTime>true</DesignTime>
		<AutoGen>true</AutoGen>
		<DependentUpon>Resources.resx</DependentUpon>
	</Compile>
</ItemGroup>
```

Ordinary `dotnet build` does not run Visual Studio custom tools. Regenerate and commit the Designer file after resource changes; CI compiles the committed file. The analyzer itself combines `LocalizableResourceString` and `nameof(generated property)` to defer language selection, distributing English neutral resources and a `zh-Hans` satellite assembly.

<a id="zs2003"></a>

### ZS2003 · Separate statement groups

**A blank line is required at only these two kinds of boundaries:**

1. More than two consecutive assignment or initialization statements immediately followed by `if`, `switch`, `for`, `foreach`, `while` or `do`.
2. Adjacent independent control structures or explicit blocks at the same level. Control structures include those conditions and loops, plus `try`, `using`, `lock`, `checked`/`unchecked` and `unsafe` blocks.

**Counting and exceptions:** Initialized local or constant declarations and ordinary, compound and deconstruction assignments count by statement. A multi-variable declaration or deconstruction counts once. A blank line or another statement kind ends the run; comments do not replace blank lines. One or two assignments, or assignments followed by a call or return, do not require separation on that basis. Related `if/else`, `try/catch/finally` and `do/while` clauses and nested bodies remain together. Applies to blocks, switch sections and top-level statements; generated code is excluded.

Method-body fragment, assuming `items` is an integer array and `Process(int)` exists. Separate three initializations from the following condition:

```csharp
var index = 0;
var count = items.Length;
var enabled = count > 0;

if(enabled)
	Process(items[index]);
```

This fragment is compliant. Assume `GetCreator(type)` returns a creation delegate, `map` is an optional mapping delegate and `state` is its state argument:

```csharp
var entity = GetCreator(type)();
map?.Invoke(entity, state);
return entity;
```

Separate adjacent independent control structures too, using the same `items`, `enabled` and `Process(int)` context:

```csharp
for(var index = 0; index < items.Length; index++)
	items[index]++;

if(enabled)
	Array.Reverse(items);

foreach(var item in items)
	Process(item);
```

**Fix:** `Ctrl+.` → **Insert blank line**, including Fix All in a document, project or solution. Only the reported boundary changes; indentation, CRLF/LF, comments and directives are preserved without formatting unrelated code. Configure `dotnet_diagnostic.ZS2003.severity`.

<a id="sdk-rules"></a>

## SDK and compiler rules

These sections describe Zongsoft settings and limits. Follow the linked official references for language semantics and SDK implementation details.

<a id="ide0009"></a>

### IDE0009 · Instance member qualification

Use `this.` for instance properties, methods and events: `dotnet_style_qualification_for_property/method/event = true`. Access private fields as `_camelCase`. The field option cannot distinguish visibility; qualification of public fields requires review. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0003-ide0009)

<a id="ide0049"></a>

### IDE0049 · C# type keywords

Use `int`, `string` and other keywords in declarations and static member access, configured through `dotnet_style_predefined_type_for_locals_parameters_members` and `dotnet_style_predefined_type_for_member_access`. This rule does not run during builds; use the editor or `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes`. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0049)

<a id="ide0055"></a>

### IDE0055 · Formatting

Use Tab indentation and Allman braces for multiline blocks, no space between control keywords and parentheses, and spaces around binary operators. Root `.editorconfig` supplies file endings and file-type exceptions. See [diagnostic exceptions](#suppressions) for conditional directives and compact exception handling.

`dotnet format whitespace` compares formatted text directly without applying diagnostic suppressors and can report permitted layouts. Use analyzer-enabled build diagnostics for these exceptions. Limit formatting to changed files and review the diff. [Official formatting rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0055)

<a id="ide0065"></a>

### IDE0065 · Using placement

`csharp_using_directive_placement = outside_namespace` places imports before the namespace. It does not enforce sorting, grouping or alias restrictions. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0065)

<a id="ide0161"></a>

### IDE0161 · File-scoped namespaces

`csharp_style_namespace_declarations = file_scoped` prefers `namespace Example;`. Do not change assemblies, RootNamespace or domain boundaries just to match layout. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0160-ide0161)

<a id="ide1006"></a>

### IDE1006 · Naming

Interfaces use an `I` prefix, types and members PascalCase, parameters and ordinary locals camelCase, private fields `_camelCase` and type parameters a `T` prefix. Private, internal and local constants use UPPER_SNAKE_CASE; public or protected constants use PascalCase. A method-local `const int SIZE = sizeof(short);` is compliant.

Naming rules distinguish symbol kind, visibility and `const`; more specific constant rules take precedence. In addition to individual naming-rule severity, `dotnet_diagnostic.IDE1006.severity = warning` enables build diagnostics. Domain meaning, type suffixes and external contracts require review. Check compatibility before automatically renaming public APIs. [Official naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)

<a id="ide2001"></a>

### IDE2001 · Separate embedded statements

Simple branches may omit braces, but their statements start on another line. `csharp_style_allow_embedded_statements_on_same_line_experimental = false` is experimental and must be rechecked after SDK upgrades. [ZSS2001](#zss2001) exempts eligible compact try/catch/finally clauses. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide2001)

<a id="ca1303"></a>

### CA1303 · Localize visible text

Reports hardcoded Console text and strings passed to parameters or properties marked `Localizable(true)`. The package enables `dotnet_code_quality.CA1303.use_naming_heuristic = true` for Text/Message/Caption naming heuristics. Follow [resource generation](#resource-generation), then access Designer properties.

Heuristics cannot determine every string's purpose. Review machine text, protocol keys, paths and custom UI/logging APIs; use `Localizable(false)` on parameters or properties that clearly do not need translation where appropriate. No custom resource-migration fix is provided. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1303)

<a id="ca2012"></a>

### CA2012 · ValueTask consumption

Consume a ValueTask once by default; do not await it repeatedly, call AsTask repeatedly or mix both approaches. If sharing is necessary, convert once and retain the Task. The rule catches only some misuse; review lifetimes too. It is an error in normal and strict builds. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2012)

<a id="cs4014"></a>

### CS4014 · Unawaited tasks

Await, return or transfer asynchronous tasks to an explicit lifecycle owner. Writing `_ = ...` does not provide exception observation, cancellation or shutdown waiting. This compiler diagnostic catches only some unawaited calls; it is an error in normal and strict builds. [Official diagnostic](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs4014)

<a id="disabled-rules"></a>

### Intentionally disabled diagnostics

These rules are `none` in the package configuration. Projects may deliberately override them without restoring bulk transformations that conflict with the guidelines.

| ID | Reason |
| --- | --- |
| <a id="ide0005"></a>IDE0005 | Replaced by [ZS0005](#zs0005) to retain ordinary System imports |
| <a id="ide0001"></a>IDE0001 | Preserve `global::` names used to resolve conflicts |
| <a id="ide0002"></a>IDE0002 | Avoid automatically simplifying those qualified accesses |
| <a id="ide0003"></a>IDE0003 | Do not uniformly simplify `this.`; fields cannot be configured by visibility |
| <a id="ide0130"></a>IDE0130 | Namespaces follow RootNamespace and domains, not folders mechanically |
| <a id="ide0210"></a>IDE0210 | Avoid bulk conversion to top-level statements |
| <a id="ide0211"></a>IDE0211 | Avoid bulk conversion to explicit Main |
| <a id="ide0290"></a>IDE0290 | Avoid bulk conversion to primary constructors |

<a id="suggestions"></a>

### SDK style suggestions

Context-dependent choices such as `var`, expression bodies, readonly fields, target-typed `new`, null handling and pattern matching remain suggestions or silent preferences; package strict mode does not uniformly promote them to errors. Collection-expression suggestions require exactly matching target types. Prefer `using(...)` blocks; use `using var` when the resource should live until the scope ends.

See the [global configuration](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) for all options and the [Microsoft rule index](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/) for SDK diagnostics. Revalidate after SDK upgrades; editor suggestions do not necessarily run during builds.

<a id="suppressions"></a>

## Diagnostic exceptions

ZSS identifiers identify suppressors, not new source warnings. They have no independent Roslyn diagnostic help link; remaining IDE warnings retain their official links.

<a id="zss0055"></a>

### ZSS0055 · Conditional-directive indentation

Suppresses IDE0055 only for whitespace before `#if`, `#elif`, `#else` and `#endif`. Directives may be at column zero or aligned with surrounding code, without extra indentation for nested conditions. Other expression-formatting issues are not exempt.

<a id="zss0056"></a>
<a id="zss2001"></a>

### ZSS0056 / ZSS2001 · Compact exception handling

When every try/catch/finally clause occupies one line and contains one simple statement, suppress block-boundary IDE0055 and block-local IDE2001 respectively. Multiple statements, nested control flow, multiline expressions and formatting inside statements are not exempt.

Property fragment, assuming `_lock` is the object's ReaderWriterLockSlim and `_list` is the protected collection:

```csharp
public int Count
{
	get
	{
		_lock.EnterReadLock();
		try { return _list.Count; }
		finally { _lock.ExitReadLock(); }
	}
}
```

<a id="configuration"></a>

## 🛠️ EditorConfig synchronization configuration

Set `ZongsoftGuidelinesSynchronization` to a destination directory for automatic synchronization during actual builds; unset or empty disables it. VS fast up-to-date checking remains enabled; use Rebuild or Clean followed by Build when needed. Standalone clean, restore and design-time builds do not synchronize. See [EditorConfig synchronization](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md#editorconfig-synchronization).

<a id="zscfg001"></a>
<a id="zscfg002"></a>
<a id="zscfg003"></a>

**ZSCFG001:** The configured synchronization destination is missing or is not a directory. Set the property to an existing repository root; relative paths resolve against the consuming project directory. Missing files are created automatically; files whose size or modification time differs are replaced; write failures fail the build. This is a build configuration error, not a Roslyn source diagnostic.

<a id="review"></a>

## Manual review

Passing analysis does not establish full compliance. Continue reviewing design, contracts and tests for:

- Alias restrictions, production global imports, test Global.cs, dependency groups and namespace-length ordering.
- Chinese responsibility regions, member ordering, adjacent overloads, domain names, external interface names and public compatibility.
- Localization intent, fixed-key lookup wrappers, generator configuration and ResX/Designer synchronization.
- Constructor arguments, comparers, enumeration timing, resource ownership, cancellation, concurrency and background-task lifetimes.
- Documentation, plugin composition, exception contracts and test quality. Repository-wide warning suppression or bulk public-API rewriting is not a substitute for review.

<a id="references"></a>

## References

- [Microsoft code-style rule index](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/): reference for organization by ID, rule and option.
- [DiagnosticDescriptor helpLinkUri](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnosticdescriptor.-ctor): custom diagnostic help links.
- [Visual Studio Error List](https://learn.microsoft.com/en-us/visualstudio/ide/error-list-window): source navigation and online help.
- [Analyzer configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files) and [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format): precedence, read-only checks and fix scope.
- [DiagnosticSuppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor): exceptions for individual diagnostic locations.

> 🔗 **Maintenance:** Rule anchors use lowercase diagnostic IDs, such as `#zs2003`, and must survive heading changes. New diagnostics require updates to both catalogs, help links, severity, fix status and tests. Retain old anchors so links from published packages keep working.
