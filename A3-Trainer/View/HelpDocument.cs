using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace A3_Trainer.View
{
    /// <summary>
    /// The online help (help/index.html, embedded in the exe) turned into RTF for the in-app help, so
    /// both always say the same: one page per panel of the online help, one sub-page per sub-tab.
    /// Only the few elements the help uses are rendered (headings, group boxes, field lists,
    /// paragraphs, lists, callouts); scripts, images, buttons and the navigation are left out.
    /// </summary>
    internal sealed class HelpDocument
    {
        /// <summary>One panel of the help (a main tab).</summary>
        internal sealed class Page
        {
            /// <summary>Title from the online help's navigation.</summary>
            public string Title { get; set; }
            /// <summary>Its sub-tabs in order, title and RTF; one entry without title if it has none.</summary>
            public List<KeyValuePair<string, string>> Sections { get; } = new List<KeyValuePair<string, string>>();
        }

        // Panels of the online help that make no sense in the program (its own About view, the
        // changelog the web page loads from GitHub).
        private static readonly HashSet<string> SkippedPanels = new HashSet<string> { "changelog", "ueber" };
        private static readonly HashSet<string> SkippedTags = new HashSet<string> { "script", "style", "head", "nav", "button", "figure", "svg", "title" };
        private static readonly HashSet<string> VoidTags = new HashSet<string> { "img", "br", "meta", "link", "hr", "input" };

        // Colours: 1 red (warnings), 2 blue (notes), 3 green (tips).
        private const string RtfHeader = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}{\f1 Courier New;}}{\colortbl ;\red192\green0\blue0;\red0\green0\blue160;\red0\green128\blue0;}\fs20 ";
        private const string Rule = "-------------------------------------------------------------------------------------------------------";

        /// <summary>The pages of the help, in the order of the online help.</summary>
        public List<Page> Pages { get; } = new List<Page>();

        /// <summary>Reads the help embedded in the exe.</summary>
        internal static HelpDocument FromResource()
        {
            using (Stream stream = typeof(HelpDocument).Assembly.GetManifestResourceStream("A3_Trainer.help.index.html"))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return Parse(reader.ReadToEnd());
        }

        private sealed class Element
        {
            public string Tag;
            public string Classes;
            public bool Skip;
            public string Sub;          // data-sub of a sub-panel
            public string Close = "";   // RTF written when it closes
        }

        /// <summary>Turns the help's HTML into pages.</summary>
        internal static HelpDocument Parse(string html)
        {
            HelpDocument document = new HelpDocument();
            Dictionary<string, string> titles = Regex.Matches(html, "<a href=\"#[^\"]*\" data-target=\"([^\"]+)\">([^<]*)</a>")
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));

            Stack<Element> open = new Stack<Element>();
            string panel = null;
            StringBuilder intro = null;
            List<string> subOrder = new List<string>();
            List<string> buttonOrder = new List<string>();
            Dictionary<string, StringBuilder> subs = new Dictionary<string, StringBuilder>();
            Dictionary<string, string> subTitles = new Dictionary<string, string>();
            StringBuilder target = null;
            string buttonSub = null;
            StringBuilder buttonText = null;
            bool lineStart = true;
            string calloutColor = @"\cf2";

            void Finish()
            {
                if (panel == null)
                    return;
                Page page = new Page { Title = titles.TryGetValue(panel, out string title) ? title : panel };
                if (subOrder.Count == 0)
                {
                    page.Sections.Add(new KeyValuePair<string, string>(null, RtfHeader + intro.ToString() + "}"));
                }
                else
                {
                    // In the order of the sub-tab buttons (the program's tab order), not of the panels in the file.
                    subOrder = subOrder.OrderBy(key => buttonOrder.IndexOf(key) < 0 ? int.MaxValue : buttonOrder.IndexOf(key)).ToList();
                    for (int i = 0; i < subOrder.Count; i++)
                    {
                        string key = subOrder[i];
                        string text = (i == 0 ? intro.ToString() : "") + subs[key].ToString();
                        page.Sections.Add(new KeyValuePair<string, string>(subTitles.TryGetValue(key, out string sub) ? sub : null, RtfHeader + text + "}"));
                    }
                }
                document.Pages.Add(page);
                panel = null;
            }

            void Write(string rtf, bool startsLine = false)
            {
                if (target == null)
                    return;
                target.Append(rtf);
                lineStart = startsLine;
            }

            foreach (Match token in Regex.Matches(html, @"<!--.*?-->|<(/?)([a-zA-Z0-9]+)([^>]*)>|([^<]+)", RegexOptions.Singleline))
            {
                if (token.Value.StartsWith("<!--"))
                    continue;
                bool skipping = open.Any(e => e.Skip);

                if (token.Groups[4].Success)
                {
                    string text = Regex.Replace(WebUtility.HtmlDecode(token.Value), @"\s+", " ");
                    if (buttonText != null)
                        buttonText.Append(text);
                    if (skipping || target == null)
                        continue;
                    if (lineStart)
                        text = text.TrimStart();
                    if (text.Length > 0)
                        Write(Escape(text));
                    continue;
                }

                string tag = token.Groups[2].Value.ToLowerInvariant();
                string attributes = token.Groups[3].Value;
                if (token.Groups[1].Value == "/")
                {
                    if (tag == "button" && buttonSub != null)
                    {
                        subTitles[buttonSub] = buttonText.ToString().Trim();
                        buttonOrder.Add(buttonSub);
                        buttonSub = null;
                        buttonText = null;
                    }
                    while (open.Count > 0)
                    {
                        Element element = open.Pop();
                        // A closing tag without output (</dl>, </fieldset>, ...) keeps the line start,
                        // so the whitespace before the next paragraph doesn't land in front of it.
                        if (!element.Skip && !open.Any(e => e.Skip) && element.Close.Length > 0)
                            Write(element.Close, element.Close.EndsWith("\\par "));
                        if (element.Sub != null)
                            target = intro;
                        if (element.Tag == tag)
                            break;
                    }
                    if (tag == "section")
                        Finish();
                    continue;
                }
                if (VoidTags.Contains(tag))
                {
                    if (tag == "br" && !skipping)
                        Write(@"\line ", true);
                    continue;
                }

                string classes = Attribute(attributes, "class") ?? "";
                Element opened = new Element { Tag = tag, Classes = classes };
                if (tag == "section" && classes.Contains("panel"))
                {
                    Finish();
                    string id = (Attribute(attributes, "id") ?? "").Replace("panel-", "");
                    if (!SkippedPanels.Contains(id))
                    {
                        panel = id;
                        intro = new StringBuilder();
                        subOrder = new List<string>();
                        buttonOrder = new List<string>();
                        subs = new Dictionary<string, StringBuilder>();
                        subTitles = new Dictionary<string, string>();
                        target = intro;
                    }
                    else
                    {
                        opened.Skip = true;
                    }
                }
                else if (tag == "button" && Attribute(attributes, "data-sub") != null)
                {
                    buttonSub = Attribute(attributes, "data-sub");
                    buttonText = new StringBuilder();
                    opened.Skip = true;
                }
                else if (SkippedTags.Contains(tag) || Attribute(attributes, "id") == "download-version" || classes.Contains("download-banner"))
                {
                    opened.Skip = true;
                }
                else if (tag == "div" && classes.Split(' ').Contains("subpanel") && panel != null)
                {
                    string sub = Attribute(attributes, "data-sub");
                    if (sub != "only")
                    {
                        opened.Sub = sub;
                        subOrder.Add(sub);
                        subs[sub] = new StringBuilder();
                        target = subs[sub];
                    }
                }
                else if (!skipping)
                {
                    // The look of the former in-app help: Arial, headings with a dashed rule under
                    // them, a field's name in bold leading its description, coloured callout labels.
                    switch (tag)
                    {
                        case "h1":
                            Write(@"\pard\sa120{\fs28\b ", true);
                            opened.Close = @"}\par ";
                            break;
                        case "h2":
                        case "h3":
                        case "legend":
                            Write(@"\pard\sb200\sa0{\fs24\b ", true);
                            opened.Close = @"}\par\pard\sa120 " + Rule + @"\par ";
                            break;
                        case "p":
                            Write(@"\pard\qj\sa120 ", true);
                            opened.Close = @"\par ";
                            break;
                        case "dt":
                            Write(@"\pard\qj\sa120{\b ", true);
                            opened.Close = @":} ";
                            break;
                        case "dd":
                            opened.Close = @"\par ";
                            break;
                        case "li":
                            Write(@"\pard\li360\fi-200\sa60 \bullet  ", true);
                            opened.Close = @"\par ";
                            break;
                        case "div":
                            if (classes.Contains("callout"))
                            {
                                calloutColor = classes.Contains("warn") ? @"\cf1" : classes.Contains("tip") ? @"\cf3" : @"\cf2";
                                Write(@"\pard\qj\li280\ri280\sa120 ", true);
                                opened.Close = @"\par ";
                            }
                            break;
                        case "strong":
                        case "b":
                            Write(classes.Contains("label") ? @"{\b" + calloutColor + " " : @"{\b ");
                            opened.Close = classes.Contains("label") ? @":} " : "}";
                            break;
                        case "em":
                        case "i":
                            Write(@"{\i ");
                            opened.Close = "}";
                            break;
                        case "code":
                            Write(@"{\f1 ");
                            opened.Close = "}";
                            break;
                        case "span":
                            if (classes.Contains("menu-path"))
                            {
                                Write(@"{\b ");
                                opened.Close = "}";
                            }
                            else if (classes.Contains("chip"))
                            {
                                Write(@"\bullet  ");
                                opened.Close = "   ";
                            }
                            break;
                    }
                }
                open.Push(opened);
            }
            Finish();
            return document;
        }

        private static string Attribute(string attributes, string name)
        {
            Match match = Regex.Match(attributes, "\\b" + Regex.Escape(name) + "=\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        // RTF special characters escaped; everything outside ASCII as a Unicode escape.
        private static string Escape(string text)
        {
            StringBuilder rtf = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '\\' || c == '{' || c == '}')
                    rtf.Append('\\').Append(c);
                else if (c > 127)
                    rtf.Append("\\u").Append((int)(short)c).Append('?');
                else
                    rtf.Append(c);
            }
            return rtf.ToString();
        }
    }
}
