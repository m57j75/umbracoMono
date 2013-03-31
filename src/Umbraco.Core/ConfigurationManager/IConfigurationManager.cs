using System;
using System.Collections.Specialized;
using System.Web;

namespace Umbraco.Core.Configuration
{
	public interface IConfigurationManager
	{
		NameValueCollection AppSettings {get;}

		Object GetSection(string SectionName);
		void RefreshSection(string SectionName);

		void SetAppSetting(string key, string val);
		void ClearAppSetting(string key);
	}
}
