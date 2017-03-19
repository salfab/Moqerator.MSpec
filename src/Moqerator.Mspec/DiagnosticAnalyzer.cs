using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moqerator.Mspec
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MoqeratorMspecAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MoqeratorMspec";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            //context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            if (context.Node.Kind() == SyntaxKind.ArgumentList)
            {
                var node = (ArgumentListSyntax)context.Node;

                // handle argument list syntax node.

                var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node);

                context.ReportDiagnostic(diagnostic);
            }
            else if (context.Node.Kind() == SyntaxKind.InvocationExpression)
            {

                var node = (InvocationExpressionSyntax)context.Node;
                // handle invocation expression syntax
                if (node.Parent.Kind() == SyntaxKind.SimpleLambdaExpression &&
                    node.Parent.Parent.Parent.Kind() == SyntaxKind.ArgumentList &&
                    node.Parent.Parent.Parent.Parent.ChildNodes().Count() == 2 &&
                    node.Parent.Parent.Parent.Parent.ChildNodes().First().Kind() == SyntaxKind.SimpleMemberAccessExpression &&
                    node.Parent.Parent.Parent.Parent.ChildNodes().First().GetLastToken().ValueText == "Setup" &&
                    context.SemanticModel.GetDiagnostics(node.GetLocation().SourceSpan).Any())
                {                    
                                              
                    var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), node);

                    context.ReportDiagnostic(diagnostic);
                }
                // if node.Parent.Kind() == SimpleLambdaExpression
                // if node.Parent.Parent.Parent.Kind() == ArgumentList
                // if node.Parent.Parent.Parent.Parent.ChildNodes().Count == 2
                // if node.Parent.Parent.Parent.Parent.ChildNodes().First()
                // if node.Parent.Parent.Parent.Parent.ChildNodes().First().Kind() == SimpleMemberAccessExpression
                // node.Parent.Parent.Parent.Parent.ChildNodes().First().GetLastToken().Value == "Setup"

                //if (node.ArgumentList.Arguments.Count == 0 && )
            }
        }

    }
}
