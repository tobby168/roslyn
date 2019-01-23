﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using System.Linq;

namespace CSharpSyntaxGenerator
{
    internal class TestWriter : AbstractFileWriter
    {
        private TestWriter(TextWriter writer, Tree tree) : base(writer, tree)
        {
        }

        public static void WriteGreen(TextWriter writer, Tree tree)
        {
            new TestWriter(writer, tree).WriteGreenFile();
        }

        public static void WriteRed(TextWriter writer, Tree tree)
        {
            new TestWriter(writer, tree).WriteRedFile();
        }

        private void WriteGreenFile()
        {
            WriteLine("// <auto-generated />");
            WriteLine();
            WriteLine("using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;");
            WriteLine("using Microsoft.CodeAnalysis.Syntax.InternalSyntax;");
            WriteLine("using Roslyn.Utilities;");
            WriteLine("using Xunit;");
            WriteLine();

            WriteLine("namespace Microsoft.CodeAnalysis.CSharp.UnitTests");
            OpenBlock();

            WriteLine();

            WriteLine("public partial class GreenNodeTests");
            OpenBlock();

            WriteLine("#region Green Generators");
            WriteNodeGenerators(isGreen: true);
            WriteLine("#endregion Green Generators");
            WriteLine();
            
            WriteLine("#region Green Factory and Property Tests");
            WriteFactoryPropertyTests(isGreen: true);
            WriteLine("#endregion Green Factory and Property Tests");
            WriteLine();
            
            WriteLine("#region Green Rewriters");
            WriteRewriterTests();
            WriteLine("#endregion Green Rewriters");

            CloseBlock();

            CloseBlock();
        }

        private void WriteRedFile()
        {
            WriteLine("// <auto-generated />");
            WriteLine();
            WriteLine("using Microsoft.CodeAnalysis.CSharp.Syntax;");
            WriteLine("using Roslyn.Utilities;");
            WriteLine("using Xunit;");
            WriteLine();

            WriteLine("namespace Microsoft.CodeAnalysis.CSharp.UnitTests");
            OpenBlock();

            WriteLine("public partial class RedNodeTests");
            OpenBlock();

            WriteLine("#region Red Generators");
            WriteNodeGenerators(isGreen: false);
            WriteLine("#endregion Red Generators");
            WriteLine();

            WriteLine("#region Red Factory and Property Tests");
            WriteFactoryPropertyTests(isGreen: false);
            WriteLine("#endregion Red Factory and Property Tests");
            WriteLine();

            WriteLine("#region Red Rewriters");
            WriteRewriterTests();
            WriteLine("#endregion Red Rewriters");

            CloseBlock();

            CloseBlock();
        }

        private void WriteNodeGenerators(bool isGreen)
        {
            var nodes = Tree.Types.Where(n => !(n is PredefinedNode) && !(n is AbstractNode));
            bool first = true;
            foreach (var node in nodes)
            {
                if (!first)
                {
                    WriteLine();
                }
                first = false;
                WriteNodeGenerator((Node)node, isGreen);
            }
        }

        private void WriteNodeGenerator(Node node, bool isGreen)
        {
            var valueFields = node.Fields.Where(n => !IsNodeOrNodeList(n.Type));
            var nodeFields = node.Fields.Where(n => IsNodeOrNodeList(n.Type));

            var strippedName = StripPost(node.Name, "Syntax");

            Write("private static {0} Generate{1}() => SyntaxFactory.{1}(", node.Name, strippedName);
            //instantiate node

            bool first = true;

            if (node.Kinds.Count > 1)
            {
                Write("SyntaxKind.{0}", node.Kinds[0].Name); //TODO: other kinds?
                first = false;
            }

            foreach (var field in nodeFields)
            {
                if (!first)
                {
                    Write(", ");
                }
                first = false;

                if (IsOptional(field))
                {
                    if (isGreen)
                    {
                        Write("null");
                    }
                    else
                    {
                        Write("default({0})", field.Type);
                    }
                }
                else if (IsAnyList(field.Type))
                {
                    string typeName = isGreen ? field.Type : (field.Type == "SyntaxList<SyntaxToken>") ? "SyntaxTokenList" : field.Type;
                    Write("new {0}()", typeName);
                }
                else if (field.Type == "SyntaxToken")
                {
                    var kind = ChooseValidKind(field);
                    var leadingTrivia = isGreen ? "null, " : "";
                    var trailingTrivia = isGreen ? ", null" : "";
                    if (kind == "IdentifierToken")
                    {
                        Write("SyntaxFactory.Identifier(\"{0}\")", field.Name);
                    }
                    else if (kind == "StringLiteralToken")
                    {
                        Write("SyntaxFactory.Literal({0}\"string\", \"string\"{1})", leadingTrivia, trailingTrivia);
                    }
                    else if (kind == "CharacterLiteralToken")
                    {
                        Write("SyntaxFactory.Literal({0}\"a\", 'a'{1})", leadingTrivia, trailingTrivia);
                    }
                    else if (kind == "NumericLiteralToken")
                    {
                        Write("SyntaxFactory.Literal({0}\"1\", 1{1})", leadingTrivia, trailingTrivia);
                    }
                    else
                    {
                        Write("SyntaxFactory.Token(SyntaxKind.{0})", ChooseValidKind(field));
                    }
                }
                else if (field.Type == "CSharpSyntaxNode")
                {
                    Write("SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(\"{0}\"))", field.Name);
                }
                else
                {
                    //drill down to a concrete type
                    var type = field.Type;
                    while (true)
                    {
                        var subTypes = ChildMap[type];
                        if (!subTypes.Any())
                        {
                            break;
                        }
                        type = subTypes.First();
                    }
                    Write("Generate{0}()", StripPost(type, "Syntax"));
                }
            }

            foreach (var field in valueFields)
            {
                if (!first)
                {
                    Write(", ");
                }
                first = false;

                Write("new {0}()", field.Type);
            }

            WriteLine(");");
        }

        private void WriteFactoryPropertyTests(bool isGreen)
        {
            var nodes = Tree.Types.Where(n => !(n is PredefinedNode) && !(n is AbstractNode));
            bool first = true;
            foreach (var node in nodes)
            {
                if (!first)
                {
                    WriteLine();
                }
                first = false;
                WriteFactoryPropertyTest((Node)node, isGreen);
            }
        }

        private void WriteFactoryPropertyTest(Node node, bool isGreen)
        {
            var valueFields = node.Fields.Where(n => !IsNodeOrNodeList(n.Type));
            var nodeFields = node.Fields.Where(n => IsNodeOrNodeList(n.Type));

            var strippedName = StripPost(node.Name, "Syntax");

            WriteLine("[Fact]");
            WriteLine("public void Test{0}FactoryAndProperties()", strippedName);
            OpenBlock();

            WriteLine("var node = Generate{0}();", strippedName);

            WriteLine();

            //check properties
            {
                string withStat = null;
                foreach (var field in nodeFields)
                {
                    if (IsOptional(field))
                    {
                        if (!isGreen && field.Type == "SyntaxToken")
                        {
                            WriteLine("Assert.Equal(SyntaxKind.None, node.{0}.Kind());", field.Name);
                        }
                        else
                        {
                            WriteLine("Assert.Null(node.{0});", field.Name);
                        }
                    }
                    else if (field.Type == "SyntaxToken")
                    {
                        if (!isGreen)
                        {
                            WriteLine("Assert.Equal(SyntaxKind.{0}, node.{1}.Kind());", ChooseValidKind(field), field.Name);
                        }
                        else
                        {
                            WriteLine("Assert.Equal(SyntaxKind.{0}, node.{1}.Kind);", ChooseValidKind(field), field.Name);
                        }
                    }
                    else
                    {
                        WriteLine("Assert.NotNull(node.{0});", field.Name);
                    }

                    if (!isGreen)
                    {
                        withStat += string.Format(".With{0}(node.{0})", field.Name);
                    }
                }

                foreach (var field in valueFields)
                {
                    WriteLine("Assert.Equal(new {0}(), node.{1});", field.Type, field.Name);
                    if (!isGreen)
                    {
                        withStat += string.Format(".With{0}(node.{0})", field.Name);
                    }
                }

                if (!isGreen && withStat != null)
                {
                    WriteLine("var newNode = node{0};", withStat);
                    WriteLine("Assert.Equal(node, newNode);");
                }
            }

            if (isGreen)
            {
                WriteLine();
                WriteLine("AttachAndCheckDiagnostics(node);");
            }

            CloseBlock();
        }

        private void WriteRewriterTests()
        {
            var nodes = Tree.Types.Where(n => !(n is PredefinedNode) && !(n is AbstractNode));
            bool first = true;
            foreach (var node in nodes)
            {
                if (!first)
                {
                    WriteLine();
                }
                first = false;
                WriteTokenDeleteRewriterTest((Node)node);
                WriteLine();
                WriteIdentityRewriterTest((Node)node);
            }
        }

        private void WriteTokenDeleteRewriterTest(Node node)
        {
            var valueFields = node.Fields.Where(n => !IsNodeOrNodeList(n.Type));
            var nodeFields = node.Fields.Where(n => IsNodeOrNodeList(n.Type));

            var strippedName = StripPost(node.Name, "Syntax");

            WriteLine("[Fact]");
            WriteLine("public void Test{0}TokenDeleteRewriter()", strippedName);
            OpenBlock();

            WriteLine("var oldNode = Generate{0}();", strippedName);
            WriteLine("var rewriter = new TokenDeleteRewriter();");
            WriteLine("var newNode = rewriter.Visit(oldNode);");

            WriteLine();
            WriteLine("if(!oldNode.IsMissing)");
            OpenBlock();
            WriteLine("Assert.NotEqual(oldNode, newNode);");
            CloseBlock();

            WriteLine();
            WriteLine("Assert.NotNull(newNode);");
            WriteLine("Assert.True(newNode.IsMissing, \"No tokens => missing\");");

            CloseBlock();
        }

        private void WriteIdentityRewriterTest(Node node)
        {
            var valueFields = node.Fields.Where(n => !IsNodeOrNodeList(n.Type));
            var nodeFields = node.Fields.Where(n => IsNodeOrNodeList(n.Type));

            var strippedName = StripPost(node.Name, "Syntax");

            WriteLine("[Fact]");
            WriteLine("public void Test{0}IdentityRewriter()", strippedName);
            OpenBlock();

            WriteLine("var oldNode = Generate{0}();", strippedName);
            WriteLine("var rewriter = new IdentityRewriter();");
            WriteLine("var newNode = rewriter.Visit(oldNode);");

            WriteLine();

            WriteLine("Assert.Same(oldNode, newNode);");

            CloseBlock();
        }

        //guess a reasonable kind if there are no constraints
        private static string ChooseValidKind(Field field)
        {
            return field.Kinds.Any() ? field.Kinds[0].Name : "IdentifierToken";
        }
    }
}
