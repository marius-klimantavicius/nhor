// Ported from src/util/parse.rs and the FromCss implementations in src/style.
// The managed tokenizer implements the CSS token forms consumed by these types.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Marius.Winter.Taffy;

public sealed class CssParseException : FormatException
{
    public int Position { get; }
    public CssParseException(int position, string message) : base($"{message} at offset {position}") => Position = position;
}

public sealed record GridTemplateTracks<T>(ImmutableList<T> Tracks, ImmutableList<ImmutableList<string>> LineNames);
public sealed record GridAutoTracks(ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> Tracks);

/// <summary>Parses a complete CSS value using the pinned Taffy style grammar.</summary>
public static class CssStyleParser
{
    private static string AsciiLower(string text) => string.Create(text.Length, text, (span, source) =>
    {
        for (int i = 0; i < source.Length; i++) span[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] + ('a' - 'A')) : source[i];
    });
    public static T Parse<T>(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(text);
        var result = parser.Value(typeof(T));
        parser.End();
        return (T)result;
    }
    public static bool TryParse<T>(string text, out T result)
    {
        try { result = Parse<T>(text); return true; }
        catch (CssParseException) { result = default!; return false; }
    }

    private sealed record Token(string Kind, string Text, float Number, int? Integer, int Position, List<Token>? Children = null);
    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _next;
        public Parser(string source) => _tokens = new Lexer(source.Replace('\0', '\ufffd').Replace("\r\n", "\n").Replace('\r', '\n').Replace('\f', '\n')).Tokens();
        private Parser(List<Token> tokens) => _tokens = tokens;
        private bool Exhausted => _next == _tokens.Count;
        private CssParseException Error(string message = "Unexpected token") => new(Exhausted ? (_tokens.Count == 0 ? 0 : _tokens[^1].Position + 1) : _tokens[_next].Position, message);
        private Token Next() => !Exhausted ? _tokens[_next++] : throw Error("Unexpected end of input");
        public void End() { if (!Exhausted) throw Error("Trailing input"); }
        private string Ident() { var token = Next(); return token.Kind == "ident" ? token.Text : throw Error("Expected identifier"); }
        private bool Eat(string kind) { if (!Exhausted && _tokens[_next].Kind == kind) { _next++; return true; } return false; }
        private bool Function(string name, out Parser nested)
        {
            if (!Exhausted && _tokens[_next] is { Kind: "function" } token && AsciiLower(token.Text) == name)
            { _next++; nested = new(token.Children!); return true; }
            nested = null!; return false;
        }
        public object Value(Type type)
        {
            if (type == typeof(LengthPercentage)) return LengthPercentage.FromRaw(Length(false, false, false, false));
            if (type == typeof(LengthPercentageAuto)) return LengthPercentageAuto.FromRaw(Length(true, false, false, false));
            if (type == typeof(Dimension)) return Dimension.FromRaw(Length(true, true, true, false));
            if (type == typeof(MinTrackSizingFunction)) return MinTrackSizingFunction.FromRaw(Length(true, true, false, false));
            if (type == typeof(MaxTrackSizingFunction)) return MaxTrackSizingFunction.FromRaw(Length(true, true, true, true));
            if (type == typeof(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>)) return Track();
            if (type == typeof(GridTemplateComponent)) return Component();
            if (type == typeof(GridTemplateTracks<GridTemplateComponent>)) return Tracks<ComponentMarker>();
            if (type == typeof(GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)) return Tracks<TrackMarker>();
            if (type == typeof(GridAutoTracks))
            {
                var tracks = ImmutableList.CreateBuilder<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>();
                do { tracks.Add(Track()); } while (!Exhausted);
                return new GridAutoTracks(tracks.ToImmutable());
            }
            if (type == typeof(GridPlacement)) return Placement();
            if (type == typeof(RepetitionCount)) return Repetition();
            if (type == typeof(AlignItems)) return ItemAlignment();
            if (type == typeof(AlignContent)) return ContentAlignment();
            if (type == typeof(Contain)) return Containment();
            if (type == typeof(FlexWrap)) return Wrap();
            if (type == typeof(GridAutoFlow)) return AutoFlow();
            if (type == typeof(AvailableSpace))
            {
                var token = Next();
                return token.Kind is "number" or "dimension" && token.Number >= 0 ? AvailableSpace.Definite(token.Number)
                    : token.Kind == "ident" && token.Text == "min-content" ? AvailableSpace.MinContent
                    : token.Kind == "ident" && token.Text == "max-content" ? AvailableSpace.MaxContent : throw Error();
            }
            // Native keyword-enum parsers compare ASCII keywords without case sensitivity.
            string keyword = AsciiLower(Ident());
            if (type == typeof(Display)) return keyword switch { "none" => Display.None, "flex" => Display.Flex, "grid" => Display.Grid, "block" => Display.Block, "flow-root" => Display.FlowRoot, _ => throw Error() };
            if (type == typeof(Position)) return keyword switch { "static" => Position.Static, "relative" => Position.Relative, "absolute" => Position.Absolute, "fixed" => Position.Fixed, "sticky" => Position.Sticky, _ => throw Error() };
            if (type == typeof(BoxSizing)) return keyword switch { "border-box" => BoxSizing.BorderBox, "content-box" => BoxSizing.ContentBox, _ => throw Error() };
            if (type == typeof(Overflow)) return keyword switch { "visible" => Overflow.Visible, "hidden" => Overflow.Hidden, "clip" => Overflow.Clip, "scroll" => Overflow.Scroll, _ => throw Error() };
            if (type == typeof(Direction)) return keyword switch { "ltr" => Direction.Ltr, "rtl" => Direction.Rtl, _ => throw Error() };
            if (type == typeof(Float)) return keyword switch { "none" => Float.None, "left" => Float.Left, "right" => Float.Right, _ => throw Error() };
            if (type == typeof(Clear)) return keyword switch { "none" => Clear.None, "left" => Clear.Left, "right" => Clear.Right, "both" => Clear.Both, _ => throw Error() };
            if (type == typeof(TextAlign)) return keyword switch { "auto" => TextAlign.Auto, "-webkit-left" => TextAlign.LegacyLeft, "-webkit-right" => TextAlign.LegacyRight, "-webkit-center" => TextAlign.LegacyCenter, _ => throw Error() };
            if (type == typeof(FlexDirection)) return keyword switch { "row" => FlexDirection.Row, "column" => FlexDirection.Column, "row-reverse" => FlexDirection.RowReverse, "column-reverse" => FlexDirection.ColumnReverse, _ => throw Error() };
            throw new NotSupportedException($"No native CSS value parser for {type}");
        }
        private CompactLength Length(bool auto, bool intrinsic, bool fit, bool fr)
        {
            var token = Next();
            if (token.Kind == "percentage") return CompactLength.Percent(token.Number);
            if (token.Kind == "dimension" && token.Text == "px") return CompactLength.Length(token.Number);
            if (fr && token.Kind == "dimension" && token.Text == "fr" && !float.IsNegative(token.Number)) return CompactLength.Fr(token.Number);
            if (token.Kind == "ident")
            {
                if (auto && token.Text == "auto") return CompactLength.AUTO;
                if (intrinsic && token.Text == "min-content") return CompactLength.MIN_CONTENT;
                if (intrinsic && token.Text == "max-content") return CompactLength.MAX_CONTENT;
                if (fit && !fr) switch (token.Text)
                {
                    case "fit-content": return CompactLength.FitContentKeyword();
                    case "stretch": return CompactLength.Stretch();
                    case "content": return CompactLength.Content();
                }
            }
            // Unlike minmax()/repeat(), native fit-content() and unit matching are case sensitive.
            if (fit && token.Kind == "function" && token.Text == "fit-content")
            {
                var nested = new Parser(token.Children!); var limit = nested.Length(false, false, false, false); nested.End();
                return limit.Tag == CompactLength.PERCENT_TAG ? CompactLength.FitContentPercent(limit.Value) : CompactLength.FitContentPx(limit.Value);
            }
            throw Error();
        }
        private MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> Track()
        {
            if (Function("minmax", out var nested))
            {
                var min = MinTrackSizingFunction.FromRaw(nested.Length(true, true, false, false));
                if (!nested.Eat(",")) throw nested.Error("Expected comma");
                var max = MaxTrackSizingFunction.FromRaw(nested.Length(true, true, true, true)); nested.End();
                return new(min, max);
            }
            var single = MaxTrackSizingFunction.FromRaw(Length(true, true, true, true));
            return new(MinTrackSizingFunction.FromRaw(single.IsLengthOrPercentage() || single.IsMinContent() || single.IsMaxContent() ? single.IntoRaw() : CompactLength.AUTO), single);
        }
        private RepetitionCount Repetition()
        {
            var token = Next();
            if (token.Kind == "number" && token.Integer is > 0) return RepetitionCount.Count((ushort)Math.Min(token.Integer.Value, ushort.MaxValue));
            return token.Kind == "ident" ? token.Text switch { "auto-fit" => RepetitionCount.AutoFit, "auto-fill" => RepetitionCount.AutoFill, _ => throw Error() } : throw Error();
        }
        private GridTemplateComponent Component()
        {
            if (!Function("repeat", out var nested)) return GridTemplateComponent.FromSingle(Track());
            var count = nested.Repetition(); if (!nested.Eat(",")) throw nested.Error("Expected comma");
            var tracks = (GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>)nested.Tracks<TrackMarker>(); nested.End();
            return GridTemplateComponent.FromRepeat(new(count, tracks.Tracks, tracks.LineNames));
        }
        private sealed class ComponentMarker;
        private sealed class TrackMarker;
        private ImmutableList<string> Names()
        {
            if (Exhausted || _tokens[_next].Kind != "[") return ImmutableList<string>.Empty;
            var nested = new Parser(Next().Children!); var names = ImmutableList.CreateBuilder<string>();
            while (!nested.Exhausted) names.Add(nested.Ident());
            return names.ToImmutable();
        }
        private object Tracks<T>()
        {
            var tracks = new List<object>(); var names = ImmutableList.CreateBuilder<ImmutableList<string>>(); names.Add(Names());
            do { tracks.Add(typeof(T) == typeof(ComponentMarker) ? Component() : Track()); names.Add(Names()); } while (!Exhausted);
            return typeof(T) == typeof(ComponentMarker)
                ? new GridTemplateTracks<GridTemplateComponent>(tracks.ConvertAll(x => (GridTemplateComponent)x).ToImmutableList(), names.ToImmutable())
                : new GridTemplateTracks<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>>(tracks.ConvertAll(x => (MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>)x).ToImmutableList(), names.ToImmutable());
        }
        private GridPlacement Placement()
        {
            bool span = false; int? number = null; string? name = null;
            while (!Exhausted)
            {
                var token = Next();
                if (token.Kind == "ident")
                {
                    if (token.Text == "auto") { if (span || number != null || name != null) throw Error(); End(); return GridPlacement.Auto; }
                    if (token.Text == "span") { if (span) throw Error(); span = true; }
                    else { if (name != null) throw Error(); name = token.Text; }
                }
                else if (token.Kind == "number" && token.Integer is { } integer && integer != 0 && number == null) number = integer;
                else throw Error();
            }
            if (span)
            {
                ushort count = (ushort)Math.Clamp(number ?? 0, 0, ushort.MaxValue);
                return name == null ? GridPlacement.FromSpanCount(count) : GridPlacement.FromNamedSpan(name, count);
            }
            short line = (short)Math.Clamp(number ?? 0, short.MinValue, short.MaxValue);
            return name != null ? GridPlacement.FromNamedLine(name, line) : number != null ? GridPlacement.FromLine(line) : throw Error();
        }
        private AlignItems ItemAlignment()
        {
            var word = AsciiLower(Ident()); var safety = AlignmentSafety.Default;
            if (word is "safe" or "unsafe") { safety = word == "safe" ? AlignmentSafety.Safe : AlignmentSafety.Unsafe; word = AsciiLower(Ident()); }
            var keyword = word switch
            {
                "normal" => AlignItemsKeyword.Normal, "start" => AlignItemsKeyword.Start, "end" => AlignItemsKeyword.End,
                "flex-start" => AlignItemsKeyword.FlexStart, "flex-end" => AlignItemsKeyword.FlexEnd,
                "self-start" => AlignItemsKeyword.SelfStart, "self-end" => AlignItemsKeyword.SelfEnd,
                "center" => AlignItemsKeyword.Center, "baseline" => AlignItemsKeyword.Baseline, "stretch" => AlignItemsKeyword.Stretch, _ => throw Error(),
            };
            if (safety != AlignmentSafety.Default && keyword is AlignItemsKeyword.Normal or AlignItemsKeyword.Baseline or AlignItemsKeyword.Stretch) throw Error();
            return new(keyword, safety);
        }
        private AlignContent ContentAlignment()
        {
            var word = AsciiLower(Ident()); var safety = AlignmentSafety.Default;
            if (word is "safe" or "unsafe") { safety = word == "safe" ? AlignmentSafety.Safe : AlignmentSafety.Unsafe; word = AsciiLower(Ident()); }
            var keyword = word switch
            {
                "normal" => AlignContentKeyword.Normal, "start" => AlignContentKeyword.Start, "end" => AlignContentKeyword.End,
                "flex-start" => AlignContentKeyword.FlexStart, "flex-end" => AlignContentKeyword.FlexEnd, "center" => AlignContentKeyword.Center,
                "stretch" => AlignContentKeyword.Stretch, "space-between" => AlignContentKeyword.SpaceBetween,
                "space-around" => AlignContentKeyword.SpaceAround, "space-evenly" => AlignContentKeyword.SpaceEvenly, _ => throw Error(),
            };
            if (safety != AlignmentSafety.Default && keyword is not (AlignContentKeyword.Start or AlignContentKeyword.End or AlignContentKeyword.FlexStart or AlignContentKeyword.FlexEnd or AlignContentKeyword.Center)) throw Error();
            return new(keyword, safety);
        }
        private Contain Containment()
        {
            Contain flags = Contain.None; int seen = 0;
            do
            {
                var word = AsciiLower(Ident());
                if (word is "none" or "content") { if (seen != 0) throw Error(); End(); return word == "none" ? Contain.None : Contain.Content; }
                int bit = word switch { "layout" => 1, "paint" => 2, "style" => 64, _ => throw Error() };
                if ((seen & bit) != 0) throw Error(); seen |= bit; flags |= (Contain)(bit & 3);
            } while (!Exhausted);
            return flags;
        }
        private FlexWrap Wrap()
        {
            string? wrap = null; bool balance = false, first = true;
            do
            {
                var word = AsciiLower(Ident());
                if (word == "nowrap" && first) return FlexWrap.NoWrap;
                if (word is "wrap" or "wrap-reverse" && wrap == null) wrap = word;
                else if (word == "balance" && !balance) balance = true;
                else throw Error();
                first = false;
            } while (!Exhausted);
            return balance ? wrap == "wrap-reverse" ? FlexWrap.BalanceReverse : FlexWrap.Balance : wrap == "wrap-reverse" ? FlexWrap.WrapReverse : FlexWrap.Wrap;
        }
        private GridAutoFlow AutoFlow()
        {
            string? axis = null; bool dense = false;
            for (int i = 0; i < 2 && !Exhausted; i++)
            {
                var word = Ident();
                if (word is "row" or "column") axis = word;
                else if (word == "dense") dense = true;
                else throw Error();
            }
            if (axis == null && !dense) throw Error();
            return axis == "column" ? dense ? GridAutoFlow.ColumnDense : GridAutoFlow.Column : dense ? GridAutoFlow.RowDense : GridAutoFlow.Row;
        }
    }

    private sealed class Lexer(string text)
    {
        private int _at;
        private char At(int ahead = 0) => _at + ahead < text.Length ? text[_at + ahead] : '\0';
        private static bool White(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
        private static bool NameStart(char c) => char.IsAsciiLetter(c) || c == '_' || c >= 128;
        private static bool Name(char c) => NameStart(c) || char.IsAsciiDigit(c) || c == '-';
        private bool Escape(int ahead = 0) => At(ahead) == '\\' && At(ahead + 1) is not ('\n' or '\r' or '\f');
        private bool StartsIdent(int ahead = 0) => NameStart(At(ahead)) || Escape(ahead) || At(ahead) == '-' && (NameStart(At(ahead + 1)) || At(ahead + 1) == '-' || Escape(ahead + 1));
        private bool StartsNumber() => char.IsAsciiDigit(At()) || At() == '.' && char.IsAsciiDigit(At(1)) || At() is '+' or '-' && (char.IsAsciiDigit(At(1)) || At(1) == '.' && char.IsAsciiDigit(At(2)));
        private string Identifier()
        {
            var result = new StringBuilder();
            while (_at < text.Length)
            {
                if (Name(At()) || At() == '\0') { result.Append(At() == '\0' ? '\ufffd' : At()); _at++; }
                else if (Escape())
                {
                    _at++; int code = 0, count = 0;
                    while (count < 6 && Uri.IsHexDigit(At())) { code = code * 16 + (At() <= '9' ? At() - '0' : char.ToLowerInvariant(At()) - 'a' + 10); _at++; count++; }
                    if (count == 0) { result.Append(_at == text.Length ? '\ufffd' : At()); _at++; }
                    else
                    {
                        result.Append(char.ConvertFromUtf32(code is 0 or > 0x10ffff or >= 0xd800 and <= 0xdfff ? 0xfffd : code));
                        if (White(At())) { if (At() == '\r' && At(1) == '\n') _at++; _at++; }
                    }
                }
                else break;
            }
            return result.ToString();
        }
        public List<Token> Tokens(char closing = '\0')
        {
            var tokens = new List<Token>();
            while (_at < text.Length)
            {
                if (White(At())) { _at++; continue; }
                if (At() == '/' && At(1) == '*')
                { _at += 2; while (_at < text.Length && !(At() == '*' && At(1) == '/')) _at++; _at = Math.Min(text.Length, _at + 2); continue; }
                if (closing != '\0' && At() == closing) { _at++; break; }
                int start = _at;
                if (StartsNumber())
                {
                    double sign = At() == '-' ? -1 : 1;
                    if (At() is '+' or '-') _at++;
                    double integral = 0;
                    while (char.IsAsciiDigit(At())) { integral = integral * 10 + (At() - '0'); _at++; }
                    bool integer = true;
                    double fraction = 0;
                    if (At() == '.' && char.IsAsciiDigit(At(1)))
                    {
                        integer = false; _at++; double factor = .1;
                        while (char.IsAsciiDigit(At())) { fraction += (At() - '0') * factor; factor *= .1; _at++; }
                    }
                    // cssparser accumulates in f64, then narrows to f32 (after dividing percentages).
                    double numeric = sign * (integral + fraction);
                    if (At() is 'e' or 'E' && (char.IsAsciiDigit(At(1)) || At(1) is '+' or '-' && char.IsAsciiDigit(At(2))))
                    {
                        integer = false; _at++; double exponentSign = At() == '-' ? -1 : 1;
                        if (At() is '+' or '-') _at++; double exponent = 0;
                        while (char.IsAsciiDigit(At())) { exponent = exponent * 10 + (At() - '0'); _at++; }
                        numeric *= Math.Pow(10, exponentSign * exponent);
                    }
                    float value = (float)numeric;
                    int? intValue = integer ? (int)Math.Clamp(numeric, int.MinValue, int.MaxValue) : null;
                    if (StartsIdent()) tokens.Add(new("dimension", Identifier(), value, intValue, start));
                    else if (At() == '%') { _at++; tokens.Add(new("percentage", "", (float)(numeric / 100), null, start)); }
                    else tokens.Add(new("number", "", value, intValue, start));
                }
                else if (StartsIdent())
                {
                    var name = Identifier();
                    if (At() == '(') { _at++; tokens.Add(new("function", name, 0, null, start, Tokens(')'))); }
                    else tokens.Add(new("ident", name, 0, null, start));
                }
                else if (At() == '[') { _at++; tokens.Add(new("[", "", 0, null, start, Tokens(']'))); }
                else { _at++; tokens.Add(new(text[start].ToString(), "", 0, null, start)); }
            }
            return tokens;
        }
    }
}
