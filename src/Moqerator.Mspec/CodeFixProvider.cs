using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp.RuntimeBinder;

namespace Moqerator.Mspec
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MoqeratorMspecCodeFixProvider)), Shared]
    public class MoqeratorMspecCodeFixProvider : CodeFixProvider
    {
        private const string title = "Complete missing arguments with It.IsAny<>";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MoqeratorMspecAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var mockedMethod = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First().ChildNodes().OfType<ArgumentListSyntax>().Single();
            
            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => this.CompleteWithItIsAny(context.Document, declaration, c, mockedMethod),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> CompleteWithItIsAny(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken, ArgumentListSyntax mockedMethod)
        {
            var mockedMethodDeclaration = SymbolFinder.FindDeclarationsAsync(
                document.Project,
                ((IdentifierNameSyntax)((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)mockedMethod.Parent).Expression).Name).Identifier.ValueText,
                false,
                cancellationToken).Result;
            var parameters = ((IMethodSymbol)mockedMethodDeclaration.First()).Parameters;
            

            var length = parameters.Length;

            SeparatedSyntaxList<ArgumentSyntax> arguments = mockedMethod.Arguments;
            for (int index = 0; index < length; index++)
            {
                var argument = arguments.ElementAtOrDefault(index);
                var isMissing = argument?.IsMissing ?? true;

                if (isMissing)
                {
                    var parameter = parameters[index];

                    if (argument != null)
                    {
                        arguments = arguments.RemoveAt(index);
                    }

                    var argumentSyntax = SyntaxFactory.Argument(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("It"),
                                SyntaxFactory.GenericName(SyntaxFactory.Identifier("IsAny"))
                                    .WithTypeArgumentList(
                                        SyntaxFactory.TypeArgumentList(
                                            SyntaxFactory.SingletonSeparatedList(
                                                SyntaxFactory.ParseTypeName(parameter.ToDisplayString()))))))
                            .NormalizeWhitespace());
                    arguments = arguments.Insert(index, argumentSyntax); 
                    
                }              
            }
            var updatedMockedMethod = mockedMethod.WithArguments(arguments); 
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);

            var updatedSyntaxTree =
                syntaxTree.GetRoot().ReplaceNode(mockedMethod, updatedMockedMethod);

            return document.WithSyntaxRoot(updatedSyntaxTree);

            return null;
            // Compute new uppercase name.
            var identifierToken = typeDecl.Identifier;
            var newName = identifierToken.Text.ToUpperInvariant();

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            //return newSolution;
        }
    }
}