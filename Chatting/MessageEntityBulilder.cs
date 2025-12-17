using dn42Bot.Buffers;
using System;
using System.Collections.Generic;
using System.Text;

namespace dn42Bot.Chatting;

internal abstract class MessageEntityBulilder
{
    public static MessageEntityBulilder Html { get; } = new HtmlMessageEntityBuilder();
    public static MessageEntityBulilder Markdown { get; } = new MarkdownMessageEntityBuilder();

    public abstract string PreFormatted(ReadOnlySpan<char> text);
    public abstract string CodeBlock(ReadOnlySpan<char> text, string language);
    public abstract string Bold(ReadOnlySpan<char> text);
    public abstract string Italic(ReadOnlySpan<char> text);
    public abstract string Strike(ReadOnlySpan<char> text);
    public abstract string Underline(ReadOnlySpan<char> text);
    public abstract string Code(ReadOnlySpan<char> text);
    public abstract string Escape(ReadOnlySpan<char> text);

    sealed class HtmlMessageEntityBuilder : MessageEntityBulilder
    {
        public override string PreFormatted(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<pre>\n");
            InternalEscape(text, buffer);
            buffer.Add('\n');
            buffer.AddRange("</pre>");

            return new string(buffer.AsArraySegment());
        }
        public override string CodeBlock(ReadOnlySpan<char> text, string language)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<pre language=\"");
            buffer.AddRange(language);
            buffer.AddRange("\">\n");
            InternalEscape(text, buffer);
            buffer.Add('\n');
            buffer.AddRange("</pre>");

            return new string(buffer.AsArraySegment());
        }
        public override string Bold(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<b>");
            InternalEscape(text, buffer);
            buffer.AddRange("</b>");

            return new string(buffer.AsArraySegment());
        }
        public override string Italic(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<i>");
            InternalEscape(text, buffer);
            buffer.AddRange("</i>");

            return new string(buffer.AsArraySegment());
        }
        public override string Underline(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<u>");
            InternalEscape(text, buffer);
            buffer.AddRange("</u>");

            return new string(buffer.AsArraySegment());
        }
        public override string Strike(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<s>");
            InternalEscape(text, buffer);
            buffer.AddRange("</s>");

            return new string(buffer.AsArraySegment());
        }
        public override string Code(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("<code>");
            InternalEscape(text, buffer);
            buffer.AddRange("</code>");

            return new string(buffer.AsArraySegment());
        }
        public override string Escape(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            InternalEscape(text, buffer);
            var segment = buffer.AsArraySegment();
            return new string(segment);
        }
        void InternalEscape(ReadOnlySpan<char> text, RentedList<char> buffer)
        {
            for (int i = 0; i < text.Length; i++)
            {
                ref readonly var c = ref text[i];
                switch (c)
                {
                    case '&':
                        buffer.AddRange("&amp;");
                        break;
                    case '<':
                        buffer.AddRange("&lt;");
                        break;
                    case '>':
                        buffer.AddRange("&gt;");
                        break;
                    default:
                        buffer.Add(c);
                        break;
                }
            }
        }
    }
    sealed class MarkdownMessageEntityBuilder : MessageEntityBulilder
    {
        public override string PreFormatted(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("```\n");
            InternalEscape(text, buffer);
            buffer.AddRange("\n```");
            return new string(buffer.AsArraySegment());
        }
        public override string CodeBlock(ReadOnlySpan<char> text, string language)
        {
            using var buffer = new RentedList<char>();
            buffer.AddRange("```");
            buffer.AddRange(language);
            buffer.Add('\n');
            InternalEscape(text, buffer);
            buffer.AddRange("\n```");
            return new string(buffer.AsArraySegment());
        }
        public override string Bold(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.Add('*');
            InternalEscape(text, buffer);
            buffer.Add('*');
            return new string(buffer.AsArraySegment());
        }
        public override string Italic(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.Add('_');
            InternalEscape(text, buffer);
            buffer.Add('_');
            return new string(buffer.AsArraySegment());
        }
        public override string Strike(ReadOnlySpan<char> text)
        {
            throw new NotSupportedException();
        }
        public override string Underline(ReadOnlySpan<char> text)
        {
            throw new NotSupportedException();
        }
        public override string Code(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            buffer.Add('`');
            InternalEscape(text, buffer);
            buffer.Add('`');
            return new string(buffer.AsArraySegment());
        }
        public override string Escape(ReadOnlySpan<char> text)
        {
            using var buffer = new RentedList<char>();
            InternalEscape(text, buffer);
            var segment = buffer.AsArraySegment();
            return new string(segment);
        }
        void InternalEscape(ReadOnlySpan<char> text, RentedList<char> buffer)
        {
            for (int i = 0; i < text.Length; i++)
            {
                ref readonly var c = ref text[i];
                switch (c)
                {
                    case '_':
                    case '*':
                    case '`':
                    case '[':
                        buffer.Add('\\');
                        goto default;
                    default:
                        buffer.Add(c);
                        break;
                }
            }
        }
    }
}
