using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Acornima;
using Acornima.Ast;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace ThorVG;

// The numeric helpers are compiler bindings, not JavaScript global properties.
// A NUL cannot occur in a parsed JS identifier. Only the generated AST can name
// these bindings, so source code cannot overwrite or enumerate them.
internal static class LottieExpressionRuntime
{
    internal static readonly string[] Roles = ["float", "number", "update", "update_strict", "update_binding", "assign", "assign_strict", "binary", "normalize_error", "store"];
    private static readonly ConditionalWeakTable<Engine, Bindings> Engines = new();
    private static readonly ConstructorInfo PreparedConstructor = FindPreparedConstructor();
    private static readonly FieldInfo EventLoopField = FindEventLoopField();
    private static readonly FieldInfo WaitingThreadField = FindWaitingThreadField();
    private static readonly ParserOptions UserOptions =
        Engine.PrepareScript("", options: new ScriptPreparationOptions { FoldConstants = false, StaticAnalysis = false }).ParserOptions!
        with { OnNode = RewriteParsedScript };
    private static readonly ParserOptions PlainOptions = UserOptions with { OnNode = null };

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicConstructors, typeof(Prepared<Script>))]
    private static ConstructorInfo FindPreparedConstructor() =>
        typeof(Prepared<Script>).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(Script), typeof(ParserOptions), typeof(ReferencedGlobals)], null)
        ?? throw new MissingMethodException("The pinned Jint Prepared constructor is unavailable.");

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(Engine))]
    private static FieldInfo FindEventLoopField() =>
        typeof(Engine).GetField("_eventLoop", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("The pinned Jint event loop is unavailable.");

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, "Jint.Runtime.EventLoop", "Jint")]
    private static FieldInfo FindWaitingThreadField() =>
        EventLoopField.FieldType.GetField("_waitingThreadId", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("The pinned Jint event-loop host configuration is unavailable.");

    internal static Engine CreateEngine()
    {
        var bindings = new Bindings();
        var engine = new Engine(options =>
        {
            options.Strict(false);
            options.SetReferencesResolver(bindings, ReferenceResolverInterests.UnresolvableReference);
        });
        // ThorVG calls jerry_eval, but never jerry_run_jobs. Promise executors
        // therefore run synchronously while reactions stay queued until the
        // interpreter is destroyed. Jint normally drains after each script.
        // Reserve its host pump for thread zero (no managed thread has that
        // ID), retaining the real Promise states/queue without executing jobs.
        WaitingThreadField.SetValue(EventLoopField.GetValue(engine), 0);
        Engines.Add(engine, bindings);
        return engine;
    }

    internal static void SetHelper(Engine engine, string name, JsValue value)
    {
        const string prefix = "$__tvg_";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException(nameof(name));
        Engines.GetValue(engine, _ => throw new InvalidOperationException()).Values[PrivateName(name[prefix.Length..])] = value;
    }

    internal static Prepared<Script> Prepare(string source)
    {
        var script = new Parser(UserOptions).ParseScript(source);
        return CreatePrepared(script);
    }

    // Owned installation scripts use fixed aliases, but their bindings are
    // lowered before evaluation and never inserted into the global object.
    internal static JsValue EvaluateOwned(Engine engine, string source)
    {
        var script = new Parser(PlainOptions).ParseScript(source);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string role in Roles) aliases.Add("$__tvg_" + role, PrivateName(role));
        var rewritten = (Script)new RenameBindings(aliases).Visit(script)!;
        return engine.Evaluate(CreatePrepared(rewritten));
    }

    private static Prepared<Script> CreatePrepared(Script script) =>
        (Prepared<Script>)PreparedConstructor.Invoke([script, UserOptions, null]);

    private static string PrivateName(string role) => "\0thorvg/" + role;

    private static void RewriteParsedScript(Node node, in OnNodeContext context)
    {
        if (node is not Script script) return;
        // Parser callbacks own this newly parsed AST exclusively. The second
        // parse has no callback, avoiding recursive compilation of generated
        // helpers. Bindings stay disjoint from all original identifier names.
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        Collect(script, identifiers);
        string prefix = "$__thorvg_numeric_";
        while (ContainsPrefix(identifiers, prefix)) prefix += "_";
        string source = LottieExpressionNumbers.Rewrite(script, context.Input, prefix);
        var rewritten = new Parser(PlainOptions).ParseScript(source);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string role in Roles) aliases.Add(prefix + role, PrivateName(role));
        AddCatchBindings(script, prefix, aliases);
        rewritten = (Script)new RenameBindings(aliases).Visit(rewritten)!;

        // Function's original parameter/body boundary is part of Jint's
        // constructor grammar validation. Numeric expansion must preserve it.
        if (script.Body.Count == 1 && rewritten.Body.Count == 1 &&
            script.Body[0] is IFunction original && rewritten.Body[0] is IFunction changed)
            CopyLocation(original.Body, changed.Body);
        Unsafe.AsRef(in script.Body) = rewritten.Body;
    }

    private static void Collect(Node node, HashSet<string> identifiers)
    {
        if (node is Identifier identifier) identifiers.Add(identifier.Name);
        foreach (var child in node.ChildNodes) Collect(child, identifiers);
    }

    private static void AddCatchBindings(Node node, string prefix, Dictionary<string, string> aliases)
    {
        if (node is CatchClause handler && handler.Param is not null and not Identifier)
            aliases.Add(prefix + "caught_" + handler.Start, PrivateName("caught/" + handler.Start));
        foreach (var child in node.ChildNodes) AddCatchBindings(child, prefix, aliases);
    }

    private static bool ContainsPrefix(HashSet<string> identifiers, string prefix)
    {
        foreach (string name in identifiers) if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static void CopyLocation(Node original, Node changed)
    {
        Unsafe.AsRef(in changed.RangeRef) = original.RangeRef;
        Unsafe.AsRef(in changed.LocationRef) = original.LocationRef;
    }

    private sealed class RenameBindings(Dictionary<string, string> names) : AstRewriter
    {
        public override object Visit(Node node)
        {
            var result = (Node)base.Visit(node)!;
            if (!ReferenceEquals(node, result))
            {
                CopyLocation(node, result);
                result.UserData = node.UserData;
            }
            return result;
        }

        protected override object VisitIdentifier(Identifier node) =>
            names.TryGetValue(node.Name, out string? name) ? new Identifier(name) : node;
    }

    private sealed class Bindings : IReferenceResolver
    {
        internal readonly Dictionary<string, JsValue> Values = new(StringComparer.Ordinal);
        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value) =>
            Values.TryGetValue(reference.ReferencedName.AsString(), out value!);
        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value) => false;
        public bool TryGetCallable(Engine engine, object callee, out JsValue value) { value = JsValue.Undefined; return false; }
        public bool CheckCoercible(JsValue value) => true;
    }
}
