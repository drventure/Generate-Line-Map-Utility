﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace GenerateLineMap
{
	/// <summary>
	/// Utility Class to generate a LineMap based on the
	/// contents of the PDB for a specific EXE or DLL
	/// (c) 2008-2008 Darin Higgins All Rights Reserved
	/// </summary>
	/// <remarks>
	/// If some of the functions seem a little odd here, that's mostly because
	/// I was experimenting with "Fluent" style apis during most of the 
	/// development of this project.
	/// </remarks>
	public class LineMapBuilder
	{

		#region " Structures"
		/// <summary>
		/// Structure used when Enumerating symbols from a PDB file
		/// Note that this structure must be marshalled very specifically
		/// to be compatible with the SYM* dbghelp dll calls
		/// </summary>
		/// <remarks></remarks>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct SYMBOL_INFO
		{

			public int SizeOfStruct;

			public int TypeIndex;

			public Int64 Reserved0;

			public Int64 Reserved1;

			public int Index;

			public int size;

			public Int64 ModBase;

			public CV_SymbolInfoFlags Flags;

			public Int64 Value;

			public Int64 Address;

			public int Register;

			public int Scope;

			public int Tag;

			public int NameLen;

			public int MaxNameLen;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 300)]
			public string Name;
		}

		private const int MAX_PATH = 260;


		/// <summary>
		/// Structure used when Enumerating line numbers from a PDB file
		/// Note that this structure must be marshalled very specifically
		/// to be compatible with the SYM* dbghelp dll calls
		/// </summary>
		/// <remarks></remarks>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		private struct SRCCODEINFO
		{
			public int SizeOfStruct;

			public int Key;

			public Int64 ModBase;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 1)]
			public string obj;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH + 1)]
			public string FileName;

			public int LineNumber;

			public Int64 Address;
		}


		private enum CV_SymbolInfoFlags
		{
			IMAGEHLP_SYMBOL_INFO_VALUEPRESENT = 0x1,

			IMAGEHLP_SYMBOL_INFO_REGISTER = 0x8,
			IMAGEHLP_SYMBOL_INFO_REGRELATIVE = 0x10,

			IMAGEHLP_SYMBOL_INFO_FRAMERELATIVE = 0x20,
			IMAGEHLP_SYMBOL_INFO_PARAMETER = 0x40,

			IMAGEHLP_SYMBOL_INFO_LOCAL = 0x80,
			IMAGEHLP_SYMBOL_INFO_CONSTANT = 0x100,

			IMAGEHLP_SYMBOL_FUNCTION = 0x800,
			IMAGEHLP_SYMBOL_VIRTUAL = 0x1000,

			IMAGEHLP_SYMBOL_THUNK = 0x2000,
			IMAGEHLP_SYMBOL_TLSREL = 0x4000,

			IMAGEHLP_SYMBOL_SLOT = 0x8000,
			IMAGEHLP_SYMBOL_ILREL = 0x10000,

			IMAGEHLP_SYMBOL_METADATA = 0x20000,
			IMAGEHLP_SYMBOL_CLR_TOKEN = 0x40000,
		}
		#endregion


		#region " API Definitions"
		[DllImport("dbghelp.dll")]
		private static extern int SymInitialize(
		   int hProcess,
		   string UserSearchPath,
		   bool fInvadeProcess);



		[DllImport("dbghelp.dll")]
		private static extern Int64 SymLoadModuleEx(
		   int hProcess, 
		   int hFile, 
		   string ImageName, 
		   int ModuleName, 
		   Int64 BaseOfDll, 
		   int SizeOfDll, 
		   int pModData, 
		   int flags);

		// I believe this is deprecatedin dbghelp 6.0 or later
		//Private Declare Function SymLoadModule Lib "dbghelp.dll" ( _
		//   ByVal hProcess As Integer, _
		//   ByVal hFile As Integer, _
		//   ByVal ImageName As String, _
		//   ByVal ModuleName As Integer, _
		//   ByVal BaseOfDll As Integer, _
		//   ByVal SizeOfDll As Integer) As Integer

		[DllImport("dbghelp.dll")]
		private static extern int SymCleanup(int hProcess);



		[DllImport("dbghelp.dll")]
		private static extern int UnDecorateSymbolName(
		   string DecoratedName ,
		   string UnDecoratedName ,
		   int UndecoratedLength ,
		   int Flags );



		[DllImport("dbghelp.dll")]
		private static extern int SymEnumSymbols(
		   int hProcess,
		   Int64 BaseOfDll,
		   int Mask,
		   SymEnumSymbolsCallback lpCallback,
		   int UserContext);



		[DllImport("dbghelp.dll")]
		private static extern int SymEnumLines(
		   int hProcess,
		   Int64 BaseOfDll,
		   int obj,
		   int File,
		   SymEnumLinesCallback lpCallback,
		   int UserContext);



		[DllImport("dbghelp.dll")]
		private static extern int SymEnumSourceLines(
		   int hProcess,
		   Int64 BaseOfDll,
		   int obj,
		   int File,
		   int Line,
		   int Flags,
		   SymEnumLinesCallback lpCallback,
		   int UserContext);


		// I believe this is deprecated in dbghelp 6.0+
		// Private Declare Function SymUnloadModule Lib "dbghelp.dll" ( _
		// ByVal hProcess As Integer, _
		// ByVal BaseOfDll As Integer) As Integer
																							

		[DllImport("dbghelp.dll")]
		private static extern bool SymUnloadModule64(int hProcess, Int64 BaseOfDll);

		#endregion


		#region " Exceptions"
		public class UnableToEnumerateSymbolsException : ApplicationException
		{ }


		public class UnableToEnumLinesException : ApplicationException
		{ }

		#endregion


		/// <summary>
		/// Constructor to setup this class to read a specific PDB
		/// </summary>
		/// <param name="FileName"></param>
		/// <remarks></remarks>
		public LineMapBuilder(string FileName)
		{
			this.Filename = FileName;
		}


		public LineMapBuilder(string FileName, string OutFileName)
		{
			this.Filename = FileName;
			this.OutFilename = OutFileName;
		}


		/// <summary>
		/// Name of EXE/DLL file to process
		/// </summary>
		/// <value></value>
		/// <remarks></remarks>

		public string Filename
		{
			get
			{
				return rFilename;
			}

			set
			{
				if (!System.IO.File.Exists(value))
				{
					throw new FileNotFoundException("The file could not be found.", value);
				}
				rFilename = value;
			}
		}
		private string rFilename = "";


		/// <summary>
		/// Name of the output file
		/// </summary>
		/// <value></value>
		/// <remarks></remarks>
		public string OutFilename
		{
			get
			{
				if (string.IsNullOrEmpty(rOutFilename))
					return rFilename;
				else
					return rOutFilename;
			}
			set
			{
				rOutFilename = value;
			}
		}
		private string rOutFilename;


		private bool rbCreateMapReport = false;
		/// <summary>
		/// true to generate a Line map report
		/// this is mainly for Debugging purposes
		/// </summary>
		/// <value></value>
		/// <remarks></remarks>
		public bool CreateMapReport
		{
			get
			{
				return rbCreateMapReport;
			}
			set
			{
				rbCreateMapReport = value;
			}
		}



		/// <summary>
		/// Create a linemap file from the PDB for the given executable file
		/// Theoretically, you could read the LineMap info from the linemap file
		/// OR from a resource in the EXE/DLL, but I doubt you'd ever really want
		/// to. This is mainly for testing all the streaming functions
		/// </summary>
		/// <remarks></remarks>
		public void CreateLineMapFile()
		{
			// compress and encrypt the stream
			// technically, I should probably CLOSE and DISPOSE the streams
			// but this is just a quick and dirty tool

			var alm = pGetAssemblyLineMap(this.Filename);

			var CompressedStream = pCompressStream(alm.ToStream());
			var EncryptedStream = pEncryptStream(CompressedStream);


			// swap out the below two lines to generate a linemap file that is not compressed or encrypted
			pStreamToFile(this.OutFilename + ".linemap", EncryptedStream);
			//pStreamToFile(Me.Filename & ".linemap", alm.ToStream);

			// write the report
			pCheckToWriteReport(alm);
		}


		/// <summary>
		/// Inject a linemap resource into the given EXE/DLL file
		/// from the PDB for that file
		/// </summary>
		/// <remarks></remarks>
		public void CreateLineMapAPIResource()
		{
			// retrieve all the symbols
			var alm = pGetAssemblyLineMap(this.Filename);

			// done one step at a time for debugging purposes
			var CompressedStream = pCompressStream(alm.ToStream());

			var EncryptedStream = pEncryptStream(CompressedStream);

			pStreamToAPIResource(this.OutFilename,
							  LineMap.LineMapKeys.ResTypeName,
							  LineMap.LineMapKeys.ResName,
							  LineMap.LineMapKeys.ResLang,
							  EncryptedStream);

			// write the report
			pCheckToWriteReport(alm);
		}


		/// <summary>
		/// Inject a linemap .net resource into the given EXE/DLL file
		/// from the PDB for that file
		/// </summary>
		/// <remarks></remarks>
		public void CreateLineMapResource()
		{
			// retrieve all the symbols
			var alm = pGetAssemblyLineMap(this.Filename);

			// done one step at a time for debugging purposes

			var CompressedStream = pCompressStream(alm.ToStream());

			var EncryptedStream = pEncryptStream(CompressedStream);

			pStreamToResource(this.OutFilename,
							  LineMap.LineMapKeys.ResName,
							  EncryptedStream);

			// write the report
			pCheckToWriteReport(alm);
		}


		/// <summary>
		/// Internal function to write out a line map report if asked to
		/// </summary>
		/// <remarks></remarks>
		private void pCheckToWriteReport(LineMap.AssemblyLineMap AssemblyLineMap)
		{

			if (this.CreateMapReport)
			{
				Console.WriteLine("Creating symbol buffer report");

				using (var tw = new StreamWriter(this.Filename + ".linemapreport", false))
				{
					tw.Write(pCreatePDBReport(AssemblyLineMap).ToString());
					tw.Flush();
				}
			}
		}


		/// <summary>
		/// Create a linemap report buffer from the PDB for the given executable file
		/// </summary>
		/// <remarks></remarks>
		private StringBuilder pCreatePDBReport(LineMap.AssemblyLineMap AssemblyLineMap)
		{

			var sb = new StringBuilder();

			sb.AppendLine("========");
			sb.AppendLine("SYMBOLS:");
			sb.AppendLine("========");
			sb.AppendLine(string.Format("   {0,-10}  {1,-10}  {2}  ", "Token", "Address", "Symbol"));
			foreach (var symbolEx in AssemblyLineMap.Symbols.Values)
			{
				sb.AppendLine(string.Format("   {0,-10:X}  {1,-10}  {2}", symbolEx.Token, symbolEx.Address, symbolEx.Name));
			}
			sb.AppendLine("========");
			sb.AppendLine("LINE NUMBERS:");
			sb.AppendLine("========");
			sb.AppendLine(string.Format("   {0,-10}  {1,-11}  {2,-10}  {3}", "Address", "Line number", "Token", "Symbol/FileName"));

			foreach (var lineex in AssemblyLineMap.AddressToLineMap)
			{
				// find the symbol for this line number
				LineMap.AssemblyLineMap.SymbolInfo sym = null;
				foreach (var symbolex in AssemblyLineMap.Symbols.Values)
				{
					if (symbolex.Address == lineex.Address)
					{
						// found the symbol for this line
						sym = symbolex;
						break;
					}
				}
				var n = (sym != null ? lineex.ObjectName + "." + sym.Name : "") + " / " + lineex.SourceFile;
				var t = sym != null ? sym.Token : 0;

				sb.AppendLine(string.Format("   {0,-10}  {1,-11}  {2,-10:X}  {3}", lineex.Address, lineex.Line, t, n));
			}

			sb.AppendLine("========");
			sb.AppendLine("NAMES:");
			sb.AppendLine("========");
			sb.AppendLine(string.Format("   {0,-10}  {1}", "Index", "Name"));
			for (int i = 0; i < AssemblyLineMap.Names.Count; i++)
			{
				sb.AppendLine(string.Format("   {0,-10}  {1}", i, AssemblyLineMap.Names[i]));
			}
			return sb;
		}


		/// <summary>
		/// Retrieve symbols and linenums and write them to a memory stream
		/// </summary>
		/// <param name="FileName"></param>
		/// <returns></returns>
		private LineMap.AssemblyLineMap pGetAssemblyLineMap(string FileName)
		{
			// create a new map to capture symbols and line info with
			_alm = LineMap.AssemblyLineMaps.Add(FileName);

			if (!System.IO.File.Exists(FileName))
			{
				throw new FileNotFoundException("The file could not be found.", FileName);
			}

			var hProcess = System.Diagnostics.Process.GetCurrentProcess().Id;
			Int64 dwModuleBase = 0;

			// clear the map
			_alm.Clear();

			try
			{

				if (SymInitialize(hProcess, "", false) != 0)
				{
					dwModuleBase = SymLoadModuleEx(hProcess, 0, FileName, 0, 0, 0, 0, 0);
			
					if (dwModuleBase != 0)
					{
						// Enumerate all the symbol names 

						var rEnumSymbolsDelegate = new SymEnumSymbolsCallback(SymEnumSymbolsProc);
						if (SymEnumSymbols(hProcess, dwModuleBase, 0, rEnumSymbolsDelegate, 0) == 0)
						{
							// unable to retrieve the symbol list
							throw new UnableToEnumerateSymbolsException();
						}

						// now enum all the source lines and their respective addresses
						var rEnumLinesDelegate = new SymEnumLinesCallback(SymEnumLinesProc);

						if (SymEnumSourceLines(hProcess, dwModuleBase, 0, 0, 0, 0, rEnumLinesDelegate, 0) == 0)
						{
							// unable to retrieve the line number list
							throw new UnableToEnumLinesException();
						}
					}
				}
			}
			catch (Exception ex)
			{
				// return return vars
				Console.WriteLine(string.Format("Unable to retrieve symbols. Error: {0}", ex.ToString()));
				_alm.Clear();

				// and rethrow
				throw;
			}
			finally
			{
				Console.WriteLine("Retrieved {0} symbols", _alm.Symbols.Count);
	
				Console.WriteLine("Retrieved {0} lines", _alm.AddressToLineMap.Count) ;
	
				Console.WriteLine("Retrieved {0} strings", _alm.Names.Count);
	
				// release the module
				if (dwModuleBase != 0) SymUnloadModule64(hProcess, dwModuleBase);

				// can clean up the dbghelp system
				SymCleanup(hProcess);
			}
			return _alm;
		}
		private LineMap.AssemblyLineMap _alm;


		/// <summary>
		/// Given a Filename and memorystream, write the stream to the file
		/// </summary>
		/// <param name="Filename"></param>
		/// <param name="MemoryStream"></param>
		/// <remarks></remarks>
		private void pStreamToFile(String Filename, MemoryStream MemoryStream)
		{
			Console.WriteLine("Writing symbol buffer to file");

			using (var fileStream = new System.IO.FileStream(Filename, FileMode.Create))
			{ 
				MemoryStream.WriteTo(fileStream);
				fileStream.Flush();
			}
		}


		/// <summary>
		/// Write out a memory stream to a named resource in the given 
		/// WIN32 format executable file (iether EXE or DLL)
		/// </summary>
		/// <param name="Filename"></param>
		/// <param name="ResourceName"></param>
		/// <param name="MemoryStream"></param>
		/// <remarks></remarks>
		private void pStreamToAPIResource(string filename, object ResourceType, object ResourceName, Int16 ResourceLanguage, MemoryStream memoryStream)
		{
			var ResWriter = new ResourceWriter();

			// the target file has to exist
			if (this.Filename != filename)
			{
				System.IO.File.Copy(this.Filename, filename);
			}

			// convert memorystream to byte array and write out to 
			// the linemap resource
			Console.WriteLine("Writing symbol buffer to resource");

			ResWriter.FileName = filename;
			var buf = memoryStream.ToArray();
			ResWriter.Update(ResourceType, ResourceName, ResourceLanguage, ref buf);
		}


		/// <summary>
		/// Write out a memory stream to a named resource in the given 
		/// WIN32 format executable file (iether EXE or DLL)
		/// </summary>
		/// <param name="OutFilename"></param>
		/// <param name="ResourceName"></param>
		/// <param name="MemoryStream"></param>
		/// <remarks></remarks>
		private void pStreamToResource(string OutFilename, string ResourceName, MemoryStream memoryStream)
		{
			var ResWriter = new ResourceWriterCecil();

			// the target file has to 
			if (this.Filename != OutFilename)
			{
				System.IO.File.Copy(this.Filename, OutFilename);
			}

			// convert memorystream to byte array and write out to 
			// the linemap resource

			Console.WriteLine("Writing symbol buffer to resource");

			ResWriter.FileName = this.Filename;
			ResWriter.Add(ResourceName, memoryStream.ToArray());

			ResWriter.Save(OutFilename);
		}


		/// <summary>
		/// Given an input stream, compress it into an output stream
		/// </summary>
		/// <param name="UncompressedStream"></param>
		/// <returns></returns>
		private MemoryStream pCompressStream(MemoryStream UncompressedStream)
		{
			var CompressedStream = new System.IO.MemoryStream();

			// note, the LeaveOpen parm MUST BE true into order to read the memory stream afterwards!
			Console.WriteLine("Compressing symbol buffer");

			using (var GZip = new System.IO.Compression.GZipStream(CompressedStream, System.IO.Compression.CompressionMode.Compress, true))
			{
				GZip.Write(UncompressedStream.ToArray(), 0, (int)UncompressedStream.Length);
			}
			return CompressedStream;
		}


		/// <summary>
		/// Given a stream, encrypt it into an output stream
		/// </summary>
		/// <param name="CompressedStream"></param>
		/// <returns></returns>
		private MemoryStream pEncryptStream(MemoryStream CompressedStream)
		{
			var Enc = new System.Security.Cryptography.RijndaelManaged();

			Console.WriteLine("Encrypting symbol buffer");
			// setup our encryption key
			Enc.KeySize = 256;
			// KEY is 32 byte array
			Enc.Key = LineMap.LineMapKeys.ENCKEY;
			// IV is 16 byte array
			Enc.IV = LineMap.LineMapKeys.ENCIV;

			var EncryptedStream = new System.IO.MemoryStream();

			var cryptoStream = new System.Security.Cryptography.CryptoStream(EncryptedStream, Enc.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write);
			cryptoStream.Write(CompressedStream.ToArray(), 0, (int)CompressedStream.Length);
			cryptoStream.FlushFinalBlock();

			return EncryptedStream;
		}


		#region " Symbol Enumeration Delegates"
		/// <summary>
		/// Delegate to handle Symbol Enumeration Callback
		/// </summary>
		/// <param name="syminfo"></param>
		/// <param name="Size"></param>
		/// <param name="UserContext"></param>
		/// <returns></returns>
		/// <remarks></remarks>
		private delegate int SymEnumSymbolsCallback(ref SYMBOL_INFO syminfo, int Size, int UserContext);

		private int SymEnumSymbolsProc(ref SYMBOL_INFO syminfo, int Size, int UserContext)
		{
			if (syminfo.Flags == (CV_SymbolInfoFlags.IMAGEHLP_SYMBOL_CLR_TOKEN | CV_SymbolInfoFlags.IMAGEHLP_SYMBOL_METADATA))
			{
				// we only really care about CLR metadata tokens
				// anything else is basically a variable or internal
				// info we wouldn't be worried about anyway.
				// This might change and I get to know more about debugging
				//.net!

				var Tokn = syminfo.Value;
				var si = new LineMap.AssemblyLineMap.SymbolInfo(syminfo.Name.Substring(0, syminfo.NameLen), syminfo.Address, Tokn);

				_alm.Symbols.Add(Tokn, si);
			}

			// return this to the call to let it keep enumerating
			return -1;
	}


		/// <summary>
		/// Handle the callback to enumerate all the Line numbers in the given DLL/EXE
		/// based on info from the PDB
		/// </summary>
		/// <param name="srcinfo"></param>
		/// <param name="UserContext"></param>
		/// <returns></returns>
		/// <remarks></remarks>
		private int SymEnumLinesProc(ref SRCCODEINFO srcinfo, int UserContext)
		{
			if (srcinfo.LineNumber == 0xFEEFEE)
			{
				// skip these entries
				// I believe they mark the end of a set of linenumbers associated
				// with a particular method, but they don't appear to contain
				// valid line number info in any case.
			}
			else
			{
				try
				{
					// add the new line number and it's address
					// NOTE, this address is an IL Offset, not a native code offset

					var FileName = srcinfo.FileName.Split('\\');

					string Name;

					var i = FileName.GetUpperBound(0);

					if (i > 2)
					{
						Name = "...\\" + FileName[i - 2] + "\\" + FileName[i - 1] + "\\" + FileName[i];
					}
					else if (i > 1)
					{
						Name = "...\\" + FileName[i - 1] + "\\" + FileName[i];
					}
					else
					{
						Name = Path.GetFileName(srcinfo.FileName);
					}

					_alm.AddAddressToLine(srcinfo.LineNumber, srcinfo.Address, Name, srcinfo.obj);
				}
				catch (Exception ex)
				{
					Console.WriteLine("ERROR: {0}", ex.ToString());
					// catch everything because we DO NOT
					// want to throw an exception from here!
				}
			}

			// Tell the caller we succeeded
			return -1;
		}

		private delegate int SymEnumLinesCallback(ref SRCCODEINFO srcinfo, int UserContext);

#endregion
	}
}
