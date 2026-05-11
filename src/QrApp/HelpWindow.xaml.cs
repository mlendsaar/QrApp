using System.IO;
using System.Reflection;
using System.Windows;
using Markdig;

namespace QrApp;

public sealed partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadContent();
    }

    private void LoadContent()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("JUHEND.md");
        if (stream is null) { Browser.NavigateToString("<p>Help content not found.</p>"); return; }

        string markdown;
        using (var reader = new StreamReader(stream))
            markdown = reader.ReadToEnd();

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var body = Markdown.ToHtml(markdown, pipeline);
        Browser.NavigateToString(BuildHtml(body));
    }

    private static string BuildHtml(string body) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <meta http-equiv="X-UA-Compatible" content="IE=edge">
        <style>
          body      { font-family:'Segoe UI',sans-serif; font-size:14px; color:#1a1a1a;
                      margin:0; padding:24px 32px; background:#ffffff; line-height:1.6; }
          h1        { font-size:22px; font-weight:600; border-bottom:2px solid #e0e0e0;
                      padding-bottom:8px; margin-top:0; }
          h2        { font-size:17px; font-weight:600; margin-top:28px; margin-bottom:6px; }
          h3        { font-size:14px; font-weight:600; margin-top:18px; margin-bottom:4px; }
          code      { font-family:'Cascadia Mono','Consolas',monospace; font-size:12.5px;
                      background:#f4f4f4; border:1px solid #e0e0e0; border-radius:3px;
                      padding:1px 5px; }
          pre       { background:#f4f4f4; border:1px solid #e0e0e0; border-radius:4px;
                      padding:10px 14px; overflow-x:auto; font-size:12.5px; }
          pre code  { background:none; border:none; padding:0; }
          table     { border-collapse:collapse; width:100%; margin:12px 0; }
          th,td     { border:1px solid #d0d0d0; padding:6px 10px; text-align:left; }
          th        { background:#f5f5f5; font-weight:600; }
          tr:nth-child(even) td { background:#fafafa; }
          blockquote{ border-left:3px solid #0078D4; margin:8px 0; padding:4px 12px;
                      background:#f0f6ff; color:#333; }
          hr        { border:none; border-top:1px solid #e0e0e0; margin:20px 0; }
          ul,ol     { padding-left:24px; }
          li        { margin-bottom:3px; }
          a         { color:#0078D4; }
          strong    { font-weight:600; }
        </style>
        </head>
        <body>{{body}}</body>
        </html>
        """;
}
