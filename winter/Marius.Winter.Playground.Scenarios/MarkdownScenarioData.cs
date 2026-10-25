using System;
using System.Linq;
using System.Text;

namespace Marius.Winter.Scenarios;

/// <summary>One native Markdown contract document shared by both playgrounds.</summary>
public static class MarkdownScenarioData
{
    private static readonly byte[] Badge = Encoding.UTF8.GetBytes("""
        <svg xmlns="http://www.w3.org/2000/svg" width="180" height="48" viewBox="0 0 180 48">
          <rect width="180" height="48" rx="8" fill="#245b86"/>
          <circle cx="24" cy="24" r="14" fill="#90d6fa"/>
          <path d="M24 12v24M12 24h24M15 15l18 18M15 33l18-18" stroke="#245b86" stroke-width="2"/>
          <path d="M53 15h105v5H53zm0 12h80v5H53z" fill="#e1f4ff"/>
        </svg>
        """);
    public static MarkdownImage? ResolveImage(string url) => url == "asset:winter-badge" ? new(Badge, "svg") : null;
    public const string Document = """
        # Winter field notes

        Native Markdown for **readable documents**, *quiet emphasis*, and ~~outdated plans~~. Visit [Winter docs](https://example.test/winter) or use <https://example.test/reference>. Links display their destination below.

        ![Winter badge from memory](asset:winter-badge)

        ## A small expedition

        Paragraphs wrap with the available width. Mix **bold and *italic*** with `inline code`, escaped \*stars\*, and entities: &amp; &lt; &gt;.
        A soft line continues here.\
        This line follows a hard break.

        > Bring curiosity, a notebook, and a spare pencil.
        >
        > Blockquotes keep their own indentation and can contain **styled text**.

        3. Review the route.
        4. Pack the essentials:
           - notebook and pencil
           - warm layers
             - gloves
             - a hat
        5. Walk at an easy pace.

        - [x] Check the weather
        - [ ] Fill the flask
        - [ ] Share the route

        ## Supplies

        | Item | Count | Status |
        | :--- | ---: | :---: |
        | **Pencils** | 12 | ready |
        | A notebook with room for sketches and longer observations | 2 | packed |
        | Tea | 4 | warm |

        ## Code keeps its space

        ```csharp
        var viewer = new MarkdownView("# Hello Winter");
        viewer.LinkClicked += destination => status.Text = destination;

        // Wide code stays on one line; use the horizontal scrollbar or Shift + wheel.
        var route = "north / river / forest / bridge / clearing / ridge / shelter / village / home";
        ```

            Indented code also preserves  spaces.
              And the next line's indentation.

        ---

        ### Observations

        The document stays a viewer. Task boxes are read-only; links call your application.

        #### Smaller landmarks

        Resize the window, switch theme, or change the font size to inspect wrapping.

        ##### Local resources

        Images use an explicit byte resolver. ![Unavailable image with useful alt text](asset:missing)

        ###### Literal HTML policy

        <b>This is literal HTML source, with no execution.</b>

        [End of the field notes](action:finished)
        """;
    public static string Updated => "# Updated field notes\n\nThe source changed while the viewer stayed mounted.\n\n" + Document;
    public static string Moderate(int sections = 24) => string.Join("\n\n", Enumerable.Range(1, sections).Select(i => $"# Section {i}\n\n" + Document));
}
