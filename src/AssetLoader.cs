using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DominusCore {
	public class AssetLoader { }

	public class IniReader {
		private static Regex _categoryRegex = new Regex(@"(.+[\[\]])", RegexOptions.Compiled);
		private static Regex _keyRegex = new Regex(@"(.+)(?=\=)", RegexOptions.Compiled);

		public readonly Dictionary<string, string> Values;
		public readonly string[] Categories;
		public readonly string FileName;

		private IniReader(Dictionary<string, string> keyValues, string[] categories, string filename) {
			this.Values = keyValues;
			this.Categories = categories;
			this.FileName = filename;
		}

		public static IniReader ReadIni(string filename) {
			string[] ini = new StreamReader(filename).ReadToEnd().Split(new string[] {
				"\r", "\n", "\r\n"
			}, StringSplitOptions.RemoveEmptyEntries);

			Dictionary<string, string> keyValues = new Dictionary<string, string>();
			List<string> categories = new List<string>();

			for (int i = 0; i < ini.Length; i++) {
				// Preprocess all lines to remove comments
				string current = ini[i].Split(";")[0].Trim();

				// Identify if it is a category
				if (_categoryRegex.Match(current).Success) {
					categories.Add($"{current.Substring(1, current.Length - 2)}");
					continue;
				}

				// Identify if this is a key-value
				if (_keyRegex.Match(current).Success) {
					string[] keyValue = current.Split("=");
					keyValues.Add($"{categories[categories.Count - 1]}.{keyValue[0].Trim()}", keyValue[1].Trim());
				}
			}
			return new IniReader(keyValues, categories.ToArray(), filename);
		}
	}
}