using System.Text;
using System.Text.RegularExpressions;
using CppAst;

namespace InteropGen;


class NativeType
{
    public bool IsString => NativeNullableType.Unwrap(this) is NativePointerType { Level: 1 } pt
                            && NativeNullableType.Unwrap(pt.ElementType) is NativePrimitiveType { DotnetType: "sbyte" };
    
    public bool IsGenericDataPointer => NativeNullableType.Unwrap(this) is NativePointerType { Level: 1 } pt
                            && NativeNullableType.Unwrap(pt.ElementType) is NativePrimitiveType { DotnetType: "byte" };
    
    public bool IsVoidPtr => NativeNullableType.Unwrap(this) is NativePointerType { Level: 1 } pt
    && NativeNullableType.Unwrap(pt.ElementType) is NativePrimitiveType { DotnetType: "void" };
}

class NativeNullableType(NativeType inner, bool nullable) : NativeType
{
    public NativeType ElementType => inner;
    public bool Nullable => nullable;
    public static NativeType Unwrap(NativeType nt) => nt is NativeNullableType nnt ? nnt.ElementType : nt;
}

class NativePointerType(NativeType inner, int level) : NativeType
{
    public NativeType ElementType => inner;
    public int Level => level;
}

class NativePrimitiveType(string dotnetType) : NativeType
{
    public string DotnetType => dotnetType;

    public static Dictionary<CppPrimitiveKind, NativePrimitiveType> Map = new()
    {
        
        [CppPrimitiveKind.Void] = new ("void"),
        [CppPrimitiveKind.Char] = new ("sbyte"),
        [CppPrimitiveKind.UnsignedChar] = new ("byte"),
        [CppPrimitiveKind.Short] = new ("short"),
        [CppPrimitiveKind.UnsignedShort] = new ("ushort"),
        [CppPrimitiveKind.Int] = new("int"),
        [CppPrimitiveKind.UnsignedInt] = new("uint"),
        [CppPrimitiveKind.LongLong] = new ("long"),
        [CppPrimitiveKind.UnsignedLongLong] = new ("ulong"),
        [CppPrimitiveKind.Float] = new ("float"),
        [CppPrimitiveKind.Double] = new ("double"),
    };

    public override string ToString() => DotnetType;
}

record NativeVar(string Name, NativeType Type)
{
    public override string ToString() => $"{Name} : {Type}";
}

class NativeStruct(string name) : NativeType
{
    public string Name => name;
    public List<NativeVar> Members { get; set; } = new();
    public override string ToString() => Name;
}

class NativeFixedArray(NativeType elementType, int size) : NativeType
{
    public NativeType ElementType { get; } = elementType;
    public int Size { get; } = size;
    public override string ToString() => $"{ElementType}[{Size}]";
}

class NativeEnum(string name) : NativeType
{
    public string Name => name;
    public List<KeyValuePair<string, int>> Members { get; set; } = new();
    public override string ToString() => Name;
}

class NativeHandle : NativeType
{
    public string Name { get; set; }
    public List<NativeFunction> Methods { get; set; } = new();
    public List<NativeFunction> Factories { get; set; } = new();
    public override string ToString() => Name;
}

class ExternalNativeType(string name) : NativeType
{
    public string Name => name;
}

class NativeFunctionPointer : NativeType
{
    public NativeType ReturnType { get; set; }
    public List<NativeVar> Parameters { get; set; } = new();
}

class NativeNamedFunctionPointer(string name) : NativeFunctionPointer
{
    public override string ToString() => Name;

    public string Name => name;
}

class NativeFunction
{
    public string Name { get; set; }
    public NativeType ReturnType { get; set; }
    public List<NativeVar> Parameters { get; set; }
}

class NativeModel
{
    private const string SystemTypes = @"
    typedef unsigned char       uint8_t;
    typedef signed char         int8_t;
    typedef unsigned short      uint16_t;
    typedef signed short        int16_t;
    typedef unsigned int        uint32_t;
    typedef signed int          int32_t;
    typedef unsigned long long  uint64_t;
    typedef signed long long    int64_t;
    typedef int                 bool;
";

    public List<NativeHandle> Handles { get; } = new();
    public List<NativeStruct> Structs { get; } = new();
    public List<NativeEnum> Enums { get; } = new();
    public List<NativeFunction> Functions { get; } = new();
    public List<NativeFunction> GlobalFunctions { get; } = new();
    public int ImpellerVersion { get; }

    private Dictionary<CppType, NativeType> _typeMap = new();

    private (CppType type, int level) UnwrapPointer(CppType type)
    {
        int level = 0;
        while (type is CppPointerType pt)
        {
            level++;
            type = pt.ElementType;
        }

        return (type, level);
    }
    
    private NativeType MapType(CppType type)
    {
        if (type is CppQualifiedType qt)
            return MapType(qt.ElementType);
        
        if (_typeMap.TryGetValue(type, out var mapped))
            return mapped;
        if (type is CppTypedef td)
            return MapType(td.ElementType);
        if (type is CppPrimitiveType primitiveType)
            return NativePrimitiveType.Map[primitiveType.Kind];

        if (type is CppPointerType { ElementType: CppFunctionType function })
        {
            return new NativeFunctionPointer()
            {
                ReturnType = MapType(function.ReturnType),
                Parameters = function.Parameters.Select(p => new NativeVar(p.Name, MapType(p.Type))).ToList()
            };
        }
        
        if (type is CppPointerType pt)
        {
            var (wrapped, level) = UnwrapPointer(pt);
            return new NativePointerType(MapType(wrapped), level);
        }

        if (type is CppArrayType arrayType)
            return new NativeFixedArray(MapType(arrayType.ElementType), arrayType.Size);


        
        throw new Exception("Unknown type " + type);
    }

    private NativeType MapType(CppType type, ICppAttributeContainer nullableAttributeSource)
    {
        var nullable = nullableAttributeSource.Attributes.Any(x => x.Name == "annotate" && x.Arguments.Contains("nullable"))
            ? (bool?)true
            : nullableAttributeSource.Attributes.Any(x => x.Name == "annotate" && x.Arguments.Contains("notnull"))
                ? false
                : null;
        
        var mappedType = MapType(type);
        if (nullable.HasValue)
        {
            mappedType = new NativeNullableType(mappedType, nullable.Value);
        }

        return mappedType;
    }

    private NativeVar MapVar(ICppAttributeContainer var, string name, CppType type) => new(name, MapType(type, var));

    public NativeModel(CppCompilation compilation, HashSet<string> manualInteropStructs)
    {
        foreach (var td in compilation.Typedefs)
        {
            if (td.ElementType is CppPointerType { ElementType: CppClass pointedClass }
                && pointedClass.Name.StartsWith("Impeller")
                && pointedClass.Name.EndsWith("_"))
            {
                var handle = new NativeHandle()
                {
                    Name = td.Name
                };
                Handles.Add(handle);
                _typeMap[td] = handle;
            }
            else if (td.ElementType is CppPointerType { ElementType: CppFunctionType function })
            {
                _typeMap[td]= new NativeNamedFunctionPointer(td.Name)
                {
                    ReturnType = MapType(function.ReturnType, td),
                    Parameters = function.Parameters.Select(p => MapVar(p, p.Name, p.Type)).ToList()
                };
            }

        }
        
        foreach (var en in compilation.Enums)
        {
            if (en.Name == "NImpellerParser" && en.Items.Any(i => i.Name == "ImpellerVersion"))
            {
                ImpellerVersion = (int)en.Items.First(i => i.Name == "ImpellerVersion").Value;
                continue;
            }
            
            var mapped = new NativeEnum(en.Name)
            {
                Members = en.Items.ToDictionary(m => m.Name, m => (int)m.Value).ToList()
            };
            Enums.Add(mapped);
            _typeMap[en] = mapped;
        }

        foreach (var cppClass in compilation.Classes)
        {
            if (manualInteropStructs.Contains(cppClass.Name))
            {
                _typeMap[cppClass] = new ExternalNativeType(cppClass.Name);
            }
            else if (!cppClass.IsDefinition || Handles.Any(h => h.Name + "_" == cppClass.Name))
            {
                // Ignore
                continue;
            }
            else
            {
                var s = new NativeStruct(cppClass.Name);
                Structs.Add(s);
                _typeMap[cppClass] = s;
            }
        }


        foreach (var cppClass in compilation.Classes)
        {
            if (_typeMap.TryGetValue(cppClass, out var mapped) && mapped is NativeStruct s)
            {
                foreach (var m in cppClass.Fields)
                {
                    //var decl = GetSpan(m.Span);
                    var comment = m.Comment?.ToString() ?? "";
                    s.Members.Add(MapVar(m, m.Name, m.Type));
                }
            }
        }

        foreach (var f in compilation.Functions)
        {
            Functions.Add(new NativeFunction()
            {
                Name = f.Name,
                ReturnType = MapType(f.ReturnType, f),
                Parameters = f.Parameters.Select(p => new NativeVar(p.Name, MapType(p.Type))).ToList()
            });
        }
        
        foreach (var f in Functions)
        {

            if (f.Parameters.FirstOrDefault()?.Type is {} firstParameterType
                && NativeNullableType.Unwrap(firstParameterType) is NativeHandle thisHandle)
            {
                thisHandle.Methods.Add(f);
            }
            else if (NativeNullableType.Unwrap(f.ReturnType) is NativeHandle returnHandle && f.Name.EndsWith("New"))
            {
                returnHandle.Factories.Add(f);
            }
            else
                GlobalFunctions.Add(f);
        }
    }
    
    public static NativeModel Load(string path, HashSet<string> manualInteropStructs)
    {
        var text = File.ReadAllText(path);
        text = Regex.Replace(text, "^#include <.*>$", "", RegexOptions.Multiline);
        // CppAst doesn't expose _Nullable and _NotNull, so we are using comments instead
        text = Regex.Replace(text, "^#define IMPELLER_NULLABLE.*$", "#define IMPELLER_NULLABLE __attribute__((annotate(\"nullable\")))", RegexOptions.Multiline);
        text = Regex.Replace(text, "^#define IMPELLER_NONNULL.*$", "#define IMPELLER_NONNULL __attribute__((annotate(\"notnull\")))", RegexOptions.Multiline);
        //text=text.Replace("IMPELLER_NONNULL", "__attribute__((annotate(\"nullable\")))");
        //text=text.Replace("IMPELLER_NULLABLE", "__attribute__((annotate(\"notnull\")))");

        text += @"
typedef enum NImpellerParser {
  ImpellerVersion = IMPELLER_VERSION
} NImpellerParser;
";
        var cpp = CppParser.Parse(text, new CppParserOptions
        {
            ParserKind = CppParserKind.C,
            ParseSystemIncludes = false,
            TargetCpu = CppTargetCpu.X86_64,
            PreHeaderText = SystemTypes,
            AutoSquashTypedef = true,
            ParseCommentAttribute = true,
            ParseComments = true,
            //ParseTokenAttributes = true
        });
        foreach(var err in cpp.Diagnostics.Messages)
            Console.Error.WriteLine(err.ToString());
        if (cpp.HasErrors)
            throw new Exception("Parse failed");
        return new NativeModel(cpp, manualInteropStructs);

    }
}