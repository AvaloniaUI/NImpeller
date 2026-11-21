namespace InteropGen;

class Generator
{

    static string Pascal(string s)
    {
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // An element count: an integer named "count" or "*_count".
    static bool IsCountParam(NativeVar p) =>
        NativeNullableType.Unwrap(p.Type) is NativePrimitiveType { DotnetType: "uint" or "ulong" or "int" }
        && (p.Name == "count" || p.Name.EndsWith("_count"));

    // A C bool, passed to native code as an int.
    static bool IsBoolParam(NativeVar p) =>
        ReferenceEquals(NativeNullableType.Unwrap(p.Type), NativePrimitiveType.Map[CppAst.CppPrimitiveKind.Bool]);

    // A non-null const pointer to primitives or plain structs, which after a count is an array of that many elements.
    static bool IsArrayParam(NativeVar p, IReadOnlyDictionary<string, string> manualMarshal) =>
        p.Type is not NativeNullableType { Nullable: true }
        && NativeNullableType.Unwrap(p.Type) is NativePointerType { Level: 1, IsConst: true, IsString: false, IsVoidPtr: false } pt
        && NativeNullableType.Unwrap(pt.ElementType) switch
        {
            NativePrimitiveType prim => prim.DotnetType != "void",
            NativeStruct ns => !manualMarshal.ContainsKey(ns.Name),
            _ => false,
        };

    static bool IsOutParam(NativeType type) =>
        NativeNullableType.Unwrap(type) is NativePointerType
        {
            Level: 1, IsConst: false, IsString: false, IsGenericDataPointer: false, IsVoidPtr: false
        } pt
        && NativeNullableType.Unwrap(pt.ElementType) is NativeStruct;

    public static void Generate(NativeModel model, CodeGen gen)
    {
        var manualInteropFunctions = new HashSet<string>()
        {
            // Handles passed as array
            "ImpellerColorSourceCreateFragmentProgramNew",
            "ImpellerImageFilterCreateFragmentProgramNew",
            "ImpellerParagraphBuilderAddText",
            "ImpellerColorSourceCreateLinearGradientNew",
            "ImpellerColorSourceCreateRadialGradientNew",
            "ImpellerColorSourceCreateConicalGradientNew",
            "ImpellerColorSourceCreateSweepGradientNew",
            "ImpellerTypographyContextRegisterFont"
        };
        // Callbacks that native code calls only during the call that receives them. A callback of these types
        // becomes a delegate that is kept alive for the call and passed through its user data.
        var callTimeCallbacks = new HashSet<string>()
        {
            "ImpellerProcAddressCallback",
            "ImpellerVulkanProcAddressCallback"
        };

        var fastFunctions = new HashSet<string>()
        {
            "ImpellerPaintNew",
            "ImpellerPaintSetColor"
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
        gen.Line("using System;");
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


            bool TryMapCommonType(NativeType type, bool allowStrings, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string res)
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
                if (type is NativeNullableType nt)
                {
                    var inner = MapInteropType(nt.ElementType, allowHandles, allowStrings);
                    return nt.Nullable && inner == "string" ? "string?" : inner;
                }
                if (TryMapCommonType(type, allowStrings, out var common))
                    return common;
                if (type is NativeHandle handle)
                {
                    if (!allowHandles)
                        return "global::System.IntPtr";
                    return $"{gns}.{handle.Name}Handle";
                }
                
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

                    // TODO: Experimental
                    if (imp.Name.StartsWith("ImpellerPathBuilder") || imp.Name.EndsWith("Retain") ||
                        imp.Name.EndsWith("Release"))
                        gen.Line("[System.Runtime.InteropServices.SuppressGCTransition]");
                    
                    if ((imp.Name.EndsWith("Release") || imp.Name.EndsWith("Retain")) && imp.Parameters.Count == 1 &&
                        NativeNullableType.Unwrap(imp.Parameters[0].Type) is NativeHandle)
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
            

            static bool IsUserData(NativeVar v) => v.Type.IsVoidPtr && (v.Name == "user_data" || v.Name.EndsWith("_user_data"));

            NativeNamedFunctionPointer? CallTimeCallback(NativeType type) =>
                NativeNullableType.Unwrap(type) is NativeNamedFunctionPointer fp && callTimeCallbacks.Contains(fp.Name)
                    ? fp
                    : null;

            // A struct passed by pointer that carries a call-time callback and its user data.
            NativeStruct? CallbackStruct(NativeVar p) =>
                NativeNullableType.Unwrap(p.Type) is NativePointerType { Level: 1, ElementType: NativeStruct ns }
                && ns.Members.Any(m => CallTimeCallback(m.Type) != null)
                && ns.Members.Any(IsUserData)
                    ? ns
                    : null;

            // Callback arguments and results: pointers pass as IntPtr, which delegates can take.
            string CallbackType(NativeType t) =>
                NativeNullableType.Unwrap(t) is NativePointerType ? "global::System.IntPtr" : MapInteropType(t, false, false);

            string CallbackArgType(NativeVar v, bool strings) =>
                strings && v.Type.IsString ? "string" : CallbackType(v.Type);

            string CallbackDelegate(NativeFunctionPointer fp, bool strings)
            {
                var args = fp.Parameters.Where(a => !IsUserData(a)).Select(a => CallbackArgType(a, strings)).ToList();
                var ret = CallbackType(fp.ReturnType);
                if (ret == "void")
                    return args.Count == 0 ? "global::System.Action" : $"global::System.Action<{string.Join(", ", args)}>";
                return $"global::System.Func<{string.Join(", ", args.Append(ret))}>";
            }

            string CallbackPointer(NativeNamedFunctionPointer fp) =>
                $"(global::System.IntPtr)(delegate* unmanaged[Cdecl]<{string.Join(", ", fp.Parameters.Select(a => CallbackType(a.Type)).Append(CallbackType(fp.ReturnType)))}>)&ImpellerCallbacks.{fp.Name}";

            static bool HasStringArgs(NativeFunctionPointer fp) => fp.Parameters.Any(a => a.Type.IsString);

            var callbackTypes = model.Functions.SelectMany(f => f.Parameters.Select(p => p.Type))
                .Concat(model.Structs.SelectMany(st => st.Members.Select(m => m.Type)))
                .Select(CallTimeCallback).OfType<NativeNamedFunctionPointer>()
                .GroupBy(fp => fp.Name).Select(g => g.First()).OrderBy(fp => fp.Name).ToList();
            using (gen.Line("static unsafe class ImpellerCallbacks").Scope())
            {
                foreach (var fp in callbackTypes)
                {
                    var args = fp.Parameters.Select(a => CallbackType(a.Type) + " " + a.Name);
                    var userData = fp.Parameters.First(IsUserData).Name;
                    var call = $"ImpellerCallbackScope.Get<{CallbackDelegate(fp, false)}>({userData})(" +
                               string.Join(", ", fp.Parameters.Where(a => !IsUserData(a)).Select(a => a.Name)) + ")";
                    gen.Line("[global::System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(global::System.Runtime.CompilerServices.CallConvCdecl)])]");
                    gen.Line($"internal static {CallbackType(fp.ReturnType)} {fp.Name}({string.Join(", ", args)}) => {call};");
                    gen.Line();
                }
            }

            string MapDotnetType(NativeType type, bool allowHandles)
            {
                if (NativeNullableType.Unwrap(type) is NativePointerType { ElementType: NativeStruct ns, Level: 1 } &&
                    manualMarshal.TryGetValue(ns.Name, out var marshalled))
                    return marshalled;

                if (type is NativeNullableType nt)
                {
                    var inner = MapDotnetType(nt.ElementType, allowHandles);
                    // A nullable void* is still a plain IntPtr (IntPtr.Zero is null).
                    return nt.Nullable && inner != "System.IntPtr" ? inner + "?" : inner;
                }
                
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
                            
                            // A mapping is always marshalled with on_release == NULL, which makes
                            // Impeller copy the bytes during the call and never invoke a release
                            // callback. The companion "*release_user_data" baton is unused, so
                            // it is hidden from the public signature and passed as IntPtr.Zero.
                            var hiddenParams = new HashSet<string>();
                            for (var i = 1; i < args.Count; i++)
                            {
                                if (args[i].Type.IsVoidPtr && args[i].Name.EndsWith("release_user_data") &&
                                    NativeNullableType.Unwrap(args[i - 1].Type) is NativePointerType { ElementType: NativeStruct prevNs, Level: 1 } &&
                                    manualMarshal.ContainsKey(prevNs.Name))
                                    hiddenParams.Add(args[i].Name);
                            }

                            // A count followed by array pointers becomes spans of equal length; the count is
                            // their length. arrays maps each array to its count, counts each count to its arrays.
                            var arrays = new Dictionary<string, string>();
                            var counts = new Dictionary<string, List<string>>();
                            for (var i = 0; i < args.Count; i++)
                            {
                                if (!IsCountParam(args[i]))
                                    continue;
                                var group = new List<string>();
                                for (var j = i + 1; j < args.Count && IsArrayParam(args[j], manualMarshal); j++)
                                {
                                    group.Add(args[j].Name);
                                    arrays[args[j].Name] = args[i].Name;
                                }
                                if (group.Count > 0)
                                    counts[args[i].Name] = group;
                            }

                            string ElementType(NativeVar p) =>
                                MapInteropType(((NativePointerType)NativeNullableType.Unwrap(p.Type)).ElementType, false, false);

                            // The API version argument, always the version the bindings were generated from.
                            static bool IsVersionParam(NativeVar p) =>
                                p.Name == "version" && NativeNullableType.Unwrap(p.Type) is NativePrimitiveType { DotnetType: "uint" };

                            // A call-time callback followed by its user data becomes one delegate parameter.
                            var callbacks = new Dictionary<string, NativeNamedFunctionPointer>();
                            var callbackUserData = new HashSet<string>();
                            for (var i = 0; i + 1 < args.Count; i++)
                                if (CallTimeCallback(args[i].Type) is { } fp && IsUserData(args[i + 1]))
                                {
                                    callbacks[args[i].Name] = fp;
                                    callbackUserData.Add(args[i + 1].Name);
                                }

                            // A struct carrying a call-time callback is passed as its other members.
                            var callbackStructs = args.Select(p => (p, ns: CallbackStruct(p)))
                                .Where(x => x.ns != null).ToDictionary(x => x.p.Name, x => x.ns!);

                            // The delegate parameters, (parameter, callback type), in declaration order.
                            var delegateParams = args.SelectMany(p =>
                                    callbackStructs.TryGetValue(p.Name, out var cs)
                                        ? cs.Members.Where(m => CallTimeCallback(m.Type) != null)
                                            .Select(m => (m.Name, CallTimeCallback(m.Type)!))
                                        : callbacks.TryGetValue(p.Name, out var cb)
                                            ? new[] { (p.Name, cb) }
                                            : Array.Empty<(string, NativeNamedFunctionPointer)>())
                                .ToList();

                            IEnumerable<string> ParamDecls(NativeVar p, bool strings)
                            {
                                if (callbackStructs.TryGetValue(p.Name, out var cs))
                                {
                                    foreach (var m in cs.Members.Where(m => !IsUserData(m)))
                                        yield return CallTimeCallback(m.Type) is { } mfp
                                            ? $"{CallbackDelegate(mfp, strings)} {m.Name}"
                                            : $"{(IsBoolParam(m) ? "bool" : MapDotnetType(m.Type, false))} {m.Name}";
                                }
                                else if (callbacks.TryGetValue(p.Name, out var fp))
                                    yield return $"{CallbackDelegate(fp, strings)} {p.Name}";
                                else if (arrays.ContainsKey(p.Name))
                                    yield return $"global::System.ReadOnlySpan<{ElementType(p)}> {p.Name}";
                                else if (IsBoolParam(p))
                                    yield return "bool " + p.Name;
                                else
                                    yield return (IsOutParam(p.Type) ? "out " : "") + MapDotnetType(p.Type, true) + " " + p.Name;
                            }

                            var publicArgs = args.Where(p => !hiddenParams.Contains(p.Name) && !counts.ContainsKey(p.Name) &&
                                                             !IsVersionParam(p) && !callbackUserData.Contains(p.Name)).ToList();
                            var retType = MapDotnetType(f.ReturnType, true);
                            string Decl(bool strings) => (isFactory ? "static " : "") + $"{retType} {name}(" +
                                string.Join(", ", publicArgs.SelectMany(p => ParamDecls(p, strings))) + ")";

                            // Callbacks with string arguments also get an overload that takes them as strings.
                            if (delegateParams.Any(d => HasStringArgs(d.Item2)))
                            {
                                var forwarded = publicArgs.SelectMany(p =>
                                    callbackStructs.TryGetValue(p.Name, out var cs)
                                        ? cs.Members.Where(m => !IsUserData(m)).Select(m => m.Name)
                                        : new[] { p.Name }).Select(n =>
                                {
                                    var d = delegateParams.FirstOrDefault(x => x.Item1 == n);
                                    if (d.Item2 == null || !HasStringArgs(d.Item2))
                                        return n;
                                    var cbArgs = d.Item2.Parameters.Where(a => !IsUserData(a)).ToList();
                                    return "(" + string.Join(", ", cbArgs.Select(a => $"{CallbackType(a.Type)} __{a.Name}")) +
                                           ") => " + n + "(" + string.Join(", ", cbArgs.Select(a => a.Type.IsString
                                               ? $"global::System.Runtime.InteropServices.Marshal.PtrToStringUTF8(__{a.Name})!"
                                               : $"__{a.Name}")) + ")";
                                });
                                gen.Line($"public {Decl(true)} => {name}({string.Join(", ", forwarded)});");
                                gen.Line();
                            }

                            using (gen.Line("public " + Decl(false)).Scope())
                            using (gen.Line("unsafe").Scope())
                            {
                                foreach (var group in counts.Values)
                                    foreach (var other in group.Skip(1))
                                        gen.Line($"if ({other}.Length != {group[0]}.Length) throw new global::System.ArgumentException(\"{other} and {group[0]} must have the same length.\", nameof({other}));");
                                var pins = new Stack<IDisposable>();
                                foreach (var array in args.Where(p => arrays.ContainsKey(p.Name)))
                                    pins.Push(gen.Line($"fixed ({ElementType(array)}* __{array.Name} = {array.Name})").Scope());

                                var isVoidRet = retType == "void";
                                var invocation = "";
                                if (!isVoidRet)
                                    invocation += "var ret = ";
                                invocation += $"UnsafeNativeMethods.{f.Name}(";
                                if (!isFactory)
                                    invocation += "_handle" + (args.Count > 0 ? ", " : "");

                                // Out-parameters can't have their address taken directly (an `out`
                                // parameter is a movable managed reference), so each writes into a
                                // pinned-by-locality local that is copied back after the native call.
                                var outParams = new List<string>();
                                for (var index = 0; index < args.Count; index++)
                                {
                                    var a = args[index];
                                    var argType = NativeNullableType.Unwrap(a.Type);
                                    var argNullable = a.Type is NativeNullableType { Nullable: true };

                                    if (IsVersionParam(a))
                                        invocation += "UnsafeNativeMethods.ImpellerVersion";
                                    else if (callbacks.TryGetValue(a.Name, out var callback))
                                    {
                                        gen.Line($"using var __{a.Name} = new ImpellerCallbackScope({a.Name});");
                                        invocation += CallbackPointer(callback);
                                    }
                                    else if (callbackUserData.Contains(a.Name))
                                        invocation += $"__{args[index - 1].Name}.UserData";
                                    else if (callbackStructs.TryGetValue(a.Name, out var cbStruct))
                                    {
                                        var cbMember = cbStruct.Members.First(m => CallTimeCallback(m.Type) != null);
                                        gen.Line($"using var __{cbMember.Name} = new ImpellerCallbackScope({cbMember.Name});");
                                        var fields = cbStruct.Members.Select(m =>
                                            $"{Pascal(m.Name)} = " + (IsUserData(m) ? $"__{cbMember.Name}.UserData"
                                                : CallTimeCallback(m.Type) is { } mfp ? CallbackPointer(mfp)
                                                : IsBoolParam(m) ? $"{m.Name} ? 1 : 0"
                                                : m.Name));
                                        gen.Line($"var __{a.Name} = new {gns}.{cbStruct.Name} {{ {string.Join(", ", fields)} }};");
                                        invocation += $"&__{a.Name}";
                                    }
                                    else if (hiddenParams.Contains(a.Name))
                                        invocation += "global::System.IntPtr.Zero";
                                    else if (counts.TryGetValue(a.Name, out var countedArrays))
                                        invocation += $"({MapInteropType(a.Type, false, false)}){countedArrays[0]}.Length";
                                    else if (arrays.ContainsKey(a.Name))
                                        invocation += $"__{a.Name}";
                                    else if (IsBoolParam(a))
                                        invocation += $"({a.Name} ? 1 : 0)";
                                    else if (argType is NativeHandle argHandle)
                                        // An invalid SafeHandle marshals as nullptr.
                                        invocation += argNullable
                                            ? $"({a.Name}?.Handle ?? new {argHandle.Name}Handle())"
                                            : $"{a.Name}.Handle";
                                    else if (argType is NativePointerType { ElementType: NativeStruct ns, Level: 1 } &&
                                             manualMarshal.ContainsKey(ns.Name))
                                    {
                                        gen.Line($"using var __marshal_{a.Name} = {ns.Name}.Marshal({a.Name});");
                                        invocation += $"__marshal_{a.Name}.Value";
                                    }
                                    else if (IsOutParam(a.Type))
                                    {
                                        var elemType = MapDotnetType(
                                            ((NativePointerType)NativeNullableType.Unwrap(a.Type)).ElementType, false);
                                        gen.Line($"{elemType} __{a.Name};");
                                        invocation += $"&__{a.Name}";
                                        outParams.Add(a.Name);
                                    }
                                    else if (argType is NativePointerType { IsString: false, Level: 1, IsGenericDataPointer: false, IsVoidPtr: false } structPtr)
                                    {
                                        if (argNullable)
                                        {
                                            var elemType = MapDotnetType(structPtr.ElementType, false);
                                            gen.Line($"{elemType} __{a.Name} = {a.Name}.GetValueOrDefault();");
                                            invocation += $"({a.Name}.HasValue ? &__{a.Name} : null)";
                                        }
                                        else
                                            invocation += $"&{a.Name}";
                                    }
                                    else
                                        invocation += a.Name;

                                    if (index != args.Count - 1)
                                        invocation += ", ";
                                }

                                invocation += ");";
                                gen.Line(invocation);

                                foreach (var outParam in outParams)
                                    gen.Line($"{outParam} = __{outParam};");

                                void GenerateHandleReturn(NativeHandle nativeHandleType)
                                {
                                    // Ownership follows the "New" naming rule, NOT the method/factory
                                    // classification: a New-suffixed function whose first parameter is a
                                    // handle is modeled as an instance method (isFactory == false) but still
                                    // returns a freshly-created, caller-owned handle. Using isFactory here
                                    // would emit RetainFromNative for those and over-retain (leak).
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
                                        // Mirror GenerateHandleReturn's New-based branch: New-suffixed
                                        // functions return a SafeHandle from P/Invoke, where a native nullptr is an
                                        // invalid handle; other handle-returning functions return a raw IntPtr
                                        // (compare to IntPtr.Zero).
                                        if (f.Name.EndsWith("New"))
                                            gen.Line("if(ret.IsInvalid) { ret.Dispose(); return null; }");
                                        else
                                            gen.Line("if(ret == global::System.IntPtr.Zero) return null;");
                                        GenerateHandleReturn(nativeHandleType);
                                    }
                                    else
                                        gen.Line("return ret;");
                                }

                                while (pins.Count > 0)
                                    pins.Pop().Dispose();
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