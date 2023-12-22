using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CSharpToUml
{
    internal class Program
    {
        private static string asmFilename;
        private static Assembly asm;

        private static string xmlCommentsFilename;
        private static XmlDocument xmlComments;

        private static List<Type> typeList;

        private static readonly Dictionary<string, Dictionary<UmlSegment, List<string>>> umlSegments = new Dictionary<string, Dictionary<UmlSegment, List<string>>>();

        private static readonly Dictionary<Type, string> uniqueTypes = new Dictionary<Type, string>();

        private static readonly Dictionary<string, Type> uniqueTypeNames = new Dictionary<string, Type>();

        private static readonly Dictionary<string, Dictionary<Relationship, HashSet<string>>> relationships = new Dictionary<string, Dictionary<Relationship, HashSet<string>>>();

        private static readonly Dictionary<string, HashSet<string>> related = new Dictionary<string, HashSet<string>>();

        private static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    Usage();
                    return;
                }

                LoadAssembly(args);

                LoadComments();

                typeList = asm.GetExportedTypes().ToList();

                CollectUmlSegments();

                List<List<string>> groups = GetObjectGroups();

                for (var groupId = 1; groupId <= groups.Count; groupId++)
                {
                    OutputUml($"{asmFilename}.ReferenceGroup{groupId}.uml", (typeName) => groups[groupId - 1].Contains(typeName), $"(Reference Group {groupId})");
                }

                OutputUml($"{asmFilename}.NoReferences.uml", SegmentsWithNoReferences, "(No References)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during inspection. {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                PressAnyKey();
            }
        }

        private static List<List<string>> GetObjectGroups()
        {
            var groups = new List<List<string>>();

            // Collect Initial Groups
            foreach (string typeName in uniqueTypeNames.Keys)
            {
                var currentGroup = new List<string>();

                currentGroup.Add(typeName);

                if (related.ContainsKey(typeName))
                {
                    foreach (var relatedTypeName in related[typeName])
                    {
                        currentGroup.Add(relatedTypeName);
                    }
                }

                groups.Add(currentGroup);
            }

            // Merge Groups
            int MaximumPassesWithoutMergers = groups.Count * groups.Count;

            int passesWithoutMergers = 0;

            for (var i = 0; i < groups.Count; i++)
            {
                var groupA = groups[i];

                passesWithoutMergers++;

                if (passesWithoutMergers > MaximumPassesWithoutMergers)
                {
                    break;
                }

                for (var j = 0; groupA.Count > 0 && j < groups.Count; j++)
                {
                    if (i == j) continue;

                    var groupB = groups[j];

                    foreach (string typeNameB in groupB)
                    {
                        if (groupA.Contains(typeNameB))
                        {
                            var newItems = groupB.Except(groupA);

                            groupA.AddRange(newItems);

                            groupB.Clear();

                            passesWithoutMergers = 0;

                            break;
                        }
                    }
                }
            }

            return groups.Where((g) => g.Count > 0).ToList();
        }

        private static bool SegmentsWithReferences(string typeName)
        {
            bool hasReferences = umlSegments[typeName][UmlSegments.Relationships].Count > 0;

            foreach (var otherTypeName in umlSegments.Keys)
            {
                foreach (var relation in umlSegments[otherTypeName][UmlSegments.Relationships])
                {
                    if (relation.Contains(typeName))
                    {
                        return true;
                    }
                }
            }

            return hasReferences;
        }

        private static bool SegmentsWithNoReferences(string typeName)
        {
            return !SegmentsWithReferences(typeName);
        }

        private static void OutputUml(string filename, Func<string, bool> typeNameFilter, string titleNotes = "")
        {
            var sb = new StringBuilder();

            sb.AppendLine("@startUml");

            sb.AppendLine($"title \"{Path.GetFileNameWithoutExtension(asmFilename)} {titleNotes}\"");

            bool HasEntriesBelow(string typeName, UmlSegment currentUmlSegment)
            {
                bool currentFound = false;

                foreach (var umlSegment in UmlSegments.ClassSegments())
                {
                    if (umlSegment.Name == currentUmlSegment.Name)
                    {
                        currentFound = true;
                        continue;
                    }

                    if (currentFound && umlSegments[typeName][umlSegment].Where((s) => s.Trim().Length > 0).Count() > 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var typeName in umlSegments.Keys.Where(typeNameFilter))
            {
                umlSegments[typeName][UmlSegments.Name].ForEach((s) => sb.AppendLine($"{s}"));

                sb.AppendLine("{");

                foreach (var umlSegment in UmlSegments.ClassSegments())
                {
                    if (umlSegments[typeName][umlSegment].Where((s) => s.Trim().Length > 0).Count() > 0)
                    {
                        umlSegments[typeName][umlSegment].ForEach((s) => sb.AppendLine($"{s}"));

                        if (HasEntriesBelow(typeName, umlSegment))
                        {
                            sb.AppendLine(umlSegment.Separator);
                        }
                    }
                }

                sb.AppendLine("}");

                sb.AppendLine();

                umlSegments[typeName][UmlSegments.Relationships].ForEach((s) => sb.AppendLine($"{s}"));

                sb.AppendLine();
            }

            sb.AppendLine("@endUml");

            File.WriteAllText(filename, sb.ToString());
        }

        private static void LoadComments()
        {
            xmlCommentsFilename = Path.ChangeExtension(asmFilename, "xml");

            if (File.Exists(xmlCommentsFilename))
            {
                xmlComments = new XmlDocument();

                xmlComments.Load(xmlCommentsFilename);
            }
        }

        private static void LoadAssembly(string[] args)
        {
            asmFilename = args[0];

            if (!File.Exists(asmFilename))
            {
                Console.WriteLine($"File not found: '{asmFilename}'");
                Console.WriteLine();
                Usage();
                return;
            }

            asm = Assembly.LoadFrom(asmFilename);
        }

        private static void CollectUmlSegments()
        {
            foreach (var type in typeList)
            {
                var typeName = GetTypeNameAndCollectRelationships(type, Relationships.RefersTo);

                InitializeRelationshipDictionary(typeName);

                if (!uniqueTypeNames.TryGetValue(typeName, out _))
                {
                    uniqueTypeNames.Add(typeName, type);
                }

                LoadUmlSegmentDictionary(typeName);

                // Base Class
                GetTypeNameAndCollectRelationships(type.BaseType, Relationships.Extends);

                // Declaring Type
                GetTypeNameAndCollectRelationships(type.DeclaringType, Relationships.Extends);

                // Implemented Interfaces
                foreach (var interfaceType in type.GetInterfaces())
                {
                    GetTypeNameAndCollectRelationships(interfaceType, Relationships.Extends);
                }

                // Name
                umlSegments[typeName][UmlSegments.Name].Add(
                    $"{(type.IsInterface ? "interface" : "class")} \"{typeName}\" {(type.IsSealed ? "<<sealed>>" : "")}");

                // Summary
                umlSegments[typeName][UmlSegments.Summary].Add(GetSummary(type));

                // Events
                foreach (var e in type.GetEvents().OrderBy((info) => info.Name.ToLower()))
                {
                    umlSegments[typeName][UmlSegments.Events].Add($"+ {e.Name}() <<signal>>");
                }

                // Fields
                foreach (var f in type.GetFields().OrderBy((info) => info.Name.ToLower()))
                {
                    var pTypeName = GetTypeNameAndCollectRelationships(f.FieldType, Relationships.ComposedOf);

                    umlSegments[typeName][UmlSegments.Fields].Add($"+ {f.Name} : {pTypeName}");
                }

                // Properties
                foreach (var p in type.GetProperties().OrderBy((info) => info.Name.ToLower()))
                {
                    var getter = (p.GetGetMethod() != null ? " <<get>>" : string.Empty);
                    var setter = (p.GetSetMethod() != null ? " <<set>>" : string.Empty);

                    var pTypeName = GetTypeNameAndCollectRelationships(p.PropertyType, Relationships.ComposedOf);

                    umlSegments[typeName][UmlSegments.Properties].Add($"+ {p.Name} : {pTypeName} {getter}{setter}");
                }

                // Constructors
                foreach (var c in type.GetConstructors())
                {
                    umlSegments[typeName][UmlSegments.Constructors].Add($"+ {typeName}({GetArgumentText(c.GetParameters(), type, Relationships.RefersTo)})");
                }

                // Methods
                foreach (var m in type.GetMethods()
                    .Where((info) => !Regex.IsMatch(info.Name, "get_.*|set_.*"))
                    .OrderBy((info) => info.Name.ToLower()))
                {
                    if (m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
                    {
                        var parameters = m.GetParameters();

                        var extensionParameter = parameters[0];

                        umlSegments[typeName][UmlSegments.Constructors].Add($"+ {GetTypeNameAndCollectRelationships(extensionParameter.ParameterType, Relationships.Extends)}.{m.Name}({GetArgumentText(parameters.Skip(1).ToArray(), type, Relationships.RefersTo)})");
                    }
                    else
                    {
                        umlSegments[typeName][UmlSegments.Constructors].Add($"+ {m.Name}({GetArgumentText(m.GetParameters(), type, Relationships.RefersTo)})");
                    }
                }

                // Direct Relationships
                foreach (var relationship in relationships[typeName].Keys)
                {
                    foreach (var relatedTypeName in relationships[typeName][relationship])
                    {
                        if (typeName == relatedTypeName) continue;

                        if (uniqueTypeNames.ContainsKey(relatedTypeName))
                        {
                            umlSegments[typeName][UmlSegments.Relationships].Add($"\"{typeName}\" {relationship.Arrow} \"{relatedTypeName}\" : {relationship.Label}");
                        }

                        CollectRelated(typeName, relatedTypeName);
                    }
                }
            }

            // Record Relationships
            foreach (var typeName in uniqueTypeNames.Keys)
            {
                // Implied Relationships
                // ---------------------
                // BUG: This loop was added to address a situation where a class with generic parameters obviously related to another
                // class, but no relationship was identified in the class diagram. The issue is likely related to name resolution of
                // the declaring class.
                foreach (var otherTypeName in umlSegments.Keys)
                {
                    if (otherTypeName == typeName) continue;

                    if (typeName.StartsWith($"{otherTypeName}."))
                    {
                        var relation = $"\"{typeName}\" {Relationships.Extends.Arrow} \"{otherTypeName}\" : {Relationships.Extends.Label}";

                        if (!umlSegments[typeName][UmlSegments.Relationships].Contains(relation))
                        {
                            umlSegments[typeName][UmlSegments.Relationships].Add(relation);
                        }

                        CollectRelated(typeName, otherTypeName);
                    }
                }
            }
        }

        private static void CollectRelated(string typeName, string relatedTypeName)
        {
            if (!related.ContainsKey(typeName))
            {
                related.Add(typeName, new HashSet<string>());
            }

            if (!related[typeName].Contains(relatedTypeName))
            {
                related[typeName].Add(relatedTypeName);
            }
        }

        private static string GetArgumentText(ParameterInfo[] parameters, Type type, Relationship relationship)
        {
            const int MaximumArgumentsToDisplay = 5;

            var sb = new StringBuilder();

            var separator = ", ";

            int argumentCount = 1;

            foreach (var parameter in parameters)
            {
                if (sb.Length > 0)
                {
                    sb.Append(separator);
                }

                if (argumentCount > MaximumArgumentsToDisplay)
                {
                    sb.Append("...");
                    break;
                }

                var typeName = GetTypeNameAndCollectRelationships(parameter.ParameterType, relationship);

                if (parameters.Length < MaximumArgumentsToDisplay)
                {
                    sb.Append($"{typeName} {parameter.Name}");
                }
                else
                {
                    sb.Append($"{parameter.Name}");
                }

                argumentCount++;
            }

            return sb.ToString();
        }

        private static void InitializeRelationshipDictionary(string typeName)
        {
            if (!relationships.ContainsKey(typeName))
            {
                relationships.Add(typeName, new Dictionary<Relationship, HashSet<string>>());
            }

            foreach (var relationship in Relationships.All())
            {
                if (!relationships[typeName].ContainsKey(relationship))
                {
                    relationships[typeName].Add(relationship, new HashSet<string>());
                }
            }
        }

        private static string GetTypeNameAndCollectRelationships(Type type, Relationship relationship)
        {
            if (type == null)
            {
                return string.Empty;
            }

            string typeName;

            if (type.DeclaringType != null && uniqueTypes.TryGetValue(type.DeclaringType, out string declaringTypeName))
            {
                typeName = $"{declaringTypeName}.{type.Name}";
            }
            else
            {
                typeName = (type.FullName ?? type.Name).Replace($"{type.Namespace}.", string.Empty).Replace('+', '.');
            }

            var typeArguments = new List<Type>();

            var parentTypeArguments = new HashSet<string>();

            if (type.DeclaringType != null)
            {
                foreach (var declaringType in type.DeclaringType.GetGenericArguments())
                {
                    parentTypeArguments.Add(declaringType.FullName ?? declaringType.Name);
                }
            }

            foreach (var typeArgument in type.GetGenericArguments())
            {
                if (!parentTypeArguments.Contains(typeArgument.FullName ?? typeArgument.Name))
                {
                    typeArguments.Add(typeArgument);
                }
            }

            if (typeArguments.Count > 0)
            {
                typeName = typeName.Replace($"`{typeArguments.Count}", string.Empty);

                var sb = new StringBuilder();

                foreach (var argType in typeArguments)
                {
                    if (sb.Length > 0) sb.Append(", ");

                    sb.Append(GetTypeNameAndCollectRelationships(argType, relationship));
                }

                typeName = $"{typeName}<{sb}>";
            }

            if (!relationships.ContainsKey(typeName))
            {
                relationships.Add(typeName, new Dictionary<Relationship, HashSet<string>>());
            }

            if (!relationships[typeName].TryGetValue(relationship, out _))
            {
                relationships[typeName].Add(relationship, new HashSet<string>());
            }

            if (typeList.Contains(type))
            {
                relationships[typeName][relationship].Add(typeName);
            }

            if (!uniqueTypes.TryGetValue(type, out _))
            {
                uniqueTypes.Add(type, typeName);
            }

            return typeName;
        }

        private static string GetSummary(Type type)
        {
            // XPath query to find the comments for the current type
            string xpathQuery = $"/doc/members/member[starts-with(@name, 'T:{type.FullName}')]/summary";

            // Select the summary node for the current type
            XmlNode summaryNode = xmlComments.SelectSingleNode(xpathQuery);

            // Display the comments if available
            if (summaryNode != null)
            {
                return summaryNode.InnerText.Trim();
            }

            return string.Empty;
        }

        private static void LoadUmlSegmentDictionary(string typeName)
        {
            if (!umlSegments.TryGetValue(typeName, out _))
            {
                umlSegments.Add(typeName, new Dictionary<UmlSegment, List<string>>());
            }

            foreach (var umlSegment in UmlSegments.AllSegments())
            {
                if (!umlSegments[typeName].TryGetValue(umlSegment, out _))
                {
                    umlSegments[typeName].Add(umlSegment, new List<string>());
                }
            }
        }

        private static void Usage()
        {
            Console.WriteLine();
            Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetExecutingAssembly().Location)} <AssemblyPath>");
            Console.WriteLine();
            Console.WriteLine("where <AssemblyPath> is the name of the .NET DLL or EXE assembly to inspect.");
            Console.WriteLine();
        }

        private static void PressAnyKey()
        {
            if (Debugger.IsAttached)
            {
                Console.Write("Press any key to continue...");
                Console.ReadKey();
                Console.WriteLine();
            }
        }
    }
}