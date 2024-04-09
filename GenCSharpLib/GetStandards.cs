using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace GenCSharpLib
{
	public class GetStandards : ILog
	{
		private readonly ILog _Log;

		private readonly HttpClient _Client = new();

		public GetStandards()
		{
			_Log = this;
		}
		public async Task GetThroughWeb(string outPutPath)
		{
			if (!Directory.Exists(outPutPath)) 
			{
				Directory.CreateDirectory(outPutPath);
			}

			using HttpResponseMessage response = await _Client.GetAsync("https://www.w3.org/TR/?filter-tr-name=&status%5B%5D=draftStandard&status%5B%5D=candidateStandard&status%5B%5D=standard");
		
			response.EnsureSuccessStatusCode();

			string str = await response.Content.ReadAsStringAsync();

			Regex regex = new("<div class=\"tr-list__item__header\">\\s+.+(\\s+.+)<\\/a>");

			Regex regexHrefAndText = new("href=\"(.+)\">(.+)");

			MatchCollection matchCollection = regex.Matches(str);

			foreach (Match match in matchCollection) 
			{
				Match matchCollectionHT = regexHrefAndText.Match(match.Groups[1].Value);
				string file = matchCollectionHT.Groups[2].Value.Replace("\\", "").Replace("/", "").Replace("™", "").Replace(":", "").Replace(" ", "").Replace("(", "").Replace(")", "");

				if (file.Length > 50) 
				{
					file = file.Substring(0, 50);
				}

				file += ".raw.txt";

				HttpResponseMessage response2 = await _Client.GetAsync(matchCollectionHT.Groups[1].Value);

				string path = Path.Combine(outPutPath, file);
				_Log.WriteLine("Path: " + path);

				if (response2.IsSuccessStatusCode)
				{
					str = await response2.Content.ReadAsStringAsync();

					await File.WriteAllTextAsync(path, str);

					_Log.WriteLine("Success!");
				}
				else 
				{
					_Log.WriteLine($"---Fail: {response2.StatusCode}");
				}
				_Log.WriteLine("\n");

				//
				//There is no crawl-delay, so 1s per request.
				//https://www.w3.org/robots.txt
				Thread.Sleep(1000);
			}

			_Log.WriteLine("Done!");
		}
		public async Task GetWebIdl(string path) 
		{
			if (!Directory.Exists(path))
			{
				_Log.WriteLine("No directory: " + path);
				return;
			}
			string webidlPath = Path.Combine(path, "webidl");
			if (!Directory.Exists(webidlPath))
			{
				Directory.CreateDirectory(webidlPath);
			}

			DirectoryInfo folder = new(path);

			FileInfo[] fileInfos = folder.GetFiles("*raw.txt");

			//id="idl-index"([\s\S])+
			Regex regex = new("id=\"idl-index\"([\\s\\S]+)");
			//<pre class="\S+">([\s\S]+?)<\/pre>
			Regex regexWebidl = new("<pre class=\"[\\w\\s]+\">([\\s\\S]+?)<\\/pre>");

			Regex tags = new(@"<[^>]*>");

			foreach (FileInfo fileInfo in fileInfos) 
			{
				_Log.WriteLine(fileInfo.FullName);
				using (var stream = File.OpenRead(fileInfo.FullName))
				{
					using (StreamReader reader = new(stream))
					{
						string text = reader.ReadToEnd();
						Match match = regex.Match(text);
						if (match.Success)
						{
							Match matchW = regexWebidl.Match(match.Groups[1].Value);

							string webidl = matchW.Groups[1].Value;

							webidl = tags.Replace(webidl, "");

							if (webidl == "") 
							{
								_Log.WriteLine("---Webidl is empty");
								_Log.WriteLine("\n");
								continue;
							}

							string path2 = Path.Combine(webidlPath, fileInfo.Name + ".webidl");

							await File.WriteAllTextAsync(path2, webidl);

							_Log.WriteLine("Webidl generated: " + path2);
						}
						else 
						{
							_Log.WriteLine("---NO webidl index!");

							Regex regexPre = new(@"<pre class=""?idl""?>([\s\S]+?)</pre>");

							MatchCollection matches = regexPre.Matches(text);
							string webidl = string.Empty;
							if (matches.Count > 0) 
							{
								for (int i = 0; i < matches.Count; i++)
								{
									Group? group = matches[i].Groups[1];

									string localwebidl = group.Value;
									Regex comms = new(@"<!--[^>]*-->");
									localwebidl = comms.Replace(localwebidl, "");
									localwebidl = tags.Replace(localwebidl, "");
									localwebidl += "\n";
									webidl += localwebidl;
								}
								string path2 = Path.Combine(webidlPath, fileInfo.Name + ".webidl");
								await File.WriteAllTextAsync(path2, webidl);

								_Log.WriteLine("Webidl with no index! generated: " + path2);
								_Log.WriteLine("\n");
								continue;
							}

							regexPre = new(@"<pre><code class=('|"")?idl('|"")?>([\s\S]+?)</code></pre>");
							
							matches = regexPre.Matches(text);

							if (matches.Count > 0)
							{
								for (int i = 0; i < matches.Count; i++)
								{
									Group? group = matches[i].Groups[3];

									string localwebidl = group.Value;
									Regex comms = new(@"<!--[^>]*-->");
									localwebidl = comms.Replace(localwebidl, "");

									string[] lines = localwebidl.Split("\n", StringSplitOptions.RemoveEmptyEntries);
									localwebidl = string.Empty;
									for (int j = 0; j < lines.Length; j++)
									{
										lines[j] = lines[j].Trim();

										if (lines[j].StartsWith("//") ||
											lines[j].StartsWith("DND-"))
										{
											continue;
										}

										Regex tags1 = new(@"<[^>]*>");
										lines[j] = tags1.Replace(lines[j], "");
										
										if (lines[j].StartsWith('-'))
										{
											continue;
										}

										localwebidl += lines[j] + "\n";
									}
									localwebidl += "\n";
									webidl += localwebidl;
								}
								string path2 = Path.Combine(webidlPath, fileInfo.Name + ".webidl");
								await File.WriteAllTextAsync(path2, webidl);

								_Log.WriteLine("Webidl with no index! generated: " + path2);
							}
							

						}

					}
				}
				_Log.WriteLine("\n");
			}

			_Log.WriteLine("Done!");
		}
	}
}
