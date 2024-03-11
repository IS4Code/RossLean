using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace RossLean.DynamicGenericBridge
{
    partial class Execution
    {
        // Comparers to group symbols by (typed to infer usage)
        static readonly IEqualityComparer<ITypeSymbol?> typeComparer = SymbolEqualityComparer.Default;
        static readonly IEqualityComparer<INamedTypeSymbol?> namedTypeComparer = SymbolEqualityComparer.Default;
        static readonly IEqualityComparer<INamespaceSymbol?> namespaceComparer = SymbolEqualityComparer.Default;

        // Formatting names in code
        static readonly SymbolDisplayFormat nameDisplay = new(miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        // Formatting type reference
        static readonly SymbolDisplayFormat typeDisplay = SymbolDisplayFormat.FullyQualifiedFormat;

        // Formatting type declaration
        static readonly SymbolDisplayFormat typeDeclarationDisplay = new(
            SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameOnly,
            SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
            SymbolDisplayMemberOptions.None,
            SymbolDisplayDelegateStyle.NameOnly,
            SymbolDisplayExtensionMethodStyle.Default,
            SymbolDisplayParameterOptions.None,
            SymbolDisplayPropertyStyle.NameOnly,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.IncludeTypeKeyword,
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        // Formatting type declaration
        static readonly SymbolDisplayFormat typeReferenceDisplay = new(
            SymbolDisplayGlobalNamespaceStyle.Included,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters,
            SymbolDisplayMemberOptions.None,
            SymbolDisplayDelegateStyle.NameOnly,
            SymbolDisplayExtensionMethodStyle.Default,
            SymbolDisplayParameterOptions.None,
            SymbolDisplayPropertyStyle.NameOnly,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.None,
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        // Partial definition display
        static readonly SymbolDisplayFormat partialDisplay = new(
            SymbolDisplayGlobalNamespaceStyle.Included,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef,
            SymbolDisplayDelegateStyle.NameAndSignature,
            SymbolDisplayExtensionMethodStyle.StaticMethod,
            SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeExtensionThis,
            SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            SymbolDisplayLocalOptions.None,
            SymbolDisplayKindOptions.IncludeMemberKeyword,
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );

        // Formatting types in errors
        static readonly SymbolDisplayFormat errorDisplay = SymbolDisplayFormat.CSharpErrorMessageFormat;

        bool failsCompilation;

        readonly GeneratorExecutionContext context;
        readonly SyntaxReceiver receiver;
        readonly INamedTypeSymbol bridgeAttributeType, methodAttributeType, arrayType, stringType, boolType, objectType, valueType, argumentExceptionType, binderExceptionType;
        readonly INamedTypeSymbol? taskType, valueTaskType, genericTaskType, genericValueTaskType, editorBrowsableAttribute, editorBrowsableState, debuggerNonUserCodeAttribute;

        const string dynamicDependencyType = "global::" + DynamicDependencyAttribute;
        const string unconditionalSuppressType = "global::" + UnconditionalSuppressMessageAttribute;

        const string dynamicBinderExceptionMarker = "DynamicGenericBridge_InnerException";

        public Execution(GeneratorExecutionContext context, SyntaxReceiver receiver)
        {
            this.context = context;
            this.receiver = receiver;

            #region Gathering relevant types

            bridgeAttributeType = LookupType(DynamicBridgeAttribute);
            methodAttributeType = LookupType(DynamicBridgeMethodAttribute);

            // Polyfill DynamicDependencyAttribute, DynamicallyAccessedMemberTypes, and UnconditionalSuppressMessageAttribute if missing
            if(context.Compilation.GetTypeByMetadataName(DynamicDependencyAttribute) == null)
            {
                context.AddSource(DynamicDependencyAttribute + ".g.cs", DynamicDependencyAttributeDefinition);
            }
            if(context.Compilation.GetTypeByMetadataName(DynamicallyAccessedMemberTypes) == null)
            {
                context.AddSource(DynamicallyAccessedMemberTypes + ".g.cs", DynamicallyAccessedMemberTypesDefinition);
            }
            if(context.Compilation.GetTypeByMetadataName(UnconditionalSuppressMessageAttribute) == null)
            {
                context.AddSource(UnconditionalSuppressMessageAttribute + ".g.cs", UnconditionalSuppressMessageAttributeDefinition);
            }

            arrayType = context.Compilation.GetSpecialType(SpecialType.System_Array);
            stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            boolType = context.Compilation.GetSpecialType(SpecialType.System_Boolean);
            objectType = context.Compilation.GetSpecialType(SpecialType.System_Object);
            valueType = context.Compilation.GetSpecialType(SpecialType.System_ValueType);
            taskType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            valueTaskType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
            genericTaskType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
            genericValueTaskType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
            editorBrowsableAttribute = context.Compilation.GetTypeByMetadataName("System.ComponentModel.EditorBrowsableAttribute");
            editorBrowsableState = context.Compilation.GetTypeByMetadataName("System.ComponentModel.EditorBrowsableState");
            debuggerNonUserCodeAttribute = context.Compilation.GetTypeByMetadataName("System.Diagnostics.DebuggerNonUserCodeAttribute");

            argumentExceptionType = LookupType("System.ArgumentException");
            binderExceptionType = LookupType("Microsoft.CSharp.RuntimeBinder.RuntimeBinderException");

            INamedTypeSymbol LookupType(string name)
            {
                return context.Compilation.GetTypeByMetadataName(name) ?? throw new Error(1, $"Predefined type '{name}' could not be found.", null);
            }

            #endregion
        }

        public void Run()
        {
            foreach(var typesGroup in
                receiver.Methods.GroupBy(m => m.ContainingType, namedTypeComparer)
                .GroupBy(methods => NullOnGlobal(methods.Key.ContainingNamespace), namespaceComparer))
            {
                // Generate a single file for each namespace
                var contents = new StringBuilder();
                var fileName = ProcessFile(typesGroup.Key, typesGroup, contents);
                if(!failsCompilation)
                {
                    context.AddSource(fileName, contents.ToString());
                }
            }
        }

        static INamespaceSymbol? NullOnGlobal(INamespaceSymbol? ns)
        {
            return ns is null or { IsGlobalNamespace: true } ? null : ns;
        }

        private bool Error(int id, FormattableString message, ISymbol? symbol)
        {
            failsCompilation = true;
            context.ReportDiagnostic(CreateDiagnostic(id, message, symbol?.Locations.FirstOrDefault()));
            return false;
        }

        private void Warning(int id, FormattableString message, ISymbol? symbol)
        {
            context.ReportDiagnostic(CreateDiagnostic(id, message, symbol?.Locations.FirstOrDefault(), DiagnosticSeverity.Warning));
        }

        internal static Diagnostic CreateDiagnostic(int id, FormattableString message, Location? location, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            var descriptor = new DiagnosticDescriptor($"DGB{id:000}", "Error from DynamicGenericBridge generator.", message.Format, severity.ToString().ToLowerInvariant(), severity, true);
            return Diagnostic.Create(descriptor, location, message.GetArguments());
        }

        private string ProcessFile(INamespaceSymbol? declaringNamespace, IEnumerable<IGrouping<INamedTypeSymbol, IMethodSymbol>> types, StringBuilder stringBuilder)
        {
            using var writer = new IndentedTextWriter(new StringWriter(stringBuilder), "    ");

            writer.WriteLine("#nullable disable");

            // Namespace may be null for non-namespaces types
            if(declaringNamespace != null)
            {
                writer.WriteLine($@"namespace {declaringNamespace.ToDisplayString()}");
                writer.WriteLine("{");
                writer.Indent++;
            }

            // Generate definitions for each class
            foreach(var methodsGroup in types)
            {
                ProcessType(methodsGroup.Key, methodsGroup, writer);
            }

            if(declaringNamespace != null)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }
            var fileName = declaringNamespace == null ? "Bridges.g.cs" : $"{declaringNamespace.ToDisplayString()}_Bridges.g.cs";
            return fileName;
        }

        private void ProcessType(INamedTypeSymbol declaringType, IEnumerable<IMethodSymbol> methods, IndentedTextWriter writer)
        {
            if(!declaringType.DeclaringSyntaxReferences.Any(s => s.GetSyntax() is BaseTypeDeclarationSyntax d && d.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))))
            {
                // Must be partial
                Error(13, $"Type '{declaringType.ToDisplayString(errorDisplay)}' must be declared as partial to add dynamic bridge methods.", declaringType);
                return;
            }

            writer.Write($"partial {declaringType.ToDisplayString(typeDeclarationDisplay)}");
            writer.WriteLine();
            writer.WriteLine("{");
            writer.Indent++;

            foreach(var method in methods)
            {
                // Process each captured method
                ProcessMethod(declaringType, method, writer);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void ProcessMethod(INamedTypeSymbol declaringType, IMethodSymbol method, IndentedTextWriter writer)
        {
            var simplifiedParams = new Dictionary<ITypeParameterSymbol, ITypeSymbol?>(typeComparer);

            foreach(var param in method.TypeParameters)
            {
                if(param.GetAttributes().FirstOrDefault(a => typeComparer.Equals(a.AttributeClass, bridgeAttributeType)) is { } attr)
                {
                    if(param.HasUnmanagedTypeConstraint || param.HasNotNullConstraint)
                    {
                        Warning(11, $"Method '{method.ToDisplayString(errorDisplay)}' has a dynamically bridged type parameter '{param.ToDisplayString(errorDisplay)}' with the 'unmanaged' or 'notnull' constraint. C#-specific constraints are not supported by the DLR and will not be enforced during binding.", param);
                    }

                    // Method has a marked type parameter
                    var args = attr.ConstructorArguments;
                    if(args.Length == 0)
                    {
                        // No simplified type specified
                        simplifiedParams[param] = null;
                        continue;
                    }
                    else if(args.Length == 1)
                    {
                        var arg = args[0];
                        if(arg.Kind == TypedConstantKind.Type)
                        {
                            // Simplified type specified
                            simplifiedParams[param] = (ITypeSymbol)arg.Value!;
                            continue;
                        }
                    }
                    // Should not happen for correct syntax
                    Error(2, $"Method '{method.ToDisplayString(errorDisplay)}' has incorrectly used DynamicBridge attribute.", param);
                    return;
                }
            }

            var methodAttr = method.GetAttributes().FirstOrDefault(a => typeComparer.Equals(a.AttributeClass, methodAttributeType));

            if(simplifiedParams.Count == 0)
            {
                if(methodAttr != null)
                {
                    Error(12, $"Method '{method.ToDisplayString(errorDisplay)}' has the DynamicBridgeMethod attribute, but none of its type parameters are annotated with the DynamicBridge attribute.", method);
                }
                return;
            }

            if(method.ExplicitInterfaceImplementations.Length > 0)
            {
                Error(10, $"Method '{method.ToDisplayString(errorDisplay)}' is an explicit interface implementation, which is not supported as a dynamic bridge method.", method);
            }

            if(method.ReturnsByRef)
            {
                // DLR does not support ref returns
                Error(3, $"Method '{method.ToDisplayString(errorDisplay)}' returns by reference, which is not supported for dynamic dispatch.", method);
            }

            // We need the "signature"
            string? targetMethodId = method.GetDocumentationCommentId();
            if(targetMethodId == null)
            {
                Error(4, $"Method '{method.ToDisplayString(errorDisplay)}' could not be referenced.", method);
                return;
            }
            // But without the class
            targetMethodId = targetMethodId.Substring(declaringType.GetDocumentationCommentId()!.Length + 1);

            string bridgeModifiers = FormatAccessibility(method.DeclaredAccessibility);
            string bridgeName = method.Name;
            string bridgeNameDisplay = method.ToDisplayString(nameDisplay);
            bool ignoreUnbound = false;

            if(methodAttr != null)
            {
                // [DynamicBridgeMethod] is specified
                var args = methodAttr.ConstructorArguments;
                if(args.Length == 1)
                {
                    var arg = args[0];
                    if(!(arg.Kind == TypedConstantKind.Primitive && (arg.IsNull || typeComparer.Equals(arg.Type, stringType))))
                    {
                        Error(5, $"Method '{method.ToDisplayString(errorDisplay)}' has incorrectly used DynamicBridgeMethod attribute.", method);
                        return;
                    }
                    if(arg.Value is string name)
                    {
                        bridgeName = bridgeNameDisplay = name;
                    }
                }

                if(methodAttr.NamedArguments.FirstOrDefault(pair => pair.Key == DynamicBridgeMethodAttributeModifiers) is { Value: var attrs })
                {
                    if(!(attrs.Kind is TypedConstantKind.Primitive or TypedConstantKind.Error && (attrs.IsNull || typeComparer.Equals(attrs.Type, stringType))))
                    {
                        Error(6, $"Method '{method.ToDisplayString(errorDisplay)}' has incorrectly used DynamicBridgeMethod.{DynamicBridgeMethodAttributeModifiers}.", method);
                        return;
                    }
                    bridgeModifiers = (attrs.Value as string) ?? bridgeModifiers;
                }

                if(methodAttr.NamedArguments.FirstOrDefault(pair => pair.Key == DynamicBridgeMethodAttributeIgnoreUnbound) is { Value: var unbound })
                {
                    if(!(unbound.Kind is TypedConstantKind.Primitive or TypedConstantKind.Error && (unbound.IsNull || typeComparer.Equals(unbound.Type, boolType))))
                    {
                        Error(6, $"Method '{method.ToDisplayString(errorDisplay)}' has incorrectly used DynamicBridgeMethod.{DynamicBridgeMethodAttributeIgnoreUnbound}.", method);
                        return;
                    }
                    ignoreUnbound = (unbound.Value as bool?) ?? ignoreUnbound;
                }
            }

            string targetMethodNameDisplay = method.ToDisplayString(nameDisplay);
            
            // Always generate target to catch inner RuntimeBinderExceptions
            //if(bridgeName == method.Name)
            {
                // A differently-named method must be targeted to prevent infinite recursion
                var targetFullParts = method.ToDisplayParts(partialDisplay).ToList();
                var targetNameAt = targetFullParts.FindIndex(part => part.Kind == SymbolDisplayPartKind.MethodName);
                if(targetNameAt == -1 || !targetMethodId.StartsWith(method.Name, StringComparison.Ordinal))
                {
                    Error(4, $"Method '{method.ToDisplayString(errorDisplay)}' could not be referenced.", method);
                    return;
                }
                // Suffix can be safely added
                string bridgeTargetName = $"_{method.Name}_PrivateDynamicBridgeTarget";
                targetFullParts[targetNameAt] = new(SymbolDisplayPartKind.MethodName, null, bridgeTargetName);
                targetMethodId = bridgeTargetName + targetMethodId.Substring(method.Name.Length);

                // Add some attributes
                if(editorBrowsableAttribute != null && editorBrowsableState != null && editorBrowsableState.GetMembers("Never").Length > 0)
                {
                    writer.WriteLine($"[{editorBrowsableAttribute.ToDisplayString(typeDisplay)}({editorBrowsableState.ToDisplayString(typeDisplay)}.Never)]");
                }
                if(debuggerNonUserCodeAttribute != null)
                {
                    writer.WriteLine($"[{debuggerNonUserCodeAttribute.ToDisplayString(typeDisplay)}]");
                }

                if(method.IsStatic)
                {
                    writer.Write("static ");
                }

                if(method.IsReadOnly)
                {
                    writer.Write("readonly ");
                }

                foreach(var part in targetFullParts)
                {
                    writer.Write(part.ToString());
                }
                writer.WriteLine();
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("try");
                writer.WriteLine("{");
                writer.Indent++;

                if(!method.ReturnsVoid)
                {
                    writer.Write("return ");
                }

                writer.Write(targetMethodNameDisplay);
                if(method.IsGenericMethod)
                {
                    // state all type parameters
                    writer.Write('<');
                    writer.WriteJoined(", ", method.TypeParameters.Select(p => p.ToDisplayString(nameDisplay)));
                    writer.Write('>');
                }
                bool first = true;
                writer.Write("(");
                foreach(var param in method.Parameters)
                {
                    if(first)
                    {
                        first = false;
                    }
                    else
                    {
                        writer.Write(", ");
                    }
                    // Pass by the same reference
                    writer.Write(FormatRefKind(param.RefKind));
                    writer.Write(param.ToDisplayString(nameDisplay));
                }
                writer.WriteLine(");");

                writer.Indent--;
                writer.WriteLine("}");
                writer.Write("catch(");
                writer.Write(binderExceptionType.ToDisplayString(typeDisplay));
                writer.WriteLine(" dynamicGenericBridge_binderException)");
                writer.WriteLine("{");
                writer.Indent++;

                // Any exception thrown from within must be marked to be exposed
                writer.Write("dynamicGenericBridge_binderException.Data[");
                writer.Write(SymbolDisplay.FormatLiteral(dynamicBinderExceptionMarker, true));
                writer.WriteLine("] = \"\";");
                writer.WriteLine("throw;");

                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");

                targetMethodNameDisplay = bridgeTargetName;
            }

            var retainedTypeParametersCount = method.TypeParameters.Count(p => !simplifiedParams.ContainsKey(p));

            // State the dynamic dependency
            writer.WriteLine($"[{dynamicDependencyType}({SymbolDisplay.FormatLiteral(targetMethodId, true)})]");

            // Suppress IL2026:RequiresUnreferencedCode
            writer.WriteLine($"[{unconditionalSuppressType}(\"AssemblyLoadTrimming\", \"IL2026:RequiresUnreferencedCode\")]");

            // Do not show in debugger
            if(debuggerNonUserCodeAttribute != null)
            {
                writer.WriteLine($"[{debuggerNonUserCodeAttribute.ToDisplayString(typeDisplay)}]");
            }

            // Look for methods with the same name that may serve as the partial definition
            var partialCandidates = declaringType.GetMembers(bridgeName).OfType<IMethodSymbol>();
            partialCandidates = partialCandidates.Where(
                // Must be a partial method, be equally static, same number of parameters, and less type parameters
                m => m.IsPartialDefinition && m.IsStatic == method.IsStatic && m.Arity == retainedTypeParametersCount && m.Parameters.Length == method.Parameters.Length
            );

            ITypeSymbol? returnsValueType;
            bool isAsync;
            IMethodSymbol? candidate;

            if(partialCandidates.Take(2).ToList() is { Count: 1 } singleCandidate)
            {
                // Implement this partial method
                candidate = singleCandidate[0];

                if(candidate.ReturnsVoid)
                {
                    returnsValueType = null;
                    // async void is externally non-async
                    isAsync = false;
                }
                else
                {
                    if(method.IsAsync || candidate.IsAsync)
                    {
                        // Check bridged method for async because partial definitions cannot be async
                        if(typeComparer.Equals(candidate.ReturnType, taskType) || typeComparer.Equals(candidate.ReturnType, valueTaskType))
                        {
                            // Non-returning
                            isAsync = true;
                            returnsValueType = null;
                        }
                        else if(candidate.ReturnType is INamedTypeSymbol { IsGenericType: true, ConstructedFrom: var typeDefinition, TypeArguments: { Length: 1 } typeArgs } && (typeComparer.Equals(typeDefinition, genericTaskType) || typeComparer.Equals(typeDefinition, genericValueTaskType)))
                        {
                            // Task with a value
                            isAsync = true;
                            returnsValueType = typeArgs[0];
                        }
                        else
                        {
                            // Unknown async type - don't make async
                            isAsync = false;
                            returnsValueType = candidate.ReturnType;
                        }
                    }
                    else
                    {
                        // Non-async non-void method must return something
                        isAsync = false;
                        returnsValueType = candidate.ReturnType;
                    }
                }

                writer.Write(FormatAccessibility(candidate.DeclaredAccessibility));
                writer.Write(" ");

                if(candidate.IsStatic)
                {
                    writer.Write("static ");
                }

                if(isAsync)
                {
                    writer.Write("async ");
                }

                if(candidate.IsReadOnly)
                {
                    writer.Write("readonly ");
                }

                writer.Write("partial ");
                writer.WriteLine(candidate.ToDisplayString(partialDisplay));
            }
            else
            {
                candidate = null;

                if(!String.IsNullOrEmpty(bridgeModifiers))
                {
                    writer.Write($"{bridgeModifiers} ");
                }

                if(method.IsStatic)
                {
                    // Only static to static/instance to instance
                    writer.Write("static ");
                }

                string returnType;
                if(method.ReturnsVoid)
                {
                    returnType = "void";
                    returnsValueType = null;
                    // async void is externally non-async
                    isAsync = false;
                }
                else
                {
                    // Return type needs simplifying if it contains the bridged type parameters
                    var simplifiedReturnType = SimplifyType(method.ReturnType, true);
                    returnType = simplifiedReturnType?.ToDisplayString(typeDisplay) ?? "object";
                    if(method.IsAsync)
                    {
                        if(typeComparer.Equals(simplifiedReturnType, taskType) || typeComparer.Equals(simplifiedReturnType, valueTaskType))
                        {
                            // Non-returning or simplified to non-returning
                            isAsync = true;
                            returnsValueType = null;
                        }
                        else if(simplifiedReturnType is INamedTypeSymbol { IsGenericType: true, ConstructedFrom: var typeDefinition, TypeArguments: { Length: 1 } typeArgs } && (typeComparer.Equals(typeDefinition, genericTaskType) || typeComparer.Equals(typeDefinition, genericValueTaskType)))
                        {
                            // Task with a value
                            isAsync = true;
                            returnsValueType = typeArgs[0];
                        }
                        else
                        {
                            // Unknown async type - don't make async
                            isAsync = false;
                            returnsValueType = method.ReturnType;
                        }
                    }
                    else
                    {
                        // Non-async non-void method must return something
                        isAsync = false;
                        returnsValueType = method.ReturnType;
                    }
                }

                if(isAsync)
                {
                    writer.Write("async ");
                }

                if(method.IsReadOnly)
                {
                    writer.Write("readonly ");
                }

                writer.Write($"{returnType} {bridgeNameDisplay}");

                var retainedTypeParameters = method.TypeParameters.Where(p => !simplifiedParams.ContainsKey(p)).ToList();

                if(retainedTypeParameters.Count > 0)
                {
                    // There are non-bridged type parameters
                    writer.Write('<');
                    writer.WriteJoined(", ", retainedTypeParameters.Select(p => p.ToDisplayString(nameDisplay)));
                    writer.Write('>');
                }

                bool paramFirst = true;
                writer.Write("(");
                foreach(var param in method.Parameters)
                {
                    if(paramFirst)
                    {
                        paramFirst = false;
                        if(method.IsExtensionMethod)
                        {
                            // Replicate extension method
                            writer.Write("this ");
                        }
                    }
                    else
                    {
                        writer.Write(", ");
                    }
                    if(param.IsParams && !NeedsSimplifying(param.Type))
                    {
                        // Replicate "params" (only if identical type, otherwise it may never be compatible when compiler-constructed)
                        writer.Write("params ");
                    }
                    if(param.RefKind != RefKind.None)
                    {
                        if(NeedsSimplifying(param.Type))
                        {
                            // Type of reference must match
                            Error(7, $"Method '{method.ToDisplayString(errorDisplay)}' has a reference parameter '{param.ToDisplayString(errorDisplay)}' that requires dynamic dispatch, which is not supported.", param);
                        }
                        writer.Write(FormatRefKind(param.RefKind));
                    }
                    // Simplify parameter type if it contains the bridged type parameters
                    var paramType = SimplifyType(param.Type)?.ToDisplayString(typeDisplay) ?? "object";
                    writer.Write(paramType);
                    writer.Write($" {param.ToDisplayString(nameDisplay)}");
                    if(param.HasExplicitDefaultValue && (!NeedsSimplifying(param.Type) || param.ExplicitDefaultValue == null))
                    {
                        // Default value is retained only if it was not simplified, or was null/default (but there are no generic types supporting constants)
                        var defaultValue = param.ExplicitDefaultValue;
                        writer.Write(" = ");
                        switch(defaultValue)
                        {
                            case null:
                                writer.Write($"default({paramType})");
                                break;
                            default:
                                writer.Write(SymbolDisplay.FormatPrimitive(defaultValue, true, false));
                                break;
                        }
                    }
                }
                writer.WriteLine(")");

                foreach(var typeParameter in retainedTypeParameters)
                {
                    var constraints = new List<string>();
                    if(typeParameter.HasReferenceTypeConstraint)
                    {
                        if(typeParameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated)
                        {
                            constraints.Add("class?");
                        }
                        else
                        {
                            constraints.Add("class");
                        }
                    }
                    if(typeParameter.HasValueTypeConstraint)
                    {
                        constraints.Add("struct");
                    }
                    if(typeParameter.HasNotNullConstraint)
                    {
                        constraints.Add("notnull");
                    }
                    if(typeParameter.HasUnmanagedTypeConstraint)
                    {
                        constraints.Add("unmanaged");
                    }
                    // Recover as many constraints as possible
                    var validConstraints = typeParameter.ConstraintTypes.Select(t => SimplifyType(t));
                    // If simplified to object or ValueType, remove (invalid constraint)
                    validConstraints = validConstraints.Where(t => t != null && !typeComparer.Equals(valueType));
                    constraints.AddRange(validConstraints.Select(t => t!.ToDisplayString(typeDisplay)));
                    if(typeParameter.HasConstructorConstraint)
                    {
                        constraints.Add("new()");
                    }
                    if(constraints.Count > 0)
                    {
                        writer.Write($"    where {typeParameter.ToDisplayString(nameDisplay)} : ");
                        writer.WriteJoined(", ", constraints);
                        writer.WriteLine();
                    }
                }
            }

            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("try");
            writer.WriteLine("{");
            writer.Indent++;

            var inferrableParams = new HashSet<ITypeParameterSymbol>(typeComparer);

            if(returnsValueType != null)
            {
                writer.Write("return ");
                // No ref since not supported
            }
            if(isAsync)
            {
                // Original method can be awaited
                writer.Write("await ");
            }
            // Call the bridged method
            writer.Write(targetMethodNameDisplay);
            // No type arguments since they must be inferred
            writer.Write("(");
            var simplifiedMethodParams = new List<IParameterSymbol>();
            for(int i = 0; i < method.Parameters.Length; i++)
            {
                var targetParam = method.Parameters[i];
                var definitionParam = (candidate ?? method).Parameters[i];

                if(i > 0)
                {
                    writer.Write(", ");
                }
                // Pass by the same reference
                writer.Write(FormatRefKind(definitionParam.RefKind));
                if(NeedsSimplifying(targetParam.Type, inferrableParams))
                {
                    // Parameter type was simplified, needs dynamic dispatch
                    writer.Write("(dynamic)");
                    simplifiedMethodParams.Add(definitionParam);
                }
                writer.Write(definitionParam.ToDisplayString(nameDisplay));
            }
            writer.WriteLine(");");

            if(inferrableParams.Count < simplifiedParams.Count)
            {
                // Some type parameters did not appear
                var missing = simplifiedParams.Keys.Where(p => !inferrableParams.Contains(p)).Select(p => p.ToDisplayString(errorDisplay));
                Error(8, $"Method '{method.ToDisplayString(errorDisplay)}' cannot not be dynamically bridged because some type parameters could not be resolved at runtime: {String.Join(", ", missing)}. The type parameters must appear in the method's parameter list in order to be inferrable.", method);
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Write("catch(");
            writer.Write(binderExceptionType.ToDisplayString(typeDisplay));
            writer.WriteLine(" dynamicGenericBridge_binderException)");
            writer.WriteLine("{");
            writer.Indent++;

            // Remove marker and re-throw
            writer.Write("if(dynamicGenericBridge_binderException.Data[");
            writer.Write(SymbolDisplay.FormatLiteral(dynamicBinderExceptionMarker, true));
            writer.WriteLine("] != null)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.Write("dynamicGenericBridge_binderException.Data.Remove(");
            writer.Write(SymbolDisplay.FormatLiteral(dynamicBinderExceptionMarker, true));
            writer.WriteLine(");");
            writer.WriteLine("throw;");

            writer.Indent--;
            writer.WriteLine("}");

            if(ignoreUnbound)
            {
                // Suppress the exception
                foreach(var param in (candidate ?? method).Parameters)
                {
                    if(param.RefKind == RefKind.Out)
                    {
                        // "out" parameters need to be assigned
                        writer.WriteLine($"{param.ToDisplayString(nameDisplay)} = default({param.Type.ToDisplayString(typeDisplay)});");
                    }
                }
                if(returnsValueType != null)
                {
                    writer.WriteLine($"return default({returnsValueType.ToDisplayString(typeDisplay)});");
                }
            }
            else
            {
                writer.Write("throw new ");
                writer.Write(argumentExceptionType.ToDisplayString(typeDisplay));
                writer.Write("(");
                if(simplifiedMethodParams.Count == 1)
                {
                    var paramType = simplifiedMethodParams[0].Type;
                    string? typeofExpression;
                    switch(paramType)
                    {
                        case INamedTypeSymbol namedParamType:
                            typeofExpression = $"typeof({namedParamType.ConstructUnboundGenericType().ToDisplayString(typeReferenceDisplay)})";
                            break;
                        case IArrayTypeSymbol { ElementType: INamedTypeSymbol elementType } arrayType:
                            string rank = arrayType.Rank <= 1 ? "" : arrayType.Rank.ToString();
                            typeofExpression = $"typeof({elementType.ConstructUnboundGenericType().ToDisplayString(typeReferenceDisplay)}).MakeArrayType({rank})";
                            break;
                        default:
                            typeofExpression = null;
                            break;
                    }
                    if(typeofExpression != null)
                    {
                        writer.Write("$\"The argument could not be dynamically resolved to its specific type '{");
                        writer.Write(typeofExpression);
                        writer.Write("}'.\"");
                    }
                    else
                    {
                        writer.Write("\"The argument could not be dynamically resolved to its specific type.\"");
                    }
                    // the parameter name can be provided
                    writer.Write(", nameof(");
                    writer.Write(simplifiedMethodParams[0].ToDisplayString(nameDisplay));
                    writer.Write(")");
                }
                else
                {
                    writer.Write("\"The arguments could not be dynamically resolved to their specific types.\"");
                }
                writer.WriteLine(", dynamicGenericBridge_binderException);");
            }

            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");

            ITypeSymbol? SimplifyType(ITypeSymbol type, bool isReturn = false)
            {
                if(!NeedsSimplifying(type))
                {
                    return type;
                }
                // Get more general type that does not use bridged type parameters
                return type switch
                {
                    // System.Array is the nearest type that does not expose the element type
                    IArrayTypeSymbol => arrayType,
                    // Look for a suitable base type
                    INamedTypeSymbol named => SimplifyNamedType(named, isReturn),
                    // Pick the specified one or treat it as a type
                    ITypeParameterSymbol param => simplifiedParams[param] ?? SimplifyTypeParameter(param),
                    _ => null
                };
            }

            ITypeSymbol? SimplifyNamedType(ITypeSymbol type, bool isReturn)
            {
                if(type.TypeKind == TypeKind.Interface)
                {
                    // Look through its interfaces
                    return SimplifyInterfaceType(type);
                }
                else if(isReturn && method.IsAsync && type is INamedTypeSymbol { IsGenericType: true, ConstructedFrom: var typeDefinition })
                {
                    // Is return for async method
                    if(typeComparer.Equals(typeDefinition, genericTaskType))
                    {
                        // Task<T> to Task
                        return taskType;
                    }
                    if(typeComparer.Equals(typeDefinition, genericValueTaskType))
                    {
                        // ValueTask<T> to ValueTask
                        return valueTaskType;
                    }
                }
                ITypeSymbol? baseType = type;
                while((baseType = baseType.BaseType) != null)
                {
                    // Is the base type usable?
                    if(typeComparer.Equals(baseType, objectType) || typeComparer.Equals(baseType, valueType))
                    {
                        // We got object or ValueType, which is too general
                        break;
                    }
                    if(!NeedsSimplifying(baseType))
                    {
                        // We got a better base type
                        return baseType;
                    }
                }
                // We did not get any suitable base type; look through the interfaces
                return SimplifyInterfaceType(type) ??
                    // or just use ValueType or object
                    (type.IsValueType ? valueType : null);
            }

            ITypeSymbol? SimplifyInterfaceType(ITypeSymbol type)
            {
                // Check how many direct candidates there are
                var options = type.Interfaces.Where(i => !NeedsSimplifying(i)).Take(2).ToList();
                switch(options.Count)
                {
                    case 0:
                        // No direct candidates, look in AllInterfaces
                        break;
                    case 1:
                        // Just a single direct interface that does not need simplifying, pick it
                        return options[0];
                    default:
                        // No obvious choice (consider adding a generic parameter)
                        return null;
                }
                // Same for all interfaces
                options = type.AllInterfaces.Where(i => !NeedsSimplifying(i)).Take(2).ToList();
                if(options.Count == 1)
                {
                    // Single best choice
                    return options[0];
                }
                // There are multiple direct or indirect implemented interfaces that can be used
                return null;
            }

            ITypeSymbol? SimplifyTypeParameter(ITypeParameterSymbol param)
            {
                // Check how many candidates there are
                var options = param.ConstraintTypes.Where(p => !NeedsSimplifying(p)).Take(2).ToList();
                switch(options.Count)
                {
                    case 0:
                        // No direct candidates, simplify all constraints
                        break;
                    case 1:
                        // Just a single direct type that does not need simplifying, pick it
                        return options[0];
                    default:
                        // No obvious choice (consider adding a generic parameter)
                        return null;
                }
                options = param.ConstraintTypes.Select(p => SimplifyType(p)).Where(t => t != null).Take(2).ToList()!;
                if(options.Count == 1)
                {
                    // Single best choice
                    return options[0];
                }
                if(param.HasValueTypeConstraint || param.HasUnmanagedTypeConstraint)
                {
                    // At least simplify to ValueType
                    return valueType;
                }
                return null;
            }

            bool NeedsSimplifying(ITypeSymbol type, ICollection<ITypeParameterSymbol>? containedParameters = null)
            {
                // Check if type contains a bridged parameter
                return type switch
                {
                    // Look for element type in array
                    IArrayTypeSymbol array => NeedsSimplifying(array.ElementType, containedParameters),
                    // Look for generic parameters
                    INamedTypeSymbol named => containedParameters != null
                    // When visited collection is given, it needs to be evaluated fully
                    ? named.TypeArguments.Count(a => NeedsSimplifying(a, containedParameters)) != 0
                    : named.TypeArguments.Any(a => NeedsSimplifying(a)),
                    // Unsupported
                    IPointerTypeSymbol or IFunctionPointerTypeSymbol => Error(9, $"Pointer type '{type.ToDisplayString(typeDisplay)}' cannot be used with the DynamicBridge attribute.", method),
                    // Return if bridged
                    ITypeParameterSymbol param => simplifiedParams.ContainsKey(param) && AddParam(containedParameters, param),
                    _ => true
                };
            }

            static bool AddParam(ICollection<ITypeParameterSymbol>? collection, ITypeParameterSymbol param)
            {
                collection?.Add(param);
                return true;
            }
        }

        static string FormatAccessibility(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Private => "private",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.Public => "public",
                _ => throw new InvalidEnumArgumentException(nameof(accessibility), (int)accessibility, typeof(Accessibility))
            };
        }

        static string FormatRefKind(RefKind refKind)
        {
            return refKind switch
            {
                RefKind.None => "",
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => throw new InvalidEnumArgumentException(nameof(refKind), (int)refKind, typeof(RefKind))
            };
        }

        static string FormatVariance(VarianceKind variance)
        {
            return variance switch
            {
                VarianceKind.None => "",
                VarianceKind.In => "in ",
                VarianceKind.Out => "out ",
                _ => throw new InvalidEnumArgumentException(nameof(variance), (int)variance, typeof(VarianceKind))
            };
        }
    }
}
