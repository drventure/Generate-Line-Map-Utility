﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace GenerateLineMapUnitTests
{
	[TestClass]
	public class GenerateLineMapUnitTests
	{
		[TestInitialize]
		public void TestInitialize()
		{
			// Set current folder to testing folder
			string assemblyCodeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;

			// Get directory name
			string dirName = Path.GetDirectoryName(assemblyCodeBase);

			// remove URL-prefix if it exists
			if (dirName.StartsWith("file:\\"))
				dirName = dirName.Substring(6);

			// set current folder
			Environment.CurrentDirectory = dirName;
		}


		[TestMethod]
		public void TestHelpScreen()
		{
			using (var consoleOutput = new ConsoleOutput())
			{
				// Check exit is normal
				StartConsoleApplication("GenerateLineMap.exe").Should().Be(0);

				// Check that help information shown correctly.
				consoleOutput.Ouput.Should().Contain("GenerateLineMap");
			}
		}


		[TestMethod]
		public void TestGenerateReportViaCommandLine()
		{
			using (var consoleOutput = new ConsoleOutput())
			{
				// Check exit is normal
				StartConsoleApplication("GenerateLineMap.exe", "/report TestApp1.exe").Should().Be(0);

				// Check that help information shown correctly.
				consoleOutput.Ouput.Should().Contain("Retrieved 12 strings");
			}
		}


		[TestMethod]
		public void TestGenerateReport()
		{
			// invoke the app main directly
			GenerateLineMap.Program.Main(new string[] { "path", "/report", "TestApp1.exe" });

			var filename = "TestApp1.exe.linemapreport";
			File.Exists(filename).Should().BeTrue();

			var buf = File.ReadAllText("TestApp1.exe.linemapreport");
			buf.Should().Contain("SYMBOLS:");
			buf.Should().Contain("LINE NUMBERS:");
			buf.Should().Contain("NAMES:");
			buf.Should().Contain("TestApp1.Program");
		}


		[TestMethod]
		public void TestForProperLineNumInTestAppViaFileMap()
		{
			using (var consoleOutput = new ConsoleOutput())
			{
				GenerateLineMap.Program.Main(new string[] { "path", "/out:TestApp1.exe", "/file", "TestApp1.exe" });

				//call the console app main routine
				TestApp1.Program.Main(new string[] { });

				// Check that help information shown correctly.
				consoleOutput.Ouput.Should().Contain("Program.cs: line 24");
			}
		}


		[TestMethod]
		public void TestForProperLineNumInTestApp()
		{
			using (var consoleOutput = new ConsoleOutput())
			{
				GenerateLineMap.Program.Main(new string[] { "path", "/out:TestApp1-1.exe", "TestApp1.exe" });

				//make sure PDB doesn't exist anymore
				while (true)
				{
					try
					{
						if (File.Exists("TestApp1.pdb"))
						{
							File.SetAttributes("TestApp1.pdb", FileAttributes.Normal);
							File.Delete("TestApp1.pdb");
						}
						break;
					}
					catch
					{ }
				}

				//execute test app, should file and write stack trace to console
				StartConsoleApplication("TestApp1-1.exe").Should().Be(0);

				// Check that help information shown correctly.
				consoleOutput.Ouput.Should().Contain("Program.cs: line 24");
			}
		}


		/// <span class="code-SummaryComment"><summary></span>
		/// Starts the console application.
		/// <span class="code-SummaryComment"></summary></span>
		/// <span class="code-SummaryComment"><param name="arguments">The arguments for console application. </span>
		/// Specify empty string to run with no arguments</param />
		/// <span class="code-SummaryComment"><returns>exit code</returns></span>
		private int StartConsoleApplication(string app, string arguments = "")
		{
			// Initialize process here
			Process proc = new Process();
			proc.StartInfo.FileName = app;
			// add arguments as whole string
			proc.StartInfo.Arguments = arguments;

			// use it to start from testing environment
			proc.StartInfo.UseShellExecute = false;

			// redirect outputs to have it in testing console
			proc.StartInfo.RedirectStandardOutput = true;
			proc.StartInfo.RedirectStandardError = true;

			// set working directory
			proc.StartInfo.WorkingDirectory = Environment.CurrentDirectory;

			// start and wait for exit
			proc.Start();
			proc.WaitForExit();

			// get output to testing console.
			System.Console.WriteLine(proc.StandardOutput.ReadToEnd());
			System.Console.Write(proc.StandardError.ReadToEnd());

			// return exit code
			return proc.ExitCode;
		}
	}
}
