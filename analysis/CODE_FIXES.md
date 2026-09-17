# 🛠️ Code fix implementation plan

[English](CODE_FIXES.md) | [简体中文](CODE_FIXES.zh-Hans.md)

This plan covers existing diagnostics and requirements not yet checked automatically, based on the [coding guidelines](../zongsoft.csharp.guidelines.md) and current Global AnalyzerConfig. Deterministic violations should have previewable fixes; changes requiring a business decision should offer bounded assistance. Architecture, public compatibility, ownership and translation cannot all be corrected automatically.

The [rule catalog](../RULES.md) is the reference for diagnostic behavior, exceptions and VS help links; this document covers fix implementation and future work.

## ✅ 1. Delivered in the first phase

| Rule | Action | Constraints preserved |
| --- | --- | --- |
| [ZS0005](../RULES.md#zs0005) | Remove unused using | Rechecks compiler CS8019; retains ordinary `using System;`, used imports, comments and directives; does not organize other imports |
| [ZS2003](../RULES.md#zs2003) | Insert blank line | Changes only the reported boundary; preserves indentation, CRLF/LF, comments and directives; inserts outside multiline trailing comments without formatting the file |

Both actions have English and Simplified Chinese titles and support individual fixes and document/project/solution Fix All. Each batch handles the selected rule only.

The implementation in [fixes/src](fixes/src) ships inside the same `Zongsoft.CodeAnalysis` NuGet package. No separate VSIX is required. Visual Studio presents quick actions through the light bulb or `Ctrl+.` at the diagnostic, with preview and application controlled by the host. Menu wording varies with IDE version and language. [Microsoft analyzer overview](https://learn.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview?view=visualstudio)

Validation covers MEF exports loaded from the actual nupkg, 33 fix cases, the existing 55 compiler consumer cases, and discovery/execution through .NET 10 SDK `dotnet format analyzers`. Assertions check exact text, diagnostic removal and compilation. Interactive VS2026 light-bulb, preview and undo testing has not been performed and remains a release acceptance step.

## 🧭 2. Capability levels

| Level | Interaction | Batch policy |
| --- | --- | --- |
| A: deterministic | Preview a local, semantics-preserving text or syntax change | Enable Fix All after conflict, idempotence and scope tests |
| B: requires a choice | Select a name, resource or migration before previewing changes | Single occurrence first; restricted batches only where the choice remains valid |
| C: requires design judgment | Explain the diagnostic, guideline and practical next steps | Do not register actions that guess business intent |

Suppressions do not count as fixes. Disabled rules and explicitly permitted patterns should not trigger actions that convert already compliant code to a different style.

## 📋 3. Existing diagnostic matrix

| Diagnostic | Approach | Acceptance focus |
| --- | --- | --- |
| `ZS0005`, `ZS2003` | Delivered, level A | Maintain comment/directive, same-line edit and three-scope regression coverage |
| `IDE0009` | Reuse the SDK qualification fix | Add `this.` to properties, methods and events as configured, without extending the policy to all fields |
| `IDE0049` | Reuse the SDK predefined-type keyword fix | Preserve meaning; validate in the IDE or `dotnet format style`, not only during builds |
| `IDE0065`, `IDE0161` | Reuse SDK using-placement and file-scoped namespace fixes | Do not force unsafe conversions involving multiple/nested namespaces, directives or changed binding |
| `IDE1006` | Prefer SDK naming actions and Roslyn Rename, level B | Resolve collisions and references across files, overloads and interface implementations; public APIs, serialization, reflection and configuration require review |
| `IDE0055`, `IDE2001` | Reuse local SDK fixes with exception regression tests | Retain permitted directive indentation and compact try/catch/finally; whole-file formatting is not a precise fix |
| `ZS1304` | Next: fixed resource key to an existing Designer property; level A only when equivalent | Prove resource-manager identity, key, accessibility, return type and culture behavior; do not guess missing properties |
| `CA1303` | Existing-resource selection first, extraction to ResX later; level B | Distinguish user text from identifiers; choose resource/key and preserve placeholders, evaluation and exception parameter names |
| `CS4014` | Context-specific assistance, level B/C | Adding await can change signatures, exception propagation, order and callers; supervised background work is not automatically converted |
| `CA2012` | Limited ValueTask actions, level B/C | Distinguish one-time awaiting, repeated consumption, storage and Task conversion; do not mechanically add `.AsTask()` |
| `ZSS0055`, `ZSS0056`, `ZSS2001` | Retain suppressor responsibilities | These permit agreed patterns and need no inverse fix |
| Disabled `IDE0001/2/3`, `IDE0005`, `IDE0130`, `IDE0210/11`, `IDE0290` | No new batch actions | Preserve deliberate `global::` and entry-point choices; ZS0005 owns unused imports |

For the remaining configured modern syntax suggestions, prefer existing SDK actions: var, target-typed new, automatic properties, expression bodies, null coalescing, patterns, initializers, collection expressions, readonly members and local functions. Establish examples against the actual SDK to verify availability and policy compatibility. Do not duplicate the SDK implementation or promote every suggestion to an error.

## 🧩 4. Requirements without diagnostic IDs

This matrix covers every guideline chapter. Assign new ZS IDs only after defining testable conditions; recommendations such as “prefer” or “where appropriate” must not become unconditional warnings.

| Guideline area | Detection and fix approach | Limits |
| --- | --- | --- |
| 1 Principles; 2 semantic names and suffixes | IDE1006 covers configured casing; assess deterministic suffix checks | Domain names, responsibility and abbreviation meanings remain level C |
| 3.1 Files, encoding, line endings and primary types | EditorConfig/Git attributes enforce editor/repository policy; splitting types is a separate refactoring | A local fix must not rewrite BOM/EOL throughout a file; account for partial/generated types |
| 3.2 Using groups and ordering | Add analysis and sorting within safe import segments | Follow dependency groups and guideline ordering; comments, directives and extern aliases bound sortable segments |
| 3.2 Using aliases | Find symbol-bound alias references, replace with `global::` qualified names, then remove the alias | Check generics, nested types, attributes, nameof constant values and conditional branches; decline when binding or values change |
| 3.2 Global using in production; test Global.cs exception | Define readable project-type/exception settings before diagnostic or migration work | Global imports affect multiple files; simply removing global is unsafe; retain permitted test imports |
| 3.3 Member sections and regions | Detect unambiguous empty/duplicate sections; offer selected-range assistance | Do not reorder field initializers, partial members or explanatory regions |
| 3.4 Whitespace, expression bodies and separation | SDK formatting and ZS2003; define exceptions before further rules | Separate an assignment group only when more than two consecutive assignments/initializations precede a conditional or loop; keep independent control structures separated without mechanically splitting declaration/call/return transitions or related clauses |
| 4 Comments and API documentation | Reuse compiler documentation diagnostics; optionally add signature-matching parameter skeletons | Do not invent descriptions, examples, thread-safety guarantees or copyright |
| 5.1 Member access, locals and properties | Reuse qualification, var and automatic-property fixes; assess field-keyword support later | Preserve validation, notifications, initialization and attributes; do not remove backing fields used elsewhere or through reflection |
| 5.2 Modern syntax | Reuse SDK actions within the configured language version | Do not upgrade SDKs automatically; preserve collection type, comparer, capacity and enumeration timing |
| 5.3 Strings, nulls and collections | Proven-equivalent syntax simplification; advice for culture/null semantics | Do not infer comparison rules or change deferred execution blindly |
| 5.4 Structures and equality | Reuse readonly-structure suggestions and inspect equality consistency | Equality fields, hashes, mutability and numeric tolerance must be specified first |
| 6.1–6.5 Responsibilities, inheritance, extensibility, composition and compatibility | Separate architecture/contract diagnostics with call-chain evidence and guideline links | Level C by default; no automatic interface splitting, dependency changes, service registration or public contract modification |
| 7.1 Async and cancellation | SDK diagnostics plus focused cancellation/signature checks | Await, ConfigureAwait and token propagation all need context; no repository-wide replacements |
| 7.2 Arguments and exceptions | Equivalent ThrowIfNull-style transformations | Preserve exception types, parameter names, evaluation count and localized messages |
| 7.3 Ownership and concurrency | Advice for provable scope problems plus lifecycle review | Do not extend using lifetimes, dispose shared instances or change locking automatically |
| 7.4 Localization | Implement the stages below; consider generator/synchronization checks separately | Include logs, interaction and exception text; do not classify every literal, protocol key or path as translatable |
| 8.1 Project settings and dependencies | Separate MSBuild/repository checks preserving local overrides | Code fixes must not upgrade packages, change central management or write publishing credentials |
| 8.2 Tests/documentation; 8.3 automated checks | Validate entry points and configuration consistency; develop behavior tests as part of implementation work | Empty tests or disabled diagnostics do not satisfy quality requirements |
| 9 References | Maintain links and validate examples | Documentation maintenance, not a source CodeFix |

## 🌐 5. Localization implementation

### 5.1 ZS1304: existing strongly typed properties

1. Inspect `IInvocationOperation` for the actual ResourceManager, constant key, explicit CultureInfo and result usage.
2. Locate the real Designer class in the current project or accessible references. Verify both the generated key and manager source; never infer identity from a Resources class or property name alone.
3. Offer direct replacement only for a unique accessible equivalent property. Qualify conflicts with `global::`, not a using alias. Present multiple equivalent candidates as separate choices.
4. Decline direct replacement where explicit culture, receiver side effects, stream/object differences, fallback, casts or nullable behavior cannot be preserved.
5. Start with existing properties. For missing resources, provide guidance and generation instructions; evaluate scoped batches only after the mapping is reliable.

### 5.2 CA1303: reuse or extract resources

First offer existing-resource candidates based on neutral values. The developer chooses the key with the correct purpose: identical text does not imply identical resource semantics.

Extraction comes later: choose the neutral ResX, a valid unique key, context and placeholders; modify source and workspace resource documents through a Solution with a complete preview. Do not overwrite or invent translations. Track translation work separately and retain neutral-resource fallback.

`ResXFileCodeGenerator` is a Visual Studio custom tool, not an ordinary dotnet build target. A portable Roslyn CodeFix cannot promise identical invocation in VS and CLI. The initial implementation must not handwrite Designer files, launch VS during analysis or disable diagnostics to bypass generation.

Before offering complete extraction, validate whether resource edits, actual custom-tool generation and source replacement can form a previewable, undoable operation in VS. If a NuGet-only fix cannot provide this, document a staged workflow: edit ResX, run the custom tool, then use the generated property. Assess an optional VS extension only if necessary. CLI/CI continue consuming committed Designer files; do not silently introduce a different generator.

## ⚙️ 6. Implementation and host boundaries

- The analyzer assembly in analyzers/src uses compiler APIs; fixes/src uses Workspaces and owns providers, localized titles and transformations. Both assemblies and satellites ship together without bundling host Roslyn/MEF binaries or adding business runtime references.
- Use stable diagnostic IDs and equivalence keys. Prefer ApplyChangesOperation over direct file writes, create immutable document/solution changes, and honor cancellation. Registering actions must not execute builds or network requests.
- Use minimal TextChange operations for layout. Use SyntaxEditor/DocumentEditor, semantic models, symbol matching and rename APIs for semantic transformations. Limit Formatter/Simplifier to new nodes and verify required `global::` qualifiers survive.
- The first phase uses WellKnownFixAllProviders.BatchFixer to merge nonconflicting document edits. Overlapping edits, symbol migrations and multiple writes to a resource file need a dedicated FixAllProvider that deduplicates, orders and checks changes in one Solution snapshot. [Microsoft BatchFixer reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.codefixes.wellknownfixallproviders.batchfixer?view=roslyn-dotnet-4.13.0)
- Keep current compatible APIs. Before supporting newer syntax such as field, assess minimum Roslyn/VS/SDK versions and versioned package layouts so one action does not break loading for existing consumers.
- Future cross-file fixes must consider linked files, target frameworks and conditional compilation in every affected context. Restrict or decline batches where equivalence cannot be proved.

## 🧪 7. Acceptance and release sequence

| Phase | Deliverable | Exit criteria |
| --- | --- | --- |
| P0: this change | Individual and three-scope ZS0005/ZS2003 fixes | Package exports/localization, exact text/semantics, batch boundaries and existing diagnostic regression tests pass |
| P1: SDK action audit | Action inventory for current settings; VS2026/CLI sample projects | Manually verify IDE0009, rename and formatting exceptions; record IDE-only behavior; measure cancellation/latency for large files and many imports |
| P2: resource reuse | Deterministic ZS1304 conversion and existing CA1303 candidates | Positive/negative tests for manager identity, type, culture, accessibility, collisions and fallback |
| P3: layout and qualification | Safe import ordering, alias removal and explicit section checks | Preserve binding across symbols/branches, nameof values and field initialization order |
| P4: extraction and richer refactoring | Verified resource-generation interaction and bounded property/field fixes | Acceptance for multi-file preview, undo, unavailable tools, concurrent edits and cross-project access |
| P5: continuing coverage | Architecture, async, compatibility and ownership guidance | Explicit rules and human review instructions; no unjustified bulk changes |

Each rule needs positive, negative and no-fix cases, idempotence, no new compiler errors, comment/directive preservation, generated-code exceptions and local configuration tests. Before enabling Fix All, cover document/project/solution scope and edit conflicts. Multi-file fixes must prove unselected documents remain unchanged.

Run a clean default Cake build and verify consumers with isolated package caches. VS2026 release acceptance covers action discovery, localized titles, preview, individual application, all three Fix All scopes, undo and reanalysis. Repeat host compatibility tests after SDK/VS upgrades; CI compilation cannot replace UI acceptance. Increment the package version after validation and let maintainers use the existing publishing workflow.

For implementation APIs, see the [Microsoft analyzer/code-fix tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix). This plan does not publish packages or change diagnostic severities.
