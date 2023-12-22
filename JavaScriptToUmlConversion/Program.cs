using System;
using System.IO;
using System.Linq;
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript;


class Program
{
    static void Main()
    {
        try
        {
            var engine = new V8ScriptEngine();
            
            // Load JavaScript files
            Directory.GetFiles(
                path: @"D:\Drive\Programming\C#\CleckTechMaps\CleckTechMaps\wwwroot\TileCraft2", 
                searchPattern: "*.*", 
                searchOption: SearchOption.AllDirectories)

                .Where((path) => Path.GetExtension(path) == ".js")
                .ToList()
                .ForEach((path) =>
                {
                    LoadJavaScriptFile(engine, path);
                });

            // Execute some JavaScript code
            string jsCode = "console.log('Hello from JavaScript!');";
            engine.Execute(jsCode);

            // Inspect classes, functions, and variables
            Console.WriteLine("Inspecting JavaScript objects:");
            InspectJavaScriptObjects(engine, engine.Global);

            // Clean up resources
            engine.Dispose();
        }
        finally
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }

    static void LoadJavaScriptFile(V8ScriptEngine engine, string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                //Console.WriteLine($"Loading file '{filePath}'...");

                string script = File.ReadAllText(filePath);

                engine.Compile(script);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load file '{filePath}'. Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"File not found: {filePath}");
        }
    }

    static void InspectJavaScriptObjects(V8ScriptEngine engine, ScriptObject script, int indent = 0)
    {
        foreach (var propertyName in script.PropertyNames)
        {
            var propertyValue = script.GetProperty(propertyName);

            Console.WriteLine($"{new string(' ', indent)}{propertyName}: {propertyValue}");

            try
            {
                InspectJavaScriptObjects(engine, (ScriptObject)propertyValue, indent + 1);
            }
            catch
            {
                // Ignore
            }
        }

        //// Example: Inspect functions
        //var functionNames = engine.GetFunctionNames();

        //Console.WriteLine("\nFunctions:");

        //foreach (var functionName in functionNames)
        //{
        //    Console.WriteLine($"Function: {functionName}");
        //}
    }
}
