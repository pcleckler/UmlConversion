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
        private static readonly Dictionary<Type, Dictionary<RelationshipType, HashSet<Type>>> relationships = new Dictionary<Type, Dictionary<RelationshipType, HashSet<Type>>>();

        private static readonly Dictionary<Type, Dictionary<UmlSegment, List<string>>> umlSegments = new Dictionary<Type, Dictionary<UmlSegment, List<string>>>();

        private static readonly Dictionary<Type, string> uniqueTypes = new Dictionary<Type, string>();

        private static Assembly asm;

        private static string asmFilename;

        private static List<Type> typeList;

        private static XmlDocument xmlComments;

        private static string xmlCommentsFilename;

        internal static void Main(string[] args)
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

                LoadTypes();

                CollectUmlSegments();

                OutputUml();
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

        private static void CollectRelationship(Type type, Type owningType, RelationshipType relationshipType)
        {
            if (!relationships.ContainsKey(owningType))
            {
                relationships.Add(owningType, new Dictionary<RelationshipType, HashSet<Type>>());
            }

            if (!relationships[owningType].TryGetValue(relationshipType, out _))
            {
                relationships[owningType].Add(relationshipType, new HashSet<Type>());
            }

            if (typeList.Contains(type) && type != owningType)
            {
                relationships[owningType][relationshipType].Add(type);
            }
        }

        private static void CollectRelationshipAndUniqueType(Type type, string typeName, Type owningType, RelationshipType relationshipType)
        {
            CollectRelationship(type, owningType, relationshipType);

            CollectUniqueType(type, typeName);
        }

        private static void CollectUmlSegments()
        {
            foreach (var type in typeList)
            {
                var typeName = GetTypeName(type, (discoveredType, discoveredTypeName) =>
                {
                    CollectUniqueType(discoveredType, discoveredTypeName);
                });

                InitializeRelationshipDictionary(type);

                CollectUniqueType(type, typeName);

                LoadUmlSegmentDictionary(type);

                // Base Class
                GetTypeName(type.BaseType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                });

                // Declaring Type
                GetTypeName(type.DeclaringType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                });

                // Implemented Interfaces
                foreach (var interfaceType in type.GetInterfaces())
                {
                    GetTypeName(interfaceType, (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                    });
                }

                // Name
                umlSegments[type][UmlSegmentList.Name].Add(
                    $"{(type.IsInterface ? "interface" : "class")} \"{typeName}\" {(type.IsSealed ? "<<sealed>>" : "")}");

                // TODO: Handle delegates.

                // Summary
                umlSegments[type][UmlSegmentList.Summary].Add(GetSummary(type));

                // Events
                // TODO: Event handling is incomplete.
                foreach (var e in type.GetEvents().OrderBy((info) => info.Name.ToLower()))
                {
                    umlSegments[type][UmlSegmentList.Events].Add($"+ {e.Name}() <<signal>>");
                }

                // Fields
                foreach (var f in type.GetFields().OrderBy((info) => info.Name.ToLower()))
                {
                    var fTypeName = GetTypeName(f.FieldType, (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.ComposedOf);
                    });

                    umlSegments[type][UmlSegmentList.Fields].Add($"+ {f.Name} : {fTypeName}");
                }

                // Properties
                foreach (var p in type.GetProperties().OrderBy((info) => info.Name.ToLower()))
                {
                    var getter = (p.GetGetMethod() != null ? " <<get>>" : string.Empty);
                    var setter = (p.GetSetMethod() != null ? " <<set>>" : string.Empty);

                    var pTypeName = GetTypeName(p.PropertyType, (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.ComposedOf);
                    });

                    umlSegments[type][UmlSegmentList.Properties].Add($"+ {p.Name} : {pTypeName} {getter}{setter}");
                }

                // Constructors
                foreach (var c in type.GetConstructors())
                {
                    var argumentText = GetArgumentText(c.GetParameters(), (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                    });

                    umlSegments[type][UmlSegmentList.Constructors].Add($"+ {typeName}({argumentText})");
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

                        var extensionTypeTypeName = GetTypeName(extensionParameter.ParameterType, (discoveredType, discoveredTypeName) =>
                        {
                            CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                        });

                        var argumentText = GetArgumentText(parameters.Skip(1).ToArray(), (discoveredType, discoveredTypeName) =>
                        {
                            CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                        });

                        umlSegments[type][UmlSegmentList.Constructors].Add($"+ {extensionTypeTypeName}.{m.Name}({argumentText})");
                    }
                    else
                    {
                        var argumentText = GetArgumentText(m.GetParameters(), (discoveredType, discoveredTypeName) =>
                        {
                            CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                        });

                        umlSegments[type][UmlSegmentList.Constructors].Add($"+ {m.Name}({argumentText})");
                    }
                }
            }
        }

        private static void CollectUniqueType(Type type, string typeName)
        {
            if (!uniqueTypes.TryGetValue(type, out _))
            {
                uniqueTypes.Add(type, typeName);
            }
        }

        private static string GetArgumentText(ParameterInfo[] parameters, TypeDiscoveryHandler typeDiscoveryHandler)
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

                var typeName = GetTypeName(parameter.ParameterType, typeDiscoveryHandler);

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

        private static List<HashSet<Type>> GetObjectGroups()
        {
            var groups = new List<HashSet<Type>>();

            // Collect Initial Groups
            foreach (Type type in uniqueTypes.Keys)
            {
                var relationshipsFound = false;

                var currentGroup = new HashSet<Type>();

                if (relationships.ContainsKey(type))
                {
                    foreach (var relationshipType in relationships[type].Keys)
                    {
                        foreach (var relatedType in relationships[type][relationshipType])
                        {
                            if (!currentGroup.Contains(relatedType))
                            {
                                currentGroup.Add(relatedType);
                            }

                            relationshipsFound = true;
                        }
                    }
                }

                if (relationshipsFound)
                {
                    currentGroup.Add(type);

                    groups.Add(currentGroup);
                }
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

                    foreach (Type typeB in groupB)
                    {
                        if (groupA.Contains(typeB))
                        {
                            var newItems = groupB.Except(groupA);

                            foreach (var item in newItems)
                            {
                                groupA.Add(item);
                            }

                            groupB.Clear();

                            passesWithoutMergers = 0;

                            break;
                        }
                    }
                }
            }

            return groups.Where((g) => g.Count > 0).ToList();
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

        private static string GetTypeName(Type type, TypeDiscoveryHandler typeDiscoveryHandler = null)
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

                    sb.Append(GetTypeName(argType, typeDiscoveryHandler));
                }

                typeName = $"{typeName}<{sb}>";
            }

            typeDiscoveryHandler?.Invoke(type, typeName);

            return typeName;
        }

        private static void InitializeRelationshipDictionary(Type type)
        {
            if (!relationships.ContainsKey(type))
            {
                relationships.Add(type, new Dictionary<RelationshipType, HashSet<Type>>());
            }

            foreach (var relationshipType in RelationshipTypeList.All())
            {
                if (!relationships[type].ContainsKey(relationshipType))
                {
                    relationships[type].Add(relationshipType, new HashSet<Type>());
                }
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

        private static void LoadComments()
        {
            xmlCommentsFilename = Path.ChangeExtension(asmFilename, "xml");

            if (File.Exists(xmlCommentsFilename))
            {
                xmlComments = new XmlDocument();

                xmlComments.Load(xmlCommentsFilename);
            }
        }

        private static void LoadTypes()
        {
            typeList = asm.GetExportedTypes().ToList();
        }

        private static void LoadUmlSegmentDictionary(Type type)
        {
            if (!umlSegments.TryGetValue(type, out _))
            {
                umlSegments.Add(type, new Dictionary<UmlSegment, List<string>>());
            }

            foreach (var umlSegment in UmlSegmentList.AllSegments())
            {
                if (!umlSegments[type].TryGetValue(umlSegment, out _))
                {
                    umlSegments[type].Add(umlSegment, new List<string>());
                }
            }
        }

        private static void OutputUml()
        {
            List<HashSet<Type>> groups = GetObjectGroups();

            var baseOutputDirectory = Path.Combine(
                Path.GetDirectoryName(asmFilename),
                "UML");

            if (!Directory.Exists(baseOutputDirectory))
            {
                Directory.CreateDirectory(baseOutputDirectory);
            }

            var baseOutputFilename = Path.Combine(
                baseOutputDirectory,
                Path.GetFileNameWithoutExtension(asmFilename));

            OutputUmlGroup($@"{baseOutputFilename}.All.uml", (typeName) => true, $"(All Exported Types)");

            for (var groupId = 1; groupId <= groups.Count; groupId++)
            {
                OutputUmlGroup($@"{baseOutputFilename}.ReferenceGroup{groupId}.uml", (typeName) => groups[groupId - 1].Contains(typeName), $"(Reference Group {groupId})");
            }

            OutputUmlGroup($@"{baseOutputFilename}.NoReferences.uml", SegmentsWithNoReferences, "(No References)");
        }

        private static void OutputUmlGroup(string filename, Func<Type, bool> typeFilter, string titleNotes = "")
        {
            var sb = new StringBuilder();

            sb.AppendLine("@startUml");

            sb.AppendLine($"title \"{Path.GetFileNameWithoutExtension(asmFilename)} {titleNotes}\"");

            bool HasEntriesBelow(Type type, UmlSegment currentUmlSegment)
            {
                bool currentFound = false;

                foreach (var umlSegment in UmlSegmentList.ClassSegments())
                {
                    if (umlSegment.Name == currentUmlSegment.Name)
                    {
                        currentFound = true;
                        continue;
                    }

                    if (currentFound && umlSegments[type][umlSegment].Where((s) => s.Trim().Length > 0).Count() > 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var type in umlSegments.Keys.Where(typeFilter))
            {
                if (!uniqueTypes.TryGetValue(type, out var typeName)) continue;

                umlSegments[type][UmlSegmentList.Name].ForEach((s) => sb.AppendLine($"{s}"));

                sb.AppendLine("{");

                foreach (var umlSegment in UmlSegmentList.ClassSegments())
                {
                    if (umlSegments[type][umlSegment].Where((s) => s.Trim().Length > 0).Count() > 0)
                    {
                        umlSegments[type][umlSegment].ForEach((s) => sb.AppendLine($"{s}"));

                        if (HasEntriesBelow(type, umlSegment))
                        {
                            sb.AppendLine(umlSegment.Separator);
                        }
                    }
                }

                sb.AppendLine("}");

                sb.AppendLine();

                relationships[type]
                    .ToList()
                    .ForEach((kvp) =>
                    {
                        kvp.Value
                            .ToList()
                            .ForEach((t) =>
                            {
                                if (uniqueTypes.TryGetValue(t, out var relatedTypeName))
                                {
                                    sb.AppendLine($"\"{typeName}\" {kvp.Key.Arrow} \"{relatedTypeName}\" : {kvp.Key.Label}");
                                }
                            });
                    });

                sb.AppendLine();
            }

            sb.AppendLine("@endUml");

            File.WriteAllText(filename, sb.ToString());
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

        private static bool SegmentsWithNoReferences(Type type)
        {
            return !SegmentsWithReferences(type);
        }

        private static bool SegmentsWithReferences(Type type)
        {
            if (relationships.ContainsKey(type))
            {
                foreach (var relationshipType in relationships[type].Keys)
                {
                    if (relationships[type][relationshipType].Count > 0)
                    {
                        return true;
                    }
                }
            }

            foreach (var otherType in relationships.Keys)
            {
                if (otherType == type) continue;

                foreach (var relationshipType in relationships[otherType].Keys)
                {
                    if (relationships[otherType][relationshipType].Contains(type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void Usage()
        {
            Console.WriteLine();
            Console.WriteLine($"Usage: {Path.GetFileName(Assembly.GetExecutingAssembly().Location)} <AssemblyPath>");
            Console.WriteLine();
            Console.WriteLine("where <AssemblyPath> is the name of the .NET DLL or EXE assembly to inspect.");
            Console.WriteLine();
        }
    }
}