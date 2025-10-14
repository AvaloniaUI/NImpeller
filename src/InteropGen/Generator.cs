namespace InteropGen;

class Generator
{

    static string Pascal(string s)
    {
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
    public static void Generate(NativeModel model, CodeGen gen)
    {
        var manualInteropFunctions = new HashSet<string>()
        {
            // Handles passed as array
            "ImpellerColorSourceCreateFragmentProgramNew",
            "ImpellerImageFilterCreateFragmentProgramNew",
            "ImpellerContextCreateOpenGLESNew",
            "ImpellerParagraphBuilderAddText",
            "ImpellerContextCreateMetalNew",
            "ImpellerContextCreateVulkanNew"
        };

        var manualMarshal = new Dictionary<string, string>()
        {
            { "ImpellerMapping", "IImpellerUnmanagedMemory" }
        };

        var recordStructs = new HashSet<string>()
        {
            "ImpellerISize", "ImpellerRect", "ImpellerColor"
        };

        var manualStructs = new HashSet<string>()
        {
            "ImpellerMatrix"
        };
        
        
        
        var gns = "global::NImpeller";
        gen.Line("#nullable enable");
        using (gen.Line("namespace NImpeller").Scope())
        {
            foreach (var nEnum in model.Enums)
            {
                using (gen.Line($"public enum {nEnum}").Scope())
                {
                    foreach (var m in nEnum.Members)
                        gen.Line($"{m.Key} = {m.Value},");
                }
            }
            
            foreach (var handle in model.Handles)
            {
                using (gen.Line("internal class " + handle.Name + "Handle : ImpellerHandle").Scope())
                {
                    foreach(var suffix in new[]{"Retain","Release"})
                        using (gen.Line($"private protected override void Unsafe{suffix}()").Scope())
                            gen.Line($"UnsafeNativeMethods.{handle.Name}{suffix}(handle);");
                }
            }


            bool TryMapCommonType(NativeType type, bool allowStrings, out string res)
            {
                string? Map()
                {
                    if (type.IsString && allowStrings)
                        return "string";
                    if (type.IsVoidPtr)
                        return "System.IntPtr";
                    if (type is NativePrimitiveType prim)
                        return prim.DotnetType;
                    if (type is NativeStruct ns)
                        return $"{gns}.{ns.Name}";
                    if (type is NativeEnum ne)
                        return $"{gns}.{ne.Name}";
                    return null;
                }

                res = Map();
                return res != null;
            }

            string MapInteropType(NativeType type, bool allowHandles, bool allowStrings)
            {
                if (TryMapCommonType(type, allowStrings, out var common))
                    return common;
                if (type is NativeHandle handle)
                {
                    if (!allowHandles)
                        return "global::System.IntPtr";
                    return $"{gns}.{handle.Name}Handle";
                }
                if (type is NativeNullableType nt)
                    return MapInteropType(nt.ElementType, allowHandles, allowStrings); //TODO?
                
                if (type is NativePointerType pt)
                {
                    if(allowStrings && pt.IsString)
                        return "string";
                    return MapInteropType(pt.ElementType, false, false) + new string('*', pt.Level);
                }

                if (type is NativeFunctionPointer)
                    return "IntPtr"; //TODO
                
                
                throw new InvalidProgramException("Unknown type " + type);
            }
            
            foreach (var s in model.Structs)
            {
                if(manualStructs.Contains(s.Name))
                    continue;
                var access = manualMarshal.ContainsKey(s.Name)
                    ? "internal"
                    : "public";
                var record = recordStructs.Contains(s.Name) ? "record " : "";
                using (gen.Line($"{access} unsafe partial {record} struct {s}").Scope())
                {
                    foreach (var m in s.Members)
                    {
                        if (m.Type is NativeFixedArray arr)
                        {
                            var arrType = MapInteropType(arr.ElementType, false, false);
                            gen.Line("public fixed " + arrType + " " + m.Name + "[" + arr.Size + "];");
                        }
                        else
                        {
                            var t = MapInteropType(m.Type, false, false);
                            using (gen.Line($"private {t} _{m.Name};")
                                       .Line($"public {t} {Pascal(m.Name)}")
                                       .Scope())
                            {
                                gen.Line("readonly get => _" + m.Name + ";");
                                gen.Line("set => _" + m.Name + " = value;");
                            }
                        }
                    }
                }
            }
            
            using (gen.Line("static unsafe partial class UnsafeNativeMethods").Line("{").Tab("}"))
            {
                gen.Line($"public const uint ImpellerVersion = {model.ImpellerVersion};").Line();
                
                foreach (var imp in model.Functions)
                {
                    gen.Line(
                            "[System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]")
                        .Line(
                            "[System.Runtime.InteropServices.LibraryImport(\"impeller\", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf8)]");
                    if ((imp.Name.EndsWith("Release") || imp.Name.EndsWith("Retain")) && imp.Parameters.Count == 1 &&
                        imp.Parameters[0].Type is NativeHandle)
                    {
                        gen.Line($"public static partial void {imp.Name}(IntPtr handle);");
                    }
                    else
                    {
                        var line = "public static partial " + MapInteropType(imp.ReturnType, imp.Name.EndsWith("New"),
                                                                true)
                                                            + " " + imp.Name + "(" + string.Join(", ",
                                                                imp.Parameters.Select(p =>
                                                                    MapInteropType(p.Type, true, true) + " " +
                                                                    p.Name)) + ");";
                        gen.Line(line);
                    }

                    gen.Line();
                }
            }
            

            string MapDotnetType(NativeType type, bool allowHandles)
            {
                if (type is NativePointerType { ElementType: NativeStruct ns, Level: 1 } &&
                    manualMarshal.TryGetValue(ns.Name, out var marshalled))
                    return marshalled;
                
                if (TryMapCommonType(type, true, out var common))
                    return common;
                
                if (type is NativeHandle handle)
                {
                    if (!allowHandles)
                        throw new UseManualInteropException();
                    return $"{gns}.{handle.Name}";
                }
                if(type.IsGenericDataPointer)
                    throw new UseManualInteropException();
                if (type is NativeNullableType nt)
                    return MapDotnetType(nt.ElementType, allowHandles) + "?";
                if (type is NativePointerType pt)
                {
                    if (pt.Level != 1)
                        throw new UseManualInteropException();
                    if(pt.IsString)
                        return "string";
                    return MapDotnetType(pt.ElementType, false);
                }

                if (type is NativeFunctionPointer)
                    throw new UseManualInteropException();

                
                throw new InvalidProgramException("Unknown type " + type);
            }
            
            foreach (var h in model.Handles)
            {
                using (gen.Line($"public partial class {h.Name} : IDisposable").Scope())
                {
                    gen.Line($"private {h.Name}Handle _handle;");
                    gen.Line($"internal {h.Name}Handle Handle => _handle;");

                    using (gen.Line($"internal {h.Name}({h.Name}Handle handle)").Scope())
                        gen.Line("_handle = handle;");

                    var funcs = h.Methods.Select(m => (m, false))
                        .Concat(h.Factories.Select(m => (m, true)));
                    foreach (var (f, isFactory) in funcs)
                    {
                        try
                        {
                            if (manualInteropFunctions.Contains(f.Name))
                                continue;

                            if (f.Name.EndsWith("Retain") || f.Name.EndsWith("Release"))
                                continue;

                            string name = f.Name;
                            if (name.StartsWith(h.Name))
                                name = name.Substring(h.Name.Length);
                            else if (name.StartsWith("Impeller"))
                                name = name.Substring("Impeller".Length);


                            var args = f.Parameters.ToList();
                            if (!isFactory)
                                args.RemoveAt(0);
                            
                            var retType = MapDotnetType(f.ReturnType, true);
                            var decl = $"{retType} {name}(" +
                                       string.Join(", ", args.Select(p => MapDotnetType(p.Type, true) + " " + p.Name)) +
                                       ")";
                            if (isFactory)
                                decl = "static " + decl;
                            using (gen.Line("public " + decl).Scope())
                            using (gen.Line("unsafe").Scope())
                            {
                                var isVoidRet = retType == "void";
                                var invocation = "";
                                if (!isVoidRet)
                                    invocation += "var ret = ";
                                invocation += $"UnsafeNativeMethods.{f.Name}(";
                                if (!isFactory)
                                    invocation += "_handle" + (args.Count > 0 ? ", " : "");

                                
                                for (var index = 0; index < args.Count; index++)
                                {
                                    var a = args[index];

                                    if (a.Type is NativeHandle)
                                        invocation += $"{a.Name}.Handle";
                                    else if (a.Type is NativePointerType { ElementType: NativeStruct ns, Level: 1 } &&
                                             manualMarshal.ContainsKey(ns.Name))
                                    {
                                        gen.Line($"using var __marshal_{a.Name} = {ns.Name}.Marshal({a.Name});");
                                        invocation += $"__marshal_{a.Name}.Value";
                                    }
                                    else if (a.Type is NativePointerType { IsString: false, Level: 1, IsGenericDataPointer: false, IsVoidPtr: false })
                                        invocation += $"&{a.Name}";
                                    else
                                        invocation += a.Name;

                                    if (index != args.Count - 1)
                                        invocation += ", ";
                                }

                                invocation += ");";
                                gen.Line(invocation);

                                void GenerateHandleReturn(NativeHandle nativeHandleType)
                                {
                                    if (f.Name.EndsWith("New"))
                                        gen.Line($"return new {nativeHandleType.Name}(ret);");
                                    else
                                        gen.Line(
                                            $"return new {nativeHandleType.Name}(ImpellerHandle.RetainFromNative<{nativeHandleType.Name}Handle>(ret));");
                                }
                                if (!isVoidRet)
                                {
                                    if (f.ReturnType is NativeHandle nativeHandle)
                                        GenerateHandleReturn(nativeHandle);
                                    else if(f.ReturnType is NativeNullableType { ElementType: NativeHandle nativeHandleType})
                                    {
                                        gen.Line("if(ret == null) return null;");
                                        GenerateHandleReturn(nativeHandleType);
                                    }
                                    else
                                        gen.Line("return ret;");
                                }
                            }
                        }
                        catch (UseManualInteropException)
                        {
                            Console.Error.WriteLine($"{f.Name} needs manual interop but is not marked as such");
                        }

                    }

                    gen.Line("public void Dispose() => Handle?.Dispose();");



                }
            }
            
            
        }

        

    }

    class UseManualInteropException() : Exception("Use manual interop for this")
    {
        
    }
}