using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace RossLean.DynamicGenericBridge
{
    class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IMethodSymbol> Methods { get; } = new List<IMethodSymbol>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if(context.Node is TypeParameterListSyntax { Parent: MethodDeclarationSyntax syntaxMethod })
            {
                if(context.SemanticModel.GetDeclaredSymbol(syntaxMethod) is IMethodSymbol method)
                {
                    if(
                        method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == Execution.DynamicBridgeMethodAttribute) ||
                        method.TypeParameters.Any(p => p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == Execution.DynamicBridgeAttribute))
                    )
                    {
                        Methods.Add(method);
                    }
                }
            }
        }
    }
}
