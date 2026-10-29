# Specification Traceability

This table maps each part of the markup specification to the tests that pin it.
The formal specification is [reef-markup](../../docs/specs/reef-markup.md).

## Document and tags

| Specification area | Tests |
|---|---|
| Root attributes, namespaces, multiple roots | `RmlParserTests.RootAttributesAndMultipleRootsAreRead`, `DocumentShapeTests.MultipleRootsKeepDeclarationOrder` |
| Empty views | `DocumentShapeTests.EmptyViewGeneratesNoOpRender` |
| Comments | `RmlParserTests.CommentsAreIgnored` |
| XML syntax and valueless attributes | `RmlParserTests.ValuelessAttributeReportsRf0001`, `RmlParserTests.WrongRootReportsRf0001` |
| Text body, trimming, `xml:space`, attribute precedence | `RmlParserTests.TextBodyUsesAttributeFirstAndTrimsByDefault`, `TextRuntimeTests.TextFormsAndInterpolationRender` |
| Casing and unknown attributes | `SpecDiagnosticsTests.UnknownAttributeReportsRf0019` |
| `Element` layout and interaction attributes | `StyleRuntimeTests`, `ControlFlowRuntimeTests`, `ViewportRuntimeTests` |
| Spacers and fillers | `Reef.Markup.Tests/Fixtures/StyleView.rml`, `LocalView.rml` |
| `Text` attributes | `TextRuntimeTests.TextFormsAndInterpolationRender` |
| `Image` source and tint | `UiExpressionRuntimeTests.UiEnvironmentAndHelpersRender`, `GalleryNormalRendersAndMatchesGolden` |
| `If`, `ElseIf`, `Else` | `ControlFlowRuntimeTests.ConditionalBranchesRender`, `RmlParserTests.ConditionsAndEmptyAreGrouped` |
| Comparison aliases | `ControlFlowView.rml` uses `gt`, `ControlFlowRuntimeTests.ConditionalBranchesRender` |
| `ForEach`, index, nested scope | `ControlFlowRuntimeTests.EmptyStateAndNestedLoopsRender` |
| `Empty` | `ControlFlowRuntimeTests.EmptyStateAndNestedLoopsRender` |
| Loop identity requirement | `SpecDiagnosticsTests.LoopInteractionWithoutKeyReportsRf0010` |
| `Content` slots | `ComponentSlotTests.ReferencedComponentRendersCallerSlotsAndStyle` |
| Local components and caller styling | `LocalComponentTests.LocalComponentInlinesAndMergesCallerStyle` |
| Cross-assembly components | `ComponentRuntimeTests.ReferencedComponentRenders`, `ComponentSlotTests.ReferencedComponentRendersCallerSlotsAndStyle` |
| Component recursion | `SpecDiagnosticsTests.CircularComponentReportsRf0025` |
| Component slot or context mismatch | `SpecDiagnosticsTests.ComponentContextMismatchReportsRf0030` |

## Layout and style

| Specification area | Tests |
|---|---|
| Fixed, relative, fill, fit, shrink | `RssParserTests.ConstraintsParseFixedRelativePercentageAndKeywords`, `StyleRuntimeTests` |
| Size shorthand | `StyleView.rml`, `GalleryView.rml` |
| Min and max bounds | `RssParserTests.ConstraintsParseFixedRelativePercentageAndKeywords` |
| Position and anchors | `GalleryView.rml`, `LocalView.rml` |
| Padding order | `RssParserTests.PaddingAndCornerRadiusShorthandsMapToEngineFields` |
| Corner radius order | `RssParserTests.PaddingAndCornerRadiusShorthandsMapToEngineFields`, `StyleRuntimeTests.StylesInheritOrderAndInlineOverride` |
| CSS property aliases | `RssParserTests.CssAliasesMapToReefProperties` |
| Constraint expressions | `ConstraintExpressionTests` |
| Inherited text color | `UIBuilderTextColorTests.TextInheritsAncestorTextColor` |
| Per-side borders | `UIBuilderBorderTests.BottomOnlyBorderDoesNotDrawOtherSides` |
| Text-color and side-border parsing | `RssParserTests.TextColorAndSideBordersMapToStyleFields` |
| Colors | `RssParserTests.ColorsParseAllSupportedForms`, `GalleryNormalRendersAndMatchesGolden` |
| Border and corner caps | `StyleRuntimeTests.StylesInheritOrderAndInlineOverride`, `UIBuilderBorderTests` |
| Variables and overrides | `RssParserTests.CommentsVariablesRulesAndPseudoStatesParse`, `SpecDiagnosticsTests.DuplicateVariableReportsRf0012`, `SpecDiagnosticsTests.DuplicateOverrideReportsRf0013` |
| Class inheritance | `RssParserTests.CommentsVariablesRulesAndPseudoStatesParse`, `StyleRuntimeTests.StylesInheritOrderAndInlineOverride` |
| Pseudo states | `StyleRuntimeTests.PseudoStatesAndDynamicClassesApplyInPrecedenceOrder` |
| Dynamic class directives | `StyleRuntimeTests.PseudoStatesAndDynamicClassesApplyInPrecedenceOrder`, `SpecDiagnosticsTests.InvalidClassDirectiveReportsRf0027` |
| Precedence order | `StyleRuntimeTests.StylesInheritOrderAndInlineOverride`, `StyleRuntimeTests.PseudoStatesAndDynamicClassesApplyInPrecedenceOrder` |
| Unknown style properties and classes | `SpecDiagnosticsTests.UnknownStylePropertyReportsRf0022`, `SpecDiagnosticsTests.UnknownStyleClassReportsRf0032` |

## Expressions, binding, and input

| Specification area | Tests |
|---|---|
| Context and UI expressions | `UiExpressionRuntimeTests.UiEnvironmentAndHelpersRender` |
| Interpolation and literal braces | `TextRuntimeTests.TextFormsAndInterpolationRender` |
| Method helpers and return inference | `UiExpressionRuntimeTests`, `CallbackDeclarationTests` |
| Two-way value binding | `WidgetRuntimeTests.WidgetsRenderAndBind` |
| Change-result binding | `WidgetRuntimeTests.WidgetsRenderAndBind` |
| `ref` state binding | `WidgetRuntimeTests.WidgetsRenderAndBind` |
| Invalid bind target | `SpecDiagnosticsTests.ReadOnlyBindTargetReportsRf0011` |
| Click, held, scroll callbacks | `ControlFlowRuntimeTests.ClickAndHotkeyCallbacksInvokeCompanion`, `ViewportRuntimeTests.ScrollOffsetAndCallbackWork` |
| Hotkeys | `ControlFlowRuntimeTests.ClickAndHotkeyCallbacksInvokeCompanion`, `UiExpressionRuntimeTests` |
| Disabled input | `StyleRuntimeTests.PseudoStatesAndDynamicClassesApplyInPrecedenceOrder` |
| Passthrough input | `ViewportRuntimeTests.ClipOverlayAndPassthroughBehavior` |
| Overlay and clipping | `ViewportRuntimeTests.ClipOverlayAndPassthroughBehavior` |
| Scrolling | `ViewportRuntimeTests.ScrollOffsetAndCallbackWork` |
| Visibility | `ViewportRuntimeTests.VisibleFalseSkipsElement` |

## Namespaces and widgets

| Specification area | Tests |
|---|---|
| External widgets | `WidgetGeneratorTests.ExternalWidgetReceivesStyleAndConstraints`, `WidgetRuntimeTests.WidgetsRenderAndBind`, `AddFavoriteViewTests.RendersFieldsAndForwardsActions` |
| Direct style parameters | `WidgetGeneratorTests.ExternalWidgetReceivesStyleAndConstraints` |
| Wrapper fallback | `WidgetRuntimeTests.WidgetsRenderAndBind` |
| Disposable widget children | `WidgetRuntimeTests.WidgetsRenderAndBind` |
| Value widget children error | `SpecDiagnosticsTests.VoidWidgetWithChildrenReportsRf0014` |
| Namespace prefix errors | `SpecDiagnosticsTests.UnresolvedNamespaceReportsRf0007` |
| Unknown component or widget | `SpecDiagnosticsTests.UnknownElementReportsRf0015` |

## Diagnostics and schema

| Specification area | Tests |
|---|---|
| `RF0001` to `RF0033` | `SpecDiagnosticsTests` |
| Duplicate components | `SpecDiagnosticsTests.DuplicateComponentClassReportsRf0029` |
| Schema load and valid documents | `XsdSchemaTests` |
| Schema negative cases | `XsdSchemaTests` |
| Visual output | `VisualGalleryTests` |
| Compile-time and AOT contract | `BasicGeneratorTests`, generated source inspections in `Reef.Generator.Tests` |

## Visual coverage

`VisualGalleryTests` renders three frames:

- `gallery-normal.png`
- `gallery-hover.png`
- `gallery-disabled.png`

The gallery covers colors, text, layout, borders, caps, radius, images,
components, clipping, anchors, rich text, and pseudo states. The test checks
specific pixels in addition to the golden images.
