/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

using System;
using System.IO;
using System.Text.RegularExpressions;

public class LoaderTools {
	public static string NormalizeSeparator(string path) {
		string output = path;
		if (Path.DirectorySeparatorChar == '/')
			output = ForwardSlash(path);
		else if (Path.DirectorySeparatorChar == '\\')
			output = BackwardSlash(path);
		return output;
	}
	
	public static string ForwardSlash(string path) {
		return Regex.Replace(path, "(\\\\)+", "/");
	}
	
	public static string BackwardSlash(string path) {
		return Regex.Replace(path, "(/)+", "\\");
	}
	
	static string MakeRelativeUri(string path1, string path2) {
		Uri uri1 = new Uri(path1);
		Uri uri2 = new Uri(path2);
		Uri diff = uri1.MakeRelativeUri(uri2);
		return NormalizeSeparator(diff.OriginalString);
	}
}