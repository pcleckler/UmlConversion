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
        private const string NoReferencesGroupId = "No References";
        private const string ReferencesGroupIdPrefix = "Reference Group";

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

                if (!LoadAssembly(args))
                {
                    Usage();
                    return;
                }

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

        private static void CollectConstructorsUml(Type type, string typeName)
        {
            foreach (var c in type.GetConstructors())
            {
                var argumentText = GetArgumentText(c.GetParameters(), (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                });

                umlSegments[type][UmlSegmentList.Constructors].Add($"+ {GetRelativeTypeName(type, type, typeName)}({argumentText})");
            }
        }

        private static void CollectEventsUml(Type type)
        {
            foreach (var e in type.GetEvents()
                .Where((info) => info.DeclaringType == type)
                .OrderBy((info) => info.Name.ToLower()))
            {
                var m = e.GetAddMethod();

                var eventTypeName = GetRelativeTypeName(type, e.EventHandlerType, GetTypeName(e.EventHandlerType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.ComposedOf);
                }));

                umlSegments[type][UmlSegmentList.Events].Add($"+ {e.Name} : {eventTypeName} <<signal>>");
            }
        }

        private static void CollectFieldsUml(Type type)
        {
            foreach (var f in type.GetFields()
                .Where((info) => info.DeclaringType == type)
                .OrderBy((info) => info.Name.ToLower()))
            {
                var fTypeName = GetTypeName(f.FieldType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.ComposedOf);
                });

                umlSegments[type][UmlSegmentList.Fields].Add($"+ {f.Name} : {GetRelativeTypeName(type, f.FieldType, fTypeName)}");
            }
        }

        private static void CollectMethodsUml(Type type)
        {
            foreach (var m in type.GetMethods()
                                    .Where((info) => !Regex.IsMatch(info.Name, "get_.*|set_.*|add_.*|remove_.*") && info.DeclaringType == type)
                                    .OrderBy((info) => info.Name.ToLower()))
            {
                var returnTypeName = GetRelativeTypeName(type, m.ReturnType, GetTypeName(m.ReturnType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                }));

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

                    umlSegments[type][UmlSegmentList.Methods].Add($"+ {extensionTypeTypeName}.{m.Name}({argumentText}) : {returnTypeName}");
                }
                else
                {
                    var argumentText = GetArgumentText(m.GetParameters(), (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                    });

                    umlSegments[type][UmlSegmentList.Methods].Add($"+ {m.Name}({argumentText}) : {returnTypeName}");
                }
            }
        }

        private static void CollectPropertiesUml(Type type)
        {
            foreach (var p in type.GetProperties()
                .Where((info) => info.DeclaringType == type)
                .OrderBy((info) => info.Name.ToLower()))
            {
                var getter = (p.GetGetMethod() != null ? " <<get>>" : string.Empty);
                var setter = (p.GetSetMethod() != null ? " <<set>>" : string.Empty);

                var pTypeName = GetTypeName(p.PropertyType, (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.ComposedOf);
                });

                umlSegments[type][UmlSegmentList.Properties].Add($"+ {p.Name} : {GetRelativeTypeName(type, p.PropertyType, pTypeName)} {getter}{setter}");
            }
        }

        private static void CollectRelationship(Type type, Type owningType, RelationshipType relationshipType)
        {
            if (!relationships.TryGetValue(owningType, out var relationshipTypes))
            {
                relationshipTypes = new Dictionary<RelationshipType, HashSet<Type>>();

                relationships.Add(owningType, relationshipTypes);
            }

            if (!relationshipTypes.TryGetValue(relationshipType, out _))
            {
                relationshipTypes.Add(relationshipType, new HashSet<Type>());
            }

            if (typeList.Contains(type) && type != owningType)
            {
                relationshipTypes[relationshipType].Add(type);
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
                var derivedInterfaces = type.GetInterfaces();

                var baseInterfaces = Array.Empty<Type>();

                if (type.BaseType != null)
                {
                    baseInterfaces = type.BaseType.GetInterfaces();
                }

                foreach (var interfaceType in derivedInterfaces.Except(baseInterfaces))
                {
                    GetTypeName(interfaceType, (discoveredType, discoveredTypeName) =>
                    {
                        CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                    });
                }

                // Name
                umlSegments[type][UmlSegmentList.Name].Add(
                    $"{GetUmlType(type)} \"{typeName}\" {GetNameAnnotations(type)}");

                // Summary
                umlSegments[type][UmlSegmentList.Summary].Add(GetSummary(type));

                // Delegates Special Case
                if (type.IsSubclassOf(typeof(System.MulticastDelegate)))
                {
                    var m = type.GetMethod("Invoke");

                    if (m != null)
                    {
                        var argumentText = GetArgumentText(m.GetParameters(), (discoveredType, discoveredTypeName) =>
                        {
                            CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.References);
                        });

                        umlSegments[type][UmlSegmentList.Methods].Add($"+ {GetRelativeTypeName(type, type, typeName)}({argumentText})");
                    }
                }
                else if (type.IsEnum)
                {
                    foreach (var value in Enum.GetValues(type))
                    {
                        umlSegments[type][UmlSegmentList.Fields].Add($"+ {Enum.GetName(type, value)} = {Convert.ToDouble(value):#,##0}");
                    }
                }
                else if (type.IsValueType)
                {
                    CollectFieldsUml(type);
                }
                else
                {
                    CollectEventsUml(type);

                    CollectFieldsUml(type);

                    CollectPropertiesUml(type);

                    CollectConstructorsUml(type, typeName);

                    CollectMethodsUml(type);
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

        private static string GetNameAnnotations(Type type)
        {
            var sb = new StringBuilder();

            if (type.IsEnum)
            {
                var enumType = GetTypeName(type.GetEnumUnderlyingType(), (discoveredType, discoveredTypeName) =>
                {
                    CollectRelationshipAndUniqueType(discoveredType, discoveredTypeName, type, RelationshipTypeList.Extends);
                });

                sb.Append($" <<{enumType}>> ");
            }

            sb.Append(type.IsSealed ? " <<sealed>> " : "");

            return sb.ToString();
        }

        private static Dictionary<string, HashSet<Type>> GetObjectGroups()
        {
            var groups = new Dictionary<string, HashSet<Type>>();

            var noReferencesGroup = new HashSet<Type>();

            // Collect Initial Groups
            foreach (Type type in uniqueTypes.Keys)
            {
                var relationshipsFound = false;

                var currentGroup = new HashSet<Type>();

                if (relationships.TryGetValue(type, out var relationshipTypes))
                {
                    foreach (var relationshipType in relationshipTypes.Keys)
                    {
                        foreach (var relatedType in relationshipTypes[relationshipType])
                        {
                            currentGroup.Add(relatedType);

                            relationshipsFound = true;
                        }
                    }
                }

                if (relationshipsFound)
                {
                    currentGroup.Add(type);

                    // The group will be labeled later, so a GUID will do now.
                    groups.Add(Guid.NewGuid().ToString(), currentGroup);
                }
                else
                {
                    if (typeList.Contains(type) && !TypeHasReferences(type))
                    {
                        noReferencesGroup.Add(type);
                    }
                }
            }

            // Merge Groups
            int MaximumPassesWithoutMergers = groups.Count * groups.Count;

            int passesWithoutMergers = 0;

            foreach (var groupId in groups.Keys)
            {
                var groupA = groups[groupId];

                passesWithoutMergers++;

                if (passesWithoutMergers > MaximumPassesWithoutMergers)
                {
                    break;
                }

                foreach (var subGroupId in groups.Keys)
                {
                    if (groupId == subGroupId) continue;

                    var groupB = groups[subGroupId];

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

            // Renumber groups and remove Empty Groups
            var groupdIdList = groups.Keys;

            var coalescedGroups = new Dictionary<string, HashSet<Type>>();

            var groupIndex = 1;

            foreach (var groupId in groupdIdList)
            {
                if (groups[groupId].Count > 0)
                {
                    coalescedGroups.Add($"{ReferencesGroupIdPrefix} {groupIndex++}", groups[groupId]);
                }
            }

            // Append no references group if needed
            if (noReferencesGroup.Count > 0)
            {
                coalescedGroups.Add(NoReferencesGroupId, noReferencesGroup);
            }

            return coalescedGroups;
        }

        private static string GetRelativeTypeName(Type owningType, Type type, string typeName)
        {
            if (type.Namespace == owningType.Namespace)
            {
                return typeName.Replace($"{owningType.Namespace}.", string.Empty);
            }

            return typeName;
        }

        private static string GetSummary(Type type)
        {
            // XPath query to find the comments for the current type
            string xpathQuery = $"/doc/members/member[starts-with(@name, 'T:{type.FullName}')]/summary";

            if (xmlComments != null)
            {
                // Select the summary node for the current type
                XmlNode summaryNode = xmlComments.SelectSingleNode(xpathQuery);

                // Display the comments if available
                if (summaryNode != null)
                {
                    // TODO: Summary extraction is incomplete. Output will include eliminate tags (references).
                    return summaryNode.InnerText.Trim();
                }
            }

            return string.Empty;
        }

        private static string GetTypeName(Type type, TypeDiscoveryHandler typeDiscoveryHandler = null)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsGenericType)
            {
                Type genericType = type.GetGenericTypeDefinition();

                if (genericType != type)
                {
                    GetTypeName(genericType, typeDiscoveryHandler);
                }
            }

            string typeName = (type.FullName ?? type.Name);

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
                foreach (var argType in typeArguments)
                {
                    GetTypeName(argType, typeDiscoveryHandler);
                }
            }

            typeDiscoveryHandler?.Invoke(type, typeName);

            return typeName;
        }

        private static string GetUmlType(Type type)
        {
            var umlType = "class";

            if (type.IsAbstract)
            {
                umlType = "abstract class";
            }
            else if (type.IsInterface)
            {
                umlType = "interface";
            }
            else if (type.IsEnum)
            {
                umlType = "enum";
            }
            else if (type.IsValueType)
            {
                umlType = "struct";
            }

            return umlType;
        }

        private static void InitializeRelationshipDictionary(Type type)
        {
            if (!relationships.TryGetValue(type, out var relationshipTypes))
            {
                relationshipTypes = new Dictionary<RelationshipType, HashSet<Type>>();

                relationships.Add(type, relationshipTypes);
            }

            foreach (var relationshipType in RelationshipTypeList.All())
            {
                if (!relationshipTypes.TryGetValue(relationshipType, out _))
                {
                    relationshipTypes.Add(relationshipType, new HashSet<Type>());
                }
            }
        }

        private static bool LoadAssembly(string[] args)
        {
            asmFilename = args[0];

            if (!File.Exists(asmFilename))
            {
                Console.WriteLine($"File not found: '{asmFilename}'");
                Console.WriteLine();

                return false;
            }

            asm = Assembly.LoadFrom(asmFilename);

            return true;
        }

        private static void LoadComments()
        {
            xmlCommentsFilename = Path.ChangeExtension(asmFilename, "xml");

            if (!File.Exists(xmlCommentsFilename))
            {
                Console.WriteLine($"No comment file found ('{xmlCommentsFilename}'). No summaries will be extracted.");
            }
            else
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

            Dictionary<string, HashSet<Type>> groups = GetObjectGroups();

            if (groups.Count > 1)
            {
                foreach (var groupId in groups.Keys)
                {
                    OutputUmlGroup($@"{baseOutputFilename}.{groupId.Replace(" ", string.Empty)}.uml", (type) => groups[groupId].Contains(type), $"({groupId})");
                }
            }
        }

        private static void OutputUmlGroup(string filename, Func<Type, bool> typeFilter, string titleNotes = "")
        {
            // Sort structs and enums first, to ensure they appear in the UML early. This overcomes an issue
            // with PlantUML where structs created at the end of the UML generated an "already exists" error.
            var typesToOutput = umlSegments.Keys
                .Where(typeFilter)
                .OrderBy((type) =>
                {
                    if (type.IsValueType)
                    {
                        return -1000;
                    }
                    else if (type.IsEnum)
                    {
                        return -900;
                    }

                    return 1;
                });

            if (!typesToOutput.Any()) return;

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

                    if (currentFound && umlSegments[type][umlSegment].Where((s) => s.Trim().Length > 0).Any())
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var type in typesToOutput)
            {
                if (!uniqueTypes.TryGetValue(type, out var typeName)) continue;

                umlSegments[type][UmlSegmentList.Name].ForEach((s) => sb.AppendLine($"{s}"));

                sb.AppendLine("{");

                foreach (var umlSegment in UmlSegmentList.ClassSegments())
                {
                    if (umlSegments[type][umlSegment].Where((s) => s.Trim().Length > 0).Any())
                    {
                        sb.AppendLine($"<i>{umlSegment.Name}</i>");

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

        private static bool TypeHasReferences(Type type)
        {
            if (relationships.TryGetValue(type, out var relationshipTypes))
            {
                foreach (var relationshipType in relationshipTypes.Keys)
                {
                    if (relationshipTypes[relationshipType].Count > 0)
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