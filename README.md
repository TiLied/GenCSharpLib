# GenCSharpLib
This library is related to [CSharpToJavaScript](https://github.com/TiLied/CSharpToJavaScript) for generating c# files.
## How to use
- Convert webidl to json files using [webidl2.js](https://github.com/w3c/webidl2.js/)
- Add in Main Method:
```csharp
GenCSharp genCSharp = new();
await genCSharp.GenerateCSFromJson("FULL PATH TO JSON FILE", "FULL OUTPUT PATH");
```
## Some Todos
- [ ] Figure out why some methods did not get generated, like "CreateElement" in "Document".
- [ ] Figure out how to get all standards from https://www.w3.org/TR/
- [x] ~Figure out what to do with esmascript and "if" how to generate c# files...~  Decided to do by hand, while using CSharpToJavaScript library, [here](https://github.com/TiLied/CSharpToJavaScript/tree/master/CSharpToJavaScript/APIs/JS/Ecma)

## Related Repository 
https://github.com/TiLied/CSharpToJavaScript

https://github.com/TiLied/GenDocsLib
