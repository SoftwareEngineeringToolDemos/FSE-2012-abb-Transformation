﻿/******************************************************************************
 * Copyright (c) 2013 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ABB.SrcML.Data.Test {
    [TestFixture]
    class RealWorldTests {
        private bool shouldRegenerateSrcML = false;
        private Dictionary<Language, AbstractCodeParser> CodeParser;

        [TestFixtureSetUp]
        public void ClassSetup() {
            CodeParser = new Dictionary<Language, AbstractCodeParser>() {
                { Language.CPlusPlus, new CPlusPlusCodeParser() },
                { Language.Java, new JavaCodeParser() },
                { Language.CSharp, new CSharpCodeParser() }
            };
        }

        [Test]
        public void TestFileUnitParsing_NotepadPlusPlus() {
            string npp62SourcePath = @"C:\Workspace\Source\Notepad++\6.2";
            string npp62DataPath = @"C:\Workspace\SrcMLData\NPP-6.2";
            
            Console.WriteLine("\nReal world test: Notepad++ 6.2 (C++)");
            Console.WriteLine("=======================================");
            TestDataGeneration(npp62SourcePath, npp62DataPath, new CPlusPlusCodeParser());
        }

        [Test]
        public void TestFileUnitParsing_Bullet() {
            string bullet281SourcePath = @"C:\Workspace\Source\bullet\2.81";
            string bullet281DataPath = @"C:\Workspace\SrcMLData\bullet-2.81";

            Console.WriteLine("\nReal World Test: Bullet 2.81 (C++)");
            Console.WriteLine("=======================================");
            TestDataGeneration(bullet281SourcePath, bullet281DataPath, new CPlusPlusCodeParser());
        }

        [Test]
        public void TestFileUnitParsing_Subversion() {
            string svn178SourcePath = @"C:\Workspace\Source\Subversion\1.7.8";
            string svn178DataPath = @"C:\Workspace\SrcMLData\subversion-1.7.8";

            Console.WriteLine("\nReal World Test: Subversion 1.7.8 (C)");
            Console.WriteLine("=======================================");
            TestDataGeneration(svn178SourcePath, svn178DataPath, new CPlusPlusCodeParser());
        }

        [Test]
        public void TestFileUnitParsing_Eclipse() {
            string eclipse422SourcePath = @"C:\Workspace\Source\eclipse\platform422";
            string eclipse422Datapath = @"C:\Workspace\SrcMLData\eclipse-4.2.2";

            Console.WriteLine("\nReal World Test: Eclipse Platform 4.2.2 (Java)");
            Console.WriteLine("=======================================");
            TestDataGeneration(eclipse422SourcePath, eclipse422Datapath, new JavaCodeParser());
        }

        [Test]
        public void TestFileUnitParsing_NDatabase() {
            string ndatabase45SourcePath = @"C:\Workspace\Source\NDatabase\master45";
            string ndatabase45DataPath = @"C:\Workspace\SrcMLData\ndatabase-4.5";

            Console.WriteLine("\nReal World Test: NDatabase 4.5 (C#)");
            Console.WriteLine("=======================================");
            TestDataGeneration(ndatabase45SourcePath, ndatabase45DataPath, new CSharpCodeParser());
        }
        private void TestDataGeneration(string sourcePath, string dataPath, AbstractCodeParser parser) {
            string fileLogPath = Path.Combine(dataPath, "parse.log");
            string callLogPath = Path.Combine(dataPath, "methodcalls.log");
            bool regenerateSrcML = shouldRegenerateSrcML;

            if(!Directory.Exists(sourcePath)) {
                Assert.Ignore("Source code for is missing");
            }
            if(File.Exists(callLogPath)) {
                File.Delete(callLogPath);
            }
            if(File.Exists(fileLogPath)) {
                File.Delete(fileLogPath);
            }
            if(!Directory.Exists(dataPath)) {
                regenerateSrcML = true;
            } else if(shouldRegenerateSrcML) {
                Directory.Delete(dataPath, true);
            }

            var archive = new SrcMLArchive(dataPath, regenerateSrcML);
            archive.XmlGenerator.ExtensionMapping[".cxx"] = Language.CPlusPlus;
            archive.XmlGenerator.ExtensionMapping[".c"] = Language.CPlusPlus;

            AbstractFileMonitor monitor = new FileSystemFolderMonitor(sourcePath, dataPath, new LastModifiedArchive(dataPath), archive);

            ManualResetEvent mre = new ManualResetEvent(false);
            Stopwatch sw = new Stopwatch();
            bool startupCompleted = false;

            monitor.StartupCompleted += (o, e) => {
                sw.Stop();
                startupCompleted = true;
                mre.Set();
            };

            sw.Start();
            monitor.Startup();
            startupCompleted = mre.WaitOne(60000);
            if(sw.IsRunning) {
                sw.Stop();
            }

            Console.WriteLine("{0} to {1} srcML", sw.Elapsed, (regenerateSrcML ? "generate" : "verify"));
            Assert.That(startupCompleted);

            Scope globalScope = null;
            sw.Reset();

            int numberOfFailures = 0;
            int numberOfSuccesses = 0;
            int numberOfFiles = 0;
            Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();
            
            using(var fileLog = new StreamWriter(fileLogPath)) {
                foreach(var unit in archive.FileUnits) {
                    if(++numberOfFiles % 100 == 0) {
                        Console.WriteLine("{0,5:N0} files completed in {1} with {2,5:N0} failures", numberOfFiles, sw.Elapsed, numberOfFailures);
                    }
                    
                    var fileName = parser.GetFileNameForUnit(unit);
                    var language = SrcMLElement.GetLanguageForUnit(unit);

                    fileLog.Write("Parsing {0}", fileName);
                    try {
                        sw.Start();
                        var scopeForUnit = CodeParser[language].ParseFileUnit(unit);
                        
                        if(null == globalScope) {
                            globalScope = scopeForUnit;
                        } else {
                            globalScope = globalScope.Merge(scopeForUnit);
                        }
                        sw.Stop();
                        fileLog.WriteLine(" PASSED");
                        numberOfSuccesses++;
                    } catch(Exception e) {
                        sw.Stop();
                        fileLog.WriteLine(" FAILED");
                        fileLog.WriteLine(e.StackTrace);
                        var key = e.StackTrace.Split('\n')[0].Trim();
                        if(!errors.ContainsKey(key)) {
                            errors[key] = new List<string>();
                        }
                        errors[key].Add(fileName);

                        numberOfFailures++;
                    }
                }
            }
            Console.WriteLine("{0,5:N0} files completed in {1} with {2,5:N0} failures", numberOfFiles, sw.Elapsed, numberOfFailures);

            Console.WriteLine("\nSummary");
            Console.WriteLine("===================");

            Console.WriteLine("{0,10:N0} failures  ({1,8:P2})", numberOfFailures, ((float)numberOfFailures) / numberOfFiles);
            Console.WriteLine("{0,10:N0} successes ({1,8:P2})", numberOfSuccesses, ((float)numberOfSuccesses) / numberOfFiles);
            Console.WriteLine("{0} to generate data", sw.Elapsed);
            Console.WriteLine("See parse log at {0}", fileLogPath);

            PrintScopeReport(globalScope);
            PrintMethodCallReport(globalScope, callLogPath);
            PrintErrorReport(errors);

            monitor.Dispose();
            Assert.AreEqual(numberOfFailures, (from e in errors.Values select e.Count).Sum());
            Assert.AreEqual(0, numberOfFailures);
        }

        private void PrintScopeReport(Scope globalScope) {
            Console.WriteLine("\nScope Report");
            Console.WriteLine("===============");

            Console.WriteLine("{0,10:N0} scopes", VariableScopeIterator.Visit(globalScope).Count());
            var namedScopes = from scope in VariableScopeIterator.Visit(globalScope)
                              where (scope as NamedScope) != null
                              select scope;
            Console.WriteLine("{0,10:N0} named scopes", namedScopes.Count());
            var namespaceScopes = from scope in namedScopes
                                  where (scope as NamespaceDefinition) != null
                                  select scope;
            var typeScopes = from scope in namedScopes
                             where (scope as TypeDefinition) != null
                             select scope;
            var methodScopes = from scope in namedScopes
                               where (scope as MethodDefinition) != null
                               select scope;
            Console.WriteLine("{0,10:N0} namespaces", namespaceScopes.Count());
            Console.WriteLine("{0,10:N0} types", typeScopes.Count());
            Console.WriteLine("{0,10:N0} methods", methodScopes.Count());
        }

        private void PrintMethodCallReport(Scope globalScope, string callLogPath) {
            Console.WriteLine("\nMethod Call Report");
            Console.WriteLine("===============");
            var methodCalls = from scope in VariableScopeIterator.Visit(globalScope)
                              from call in scope.MethodCalls
                              select call;

            int numMethodCalls = 0;
            int numMatchedMethodCalls = 0;
            Stopwatch sw = new Stopwatch();

            using(var callLog = new StreamWriter(callLogPath)) {
                foreach(var call in methodCalls) {
                    sw.Start();
                    var match = call.FindMatches().FirstOrDefault();
                    sw.Stop();
                    numMethodCalls++;
                    if(null != match) {
                        numMatchedMethodCalls++;
                        callLog.WriteLine("{0} ({1}:{2}) -> {3} ({4}:{5})", call.Name, call.Location.SourceFileName, call.Location.StartingLineNumber, match.Name, match.PrimaryLocation.SourceFileName, match.PrimaryLocation.StartingLineNumber);
                    }
                }
            }
            
            Console.WriteLine("{0,10:N0} method calls", numMethodCalls);
            Console.WriteLine("{0,10:N0} matched method calls ({1,8:P2})", numMatchedMethodCalls, ((float)numMatchedMethodCalls) / numMethodCalls);
            Console.WriteLine("{0,10:N0} matches / millisecond ({1,7:N0} ms elapsed)", ((float) numMethodCalls) / sw.ElapsedMilliseconds, sw.ElapsedMilliseconds);
            Console.WriteLine("See matched method calls in {0}", callLogPath);
        }

        private void PrintErrorReport(Dictionary<string, List<string>> errors) {
            Console.WriteLine("\nError Report");
            Console.WriteLine("===============");
            var sortedErrors = from kvp in errors
                               orderby kvp.Value.Count descending
                               select kvp;

            if(sortedErrors.Any()) {
                foreach(var kvp in sortedErrors) {
                    Console.WriteLine("{0} exceptions {1}", kvp.Value.Count, kvp.Key);
                    foreach(var fileName in kvp.Value) {
                        Console.WriteLine("\t{0}", fileName);
                    }
                }
            } else {
                Console.WriteLine("No parsing errors!");
            }
            
        }
    }
}