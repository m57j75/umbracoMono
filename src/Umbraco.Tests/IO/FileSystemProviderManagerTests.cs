﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rhino.Mocks;
using Umbraco.Core.IO;
using Umbraco.Core.Configuration;

namespace Umbraco.Tests.IO
{

    [TestFixture]
    public class FileSystemProviderManagerTests
    {
        
		protected IConfigurationManager configManagerTest = null;
		

		[TestFixtureSetUp]
		public void SetUp()
		{
			var testConfig = ConfigurationManager.OpenExeConfiguration(@"Umbraco.Tests.dll");

			var mockedConfigManager = 
				MockRepository
				.GenerateStub<IConfigurationManager>();

			var fileSystemProvider = new FileSystemProvidersSection();

			mockedConfigManager.Expect(cm => 
				                           cm
				                           .GetSection("FileSystemProviders")
			                           )
				.Return((FileSystemProvidersSection)testConfig.GetSection("FileSystemProviders"));


			ConfigurationManagerService
				.Instance
					.SetManager(mockedConfigManager);

			configManagerTest = 
				ConfigurationManagerService
					.Instance
					.GetConfigManager();

		}

		[Test]
        public void Can_Get_Base_File_System()
        {
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider(FileSystemProvider.Media);

            Assert.NotNull(fs);
        }

        [Test]
        public void Can_Get_Typed_File_System()
        {
            var fs = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();

            Assert.NotNull(fs);
        }

		[Test]
        public void Exception_Thrown_On_Invalid_Typed_File_System()
		{
			Assert.Throws<InvalidOperationException>(() => FileSystemProviderManager.Current.GetFileSystemProvider<InvalidTypedFileSystem>());
		}

		#region

	    /// <summary>
	    /// Used in unit tests, for a typed file system we need to inherit from FileSystemWrapper and they MUST have a ctor
	    /// that only accepts a base IFileSystem object
	    /// </summary>
	    internal class InvalidTypedFileSystem : FileSystemWrapper
	    {
		    public InvalidTypedFileSystem(IFileSystem wrapped, string invalidParam) : base(wrapped)
		    {
		    }
	    }

	    #endregion
	}
}
