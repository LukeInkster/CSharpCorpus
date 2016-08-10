﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// <copyright file="XslTransformation_Tests.cs" company="Microsoft">
// Copyright (c) 2015 All Right Reserved
// </copyright>
// <date>2008-12-28</date>
// <summary>The unit tests for XslTransformation buildtask.</summary>

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Xsl;
using System.Xml;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class XmlPeek_Tests
    {
        private string _xmlFileWithNs = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test' xmlns:s='http://nsurl'>
  <s:variable Type='String' Name='a'></s:variable>
  <s:variable Type='String' Name='b'></s:variable>
  <s:variable Type='String' Name='c'></s:variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>
";

        private string _xmlFileWithNsWithText = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test' xmlns:s='http://nsurl'>
  <s:variable Type='String' Name='a'>This</s:variable>
  <s:variable Type='String' Name='b'>is</s:variable>
  <s:variable Type='String' Name='c'>Sparta!</s:variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>
";

        private string _xmlFileNoNs = @"<?xml version='1.0' encoding='utf-8'?>
        
<class AccessModifier='public' Name='test'>
  <variable Type='String' Name='a'></variable>
  <variable Type='String' Name='b'></variable>
  <variable Type='String' Name='c'></variable>
  <method AccessModifier='public static' Name='GetVal' />
</class>
";

        [Fact]
        public void PeekWithNamespaceAttribute()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";
            p.Namespaces = "<Namespace Prefix=\"s\" Uri=\"http://nsurl\" />";

            Assert.True(p.Execute()); // "Test should've passed"
            Assert.Equal(3, p.Result.Length); // "result Length should be 3"
            string[] results = new string[] { "a", "b", "c" };
            for (int i = 0; i < p.Result.Length; i++)
            {
                Assert.True(p.Result[i].ItemSpec.Equals(results[i]), "Results don't match: " + p.Result[i].ItemSpec);
            }
        }

        [Fact]
        public void PeekWithNamespaceNode()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/.";
            p.Namespaces = "<Namespace Prefix=\"s\" Uri=\"http://nsurl\" />";

            Assert.True(p.Execute()); // "Test should've passed"
            Assert.Equal(3, p.Result.Length); // "result Length should be 3"

            string[] results = new string[] {
                "<s:variable Type=\"String\" Name=\"a\" xmlns:s=\"http://nsurl\"></s:variable>",
                "<s:variable Type=\"String\" Name=\"b\" xmlns:s=\"http://nsurl\"></s:variable>",
                "<s:variable Type=\"String\" Name=\"c\" xmlns:s=\"http://nsurl\"></s:variable>"
            };

            for (int i = 0; i < p.Result.Length; i++)
            {
                Assert.True(p.Result[i].ItemSpec.Equals(results[i]), "Results don't match: " + p.Result[i].ItemSpec);
            }
        }

        [Fact]
        public void PeekWithNamespaceText()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNsWithText, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/text()";
            p.Namespaces = "<Namespace Prefix=\"s\" Uri=\"http://nsurl\" />";
            Assert.True(p.Namespaces.Equals("<Namespace Prefix=\"s\" Uri=\"http://nsurl\" />"));
            Assert.True(p.Execute()); // "Test should've passed"
            Assert.Equal(3, p.Result.Length); // "result Length should be 3"

            string[] results = new string[] {
                "This",
                "is",
                "Sparta!"
            };

            for (int i = 0; i < p.Result.Length; i++)
            {
                Assert.True(p.Result[i].ItemSpec.Equals(results[i]), "Results don't match: " + p.Result[i].ItemSpec);
            }
        }

        [Fact]
        public void PeekNoNamespace()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//variable/@Name";

            Assert.True(p.Execute()); // "Test should've passed"
            Assert.Equal(3, p.Result.Length); // "result Length should be 3"
            string[] results = new string[] { "a", "b", "c" };
            for (int i = 0; i < p.Result.Length; i++)
            {
                Assert.True(p.Result[i].ItemSpec.Equals(results[i]), "Results don't match: " + p.Result[i].ItemSpec);
            }
        }

        [Fact]
        public void PeekNoNSXmlContent()
        {
            MockEngine engine = new MockEngine(true);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlContent = _xmlFileNoNs;
            p.Query = "//variable/@Name";

            Assert.True(p.Execute()); // "Test should've passed"
            Assert.Equal(3, p.Result.Length); // "result Length should be 3"
            string[] results = new string[] { "a", "b", "c" };
            for (int i = 0; i < p.Result.Length; i++)
            {
                Assert.True(p.Result[i].ItemSpec.Equals(results[i]), "Results don't match: " + p.Result[i].ItemSpec);
            }
        }

        [Fact]
        public void PeekNoNSXmlContentAndXmlInputError1()
        {
            MockEngine engine = new MockEngine(true);

            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.XmlContent = _xmlFileNoNs;
            Assert.True(p.XmlInputPath.ItemSpec.Equals(xmlInputPath));
            Assert.True(p.XmlContent.Equals(_xmlFileNoNs));

            p.Query = "//variable/@Name";
            Assert.True(p.Query.Equals("//variable/@Name"));

            Assert.False(p.Execute()); // "Test should've failed"
            Assert.True(engine.Log.Contains("MSB3741")); // "Error message MSB3741 should fire"
        }

        [Fact]
        public void PeekNoNSXmlContentAndXmlInputError2()
        {
            MockEngine engine = new MockEngine(true);

            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.Query = "//variable/@Name";

            Assert.False(p.Execute()); // "Test should've failed"
            Assert.True(engine.Log.Contains("MSB3741")); // "Error message MSB3741 should fire"
        }

        [Fact]
        public void PeekNoNSWPrefixedQueryError()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileNoNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;

            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";

            Assert.False(p.Execute()); // "Test should've failed"
            Assert.True(engine.Log.Contains("MSB3743")); // "Engine log should contain error code MSB3743"
        }

        [Fact]
        public void ErrorInNamespaceDecl()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            XmlPeek p = new XmlPeek();
            p.BuildEngine = engine;
            p.XmlInputPath = new TaskItem(xmlInputPath);
            p.Query = "//s:variable/@Name";
            p.Namespaces = "<!THIS IS ERROR Namespace Prefix=\"s\" Uri=\"http://nsurl\" />";

            bool executeResult = p.Execute();
            Assert.True(engine.Log.Contains("MSB3742"), "Engine Log: " + engine.Log);
            Assert.False(executeResult); // "Execution should've failed"
        }

        [Fact]
        public void MissingNamespaceParameters()
        {
            MockEngine engine = new MockEngine(true);
            string xmlInputPath;
            Prepare(_xmlFileWithNs, out xmlInputPath);

            string[] attrs = new string[] { "Prefix=\"s\"", "Uri=\"http://nsurl\"" };
            for (int i = 0; i < Math.Pow(2, attrs.Length); i++)
            {
                string res = "";
                for (int k = 0; k < attrs.Length; k++)
                {
                    if ((i & (int)Math.Pow(2, k)) != 0)
                    {
                        res += attrs[k] + " ";
                    }
                }
                XmlPeek p = new XmlPeek();
                p.BuildEngine = engine;
                p.XmlInputPath = new TaskItem(xmlInputPath);
                p.Query = "//s:variable/@Name";
                p.Namespaces = "<Namespace " + res + " />";

                bool result = p.Execute();
                Console.WriteLine(engine.Log);

                if (i == 3)
                {
                    Assert.True(result); // "Only 3rd value should pass."
                }
                else
                {
                    Assert.False(result); // "Only 3rd value should pass."
                }
            }
        }

        [Fact]
        public void PeekWithoutUsingTask()
        {
            string projectContents = @"
<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Target Name='x'>
    <XmlPeek Query='abc' ContinueOnError='true' />
  </Target>
</Project>";

            // The task won't complete properly, but ContinueOnError converts the errors to warnings, so the build should succeed
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(projectContents);

            // Verify that the task was indeed found. 
            logger.AssertLogDoesntContain("MSB4036");
        }

        private void Prepare(string xmlFile, out string xmlInputPath)
        {
            string dir = Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString());
            Directory.CreateDirectory(dir);
            xmlInputPath = dir + "\\doc.xml";
            using (StreamWriter sw = new StreamWriter(xmlInputPath, false))
            {
                sw.Write(xmlFile);
                sw.Close();
            }
        }
    }
}
