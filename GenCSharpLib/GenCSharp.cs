using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace GenCSharpLib
{
	public class GenCSharp : ILog
	{
		private readonly ILog _Log;
		private string _Output = string.Empty;

		private Welcome _Main = new();
		private int _UnionId = 0;
		private TType _CurrentTType = new();
		private List<string> _ListNamesForToAttr = new();
		private List<string> _ListNamesWithDefaultAttr = new();
		private List<TType> _ListOfTypeDefs = new();

		private bool _OneForDOMParser = false;

		//
		//0 = Typedef. Needs to be in every other StringBuilder!
		//1 = Enums.
		//2 = Interfaces.
		//3 = Classes. TODO! Seperate to multiple files!
		//4 = Structs.
		//5 = Uinion structs.
		//
		private StringBuilder[] _SB = new StringBuilder[6];
		private int _SBIndex = 0;

		public GenCSharp()
		{
			_Log = this;
		}

		public async Task GenerateCSFromJson(string path, string output)
		{
			_Output = output;

			string jsonString = File.ReadAllText(path);
			
			JsonSerializerOptions serializeOptions = new();

			Welcome? welcome = JsonSerializer.Deserialize<Welcome>(jsonString, serializeOptions);

			_Main = welcome ?? new();

			//
			//
			//Not working, because classes/interfaces with the same name,
			//can be both partial and not)
			//TODO!
			/*
			List<TType> all = _Main.TType.DistinctBy((i) => { return i.Name; }).ToList();
			List<TType> par = all.Select((i) => { if (i.Partial == true) return i; else return null; }).ToList();
			foreach (TType item in par)
			{
				if(item != null)
					all.Remove(item);
			}
			List<TType> par2 = _Main.TType.Select((i) => { if (i.Partial == true) return i; else return null; }).ToList();
			par2.RemoveAll((i) => i == null);

			_Main.TType = all.Concat(par2).ToList();
			*/

			//var a = _Main.TType.Select((i) => { if (i.Name == "Window") return i; else return null; }).ToList();
			//a.RemoveAll((i) => i == null);

			int length = _Main.TType.Count;

			for (int i = 0; i < length; i++)
			{
				ResolveUnion(_Main.TType[i]);

				if (_Main.TType[i].Type == "includes")
				{
					TType? target = _Main.TType.Find((e) => e.Name == _Main.TType[i].Target);
					TType? includes = _Main.TType.Find((e) => e.Name == _Main.TType[i].Includes);
					if (target != null && includes != null)
						target.ListAdditionalInheritance.Add(includes);
				}
				if (_Main.TType[i].Type == "typedef")
				{
					_ListOfTypeDefs.Add(_Main.TType[i]);
				}
			}

			_SB[0] = new();
			_SB[0].AppendLine($"//{DateTime.Now}");
			_SB[0].AppendLine();
			_SB[0].AppendLine("using static CSharpToJavaScript.APIs.JS.GlobalObject;");
			_SB[0].AppendLine("using CSharpToJavaScript.Utils;");
			_SB[0].AppendLine("using System.Collections.Generic;");
			_SB[0].AppendLine("using System.Threading.Tasks;");
			_SB[0].AppendLine();
			_SB[0].AppendLine($"namespace CSharpToJavaScript.APIs.JS;");
			_SB[0].AppendLine();
			_SB[0].AppendLine("//");

			_SB[0].AppendLine("using WindowProxy = Window;");
			_SB[0].AppendLine("using USVString = string;");
			_SB[0].AppendLine("using ByteString = string;");
			_SB[0].AppendLine("using DOMString = string;");

			_SB[0].AppendLine("//");

			_SB[0].AppendLine();

			for (int j = 0; j < length; j++)
			{
				ResolveTypeDef(ref _SB[0], _Main.TType[j]);
			}
			_SB[0].AppendLine();

			for (int i = 1; i < _SB.Length; i++)
			{
				_SB[i] = new();
				_SB[i].Append(_SB[0]);
				_SB[i].AppendLine();
			}

			
			foreach (TType item in _Main.TType)
			{
				ProcessTType(item);
				_SB[_SBIndex].AppendLine();
			}

			for (int i = 1; i < _SB.Length; i++)
			{
				if (File.Exists(Path.Combine(output, $"JS{i}.generated.cs")))
					await File.WriteAllTextAsync(Path.Combine(output, $"JS{i}{i}.generated.cs"), _SB[i].ToString());
				else
					await File.WriteAllTextAsync(Path.Combine(output, $"JS{i}.generated.cs"), _SB[i].ToString());
			}

			_Log.WriteLine("--- Done!");
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
						_CurrentTType = tType;

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

		private void ProcessTType(TType tType)
		{
			_CurrentTType = tType;

			switch (tType.Type)
			{
				case "typedef":
				case "includes":
					return;
				case "enum":
					{
						_SBIndex = 1;
						AddXmlRef(tType.Name);
						_SB[_SBIndex].AppendLine($"[To(ToAttribute.None)]");
						_SB[_SBIndex].Append($"public enum {tType.Name}");
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append("{");
						_SB[_SBIndex].AppendLine();

						foreach (Value val in tType.Values)
						{
							_SB[_SBIndex].Append("\t");
							_SB[_SBIndex].AppendLine($"[EnumValue(\"{val.ValueObj.ToString()}\")]");
							_SB[_SBIndex].Append("\t");
							ProcessValue(val);
							_SB[_SBIndex].Append(",");
							_SB[_SBIndex].AppendLine();
						}

						_SB[_SBIndex].Append("}");
						_SB[_SBIndex].AppendLine();

						break;
					}
				case "callback interface":
				case "interface mixin":
					{
						_SBIndex = 2;
						AddXmlRef(tType.Name);
						string? exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null)
						{
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
							_ListNamesForToAttr.Add(tType.Name);
						}
						_SB[_SBIndex].Append($"public partial interface {tType.Name}");
						if (tType.Inheritance != null)
						{
							_SB[_SBIndex].Append($" : ");
							TType? arrL =  _Main.TType.Find((e) => e.Name == tType.Inheritance);

							if (arrL != null)
								_SB[_SBIndex].Append($"{tType.Inheritance}");
							if (tType.ListAdditionalInheritance.Count != 0)
							{
								if (arrL != null)
									_SB[_SBIndex].Append($", ");
								foreach (TType item in tType.ListAdditionalInheritance)
								{
									_SB[_SBIndex].Append($"{item.Name}, ");
								}
								_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 2, 2);
							}
							else if (arrL == null)
							{
								_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 3, 3);
							}
						}
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append("{");
						_SB[_SBIndex].AppendLine();

						foreach (Member mem in tType.Members)
						{
							_SB[_SBIndex].Append("\t");
							ProcessMember(mem);
							_SB[_SBIndex].AppendLine();
						}

						_SB[_SBIndex].Append("}");
						_SB[_SBIndex].AppendLine();
						break;
					}
				case "namespace":
				case "interface":
				case "dictionary":
					{
						_SBIndex = 3;
						if (tType.Name == "console")
							AddXmlRef(tType.Name.FirstCharToUpperCase());
						else
							AddXmlRef(tType.Name);

						string? exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null)
						{
							if (tType.Name.StartsWith("HTML") ||
								tType.Name.StartsWith("Text") ||
								tType.Name.StartsWith("CSS") ||
								tType.Name == "Window" ||
								tType.Name == "CustomEvent")
							{
								_SB[_SBIndex].AppendLine($"[To(ToAttribute.Default)]");
								_ListNamesWithDefaultAttr.Add(tType.Name);
							}
							else
								_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");

							_ListNamesForToAttr.Add(tType.Name);
						}
						_SB[_SBIndex].Append($"public partial class {tType.Name}");
						if (tType.Inheritance != null)
						{
							_SB[_SBIndex].Append($" : ");
							TType arrL = _Main.TType.Find((e) => e.Name == tType.Inheritance);

							if (arrL != null)
								_SB[_SBIndex].Append($"{tType.Inheritance}");
							
							if (tType.ListAdditionalInheritance.Count != 0)
							{
								if (arrL != null)
									_SB[_SBIndex].Append($", ");
								foreach (TType item in tType.ListAdditionalInheritance)
								{
									_SB[_SBIndex].Append($"{item.Name}, ");
								}
								_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 2, 2);
							}
							else if (arrL == null)
							{
								_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 3, 3);
							}
						}
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append("{");
						_SB[_SBIndex].AppendLine();

						foreach (Member mem in tType.Members)
						{
							_SB[_SBIndex].Append("\t");
							ProcessMember(mem);
							_SB[_SBIndex].AppendLine();
						}

							foreach (Member mem in tType.Members)
							{
								if (mem.Type == "constructor")
								{
									if (mem.Arguments.Count == 0)
										break;
									if (mem.Arguments.Count >= 1)
									{
										_SB[_SBIndex].Append("\t");
										_SB[_SBIndex].Append($"public {_CurrentTType.Name}() {{ }}");
										_SB[_SBIndex].AppendLine();
										break;
									}
								}
							}
						

						_SB[_SBIndex].Append("}");
						_SB[_SBIndex].AppendLine();
						break;
					}
				case "callback":
					{
						_SBIndex = 4;
						AddXmlRef(tType.Name);
						string exist = _ListNamesForToAttr.Find(e => e == tType.Name);
						if (exist == null) 
						{
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
							_ListNamesForToAttr.Add(tType.Name);
						}
						_SB[_SBIndex].Append($"public struct {tType.Name}");
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append("{");
						_SB[_SBIndex].AppendLine();

						//TODO Action ?

						_SB[_SBIndex].Append("}");
						_SB[_SBIndex].AppendLine();
						break;
					}
				case "UnionStruct":
					{
						_SBIndex = 5;
						//
						//
						//TODO! better!

						StringBuilder _lsb = new();

						_SB[_SBIndex].Append($"///<summary>");
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append($"///");
						foreach (Member member in tType.Members)
						{
							foreach (WebIDLType item in member.IDLType)
							{
								_lsb.Append($"<see cref=\"");
								ProcessWebIDLType(ref _lsb, item);

								string _str = _lsb.ToString();
								if (_str.EndsWith("[]"))
								{
									_lsb.Remove(_lsb.Length - 2, 2);
								}
								_lsb.Append($"\"/> or ");
							}
						}
						_lsb.Replace("<", "{");
						_lsb.Replace(">", "}");
						_lsb.Replace("{see", "<see");
						_lsb.Replace("/}", "/>");
						_lsb.Replace("{c}", "<c>");
						_lsb.Replace("{/c}", "</c>");

						_SB[_SBIndex].Append(_lsb.ToString());
						_SB[_SBIndex].Remove(_SB[_SBIndex].Length - 4, 4);
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append($"///</summary>");



						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append($"public struct {tType.Name}");
						_SB[_SBIndex].AppendLine();
						_SB[_SBIndex].Append("{");
						_SB[_SBIndex].AppendLine();

						_SB[_SBIndex].Append("\t");
						_SB[_SBIndex].Append("public dynamic Value { get; set; }");
						_SB[_SBIndex].AppendLine();

						bool oneUnsupported = false;
						
						foreach (Member member in tType.Members)
						{
							if (member.IDLType.First().IDLTypeStr != null) 
							{
								string str = ProcessString(member.IDLType.First().IDLTypeStr);
								//TODO?
								if (str == "object")
									continue;
								if (str.Contains("Unsupported") || 
									str.Contains("string"))
								{
									if (oneUnsupported == false)
									{
										oneUnsupported = true;
									}
									else
										continue;
								}
							}

							_SB[_SBIndex].Append("\t");
							ProcessMember(member);
							_SB[_SBIndex].AppendLine();
						}
						_SB[_SBIndex].Append("}");
						_SB[_SBIndex].AppendLine();

						break;
					}
				default:
					_Log.WriteLine($"{tType.Type}");
					break;
			}
		}

		private void ProcessValue(Value value)
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
						if (_str.Contains('-'))
						{

							string[] arr = _str.Split("-");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains('/'))
						{

							string[] arr = _str.Split("/");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains('+'))
						{

							string[] arr = _str.Split("+");
							_str = "";
							foreach (string item in arr)
							{
								_str += item.FirstCharToUpperCase();
							}
						}
						if (_str.Contains(' '))
						{

							string[] arr = _str.Split(" ");
							_str = "";
							foreach (string a in arr)
							{
								_str += a.FirstCharToUpperCase();
							}
						}
						if (_str.Contains('.'))
						{
							_str = _str.Replace(".", "_");
						}
						_SB[_SBIndex].Append(_str.FirstCharToUpperCase());
						break;
					}
				case "number": 
					{
						string _str = value.ValueObj.ToString();
						_SB[_SBIndex].Append(_str);
						break;
					}
				default:
					_Log.WriteLine($"{value.Type}");
					break;
			}
		}

		private void ProcessMember(Member member) 
		{
			switch (member.Type) 
			{
				case "attribute": 
					{
						string _name = _CurrentTType.Name + member.Name.FirstCharToUpperCase();
						AddXmlRef(_name);

						string exist = _ListNamesWithDefaultAttr.Find(e => e == _CurrentTType.Name);
						if (exist != null)
						{
							_SB[_SBIndex].Append("\t");
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						_SB[_SBIndex].Append("\t");

						_SB[_SBIndex].Append($"public ");

						if (member.Special == "static")
						{
							_SB[_SBIndex].Append($"static ");
						}

						if (member.Required != null)
							if (member.Required! == true)
								_SB[_SBIndex].Append("required ");

						ProcessWebIDLType(ref _SB[_SBIndex], member.IDLType.First());

						string name = member.Name;

						if (name == "window")
							name = "_" + name;

						if (name.Contains('-'))
							name = name.Replace("-", "_");

						_SB[_SBIndex].Append($" {name.FirstCharToUpperCase()} ");

						if (member.Readonly! == true)
						{
							if (_CurrentTType.Type == "callback interface" ||
								_CurrentTType.Type == "interface mixin")
								_SB[_SBIndex].Append("{ get { throw new System.NotImplementedException(); } }");
							else
								_SB[_SBIndex].Append("{ get; }");
						}
						else
						{
							if (_CurrentTType.Type == "callback interface" ||
								_CurrentTType.Type == "interface mixin")
								_SB[_SBIndex].Append("{ get { throw new System.NotImplementedException(); } set { throw new System.NotImplementedException(); } }");
							else
								_SB[_SBIndex].Append("{ get; set; }");
						}

						/*
						if (member.Default != null)
						{
							ProccesMemberDefault(member.Default);
							_SB[_SBIndex].Append(";");
							break;
						}*/

						//_SB[_SBIndex].Append(";");
						break;
					}
				case "const": 
					{
						string _name = _CurrentTType.Name + member.Name.FirstCharToUpperCase();
						AddXmlRef(_name);

						string exist = _ListNamesWithDefaultAttr.Find(e => e == _CurrentTType.Name);
						if (exist != null)
						{
							_SB[_SBIndex].Append("\t");
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						_SB[_SBIndex].Append("\t");

						_SB[_SBIndex].Append($"public const ");

						ProcessWebIDLType(ref _SB[_SBIndex], member.IDLType.First());

						_SB[_SBIndex].Append($" {member.Name} = ");

						ProcessValue(member.Value);

						_SB[_SBIndex].Append($";");
						break;
					}
				case "field":
					{
						string _name = _CurrentTType.Name + member.Name.FirstCharToUpperCase();
						AddXmlRef(_name);

						string exist = _ListNamesWithDefaultAttr.Find(e => e == _CurrentTType.Name);
						if (exist != null)
						{
							_SB[_SBIndex].Append("\t");
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						_SB[_SBIndex].Append("\t");

						_SB[_SBIndex].Append($"public ");

						if (member.Required != null)
							if (member.Required! == true)
								_SB[_SBIndex].Append("required ");

						ProcessWebIDLType(ref _SB[_SBIndex], member.IDLType.First());

						_SB[_SBIndex].Append($" {member.Name.FirstCharToUpperCase()}");

						/*
						if (member.Default != null)
						{
							_SB[_SBIndex].Append(" = ");
							ProccesMemberDefault(member.Default);
						}*/

						_SB[_SBIndex].Append(";");
						break;
					}
				case "constructor":
					{
						if (_CurrentTType.Name == "DOMParser" || 
							_CurrentTType.Name == "CaptureController")
						{
							if (_OneForDOMParser == false)
							{
								_OneForDOMParser = true;
							}
							else
							{
								break;
							}
						}
						string _name = _CurrentTType.Name + _CurrentTType.Name;
						AddXmlRef(_name);
						_SB[_SBIndex].Append("\t");
						_SB[_SBIndex].Append($"public ");

						_SB[_SBIndex].Append($"{_CurrentTType.Name}(");

						if (member.Arguments.Count >= 1)
						{
							foreach (Argument item in member.Arguments)
							{
								ProcessArguments(item);
								_SB[_SBIndex].Append($", ");
							}

							_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 2, 2);
						}

						_SB[_SBIndex].Append(") { }");

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

						string _name = string.Empty;
						if (_CurrentTType.Name.Contains("console"))
							_name = _CurrentTType.Name.FirstCharToUpperCase() + member.Name.FirstCharToUpperCase();
						else
							_name =	_CurrentTType.Name + member.Name.FirstCharToUpperCase();
						AddXmlRef(_name);

						string exist = _ListNamesWithDefaultAttr.Find(e => e == _CurrentTType.Name);
						if (exist != null)
						{
							_SB[_SBIndex].Append("\t");
							_SB[_SBIndex].AppendLine($"[To(ToAttribute.FirstCharToLowerCase)]");
						}
						_SB[_SBIndex].Append("\t");

						_SB[_SBIndex].Append($"public ");

						if (member.Special == "static")
							_SB[_SBIndex].Append($"static ");

						if (member.Async != null)
						{
							if (member.Async! == true)
							{
								_SB[_SBIndex].Append($"async ");
							}
						}
						ProcessWebIDLType(ref _SB[_SBIndex], member.IDLType.First());

						_SB[_SBIndex].Append($" {member.Name.FirstCharToUpperCase()}(");

						if (member.Arguments.Count >= 1)
						{
							foreach (Argument item in member.Arguments)
							{
								ProcessArguments(item);
								_SB[_SBIndex].Append($", ");
							}

							_SB[_SBIndex] = _SB[_SBIndex].Remove(_SB[_SBIndex].Length - 2, 2);
						}

						_SB[_SBIndex].Append(") { throw new System.NotImplementedException(); }");
						break;
					}
				case "UnionStruct": 
					{
						foreach (WebIDLType item in member.IDLType)
						{
							_SB[_SBIndex].Append($"public static implicit operator {_CurrentTType.Name}(");
							ProcessWebIDLType(ref _SB[_SBIndex], item);
							_SB[_SBIndex].Append(" value)");
							_SB[_SBIndex].Append("{");
							_SB[_SBIndex].Append($"return new {_CurrentTType.Name} {{ Value = value }};");
							_SB[_SBIndex].Append("}");
						}
						break;
					}
				case "iterable":
					{
						//Todo with multiple IDLType. How?
						//
						//foreach (WebIDLType item in member.IDLType)
						//{
						_SB[_SBIndex].Append($"public ");
							ProcessWebIDLType(ref _SB[_SBIndex], member.IDLType?[0]);
							_SB[_SBIndex].Append(" this[int i] ");
							_SB[_SBIndex].Append(" { ");
							_SB[_SBIndex].Append($" get {{ throw new System.NotImplementedException(); }} ");
							_SB[_SBIndex].Append($" set {{ throw new System.NotImplementedException(); }} ");
							_SB[_SBIndex].Append(" } ");
						//}
						break;
					}
				//TODO?!
				case "setlike":
				case "maplike":
					return;
				default:
					_Log.WriteLine($"{member.Type}");
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
							case "ObservableArray":
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
									if (webIDLType.IDLTypeStr == null)
										sb.Append(ProcessString(webIDLType.IDLType.First().IDLTypeStr));
									else
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
							case "FrozenArray":
								{
									ProcessWebIDLType(ref sb, webIDLType.IDLType.First());
									sb.Append("[]");
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
									if (webIDLType.IDLTypeStr == null)
										sb.Append(ProcessString(webIDLType.IDLType.First().IDLTypeStr));
									else
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
									if (webIDLType.IDLTypeStr == null)
										sb.Append(ProcessString(webIDLType.IDLType.First().IDLTypeStr));
									else
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
								_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
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
							_Log.WriteLine($"{webIDLType.Type} {webIDLType.Generic}");
							break;
					}

					if(webIDLType.Nullable != null)
						if (webIDLType.Nullable! == true)
							sb.Append("?");
					break;
			}
		}

		private void ProcessArguments(Argument argument) 
		{
			switch (argument.Type)
			{
				case "argument":
					{
						if (argument.Variadic == true)
							_SB[_SBIndex].Append($"params ");

						ProcessWebIDLType(ref _SB[_SBIndex], argument.IDLType.First());

						if (argument.Variadic == true)
							_SB[_SBIndex].Append($"[]");


						if (argument.Name == "base" ||
							argument.Name == "namespace" ||
							argument.Name == "event" ||
							argument.Name == "string" ||
							argument.Name == "default" ||
							argument.Name == "interface" ||
							argument.Name == "ref" ||
							argument.Name == "params")
						{
							_SB[_SBIndex].Append($" {argument.Name}_");
						}
						else
							_SB[_SBIndex].Append($" {argument.Name}");

						//
						//if (argument.Optional == true)
						//	_SB[_SBIndex].Append($" = null");

						if (argument.Default != null)
						{
							//TODO!
							//_SB[_SBIndex].Append(" = ");
							//ProccesMemberDefault(memberArgument.Default);
						}

						break;
					}
				default:
					_Log.WriteLine($"{argument.Type}");
					break;
			}
		}



		private string ProcessString(string str, bool en = false)
		{
			if (str.Contains("USVString") ||
				str.Contains("ByteString") ||
				str.Contains("CSSOMString") ||
				str.Contains("DOMString"))
			{
				if(_CurrentTType.Type == "typedef")
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
			if (str.Contains("bigint"))
			{
				str = "BigInt";
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
				str = "Number";
				return str;
			}

			//
			//
			if (str.Contains("WindowProxy"))
			{
				return str;
			}
			//
			//
			
			if (str == "")
			{
				str = "Empty";
				return str;
			}


			if (str == "undefined")
			{
				str = $"Undefined";
				return str;
			}

			if (str == "object")
			{
				str = "Object";
				return str;
			}

			if (char.IsUpper(str[0]) && en == false)
			{
				TType? arrL = _Main.TType.Find((e) => e.Name == str);

				if (arrL == null)
				{
					str = $"Unsupported /*{str}*/";
				}
				else 
				{
					if (_CurrentTType.Type == "typedef")
					{
						TType? typeDef = _ListOfTypeDefs.Find((e) => e.Name == str);
						if (typeDef != null)
						{
							str = $"Unsupported /*{str}*/";
						}
					}
				}
			}

			return str;
		}

		private void AddXmlRef(string localName)
		{
			string name = localName;

			if (name.StartsWith("NonElementParentNode")) 
			{
				name = name.Replace("NonElementParentNode", "Document");
			}
			if (name.StartsWith("ParentNode")) 
			{
				name = name.Replace("ParentNode", "Element");
			}
			if (name.StartsWith("ChildNode"))
			{
				name = name.Replace("ChildNode", "Element");
			}



			if (name.Length >= 60)
			{
				name = name.Substring(0, 60);
			}

			//The output path needs to be in CSharpToJavaScript!
			string directory = _Output.Replace("APIs", "Utils");
			directory = directory.Replace("JS\\Generated", "");

			if (File.Exists($"{directory}\\Docs2\\{name}\\{name}.xml"))
				_SB[_SBIndex].AppendLine($"///<include file='Utils/Docs2/{name}/{name}.xml' path='docs/{name}/*'/>");
			if (File.Exists($"{directory}\\Docs\\{name}\\{name}.generated.xml"))
				_SB[_SBIndex].AppendLine($"///<include file='Utils/Docs/{name}/{name}.generated.xml' path='docs/{name}/*'/>");
		}
	}
}