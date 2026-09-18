# Zongsoft .NET/C# rule catalog

[English](RULES.md) | [简体中文](RULES.zh-Hans.md)

This page documents diagnostic severity, triggers, exceptions and fixes in `Zongsoft.CodeAnalysis`. The [development guidelines](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) retain coding principles and design requirements; see the [README](https://github.com/Zongsoft/Guidelines/blob/main/README.md#code-analysis) for setup and validation commands.

[Index](#index) · [Custom rules](#custom-rules) · [SDK rules](#sdk-rules) · [Exceptions](#suppressions) · [Configuration](#configuration) · [Review](#review) · [References](#references)

> 💡 **Open a rule from VS:** Custom diagnostics `ZS0005`, `ZS1301`, `ZS1302`, `ZS1304` and `ZS2003` carry help links to stable anchors in the Chinese catalog. Use the diagnostic help link or the rule code in the Error List; selecting an entry and pressing `F1` also opens help. SDK IDE/CA and compiler diagnostics retain their official links. Opening help and applying a `Ctrl+.` code fix are separate actions.

This catalog describes the current main branch; actual behavior depends on the installed package and SDK versions. Merge new documentation to GitHub's `main` branch, publish the updated package and upgrade consuming projects for both online links and new behavior to become available. Help links open the Chinese page by default; its language switch opens this English version. Both versions retain identical rule anchors.

<a id="index"></a>

## Rule index

Severities below come from the packaged [Global AnalyzerConfig](https://github.com/Zongsoft/Guidelines/blob/main/analysis/analyzers/src/Zongsoft.CodeAnalysis.Analyzers.globalconfig) and [build settings](https://github.com/Zongsoft/Guidelines/blob/main/analysis/Zongsoft.CodeAnalysis.targets). Project settings may override them. Strict mode means `ZongsoftCodeStyleStrict=true`. “SDK” denotes host-provided fixes; available actions depend on the diagnostic and SDK version.

| ID | Rule | Default / strict build | Fix |
| --- | --- | --- | --- |
| [ZS0005](#zs0005) | Remove unused alias, static and global imports | Warning / error | ✅ Remove unused using |
| [ZS1301](#zs1301) | Localize exception messages | Warning / error | Manual migration |
| [ZS1302](#zs1302) | Localize non-ASCII text | Warning / error | Manual migration |
| [ZS1304](#zs1304) | Access fixed resource entries through generated properties | Warning / error | Manual resource-access migration |
| [ZS2003](#zs2003) | Separate specified statement groups | Warning / error | ✅ Insert blank line |
| [IDE0009](#ide0009) | Qualify instance member access with `this.` | Warning / error | SDK |
| [IDE0049](#ide0049) | Use C# type keywords | Warning / not run during builds | SDK |
| [IDE0055](#ide0055) | Whitespace, indentation and line breaks | Warning / error | SDK; review exceptions |
| [IDE0065](#ide0065) | Place using directives outside namespaces | Warning / error | SDK |
| [IDE0161](#ide0161) | Use file-scoped namespaces | Warning / error | SDK |
| [IDE1006](#ide1006) | Symbol naming | Warning / error | SDK; review public contracts |
| [IDE2001](#ide2001) | Put embedded statements on separate lines | Warning / error | SDK; review exceptions |
| [CA2012](#ca2012) | Consume ValueTask correctly | Error / error | Follow the async contract |
| [CS4014](#cs4014) | Observe asynchronous task completion | Error / error | Follow task ownership |

[Intentionally disabled diagnostics](#disabled-rules), [SDK suggestions](#suggestions), [ZSS exceptions](#suppressions) and [ZSCFG configuration errors](#configuration) are listed separately from source warnings. A rule without a custom fix is not a promise of safe one-click conversion.

<a id="custom-rules"></a>

## Custom rules

<a id="zs0005"></a>

### ZS0005 · Remove unused imports

- **Trigger:** Compiler diagnostic CS8019 identifies an unused import; the analyzer reports each directive separately.
- **Exception:** Ordinary imports of any namespace are allowed. Aliases, `using static` and `global using` are not exempt.
- **Build requirement:** Enable `GenerateDocumentationFile=true`; this package does not override a project's choice to disable XML documentation.
- **Fix:** `Ctrl+.` → **Remove unused using**, including Fix All in a document, project or solution. Comments and directives are retained.
- **Configuration:** `dotnet_diagnostic.ZS0005.severity`. The original [IDE0005](#ide0005) is disabled because its grouping of consecutive imports conflicts with the ordinary-namespace exception.

This rule only checks usage. Alias restrictions, global imports, dependency grouping and namespace-length ordering remain review requirements under guideline 3.2.

<a id="zs1301"></a>

### ZS1301 · Localize exception messages

- **Trigger:** An object creation of `System.Exception` or a derived class supplies directly written, non-whitespace string text to a declared `string message` parameter (exact name, case-insensitive). Positional/named arguments, ordinary construction, `throw new` and target-typed `new(...)` behave alike.
- **Scope:** String literals, parentheses/built-in casts, fixed built-in concatenation fragments, interpolation text and directly written string interpolation expressions. `System.String.Format` checks the template, not formatting data arguments. Each fixed fragment is reported once.
- **Deliberately untracked:** Variables, const identifiers, fields, properties, arbitrary return values, exception factories, constructor chains, user-defined operators/conversions and conditional expressions. Names such as `reason`, `text`, `errorMessage` and `paramName` are not guessed. Omitted optional defaults are not checked at call sites.
- **Exceptions:** null, empty and whitespace-only text. Direct numeric, punctuation, Emoji, ANSI and drawing messages still report. `LocalizableAttribute` has no effect.
- **Fix:** Use a generated resource property or resource format template. Resource keys use dots, exception message keys end in `.Message`, and equivalent messages should reuse an entry. No resource-migration fix is provided.
- **Configuration:** `dotnet_diagnostic.ZS1301.severity`; warning normally, error in strict mode.

```csharp
throw new Exception("Failed.");                            //ZS1301
throw new Exception(message: $"Invalid value: {value}");   //ZS1301
throw new Exception(Properties.Resources.Failure_Message); //Resource access
throw new Exception(message);                             //Not traced
```

The caller supplies resource properties and variables in these fragments. No diagnostic does not prove localization; the rule checks direct text, not its runtime provenance.

<a id="zs1302"></a>

### ZS1302 · Localize non-ASCII text

- **Trigger:** Handwritten C# strings contain non-ASCII letters, including Chinese/Japanese/Korean/Thai, Cyrillic, Greek, Arabic, accented Latin and combining marks attached to letters.
- **All locations:** Ordinary, verbatim, raw and UTF-8 literals; interpolation text and format text; locals, fields, property initializers, attributes and defaults. Paths, JSON keys, regex and technical keys have no allowlist. Decoded `\u`/`\U` escapes behave like literal characters; values assembled across expressions are not traced.
- **Character boundary:** Pinned Unicode 17.0 Letter/Mark categories and Emoji properties. ASCII letters, Emoji, icons, punctuation, ordinary mathematical symbols, controls, private-use icons and isolated marks do not trigger on their own. Both `é` and `e\u0301` trigger; variation selectors do not. Emoji mixed with Chinese still triggers.
- **Excluded input:** Comments, ordinary identifiers, character literals, generated code and `.resx`. Runtime values are not inferred. The rule does not guess a language, and `LocalizableAttribute` does not exempt text.
- **Deduplication:** A fragment checked as an exception message reports ZS1301 only. Disabling ZS1301 still allows ZS1302.
- **Fix:** Move text to resources. Constant-only contexts such as attributes and defaults require redesign, not an invalid property substitution. No automatic fix is supplied.
- **Configuration:** `dotnet_diagnostic.ZS1302.severity`; warning normally, error in strict mode.

```csharp
var title = "你好";       //ZS1302
var title = "café";      //ZS1302
var icon = "ℹ️ 👩🏽‍💻";  //Allowed
var title = "ℹ️ 提示";  //ZS1302
```

**Shared boundary:** ZS1301, ZS1302 and ZS1304 do not register checks when `IsTestProject=true`, in both IDE and builds. Missing/false values enable checks. No project-name, folder or test-framework heuristics are used. Generated code is excluded. A test project's exemption does not propagate to production references.

Unicode data and license: [categories](https://www.unicode.org/Public/17.0.0/ucd/extracted/DerivedGeneralCategory.txt), [Emoji](https://www.unicode.org/Public/17.0.0/ucd/emoji/emoji-data.txt), [regeneration](https://github.com/Zongsoft/Guidelines/blob/main/analysis/README.md#unicode). Tables are regenerated by maintainers; builds and analysis require no network access.

<a id="zs1304"></a>

### ZS1304 · Use generated resource properties

- **Trigger:** A direct `ResourceManager.GetString`, `GetObject` or `GetStream` call whose first argument is a compile-time nonempty string key, including calls through ResourceManager-derived types.
- **Exceptions:** Test projects, dynamic keys and generated code are excluded. Unrelated methods with the same names are not reported.
- **Fix:** Maintain the entry in the neutral `.resx` and access the property generated by `ResXFileCodeGenerator`. No automatic fix is provided: replacing a string with an assumed property can be unsafe.
- **Configuration:** `dotnet_diagnostic.ZS1304.severity`. [ZS1301](#zs1301) and [ZS1302](#zs1302) find hardcoded text that needs translation.

Method-body fragments, assuming `manager` is a ResourceManager and the neutral resource defines `InvalidValue`:

```csharp
//Before: fixed key bypasses the generated property
var message = manager.GetString("InvalidValue");
```

```csharp
//After: property generated by the custom tool
var message = Properties.Resources.InvalidValue;
```

These are separate before/after fragments. ZS1304 checks fixed resource access; string content is checked separately by ZS1301 and ZS1302. Custom lookup wrappers and resource synchronization require review.

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

Consecutive simple control statements of the same kind (`if`, `for`, `foreach` or `while`) may omit blank lines; different kinds must be separated by a blank line. Each body must be a single non-control statement without braces, and an `if` must have no `else`. A block, an `else`, or a nested control structure still requires separation.

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

This compliant method-body fragment groups controls by kind: blank lines are optional within a same-kind group and required between different kinds.

```csharp
if(enabled)
	;
if(disabled)
	;

for(var i = 0; i < items.Length; i++)
	;
for(var j = 0; j < entries.Count; j++)
	;

foreach(var item in items)
	;
foreach(var entry in entries)
	;

while(enabled)
	;
while(disabled)
	;
```

**Fix:** `Ctrl+.` → **Insert blank line**, including Fix All in a document, project or solution. Only the reported boundary changes; indentation, CRLF/LF, comments and directives are preserved without formatting unrelated code. Configure `dotnet_diagnostic.ZS2003.severity`.

<a id="sdk-rules"></a>

## SDK and compiler rules

These sections describe Zongsoft settings and limits. Follow the linked official references for language semantics and SDK implementation details.

<a id="ide0009"></a>

### IDE0009 · Instance member qualification

Use `this.` for instance properties, methods and events: `dotnet_style_qualification_for_property/method/event = true`. Access private fields directly by name. The field option cannot distinguish visibility; qualification of public fields requires review. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0003-ide0009)

<a id="ide0049"></a>

### IDE0049 · C# type keywords

Use `int`, `string` and other keywords in declarations and static member access, configured through `dotnet_style_predefined_type_for_locals_parameters_members` and `dotnet_style_predefined_type_for_member_access`. This rule does not run during builds; use the editor or `dotnet format style <project> --diagnostics IDE0049 --verify-no-changes`. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0049)

<a id="ide0055"></a>

### IDE0055 · Formatting

Use Tab indentation and Allman braces for multiline blocks, no space between control keywords and parentheses, and spaces around binary operators. Root `.editorconfig` supplies file endings and file-type exceptions. See [diagnostic exceptions](#suppressions) for directives, local alignment and compact layouts.

`dotnet format whitespace` compares formatted text directly without applying diagnostic suppressors and can report permitted layouts. Use analyzer-enabled build diagnostics for these exceptions. Limit formatting to changed files and review the diff. [Official formatting rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0055)

<a id="ide0065"></a>

### IDE0065 · Using placement

`csharp_using_directive_placement = outside_namespace` places imports before the namespace. It does not enforce sorting, grouping or alias restrictions. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0065)

<a id="ide0161"></a>

### IDE0161 · File-scoped namespaces

`csharp_style_namespace_declarations = file_scoped` prefers `namespace Example;`. Do not change assemblies, RootNamespace or domain boundaries just to match layout. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0160-ide0161)

<a id="ide1006"></a>

### IDE1006 · Naming

Interfaces use an `I` prefix, types and members PascalCase, parameters and ordinary locals camelCase, private fields `_camelCase` by default and type parameters a `T` prefix. Private, internal and local constants use UPPER_SNAKE_CASE; public or protected constants use PascalCase. A method-local `const int SIZE = sizeof(short);` is compliant.

Private fields may also use [uppercase names or names enclosed in underscores](#zss1006).

Naming rules distinguish symbol kind, visibility and `const`; more specific constant rules take precedence. In addition to individual naming-rule severity, `dotnet_diagnostic.IDE1006.severity = warning` enables build diagnostics. Domain meaning, type suffixes and external contracts require review. Check compatibility before automatically renaming public APIs. [Official naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)

<a id="ide2001"></a>

### IDE2001 · Separate embedded statements

Simple branches may omit braces, but their statements start on another line. `csharp_style_allow_embedded_statements_on_same_line_experimental = false` is experimental and must be rechecked after SDK upgrades. [ZSS2001](#zss2001) exempts eligible compact try/catch/finally clauses; [ZSS2002](#zss2002) exempts empty `while(...);` loops; [ZSS2003](#zss2003) exempts single-line method and anonymous function bodies with at most two simple statements. [Official rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide2001)

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
| <a id="ca1303"></a>CA1303 | Localization scope is defined by [ZS1301](#zs1301)/[ZS1302](#zs1302); consumers may explicitly enable native SDK checks |
| <a id="ide0005"></a>IDE0005 | Unused imports are checked by [ZS0005](#zs0005), which permits ordinary namespace imports |
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

### ZSS0055 · Directive indentation

Suppresses IDE0055 for indentation before all `#` directives, including `#pragma`, `#region`, `#nullable`, `#line` and conditional directives. Directives may be at column zero or aligned with surrounding code, without extra indentation for nested conditions. Other expression-formatting issues are not exempt.

<a id="zss0056"></a>
<a id="zss2001"></a>

### ZSS0056 / ZSS2001 · Compact exception handling

Each try/catch/finally clause is evaluated separately: a single-line clause containing at most two direct statements is exempt from block-boundary IDE0055 and block-local IDE2001. Statement types are unrestricted, including break, continue, yield break and control flow; nested statements are not counted again in the outer clause, while local and anonymous function bodies are checked independently. Adjacent compact clauses may share a line, and compact and multiline clauses may be mixed. More than two direct statements, multiline layouts and formatting inside expressions are not exempt.

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

<a id="zss0057"></a>

### ZSS0057 · Local alignment and compact boundaries

Suppresses IDE0055 only at these locations; other formatting inside statements remains checked:

- Alignment whitespace between a declaration type and variable name, or before its initializer `=`.
- Binary/conditional expression continuations aligned with the expression start, including operands inside lambdas, throw statements, parentheses and negation. Conditional assignments may also align with `=`.
- Subsequent call arguments aligned with the first argument; constructor arguments may also align with the type name after `new`. Preserve the starting line's Tab indentation and add alignment whitespace; display columns honor `tab_width`. This does not exempt incorrect block indentation or operator spacing inside expressions.
- Omitted spaces between parameter attribute lists, or between the last attribute list and the parameter type or modifier.
- Spaces before tuple-element colons; an omitted space before a conditional-expression colon at the end of a line.
- The boundary between an empty while loop's closing parenthesis and same-line semicolon.
- One space before an empty `{ }` on the constructor initializer's line, including multiline initializers. A Tab separator is not exempt.
- No space between an anonymous method's `delegate` keyword and `(`.

<a id="zss0058"></a>
<a id="zss2003"></a>

### ZSS0058 / ZSS2003 · Compact method and anonymous function bodies

A method, local function, lambda or anonymous method may have a single-line block containing at most two simple statements: calls/assignments, local declarations, return or throw. The exception suppresses block-boundary IDE0055 and block-local IDE2001; it does not suppress formatting within expressions. Three or more statements, nested control flow, multiline bodies and yield statements are not exempt. Try/catch/finally follows its separate limit of two direct statements per clause, with unrestricted statement types.

```csharp
public int Next(int value) { value++; return value; }
```

<a id="zss2002"></a>

### ZSS2002 · Empty while loops

Allows `while(queue.TryDequeue(out _));` without IDE2001, assuming `queue` is a concurrent queue. Nonempty loop bodies still start on another line. This exception does not determine whether the empty loop is intentional.

<a id="zss1006"></a>

### ZSS1006 · Private field naming exceptions

In addition to the default `_camelCase`, these private field names are exempt from IDE1006:

- Uppercase names containing at least one uppercase letter and otherwise only uppercase letters, digits or underscores, such as `SIZE`, `DEFAULT_CAPACITY` and `HTTP2_BUFFER`.
- Valid C# identifiers starting and ending with `_`, without further restrictions on internal casing or the number and arrangement of underscores, such as `_GaugeMethod_`, `_gauge_method_` and `__getHandlers__`.

The exception requires private field accessibility only: instance/static, mutable/readonly and private constant fields qualify. It does not relax naming for fields with other accessibility, properties, parameters or locals. For example, `_GaugeMethod` does not have both delimiters and remains subject to the original rule.

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

> 🔗 **Maintenance:** Rule anchors use lowercase diagnostic IDs, such as `#zs2003`, and must survive heading changes. New diagnostics require updates to both catalogs, help links, severity, fix status and tests. Remove the corresponding anchors and references when deleting a rule.
