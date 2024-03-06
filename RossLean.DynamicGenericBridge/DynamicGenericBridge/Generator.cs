using Microsoft.CodeAnalysis;

namespace RossLean.DynamicGenericBridge
{
    [Generator(LanguageNames.CSharp)]
    public partial class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // Prepare attributes
            context.RegisterForPostInitialization(c => c.AddSource("Attributes.g.cs", AttributeDefinition));

            // Watch for type parameters
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if(context.SyntaxContextReceiver is not SyntaxReceiver receiver)
            {
                return;
            }

            var execution = new Execution(context, receiver);
            try
            {
                execution.Run();
            }
            catch(Error e)
            {
                context.ReportDiagnostic(Execution.CreateDiagnostic(e.Id, e.Message, e.Location));
            }
        }
    }
}
