# Zongsoft .NET/C# rule catalog

[English](RULES.md) | [简体中文](RULES.zh-Hans.md)

This page documents diagnostic severity, triggers, exceptions and fixes in `Zongsoft.CodeAnalysis`. The [development guidelines](https://github.com/Zongsoft/Guidelines/blob/main/zongsoft.csharp.guidelines.md) retain coding principles and design requirements; see the [README](https://github.com/Zongsoft/Guidelines/blob/main/README.md#code-analysis) for setup and validation commands.

[Index](#index) · [Custom rules](#custom-rules) · [SDK rules](#sdk-rules) · [Exceptions](#suppressions) · [Configuration](#configuration) · [Review](#review) · [References](#references)

> 💡 **Open a rule from VS:** Custom `ZS` diagnostics carry help links to stable anchors in the Chinese catalog. Use the diagnostic help link or the rule code in the Error List; selecting an entry and pressing `F1` also opens help. SDK IDE/CA and compiler diagnostics retain their official links. Opening help and applying a `Ctrl+.` code fix are separate actions.

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
| [ZS2004](#zs2004) | Require regions for files with more than 9 methods and properties in non-test projects | Warning / error | Group by responsibility manually |
| [ZS2005](#zs2005) | Add braces to nested control bodies | Warning / error | ✅ Add braces |
| [ZS2006](#zs2006) | Place local functions at the end with required spacing | Warning / error | ✅ Arrange local functions |
| [ZS3001](#zs3001) | Document every method parameter | Warning / error | Describe the contract manually |
| [ZS3002](#zs3002) | Document non-void method returns | Warning / error | Describe the contract manually |
| [ZS3003](#zs3003) | Keep single-line XML content and tags on one line | Warning / error | ✅ Join XML documentation lines |
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

**At least one blank line is required at these four kinds of boundaries, subject to the exceptions below:**

1. Consecutive ordinary statements immediately followed by `if`, `switch`, `for`, `foreach`, `while` or `do`: at least two statements before a block or complex structure, or at least three before a simple braceless structure, require separation. Below the applicable threshold, either adjacency or a blank line is allowed.
2. Adjacent independent control structures or explicit blocks at the same level. Control structures include those conditions and loops, plus `try`, `using`, `lock`, `checked`/`unchecked`, `unsafe` and `fixed`.
3. Before `try`, `using(...)`, `lock`, `fixed`, `checked`/`unchecked`, `unsafe` or an explicit block when another independent statement precedes it at the same level. Even a single declaration, assignment or call requires separation. See the table below for resource-group boundaries.
4. A complete compound statement containing an executable statement block, or an explicit block, immediately followed by any independent statement at the same level. Calls, assertions, event notifications, assignments, local declarations, disposal, `return`, `throw`, `yield` and jumps are not exempt because of their purpose.

“Same level” means direct statements in the same block, switch section or top-level statement list. An executable block can belong to the control structure itself or occur along its nested statement bodies. For example, a braceless outer `if` wrapping a `while` block still requires a blank line after the complete `if`. This also applies to `await using`, `await foreach` and labeled statement blocks.

**Thresholds before conditions and loops:** This table only determines item 1; items 2, 3 and 4 and their exceptions apply independently and are not canceled by the relaxed counts.

| Following condition or loop | 1 consecutive ordinary statement | 2 consecutive ordinary statements | 3 or more consecutive ordinary statements |
| --- | --- | --- | --- |
| Block or complex structure | Adjacency allowed | Blank line required | Blank line required |
| Simple braceless structure | Adjacency allowed | Adjacency allowed | Blank line required |

**Simple and complex structures:** Simple braceless structures are limited to `if`, `for`, `foreach` and `while`, with a single simple statement body as defined in the exceptions table below and no `else` on an `if`. All other conditions and loops are complex, including `switch`, `do`, branches with `else`/`else if`, empty blocks, single-statement blocks and nested control bodies. A complete single-line structure has the same threshold. A labeled statement is classified by its attached complete statement. Calls used for assertions, logging, cancellation checks or resource acquisition count as ordinary statements; names, purpose and data dependencies do not determine the threshold.

**Complete structures:** Keep related `if/else if/else`, `try/catch/finally` and `do/while` clauses together, as well as a control statement and its nested body. For items 1 and 3, place the separator before the complete structure; no extra leading blank line is required when there is no preceding statement at the same level. For item 4, place it after the final branch or clause; for `do`, after the final `while(...);`. A braceless final `else` does not change the following boundary if any branch contains an executable block. Successive outer closing braces and labels of different switch sections are not independent statements requiring separation under this rule.

**Optional separation:** These exceptions preserve existing blank lines and do not exempt other boundaries inside the structures.

| Layout | Exact conditions |
| --- | --- |
| Consecutive simple controls of the same kind | Only `if`, `for`, `foreach` and `while`; each body has no braces and is a single expression statement (such as a call, assignment, increment or decrement), `return`, `throw`, empty statement, `break`, `continue` or `yield`. An `if` has no `else`. Different kinds, blocks and nested controls still require separation. |
| Consecutive compact exception-handling statements | Each complete `try/catch/finally` occupies a different single physical line, with all its clauses on that line. Each clause contains at most two direct statements, and the complete structure contains no preprocessor directives. Individually single-line clauses forming a multiline structure do not qualify. A preceding independent statement or following ordinary statement still requires separation from the group. |
| Resource declarations and a nested `using` chain | In the same statement list, consecutive `using var`, `using Type` or corresponding `await using` declarations without separator blank lines may join the immediately following complete `using(...)`/`await using(...)` structure as a resource group. Declarations and directly nested `using` statements inside the group need no blank lines. Check item 3 before the first resource declaration. An ordinary statement or existing separator blank line ends the group. Declarations alone do not require preceding separation under item 3. Ordinary variable declarations and two already completed independent `using` blocks do not form such a group. |
| Terminal switch-section `break` | The `break` is the last direct statement in that switch section and exits that switch. A `break` inside a nested block or loop, a nonterminal section `break`, and `return`, `throw`, `continue` or `goto case/default` do not qualify. |

**Counting and blank lines:** Ordinary statements are local variable or constant declarations (with or without initialization, including resource declarations) and expression statements (calls, awaited calls, ordinary/compound/deconstruction assignments, increments, decrements and similar expressions). Mixed kinds accumulate. A multi-variable declaration, deconstruction assignment or multiline declaration/expression statement counts once. An existing blank line or another statement kind ends the run; item 3 still determines separation before a resource group. Item 1 does not require blank lines between individual ordinary statements or before a following return or other statement that is neither a condition nor a loop. Responsibility and stage grouping still requires manual review. A separator line contains only whitespace and is outside comments. Comment lines, preprocessor directive lines, and blank lines inside block comments or inactive conditional-compilation text do not count. Existing blank lines below the threshold need not be removed.

**Scope:** Production and test code follow the same rule; generated code is excluded. AI-written, manually maintained source is not exempt because of its origin. Spacing between method, type, property and accessor declarations follows other guidelines. A local function declaration is not a complex control structure under item 3, and its closing block does not trigger item 4. Braces in object/collection initializers, property patterns, switch expressions, lambdas and anonymous methods belong to expressions and do not require separation of the enclosing statements. Statement boundaries inside those function bodies are still checked.

The following are independent method-body fragments. Assume `items` is an integer array, `enabled` and `disabled` are booleans, `Process(int)`, `Complete()` and `NotifyChanged()` exist, and the `System` namespace is imported. `Assert.True(bool)` comes from the project's test framework. Statements marked `//ZS2003` are reported; the other fragments comply with this rule.

One ordinary statement before a condition block may remain adjacent.

```csharp
var count = items.Length;
if(count > 0)
{
	Process(count);
}
```

A blank line at the same boundary also complies; the fixer does not remove it.

```csharp
var count = items.Length;

if(count > 0)
{
	Process(count);
}
```

Two declarations before a condition block require a blank line.

```csharp
var count = items.Length;
var index = 0;
if(count > 0) //ZS2003
{
	Process(items[index]);
}
```

The compliant separation is:

```csharp
var count = items.Length;
var index = 0;

if(count > 0)
{
	Process(items[index]);
}
```

A declaration and a call accumulate; changing statement kinds does not reset the count.

```csharp
var count = items.Length;
Complete();
if(count > 0) //ZS2003
{
	Process(count);
}
```

Two ordinary statements may directly precede a simple braceless condition.

```csharp
var count = items.Length;
var index = 0;
if(count > 0)
	Process(items[index]);
```

Three ordinary statements before a simple braceless condition require separation; calls count too.

```csharp
var count = items.Length;
var index = 0;
Complete();
if(count > 0) //ZS2003
	Process(items[index]);
```

A multiline initialization is still one ordinary statement and may directly precede a condition block.

```csharp
var values = new int[]
{
	1, 2, 3,
};
if(enabled)
{
	Process(values[0]);
}
```

One initialization followed by `try`: preceding separation is required even for one ordinary statement.

```csharp
var x = 100;
try //ZS2003
{
	Process(x);
}
catch { }
```

One assertion followed by a loop block is below the two-statement threshold and may remain adjacent; tests and production code use the same counting rule.

```csharp
Assert.True(items.Length > 0);
foreach(var item in items)
{
	Process(item);
}
```

A braceless multi-branch statement is still complex, but may follow one ordinary statement directly; keep `else` together with its `if`.

```csharp
Complete();
if(enabled)
	Process(items[0]);
else
	NotifyChanged();
```

One initialization followed by a simple braceless `if` without `else` does not require preceding separation.

```csharp
var count = items.Length;
if(count == 0)
	return;
```

A resource group: separate the preparation call from the first resource declaration, keeping the declarations and nested `using` statements together.

```csharp
Complete();

using var buffer = new global::System.IO.MemoryStream();
using(var stream = new global::System.IO.MemoryStream())
using(var writer = new global::System.IO.StreamWriter(stream))
{
	writer.Write(items.Length);
}
```

Report missing separation before a resource group at its first declaration; the fix keeps the group together.

```csharp
Complete();
using var buffer = new global::System.IO.MemoryStream(); //ZS2003
using(var stream = new global::System.IO.MemoryStream())
{
	GC.KeepAlive(stream);
}
```

An ordinary call interrupts the resource group; the following `using(...)` still requires preceding separation.

```csharp
using var buffer = new global::System.IO.MemoryStream();
GC.KeepAlive(buffer);
using(var stream = new global::System.IO.MemoryStream()) //ZS2003
{
	GC.KeepAlive(stream);
}
```

Two completed independent `using` blocks do not qualify for the nested resource-chain exception.

```csharp
using(var first = new global::System.IO.MemoryStream())
{
	GC.KeepAlive(first);
}
using(var second = new global::System.IO.MemoryStream()) //ZS2003
{
	GC.KeepAlive(second);
}
```

A block followed by return or an assertion: returns are not exempt. In tests, separate an assertion from the preceding loop.

```csharp
foreach(var item in items)
{
	Process(item);
}
return; //ZS2003
```

These assertions are separated from the loop; both preceding and following boundaries comply.

```csharp
Assert.True(items.Length > 0);

foreach(var item in items)
{
	Process(item);
}

Assert.True(items.Length > 0);
```

A complete branch followed by a notification: keep the related else with its if and separate the notification. A braceless final branch does not change the boundary.

```csharp
if(enabled)
{
	Process(items[0]);
}
else
	Complete();

NotifyChanged();
```

do/while: keep the related while with the body, then separate the following statement after the semicolon.

```csharp
do
{
	Complete();
}
while(enabled);

NotifyChanged();
```

A compact exception-handling group: two complete single-line try statements may remain adjacent; separate the following call.

```csharp
Complete();

try { Process(items[0]); } catch { }
try { Complete(); } catch { }

NotifyChanged();
```

Here each clause is single-line, but the first complete try spans lines, so the second try requires separation.

```csharp
try { Process(items[0]); }
catch { }
try { Complete(); } catch { } //ZS2003
```

The end of a switch section: a terminal section break may directly follow a block. The call after the switch still requires separation.

```csharp
switch(items.Length)
{
	case 0:
		if(enabled)
		{
			Complete();
		}
		break;
}

NotifyChanged();
```

A loop's break has no such exception.

```csharp
while(enabled)
{
	if(items.Length == 0)
	{
		Complete();
	}
	break; //ZS2003
}
```

A switch section's return has no such exception.

```csharp
switch(items.Length)
{
	case 0:
		if(enabled)
		{
			Complete();
		}
		return; //ZS2003
}
```

Separate a following statement's comment from the preceding structure.

```csharp
if(enabled)
{
	Complete();
}

//Notify observers.
NotifyChanged();
```

Closing initializer braces do not trigger separation of enclosing statements.

```csharp
var values = new int[]
{
	1, 2, 3,
};
GC.KeepAlive(values);
```

Closing lambda braces do not trigger separation of enclosing statements.

```csharp
Action action = () =>
{
	Complete();
};
action();
```

Ordinary statement groups: three initializations before a simple braceless condition require separation. This fragment declares its own enabled variable instead of using the one assumed above.

```csharp
var index = 0;
var count = items.Length;
var enabled = count > 0;

if(enabled)
	Process(items[index]);
```

The following has only one initialization followed by a call and a return, so it complies. Its containing method returns an object; GetCreator(type) returns a creation delegate, map is an optional mapping delegate and state is its state argument.

```csharp
var entity = GetCreator(type)();
map?.Invoke(entity, state);
return entity;
```

Simple braceless control groups: blank lines are optional within a same-kind group and required between different kinds.

```csharp
if(enabled)
	;
if(disabled)
	;

for(var i = 0; i < items.Length; i++)
	;
for(var j = 0; j < items.Length; j++)
	;

foreach(var item in items)
	;
foreach(var entry in items)
	;

while(enabled)
	;
while(disabled)
	;
```

**Fix:** `Ctrl+.` → **Insert blank line**, including Fix All in a document, project or solution. Reports the first token of the following statement or the first resource declaration of a group. The fix rechecks the boundary and exceptions and inserts only missing separation, preserving indentation, CRLF/LF, comments and conditional compilation without formatting other code. Configure `dotnet_diagnostic.ZS2003.severity`; warning by default and error in strict mode.

<a id="zs2004"></a>

### ZS2004 · Method and property regions

- **Test project exception:** No checks are registered when `IsTestProject=true`, in both the IDE and builds, including strict mode. Missing, false or invalid Boolean values keep checks enabled. Project names, directories and test frameworks are not used to infer this setting. The exception does not propagate to production projects under test.
- **Trigger:** A code file with more than 9 methods and properties combined must enclose those members in matched `#region` / `#endregion` pairs. Reports once per file at the first uncovered member's name.
- **Counting:** Includes methods, properties and indexers in all types and nested types in the current file, one per declaration, including interface, abstract and explicit interface members. Excludes constructors, operators, accessors, local functions, fields and events. Does not combine partial declarations across files or count inactive conditional code.
- **Boundaries:** Files with 9 or fewer members do not require regions. Empty, unmatched or method-body regions cannot cover members. Generated code is excluded. Chinese region names, responsibility grouping, ordering and empty regions themselves still need manual review.
- **Configuration and fix:** `dotnet_diagnostic.ZS2004.severity`; warning by default, error in strict mode. Group manually by responsibility; the analyzer does not invent names.

<a id="zs2005"></a>

### ZS2005 · Add braces to nested control bodies

- **Trigger:** The direct body of an `if` then branch, `else` branch, `for`, `foreach`, `while`, `do`, `using`, `lock` or `fixed` has no braces and is itself a control structure. Add a block around the outer body, even if the inner control already has its own block. `foreach` includes deconstruction and `await foreach`; `using` includes `await using`.
- **Inner structures:** Conditions, loops, `using`, `lock`, `fixed`, `switch`, `try`, `checked`/`unchecked` and `unsafe` statements qualify. Keywords in expressions do not. An outer body that is already an explicit block complies.
- **Each direct relationship:** Check direct bodies, rather than indentation or physical lines. Two nested controls already trigger the rule; the method body and terminal ordinary statement do not count as levels. Check each direct relationship in deeper nesting.
- **Location:** Reports the first token of the outer head requiring a block: `else` for an else branch, and `await` for `await using`/`await foreach`.
- **Related branches:** A standard `else if` chain does not require a block between `else` and its `if`. An `if` then body that is another `if`, or an `else` body that directly contains a loop or another control kind, still requires a block.
- **Pure chains:** Directly consecutive `using`/`await using`, pure `fixed`, and pure `lock` heads may share their final body. A single simple ordinary statement may omit the final block; multiple statements use a shared block. Mixed kinds do not qualify. When the final body is another control kind, the last chain head requires a block. Expression complexity in ordinary statements remains a guideline review concern.
- **Scope and configuration:** Production and test code follow the same rule; generated code is excluded. Configure `dotnet_diagnostic.ZS2005.severity`; warning by default and error in strict mode. This rule does not require braces around every simple body and does not check logical blank lines or embedded-statement line breaks.

The following are independent method-body fragments. Assume `items` is an integer array, `enabled` is a boolean, and `Process(int)` and `Complete()` exist. Resource fragments use fully qualified BCL types. `//ZS2005` marks an outer head requiring a block. These fragments demonstrate this rule; surrounding spacing still follows [ZS2003](#zs2003).

Noncompliant: two controls trigger the rule; the final call is not a third control level.

```csharp
foreach(var item in items) //ZS2005
	if(item > 0)
		Process(item);
```

Fixed: brace the outer body; the simple inner body may remain braceless.

```csharp
foreach(var item in items)
{
	if(item > 0)
		Process(item);
}
```

An inner block does not exempt the outer `if`.

```csharp
if(enabled) //ZS2005
	foreach(var item in items)
	{
		Process(item);
	}
```

Deeper nesting checks each relationship: both the `for` and its direct `if` need blocks.

```csharp
for(var index = 0; index < items.Length; index++) //ZS2005
	if(enabled) //ZS2005
		while(items[index] > 0)
			items[index]--;
```

A pure resource chain may end in a single braceless call.

```csharp
using(var stream = new global::System.IO.MemoryStream())
using(var writer = new global::System.IO.StreamWriter(stream))
	writer.Flush();
```

A pure lock chain may also end in a single braceless call. Assume both synchronization objects exist.

```csharp
lock(firstSync)
lock(secondSync)
	Complete();
```

Pure fixed chains use the same exception. This fragment belongs to an `unsafe` method with unsafe compilation enabled.

```csharp
fixed(int* first = items)
fixed(int* second = items)
	global::System.GC.KeepAlive(items);
```

Mixed chains need a block at the change of kind. The pure resource chain remains inside the lock block; assume `sync` exists.

```csharp
lock(sync)
{
	using(var first = new global::System.IO.MemoryStream())
	using(var second = new global::System.IO.MemoryStream())
		global::System.GC.KeepAlive(second);
}
```

The reverse mixture is not exempt: the last `using` below requires a block.

```csharp
using(var first = new global::System.IO.MemoryStream())
using(var second = new global::System.IO.MemoryStream()) //ZS2005
	lock(sync)
	{
		global::System.GC.KeepAlive(second);
	}
```

A standard `else if` chain is a related branch structure and complies.

```csharp
if(enabled)
	Complete();
else if(items.Length > 0)
	Process(items[0]);
else
	Complete();
```

**Fix:** `Ctrl+.` → **Add braces**, including Fix All in a document, project or solution. Rechecks nesting and exceptions, wraps the corresponding body, and adjusts its indentation while preserving comments, CRLF/LF and the existing `else` association without formatting unrelated code. No automatic fix is offered when the target control has syntax errors or the range from the outer header's last token to the end of its direct body contains directives, inactive source or damaged syntax. Ordinary directives preceding the structure do not prevent a fix. A multiline trailing header comment ending on the body's start line can also prevent safe insertion and needs manual handling. Logical grouping and return spacing require [manual review](#review).

<a id="zs2006"></a>

### ZS2006 · Place local functions at the end with required spacing

- **Position:** Group local functions at the end of their containing block, switch section or top-level statement list. No ordinary statement in that list may follow them. Preserve their scope; do not lift them to an outer method. Covers every host permitting local functions, including constructors, accessors, operators, lambdas, anonymous methods and other local functions.
- **Body separation:** Exactly one real blank line separates body code from the first local function. Documentation stays with its function: separation precedes the comments, with no separating blank lines between comments and declaration. No additional blank line after the opening brace is required when there is no preceding body code.
- **Single-line exception:** Zero or one blank line is allowed when all body code in that list occupies one physical line, the first function has an expression body (`=>`, without a brace body), and the preceding area has no standalone comment lines or directives. A trailing comment on a body statement does not cancel the exception. One statement spanning multiple lines does not qualify.
- **Adjacent functions:** Two consecutive expression-bodied local functions require zero blank lines. An expression body followed by a block body allows zero or one. A block-bodied function requires exactly one blank line afterward. An `extern` function without an expression body uses the non-expression-bodied rule. Documentation stays adjacent to the following function.
- **Blank lines:** Count whitespace-only lines outside comments and inactive source. Comment lines do not replace blank lines, and internal block-comment blank lines are preserved. Separation before local functions belongs to this rule, avoiding duplicate [ZS2003](#zs2003) reports. Ordinary statements within function bodies still follow ZS2003.
- **Reporting and configuration:** Report at the offending function's first token. A misplaced function does not receive a second spacing report. Production and test code follow the same rule; generated code is excluded. Configure `dotnet_diagnostic.ZS2006.severity`; warning by default and error in strict mode.
- **Fix:** `Ctrl+.` → **Arrange local functions**, with document, project and solution Fix All. Preserve the relative order of ordinary statements and of functions in their original list. Keep documentation and trailing comments with their original owners. Move bodies as source fragments, preserve CRLF/LF, and do not format unrelated code.
- **Move boundaries:** Check project compilation after moving. No move is offered when the project already has compiler errors, the target list has syntax errors, directives or inactive source, moving would cross a `using`/`await using` declaration, comment ownership is ambiguous, or an implicit `CallerLineNumber` argument is present. Spacing fixes also do not cross directives or inactive source.

Each fragment is an independent method declaration inside a type with `System` imported. `//ZS2006` marks an offending declaration; other fragments comply.

Body code precedes the trailing local functions:

```csharp
static void Foo()
{
	Console.Write("before");
	Console.Write(GetMessage());

	static string GetMessage() => "This is a message.";
}
```

Invalid: the local function interrupts the body code.

```csharp
static void Foo()
{
	Console.Write("before");

	static string GetMessage() => "This is a message."; //ZS2006

	Console.Write(GetMessage());
}
```

A single-line body may omit separation; consecutive expression bodies must remain compact:

```csharp
static void Foo()
{
	Console.Write(GetMessage());
	static string GetMessage() => Format("message");
	static string Format(string value) => value;
}
```

Invalid: a blank line separates two expression-bodied functions.

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	static string GetMessage() => Format("message");

	static string Format(string value) => value; //ZS2006
}
```

Standalone documentation cancels the compact exception; separation precedes the comment:

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	// Function documentation.
	static string GetMessage() => "This is a message.";
}
```

A block-bodied function requires one blank line afterward:

```csharp
static void Foo()
{
	Console.Write(GetMessage());

	static string GetMessage()
	{
		return Format("message");
	}

	static string Format(string value) => value;
}
```

The inner block preserves the local function's scope while the outer method continues:

```csharp
static void Foo(bool enabled)
{
	if(enabled)
	{
		var message = "This is a message.";
		Console.Write(GetMessage());

		string GetMessage() => message;
	}

	Console.Write("after");
}
```


<a id="zs3001"></a>

### ZS3001 · Method parameter documentation

A method with XML documentation must have a top-level `<param name="parameterName">...</param>` for every parameter. Names match case-sensitively using the actual identifier name, excluding a C# `@` escape. Each missing element is reported at its parameter identifier. A summary, a `<paramref>` reference or a mismatched name does not satisfy this requirement.

Applies to methods of any accessibility, including interface, abstract and explicit interface methods; excludes constructors, local functions, delegates and operators. Undocumented methods are not required to add documentation by this rule. `<inheritdoc />` and `<include />` do not replace explicit parameter elements. A self-closing `<param name="..." />` counts as present; content quality still requires review.

Configure `dotnet_diagnostic.ZS3001.severity`; warning by default, error in strict mode. Describe the parameter contract manually.

<a id="zs3002"></a>

### ZS3002 · Method return documentation

A method with XML documentation and a return type other than `void` must have a top-level `<returns>` element. Missing documentation is reported at the return type. Includes `Task`, `ValueTask`, generic and `ref` returns; `void` / `async void` do not require it. Method scope and inherited documentation follow [ZS3001](#zs3001). A self-closing `<returns />` counts as present.

Configure `dotnet_diagnostic.ZS3002.severity`; warning by default, error in strict mode. Describe the return contract manually rather than generating empty descriptions.

<a id="zs3003"></a>

### ZS3003 · Single-line XML elements

When a paired XML element has only one line of content, its opening tag, content and closing tag must share one line. Documentation prefixes and blank lines are ignored; inline nested elements such as `<see />` and `<paramref />` are allowed. Checks `///` and `/** */` documentation on all declarations, not just methods or summaries. Actual multiline text or XML structure retains its layout. Empty content and syntactically incomplete XML are excluded.

Before and after, shown as method declaration fragments:

```csharp
/// <summary>
/// One line of content.
/// </summary>
void Foo() { }
```

```csharp
/// <summary>One line of content.</summary>
void Foo() { }
```

Reports at the opening tag. Configure `dotnet_diagnostic.ZS3003.severity`; warning by default, error in strict mode.

**Fix:** `Ctrl+.` → “Join XML documentation lines”, with Fix All for documents, projects and solutions. Joins only lines within the element, removing documentation prefixes and surrounding layout whitespace from its content. Preserves original text, XML entities, inline nested elements, surrounding comments and code, and remaining CRLF / LF endings. Supports `///` and `/** */`. No fix is offered for multiline tags, malformed XML, or an explicit `xml:space="preserve"` on the element or an ancestor. Actual multiline content, empty content, already joined elements and stale diagnostics are not rewritten.

ZS3001 / ZS3002 / ZS3003 skip generated code and require parsed XML documentation (`DocumentationMode.Parse` or `Diagnose`), without requiring an XML output file. `DocumentationMode.None` is not checked. Element presence does not prove contract accuracy or content completeness, nor replace compiler checks for invalid parameter names and other XML errors.

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

An expression-bodied method may immediately precede another method without a blank line, whether its expression follows `=>` on the same line or the next line. The following method may have a block body; [ZS2003](#zs2003) does not require a blank line between these methods.

Structs, classes and records with no declared members may also be adjacent without blank lines, including empty types with primary constructors and `record struct`. These type declaration fragments are valid:

```csharp
struct MyStruct { }
class MyClass { }
record MyRecord(int Value) { }
```

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

- [Statement groups](zongsoft.csharp.guidelines.md#statement-groups): separate responsibilities, execution stages and verification targets with blank lines, without a hard physical-line limit. Statement kind, receiver identity or data dependency alone does not establish a compact group. Homogeneous mapping, registration, initialization and assertion tables must meet the guideline conditions. In tests, simple input construction may stay with the execution that immediately uses it. Setup precondition assertions may immediately follow the environment or data acquisition they directly check, including asynchronous acquisition. Assertions that jointly verify one operation result, such as its status and content, may stay together, as may auxiliary value extraction and its assertions. Separate independent setup stages from execution, execution from verification, different verification targets and each field requiring multiple checks.
- [Return spacing](zongsoft.csharp.guidelines.md#return-spacing): compact acquisition, wrapping and status-result units may precede a return directly. Separate independent fallback returns and returns after completed multi-step packet filling or creation/acquisition/publication stages. A multiline conditional expression alone does not force separation; a final configuration callback may stay with its return. Do not add blank lines before every return.
- ZS2003 checks only its listed deterministic syntax boundaries. It does not infer these logical separators from method names, receiver identity or business phases and does not fix manually judged boundaries. Compact logical groups cannot override existing ZS2003 boundaries.

- Alias restrictions, production global imports, test project-level Using, dependency groups and namespace-length ordering.
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
