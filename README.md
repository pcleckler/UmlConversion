# UML Conversion From Code

This project came from reviewing unsatisfactory results with other C# -> UML conversion tools. Conversion from UML to images is currently handled by [PlantUML](https://plantuml.com/).

## CSharpToUml

This project is for conversion from C# to UML, as the name states. Public classes, delegates, structs, enums, and interfaces currently handled. Available documentation is pulled in to supply class summary, including SEE references and PARAM and RETURNS nodes for delegates.

## JavaScriptToUmlConversion

This project is a placeholder for JavaScript to UML conversion. The current project uses Microsoft's ClearScript.V8 to load scripts, but no reflection options resulted. Will be rewriting this with a JavaScript parser.
