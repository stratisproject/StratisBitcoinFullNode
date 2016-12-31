using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests
{
	public class TestDirectory : IDisposable
	{
		public static TestDirectory Create([CallerMemberNameAttribute]string name = null, bool clean = true)
		{
			var directory = new TestDirectory(Path.Combine("TestData", name), clean);
			directory.EnsureExists();
			return directory;
		}

		private void EnsureExists()
		{
			if(!Directory.Exists("TestData"))
				Directory.CreateDirectory("TestData");
			if(!Directory.Exists(FolderName))
				Directory.CreateDirectory(FolderName);
		}

		public TestDirectory(string name, bool clean)
		{
			FolderName = name;
			Clean = clean;
			if(Clean)
				CleanDirectory();
		}

		public string FolderName
		{
			get; set;
		}

		public bool Clean
		{
			get;
			private set;
		}
		private void CleanDirectory()
		{
			try
			{
				Directory.Delete(FolderName, true);
			}
			catch(DirectoryNotFoundException)
			{
			}
		}

		public void Dispose()
		{
			if(Clean)
				CleanDirectory();
		}		
	}
}
