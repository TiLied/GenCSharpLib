# GenCSharpLib
This library is related to [CSharpToJavaScript](https://github.com/TiLied/CSharpToJavaScript) for generating c# files from webidl.
## How to use
- Download standards:
```csharp
GetStandards getStandards = new();
await getStandards.GetThroughWeb("FULL STANDARDS OUTPUT PATH");	
```
- Add any missing standards with extension "raw.txt" in "FULL STANDARDS OUTPUT PATH", like [console](https://console.spec.whatwg.org/) etc.
- Convert "raw.txt" to webidl only:
```csharp
GetStandards getStandards = new();
await getStandards.GetWebIdl("FULL STANDARDS OUTPUT PATH");
```
- Convert webidl to json files using [webidl2.js](https://github.com/w3c/webidl2.js/). This step requires a lot of manual editing!
- Make sure that json starts with :
```
{
	"TType":...
}
```
- Generate csharp file:
```csharp
GenCSharp genCSharp = new();
await genCSharp.GenerateCSFromJson("FULL PATH TO JSON FILE", "FULL OUTPUT PATH");
```
## Some Todos
- [x] ~Figure out why some methods did not get generated, like "CreateElement" in "Document".~
- [x] ~Figure out how to get all standards from https://www.w3.org/TR/~
- [x] ~Figure out what to do with esmascript and "if" how to generate c# files...~  Decided to do by hand, while using CSharpToJavaScript library, [here](https://github.com/TiLied/CSharpToJavaScript/tree/master/CSharpToJavaScript/APIs/JS/Ecma)

## Related Repository 
CSharpToJavaScript library: https://github.com/TiLied/CSharpToJavaScript
- Library for generating docs: https://github.com/TiLied/GenDocsLib

CLI for library: https://github.com/TiLied/CSTOJS_CLI
  
VS Code Extension using CLI: https://github.com/TiLied/CSTOJS_VSCode_Ext

VS Extension using CLI: https://github.com/TiLied/CSTOJS_VS_Ext

Website/documentation: https://github.com/TiLied/CSTOJS_Pages
- Blazor WebAssembly: https://github.com/TiLied/CSTOJS_BWA


## Thanks for packages and content <3
[webidl2.js](https://github.com/w3c/webidl2.js/)

