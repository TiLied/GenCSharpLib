﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace GenCSharpLib
{
	public class GenCSharp
	{
		private Welcome _Main = new();
		private int _UnionId = 0;
		private TType _CurrentTType = new();
		private List<string> _ListNamesForToAttr = new();

		private bool _DefaultTrue = false; 

		//Do I need this?
		private List<string> _ECMATypes = new() { "Date", "Math", "Object", "RegExp" };

		public GenCSharp()
		{
			if (File.Exists(Directory.GetCurrentDirectory() + "/debugGenCSharp.txt"))
				File.Delete(Directory.GetCurrentDirectory() + "/debugGenCSharp.txt");

			Trace.Listeners.Add(new TextWriterTraceListener("debugGenCSharp.txt"));
			Trace.AutoFlush = true;
			Trace.Listeners.Add(new ConsoleTraceListener());
		}

		public async void GenerateCSFromJson(string path, string output)
		{
			string jsonString = File.ReadAllText(path);
			
			JsonSerializerOptions serializeOptions = new();

			Welcome? welcome = JsonSerializer.Deserialize<Welcome>(jsonString, serializeOptions);

			_Main = welcome;

			StringBuilder sb = new();
			sb.AppendLine($"//{DateTime.Now}");
			sb.AppendLine("using CSharpToJavaScript.Utils;");
			sb.AppendLine("using System.Collections.Generic;");
			sb.AppendLine("using System.Threading.Tasks;");
			sb.AppendLine();
			sb.AppendLine($"namespace CSharpToJavaScript.APIs.JS;");
			sb.AppendLine();

			int length = _Main.TType.Count;

			for (int i = 0; i < length; i++)
			{
				ResolveUnion(_Main.TType[i]);

				if (_Main.TType[i].Type == "includes")
				{
					TType target = _Main.TType.Find((e) => e.Name == _Main.TType[i].Target);
					TType includes = _Main.TType.Find((e) => e.Name == _Main.TType[i].Includes);
					if (target != null && includes != null)
						target.ListAdditionalInheritance.Add(includes);
				}

				ResolveTypeDef(ref sb, _Main.TType[i]);
			}

			sb.AppendLine();
			
			foreach (TType item in _Main.TType)
			{
				ProcessTType(ref sb, item);
				sb.AppendLine();
			}

			await File.WriteAllTextAsync(Path.Combine(output, "JS.generated.cs"), sb.ToString());
		}
		
		private void ResolveUnion(TType main)
		{
			if (main.IDLType != null &&
					main.IDLType.First().Union == true)
			{
				List<WebIDLType> _cloneList = (List<WebIDLType>)main.IDLType.CloneObject();

				WebIDLType _mainIDLType = main.IDLType.First();

				_mainIDLType.Union = false;
				_mainIDLType.IDLType = null;
				_mainIDLType.IDLTypeStr = CreateStructUnion(_cloneList);
			}

			if (main.Arguments != null &&
					main.Arguments.Count > 0)
			{
				for (int _i = 0; _i < main.Arguments.Count; _i++)
				{
					Argument _argument = main.Arguments[_i];
					if (_argument.IDLType.First().Union == true)
					{
						List<WebIDLType> _cloneList = (List<WebIDLType>)_argument.IDLType.CloneObject();

						WebIDLType _argumentIDLType = _argument.IDLType.First();

						_argumentIDLType.Union = false;
						_argumentIDLType.IDLType = null;
						_argumentIDLType.IDLTypeStr = CreateStructUnion(_cloneList);
					}
				}
			}

			if (main.Members == null)
				return;

			for (int i = 0; i < main.Members.Count; i++)
			{
				Member _member = main.Members[i];

				if (_member.IDLType != null &&
					_member.IDLType.First().Union == true)
				{
					List<WebIDLType> _cloneList = (List<WebIDLType>)_member.IDLType.CloneObject();
					
					WebIDLType _mainIDLType = _member.IDLType.First();

					_mainIDLType.Union = false;
					_mainIDLType.IDLType = null;
					_mainIDLType.IDLTypeStr = CreateStructUnion(_cloneList);
				}

				if (_member.Arguments != null && 
					_member.Arguments.Count > 0) 
				{
					for (int _i = 0; _i < _member.Arguments.Count; _i++)
					{
						Argument _argument = _member.Arguments[_i];
						if (_argument.IDLType.First().Union == true)
						{
							List<WebIDLType> _cloneList = (List<WebIDLType>)_argument.IDLType.CloneObject();

							WebIDLType _argumentIDLType = _argument.IDLType.First();

							_argumentIDLType.Union = false;
							_argumentIDLType.IDLType = null;
							_argumentIDLType.IDLTypeStr = CreateStructUnion(_cloneList);
						}
					}
				}
			}
		}

		private void ResolveTypeDef(ref StringBuilder sb, TType tType)
		{
			switch (tType.Type)
			{
				case "typedef":
					{
						//ListTypedefs.Add(arr);

						sb.Append($"using {tType.Name} = ");

						ProcessWebIDLType(ref sb, tType.IDLType.First());

						sb.Append(";");

						sb.AppendLine();
						break;
					}
				default:
					break;
			}
		}

		private string CreateStructUnion(List<WebIDLType> _list) 
		{
			TType localTT = new();
			localTT.Name = "Union" + _UnionId++;

			localTT.Type = "UnionStruct";

			localTT.Members = new();

			WebIDLType first = _list.First();

			for (int i = 0; i < first.IDLType.Count; i++)
			{
				if (first.IDLType[i].IDLTypeStr == null) 
				{
					for (int _i = 0; _i < first.IDLType[i].IDLType.Count; _i++)
					{
						if (first.IDLType[i].IDLType[_i].Union == true) 
						{
							List<WebIDLType> _cloneList = (List<WebIDLType>)first.IDLType[i].IDLType.CloneObject();

							WebIDLType _mainIDLType = first.IDLType[i].IDLType.First();

							_mainIDLType.Union = false;
							_mainIDLType.IDLType = null;
							_mainIDLType.IDLTypeStr = CreateStructUnion(_cloneList);
						}
					}
				}

				Member _member = new()
				{
					IDLType = new()
					{
						first.IDLType[i]
					},
					Type = "UnionStruct"
				};

				localTT.Members.Add(_member);
			}

			_Main.TType.Add(localTT);

			return localTT.Name;
		}

		private void ProcessTType(ref StringBuilder sb, TType tType)
		{
			_CurrentTType = tType;

			switch (tType.Type)
			{
				case "typedef":
				case "includes":
					return;
				case "enum":
					{
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{tType.Name}/*'/>");
						sb.Append($"public enum {tType.Name}");
						sb.AppendLine();
						sb.Append("{");
						sb.AppendLine();

						foreach (Value val in tType.Values)
						{
							sb.Append("\t");
							ProcessValue(ref sb, val);
							sb.Append(",");
							sb.AppendLine();
						}

						sb.Append("}");
						sb.AppendLine();

						break;
					}
				case "callback interface":
				case "interface mixin":
					{
						string exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null)
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
							_ListNamesForToAttr.Add(tType.Name);
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{tType.Name}/*'/>");
						sb.Append($"public partial interface {tType.Name}");
						if (tType.Inheritance != null)
						{
							sb.Append($" : ");
							TType arrL =  _Main.TType.Find((e) => e.Name == tType.Inheritance);
							bool nameMatch = _ECMATypes.Contains(tType.Inheritance);
							if (arrL != null || nameMatch)
								sb.Append($"{tType.Inheritance}");
							if (tType.ListAdditionalInheritance.Count != 0)
							{
								sb.Append($", ");
								foreach (TType item in tType.ListAdditionalInheritance)
								{
									sb.Append($"{item.Name}, ");
								}
								sb = sb.Remove(sb.Length - 2, 2);
							}
							else if (arrL == null)
							{
								sb = sb.Remove(sb.Length - 3, 3);
							}
						}
						sb.AppendLine();
						sb.Append("{");
						sb.AppendLine();

						foreach (Member mem in tType.Members)
						{
							sb.Append("\t");
							ProcessMember(ref sb, mem);
							sb.AppendLine();
						}

						sb.Append("}");
						sb.AppendLine();
						break;
					}
				case "namespace":
				case "interface":
				case "dictionary":
					{
						string exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null)
						{
							if (tType.Name.StartsWith("HTML") || tType.Name.StartsWith("Text"))
							{
								sb.AppendLine($"[To(ToAttribute.Default)]");
								_DefaultTrue = true;
							}
							else
								sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");

							_ListNamesForToAttr.Add(tType.Name);
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{tType.Name}/*'/>");
						sb.Append($"public partial class {tType.Name}");
						if (tType.Inheritance != null)
						{
							sb.Append($" : ");
							TType arrL = _Main.TType.Find((e) => e.Name == tType.Inheritance);
							bool nameMatch = _ECMATypes.Contains(tType.Inheritance);
							if (arrL != null || nameMatch)
								sb.Append($"{tType.Inheritance}");
							
							if (tType.ListAdditionalInheritance.Count != 0)
							{
								sb.Append($", ");
								foreach (TType item in tType.ListAdditionalInheritance)
								{
									sb.Append($"{item.Name}, ");
								}
								sb = sb.Remove(sb.Length - 2, 2);
							}
							else if (arrL == null)
							{
								sb = sb.Remove(sb.Length - 3, 3);
							}
						}
						sb.AppendLine();
						sb.Append("{");
						sb.AppendLine();

						foreach (Member mem in tType.Members)
						{
							sb.Append("\t");
							ProcessMember(ref sb, mem);
							sb.AppendLine();
						}

						sb.Append("}");
						sb.AppendLine();
						_DefaultTrue = false;
						break;
					}
				case "callback":
					{
						string exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null) 
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
							_ListNamesForToAttr.Add(tType.Name);
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{tType.Name}/*'/>");
						sb.Append($"public struct {tType.Name}");
						sb.AppendLine();
						sb.Append("{");
						sb.AppendLine();

						//TODO Action ?

						sb.Append("}");
						sb.AppendLine();
						break;
					}
				case "UnionStruct":
					{
						sb.Append($"public struct {tType.Name}");
						sb.AppendLine();
						sb.Append("{");
						sb.AppendLine();

						sb.Append("\t");
						sb.Append("public dynamic Value { get; set; }");
						sb.AppendLine();

						bool oneUnsupported = false;
						
						foreach (Member member in tType.Members)
						{
							if (member.IDLType.First().IDLTypeStr != null) 
							{
								string str = ProcessString(member.IDLType.First().IDLTypeStr);
								if (str.Contains("Unsupported"))
								{
									if (oneUnsupported == false)
									{
										oneUnsupported = true;
									}
									else
										continue;
								}
							}

							sb.Append("\t");
							ProcessMember(ref sb, member);
							sb.AppendLine();
						}
						sb.Append("}");
						sb.AppendLine();

						break;
					}
				default:
					Log($"{tType.Type}");
					break;
			}
		}

		private void ProcessValue(ref StringBuilder sb, Value value)
		{
			switch (value.Type)
			{
				case "enum-value": 
					{
						string _str = value.ValueObj.ToString();
						if (_str == "")
							_str = "Empty";
						if (_str != "")
						{
							bool startWithNum = char.IsDigit(_str[0]);
							if (startWithNum)
							{
								_str = "_" + _str;
							}
						}
						if (_str.Contains("-"))
						{

							string[] arr = _str.Split("-");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains("/"))
						{

							string[] arr = _str.Split("/");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains("+"))
						{

							string[] arr = _str.Split("+");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains(" "))
						{

							string[] arr = _str.Split(" ");
							_str = "";
							foreach (string a in arr)
							{
								_str += a.FirstCharToUpperCase();
							}
						}
						sb.Append(_str.FirstCharToUpperCase());
						break;
					}
				case "number": 
					{
						string _str = value.ValueObj.ToString();
						sb.Append(_str);
						break;
					}
				default:
					Log($"{value.Type}");
					break;
			}
		}

		private void ProcessMember(ref StringBuilder sb, Member member) 
		{
			switch (member.Type) 
			{
				case "attribute": 
					{
						if (_DefaultTrue)
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{_CurrentTType.Name + member.Name.FirstCharToUpperCase()}/*'/>");
						sb.Append("\t");

						sb.Append($"public ");

						if (member.Special == "static")
						{
							sb.Append($"static ");
						}

						if (member.Required != null)
							if (member.Required! == true)
								sb.Append("required ");

						ProcessWebIDLType(ref sb, member.IDLType.First());

						if (member.Name == "window")
							member.Name = "_" + member.Name;

						sb.Append($" {member.Name.FirstCharToUpperCase()} ");

						if (member.Readonly! == true)
						{
							if (_CurrentTType.Type == "callback interface" ||
								_CurrentTType.Type == "interface mixin")
								sb.Append("{ get { throw new System.NotImplementedException(); } }");
							else
								sb.Append("{ get; }");
						}
						else
						{
							if (_CurrentTType.Type == "callback interface" ||
								_CurrentTType.Type == "interface mixin")
								sb.Append("{ get { throw new System.NotImplementedException(); } set { throw new System.NotImplementedException(); } }");
							else
								sb.Append("{ get; set; }");
						}

						/*
						if (member.Default != null)
						{
							ProccesMemberDefault(ref sb, member.Default);
							sb.Append(";");
							break;
						}*/

						//sb.Append(";");
						break;
					}
				case "const": 
					{
						if (_DefaultTrue)
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{_CurrentTType.Name + member.Name.FirstCharToUpperCase()}/*'/>");
						sb.Append("\t");

						sb.Append($"public const ");

						ProcessWebIDLType(ref sb, member.IDLType.First());

						sb.Append($" {member.Name} = ");

						ProcessValue(ref sb, member.Value);

						sb.Append($";");
						break;
					}
				case "field":
					{
						if (_DefaultTrue)
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{_CurrentTType.Name + member.Name.FirstCharToUpperCase()}/*'/>");
						sb.Append("\t");

						sb.Append($"public ");

						if (member.Required != null)
							if (member.Required! == true)
								sb.Append("required ");

						ProcessWebIDLType(ref sb, member.IDLType.First());

						sb.Append($" {member.Name.FirstCharToUpperCase()}");

						/*
						if (member.Default != null)
						{
							sb.Append(" = ");
							ProccesMemberDefault(ref sb, member.Default);
						}*/

						sb.Append(";");
						break;
					}
				case "constructor": 
					{
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{_CurrentTType.Name}/*'/>");
						sb.Append("\t");
						sb.Append($"public ");

						sb.Append($"{_CurrentTType.Name}(");

						if (member.Arguments.Count >= 1)
						{
							foreach (Argument item in member.Arguments)
							{
								ProcessArguments(ref sb, item);
								sb.Append($", ");
							}

							sb = sb.Remove(sb.Length - 2, 2);
						}

						sb.Append(") { }");

						break;
					}
				case "operation": 
					{
						//TODO!
						if (member.Special == "stringifier" ||
							member.Special == "setter" ||
							member.Special == "getter" ||
							member.Special == "deleter")
							return;

						if (_DefaultTrue)
						{
							sb.AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						sb.AppendLine($"///<include file='Utils/Docs.generated.xml' path='docs/{_CurrentTType.Name + member.Name.FirstCharToUpperCase()}/*'/>");
						sb.Append("\t");

						sb.Append($"public ");

						if (member.Special == "static")
							sb.Append($"static ");

						if (member.Async != null)
						{
							if (member.Async! == true)
							{
								sb.Append($"async ");
							}
						}
						ProcessWebIDLType(ref sb, member.IDLType.First());

						sb.Append($" {member.Name.FirstCharToUpperCase()}(");

						if (member.Arguments.Count >= 1)
						{
							foreach (Argument item in member.Arguments)
							{
								ProcessArguments(ref sb, item);
								sb.Append($", ");
							}

							sb = sb.Remove(sb.Length - 2, 2);
						}

						sb.Append(") { throw new System.NotImplementedException(); }");
						break;
					}
				case "UnionStruct": 
					{
						foreach (WebIDLType item in member.IDLType)
						{
							sb.Append($"public static implicit operator {_CurrentTType.Name}(");
							ProcessWebIDLType(ref sb, item);
							sb.Append(" value)");
							sb.Append("{");
							sb.Append($"return new {_CurrentTType.Name} {{ Value = value }};");
							sb.Append("}");
						}
						break;
					}
				case "iterable":
					{
						//Todo with multiple IDLType. How?
						//
						//foreach (WebIDLType item in member.IDLType)
						//{
						sb.Append($"public ");
							ProcessWebIDLType(ref sb, member.IDLType?[0]);
							sb.Append(" this[int i]\n");
							sb.Append("\t{\n");
							sb.Append($"\t\tget {{ throw new System.NotImplementedException(); }}\n");
							sb.Append($"\t\tset {{ throw new System.NotImplementedException(); }}\n");
							sb.Append("\t}\n");
						//}
						break;
					}
				//TODO?!
				case "maplike":
					return;
				default:
					Log($"{member.Type}");
					break;
			}
		}

		private void ProcessWebIDLType(ref StringBuilder sb, WebIDLType webIDLType) 
		{
			switch (webIDLType.Type) 
			{
				case "attribute-type": 
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "FrozenArray":
								{
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append("[]");
									break;
								}
							case "Promise":
								{
									sb.Append("Task<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									string str = sb.ToString();
									if (str.EndsWith("void"))
									{
										str = str.Remove(str.Length - 5, 5);
										sb = new(str);
									}
									else
										sb.Append(">");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}

						if (webIDLType.Nullable != null)
							if (webIDLType.Nullable! == true)
								sb.Append("?");
						break;
					}
				case "const-type":
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "FrozenArray":
								{
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append("[]");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}

						if (webIDLType.Nullable != null)
							if (webIDLType.Nullable! == true)
								sb.Append("?");
						break;
					}
				case "dictionary-type":
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "sequence":
								{
									sb.Append("List<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append(">");
									break;
								}
							case "Promise":
								{
									sb.Append("Task<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									string str = sb.ToString();
									if (str.EndsWith("void"))
									{
										str = str.Remove(str.Length - 5, 5);
										sb = new(str);
									}
									else
										sb.Append(">");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}

						if (webIDLType.Nullable != null)
							if (webIDLType.Nullable! == true)
								sb.Append("?");
						break;
					}
				case "return-type": 
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "FrozenArray":
								{
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append("[]");
									break;
								}
							case "Promise":
								{
									sb.Append("Task<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									string str = sb.ToString();
									if (str.EndsWith("void"))
									{
										str = str.Remove(str.Length - 5, 5);
										sb = new(str);
									}
									else
										sb.Append(">");
									break;
								}
							case "sequence":
								{
									sb.Append("List<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append(">");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}

						if (webIDLType.Nullable != null)
							if (webIDLType.Nullable! == true)
								sb.Append("?");
						break;
					}
				case "argument-type":
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "FrozenArray":
								{
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append("[]");
									break;
								}
							case "sequence": 
								{
									sb.Append("List<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append(">");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}

						if (webIDLType.Nullable != null)
							if (webIDLType.Nullable! == true)
								sb.Append("?");
						break;
					}
				case "typedef-type": 
					{
						switch (webIDLType.Generic)
						{
							case "":
								{
									sb.Append(ProcessString(webIDLType.IDLTypeStr));
									break;
								}
							case "sequence":
								{
									sb.Append("List<");
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append(">");
									break;
								}
							case "record":
								{
									sb.Append("Dictionary<");
									foreach (WebIDLType _item in webIDLType.IDLType)
									{
										ProcessWebIDLType(ref sb, _item);
										sb.Append(", ");
									}
									sb = sb.Remove(sb.Length - 2, 2);
									sb.Append(">");
									break;
								}
							default:
								Log($"{webIDLType.Type} {webIDLType.Generic}");
								break;
						}
						break;
					}
				default:
					//Log($"{webIDLType.Type}");
					switch (webIDLType.Generic)
					{
						case "":
							{
								sb.Append(ProcessString(webIDLType.IDLTypeStr));
								break;
							}
						case "sequence":
							{
								sb.Append("List<");
								ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
								sb.Append(">");
								break;
							}
						case "record":
							{
								sb.Append("Dictionary<");
								foreach (WebIDLType _item in webIDLType.IDLType)
								{
									ProcessWebIDLType(ref sb, _item);
									sb.Append(", ");
								}
								sb = sb.Remove(sb.Length - 2, 2);
								sb.Append(">");
								break;
							}
						default:
							Log($"{webIDLType.Type} {webIDLType.Generic}");
							break;
					}

					if(webIDLType.Nullable != null)
						if (webIDLType.Nullable! == true)
							sb.Append("?");
					break;
			}
		}

		private void ProcessArguments(ref StringBuilder sb, Argument argument) 
		{
			switch (argument.Type)
			{
				case "argument":
					{
						if (argument.Variadic == true)
							sb.Append($"params ");

						ProcessWebIDLType(ref sb, argument.IDLType.First());

						if (argument.Variadic == true)
							sb.Append($"[]");


						if (argument.Name == "base" ||
							argument.Name == "namespace" ||
							argument.Name == "event" ||
							argument.Name == "string" ||
							argument.Name == "default")
						{
							sb.Append($" {argument.Name}_");
						}
						else
							sb.Append($" {argument.Name}");

						//
						//if (argument.Optional == true)
						//	sb.Append($" = null");

						if (argument.Default != null)
						{
							//TODO!
							//sb.Append(" = ");
							//ProccesMemberDefault(ref sb, memberArgument.Default);
						}

						break;
					}
				default:
					Log($"{argument.Type}");
					break;
			}
		}



		private string ProcessString(string str, bool en = false)
		{
			if (str.Contains("DOMString") ||
				str.Contains("USVString") ||
				str.Contains("ByteString"))
			{
				str = "string";
				return str;
			}
			if (str.Contains("unsigned long"))
			{
				str = "ulong";
				return str;
			}
			if (str.Contains("unsigned short"))
			{
				str = "ushort";
				return str;
			}
			if (str.Contains("unrestricted float"))
			{
				str = "float";
				return str;
			}
			if (str.Contains("long long"))
			{
				str = "long";
				return str;
			}
			if (str.Contains("boolean"))
			{
				str = "bool";
				return str;
			}
			if (str.Contains("octet"))
			{
				str = "byte";
				return str;
			}
			if (str.Contains("any"))
			{
				str = "dynamic";
				return str;
			}
			if (str.Contains("unrestricted double"))
			{
				str = "double";
				return str;
			}
			if (str == "ArrayBuffer")
			{
				str = "byte[]";
				return str;
			}
			if (str.Contains("Int8Array"))
			{
				str = "System.SByte";
				return str;
			}
			if (str.Contains("Int16Array"))
			{
				str = "System.Int16";
				return str;
			}
			if (str.Contains("Int32Array"))
			{
				str = "System.Int32";
				return str;
			}
			if (str.Contains("BigInt64Array"))
			{
				str = "System.Int64";
				return str;
			}
			if (str.Contains("BigUint64Array"))
			{
				str = "System.UInt64";
				return str;
			}
			if (str.Contains("Uint8Array"))
			{
				str = "System.Byte";
				return str;
			}
			if (str.Contains("Uint16Array"))
			{
				str = "System.UInt16";
				return str;
			}
			if (str.Contains("Uint32Array"))
			{
				str = "System.UInt32";
				return str;
			}
			if (str.Contains("Float32Array"))
			{
				str = "System.Single";
				return str;
			}
			if (str.Contains("Float64Array"))
			{
				str = "System.Double";
				return str;
			}
			//	TODO ? CSharpToJavaScript.Utils.Unsupported /*DataView>

			if (str.Contains("double") ||
				str.Contains("short") ||
				str.Contains("float"))
			{
				return str;
			}


			//WindowProxy
			if (str.Contains("WindowProxy"))
			{
				str = "Window";
				return str;
			}

			if (str == "")
			{
				str = "Empty";
				return str;
			}


			if (str == "undefined")
			{
				str = $"CSharpToJavaScript.Utils.Unsupported /*{str}*/";
				//str = $"void";
				return str;
			}

			
			if (char.IsUpper(str[0]) && en == false)
			{
				TType arrL = _Main.TType.Find((e) => e.Name == str);
				bool nameMatch = _ECMATypes.Contains(str);
				if (arrL == null && nameMatch == false)
				{
					str = $"CSharpToJavaScript.Utils.Unsupported /*{str}*/";
				}
			}

			return str;
		}

		private static void Log(string message, [CallerFilePath] string? file = null, [CallerMemberName] string? member = null, [CallerLineNumber] int line = 0)
		{
			Trace.WriteLine($"({line}):{Path.GetFileName(file)} {member}: {message}");
		}
	}
}