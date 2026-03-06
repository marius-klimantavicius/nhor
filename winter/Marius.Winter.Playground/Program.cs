using System.Linq;
using Marius.Winter;

// Pick backend from command line: --gl or --sw (default)
var backend = args.Contains("--gl") ? RenderBackend.GL : RenderBackend.SW;
var suffix = backend == RenderBackend.GL ? " [GL]" : " [SW]";

// Start with light theme
bool isDark = false;
var window = new Window(900, 650, "Winter GUI Playground" + suffix, Theme.Light, backend);

const float menuBarH = 20; // menu bar height

// ============================================================
// LEFT COLUMN — Basic controls
// ============================================================

var leftPanel = new Panel { Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start } };
window.AddChild(leftPanel);

var titleLabel = new Label("Winter GUI Framework");
titleLabel.FontSize = 20;
leftPanel.AddChild(titleLabel);

// ============================================================
// MENU BAR (declared after titleLabel so lambdas can reference it)
// ============================================================

var menuBar = new MenuBar();
window.AddChild(menuBar);

var fileMenu = menuBar.AddMenu("File");
fileMenu.AddItem("New", () => titleLabel.Text = "File → New");
fileMenu.AddItem("Open", () => titleLabel.Text = "File → Open");
fileMenu.AddItem("Save", () => titleLabel.Text = "File → Save");
fileMenu.AddSeparator();
fileMenu.AddItem("Exit", () => Glfw.Glfw.glfwSetWindowShouldClose(null!, 1));

var editMenu = menuBar.AddMenu("Edit");
editMenu.AddItem("Undo", () => titleLabel.Text = "Edit → Undo");
editMenu.AddItem("Redo", () => titleLabel.Text = "Edit → Redo");
editMenu.AddSeparator();
editMenu.AddItem("Cut", () => titleLabel.Text = "Edit → Cut");
editMenu.AddItem("Copy", () => titleLabel.Text = "Edit → Copy");
editMenu.AddItem("Paste", () => titleLabel.Text = "Edit → Paste");

var viewMenu = menuBar.AddMenu("View");
viewMenu.AddItem("Toggle Theme", () => { isDark = !isDark; window.Theme = isDark ? Theme.Dark : Theme.Light; });
var zoomSub = viewMenu.AddSubMenu("Zoom");
zoomSub.AddItem("Zoom In", () => titleLabel.Text = "View → Zoom → In");
zoomSub.AddItem("Zoom Out", () => titleLabel.Text = "View → Zoom → Out");
zoomSub.AddSeparator();
zoomSub.AddItem("Reset", () => titleLabel.Text = "View → Zoom → Reset");
var panelsSub = viewMenu.AddSubMenu("Panels");
panelsSub.AddItem("Left Panel", () => titleLabel.Text = "View → Panels → Left");
panelsSub.AddItem("Right Panel", () => titleLabel.Text = "View → Panels → Right");
var nestedSub = panelsSub.AddSubMenu("More...");
nestedSub.AddItem("Output", () => titleLabel.Text = "View → Panels → More → Output");
nestedSub.AddItem("Terminal", () => titleLabel.Text = "View → Panels → More → Terminal");

var helpMenu = menuBar.AddMenu("Help");
helpMenu.AddItem("About", () => titleLabel.Text = "Winter GUI Playground v1.0");

var subtitleLabel = new Label("A nanogui-inspired UI toolkit");
leftPanel.AddChild(subtitleLabel);

leftPanel.AddChild(new Separator());

// Buttons in a horizontal row
var buttonRow = new Panel { Layout = new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, CrossAlignment = Alignment.Center } };
leftPanel.AddChild(buttonRow);

var btn1 = new Button("Click Me");
btn1.Clicked = () => titleLabel.Text = "Button was clicked!";
buttonRow.AddChild(btn1);

var btn2 = new Button("Reset");
btn2.Clicked = () => titleLabel.Text = "Winter GUI Framework";
buttonRow.AddChild(btn2);

// Theme toggle
var themeBtn = new Button("Toggle Theme");
themeBtn.Clicked = () =>
{
    isDark = !isDark;
    window.Theme = isDark ? Theme.Dark : Theme.Light;
};
leftPanel.AddChild(themeBtn);

leftPanel.AddChild(new Separator());

// Checkbox
var checkbox = new Checkbox("Enable feature", false);
checkbox.Changed = isChecked => subtitleLabel.Text = isChecked ? "Feature enabled" : "Feature disabled";
leftPanel.AddChild(checkbox);

// Slider with label
var sliderLabel = new Label("Slider: 50");
leftPanel.AddChild(sliderLabel);

var slider = new Slider(0, 100, 50);
slider.Step = 1;
leftPanel.AddChild(slider);

// ProgressBar
var progressLabel = new Label("Progress: 50%");
leftPanel.AddChild(progressLabel);

var progressBar = new ProgressBar(0.5f);
leftPanel.AddChild(progressBar);

// Wire slider to progress bar
slider.ValueChanged = v =>
{
    sliderLabel.Text = $"Slider: {v:F0}";
    progressBar.Value = v / 100f;
    progressLabel.Text = $"Progress: {v:F0}%";
};

leftPanel.AddChild(new Separator());

// ComboBox
var comboLabel = new Label("Selection: Option A");
leftPanel.AddChild(comboLabel);

var combo = new ComboBox(new[] { "Option A", "Option B", "Option C", "Option D" });
combo.SelectionChanged = idx => comboLabel.Text = $"Selection: {combo.SelectedItem}";
leftPanel.AddChild(combo);

// TextBox
var textBox = new TextBox("Hello, World!", "Type something...");
leftPanel.AddChild(textBox);

var textBox2 = new TextBox("", "Another text field...");
leftPanel.AddChild(textBox2);

// ============================================================
// RIGHT COLUMN — TabWidget with scroll panel
// ============================================================

var tabWidget = new TabWidget();
window.AddChild(tabWidget);

// Tab 1: Scrollable list
var scrollPanel = new ScrollPanel
{
    Layout = new StackLayout { Spacing = 8, CrossAlignment = Alignment.Start }
};

for (int i = 0; i < 20; i++)
{
    var itemLabel = new Label($"Item {i + 1}: Scroll to see more content");
    scrollPanel.AddChild(itemLabel);
}

tabWidget.AddTab("Scroll Demo", scrollPanel);

// Tab 2: Controls
var tab2Panel = new Panel { Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start } };

tab2Panel.AddChild(new Label("Controls in a tab"));
tab2Panel.AddChild(new Button("Tab Button") { });
tab2Panel.AddChild(new Checkbox("Tab Checkbox", true));
var tabSlider = new Slider(0, 100, 30);
tabSlider.Step = 5;
tab2Panel.AddChild(tabSlider);

tabWidget.AddTab("Controls", tab2Panel);

// Tab 3: Info
var tab3Panel = new Panel { Layout = new StackLayout { Spacing = 8, CrossAlignment = Alignment.Start } };
tab3Panel.AddChild(new Label("About Winter"));
tab3Panel.AddChild(new Label("Built on ThorVG + GLFW"));
tab3Panel.AddChild(new Label("Retained-mode scene graph"));
tab3Panel.AddChild(new Label("Zero allocations in steady state"));
tab3Panel.AddChild(new Label("Software rendering, GL blit"));

tabWidget.AddTab("About", tab3Panel);

// Tab 4: Flexbox demo
var flexPanel = new ScrollPanel
{
    Layout = new StackLayout { Spacing = 12, CrossAlignment = Alignment.Stretch }
};

flexPanel.AddChild(new Label("Flex Row — Grow"));
var flexRow1 = new Panel { Layout = new FlexLayout { Direction = Orientation.Horizontal, Gap = 6, AlignItems = Alignment.Stretch } };
var flexBtn1 = new Button("Fixed");
var flexBtn2 = new Button("Grow 1");
flexBtn2.LayoutData = new FlexItem { Grow = 1 };
var flexBtn3 = new Button("Grow 2");
flexBtn3.LayoutData = new FlexItem { Grow = 2 };
flexRow1.AddChild(flexBtn1);
flexRow1.AddChild(flexBtn2);
flexRow1.AddChild(flexBtn3);
flexPanel.AddChild(flexRow1);

flexPanel.AddChild(new Label("Flex Row — Justify: SpaceBetween"));
var flexRow2 = new Panel { Layout = new FlexLayout { Direction = Orientation.Horizontal, Gap = 6, JustifyContent = JustifyContent.SpaceBetween } };
flexRow2.AddChild(new Button("Left"));
flexRow2.AddChild(new Button("Center"));
flexRow2.AddChild(new Button("Right"));
flexPanel.AddChild(flexRow2);

flexPanel.AddChild(new Label("Flex Row — Justify: SpaceEvenly"));
var flexRow3 = new Panel { Layout = new FlexLayout { Direction = Orientation.Horizontal, Gap = 0, JustifyContent = JustifyContent.SpaceEvenly } };
flexRow3.AddChild(new Button("A"));
flexRow3.AddChild(new Button("B"));
flexRow3.AddChild(new Button("C"));
flexRow3.AddChild(new Button("D"));
flexPanel.AddChild(flexRow3);

flexPanel.AddChild(new Label("Flex Row — Align: Center"));
var flexRow4 = new Panel { Layout = new FlexLayout { Direction = Orientation.Horizontal, Gap = 8, AlignItems = Alignment.Center } };
var tallLabel = new Label("Tall\nItem");
var shortBtn = new Button("Short");
var medBtn = new Button("Medium");
flexRow4.AddChild(tallLabel);
flexRow4.AddChild(shortBtn);
flexRow4.AddChild(medBtn);
flexPanel.AddChild(flexRow4);

flexPanel.AddChild(new Label("Flex Column — Grow"));
var flexCol = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 4, AlignItems = Alignment.Stretch } };
flexCol.AddChild(new Button("No grow"));
var growBtn = new Button("Grow = 1");
growBtn.LayoutData = new FlexItem { Grow = 1 };
flexCol.AddChild(growBtn);
flexCol.AddChild(new Button("No grow"));
flexPanel.AddChild(flexCol);

tabWidget.AddTab("Flexbox", flexPanel);

// Tab 5: Grid demo
var gridPanel = new ScrollPanel
{
    Layout = new StackLayout { Spacing = 12, CrossAlignment = Alignment.Stretch }
};

gridPanel.AddChild(new Label("Grid — 3 columns (1fr, 2fr, 1fr)"));
var grid1 = new Panel
{
    Layout = new GridLayout
    {
        Columns = new[] { TrackSize.Fr(1), TrackSize.Fr(2), TrackSize.Fr(1) },
        ColumnGap = 6, RowGap = 6,
    }
};
grid1.AddChild(new Button("1:1"));
grid1.AddChild(new Button("1:2 (wider)"));
grid1.AddChild(new Button("1:3"));
grid1.AddChild(new Button("2:1"));
grid1.AddChild(new Button("2:2"));
grid1.AddChild(new Button("2:3"));
gridPanel.AddChild(grid1);

gridPanel.AddChild(new Label("Grid — Mixed: 100px, auto, 1fr"));
var grid2 = new Panel
{
    Layout = new GridLayout
    {
        Columns = new[] { TrackSize.Px(100), TrackSize.Auto(), TrackSize.Fr(1) },
        ColumnGap = 6, RowGap = 6,
    }
};
grid2.AddChild(new Button("100px"));
grid2.AddChild(new Button("Auto"));
grid2.AddChild(new Button("Fill"));
grid2.AddChild(new Button("100px"));
grid2.AddChild(new Button("Auto Wider"));
grid2.AddChild(new Button("Fill"));
gridPanel.AddChild(grid2);

gridPanel.AddChild(new Label("Grid — Spanning cells"));
var grid3 = new Panel
{
    Layout = new GridLayout
    {
        Columns = new[] { TrackSize.Fr(1), TrackSize.Fr(1), TrackSize.Fr(1) },
        ColumnGap = 6, RowGap = 6,
    }
};
var spanBtn = new Button("Spans 2 columns");
spanBtn.LayoutData = new GridItem { Column = 0, Row = 0, ColumnSpan = 2 };
grid3.AddChild(spanBtn);
var rightBtn = new Button("1x1");
rightBtn.LayoutData = new GridItem { Column = 2, Row = 0 };
grid3.AddChild(rightBtn);
var bottomSpan = new Button("Spans 3 columns");
bottomSpan.LayoutData = new GridItem { Column = 0, Row = 1, ColumnSpan = 3 };
grid3.AddChild(bottomSpan);
grid3.AddChild(new Button("Auto R2"));
grid3.AddChild(new Button("Auto R2"));
grid3.AddChild(new Button("Auto R2"));
gridPanel.AddChild(grid3);

gridPanel.AddChild(new Label("Grid — 4 equal columns"));
var grid4 = new Panel
{
    Layout = new GridLayout
    {
        Columns = new[] { TrackSize.Fr(1), TrackSize.Fr(1), TrackSize.Fr(1), TrackSize.Fr(1) },
        ColumnGap = 4, RowGap = 4,
    }
};
for (int i = 0; i < 8; i++)
    grid4.AddChild(new Button($"Cell {i + 1}"));
gridPanel.AddChild(grid4);

tabWidget.AddTab("Grid", gridPanel);

// Tab 6: TreeView demo
var treeView = new TreeView();

var projRoot = treeView.AddNode("Project");
projRoot.IsExpanded = true;
var src = treeView.AddNode("src", projRoot);
src.IsExpanded = true;
var core = treeView.AddNode("Core", src);
core.IsExpanded = true;
treeView.AddNode("Element.cs", core);
treeView.AddNode("Layout.cs", core);
treeView.AddNode("Style.cs", core);
treeView.AddNode("Window.cs", core);
var controls = treeView.AddNode("Controls", src);
controls.IsExpanded = true;
treeView.AddNode("Button.cs", controls);
treeView.AddNode("Checkbox.cs", controls);
treeView.AddNode("ComboBox.cs", controls);
treeView.AddNode("Label.cs", controls);
treeView.AddNode("Panel.cs", controls);
treeView.AddNode("ScrollPanel.cs", controls);
treeView.AddNode("Slider.cs", controls);
treeView.AddNode("TabWidget.cs", controls);
treeView.AddNode("TextBox.cs", controls);
treeView.AddNode("TreeView.cs", controls);
var tests = treeView.AddNode("tests", src);
treeView.AddNode("LayoutTests.cs", tests);
treeView.AddNode("TreeTests.cs", tests);
var docs = treeView.AddNode("docs", projRoot);
treeView.AddNode("README.md", docs);
treeView.AddNode("CHANGELOG.md", docs);
treeView.AddNode("LICENSE", projRoot);
treeView.AddNode(".gitignore", projRoot);

treeView.SelectionChanged = node =>
{
    if (node != null)
        titleLabel.Text = $"Selected: {node.Text}";
};

tabWidget.AddTab("Tree", treeView);

// Tab 7: Rich Controls — SvgImage, RichLabel, Tooltip demo
var richPanel = new ScrollPanel
{
    Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start }
};

richPanel.AddChild(new Label("SvgImage Control") { FontSize = 18 });

// Simple SVG star
var starSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 64 64"" width=""64"" height=""64"">
  <polygon points=""32,4 40,24 62,24 44,38 50,58 32,46 14,58 20,38 2,24 24,24""
           fill=""#FFD700"" stroke=""#DAA520"" stroke-width=""2""/>
</svg>";
var svgImage = new SvgImage(starSvg);
richPanel.AddChild(svgImage);

var circleSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 48 48"" width=""48"" height=""48"">
  <circle cx=""24"" cy=""24"" r=""20"" fill=""#4488CC"" stroke=""#2266AA"" stroke-width=""2""/>
  <text x=""24"" y=""30"" text-anchor=""middle"" fill=""white"" font-size=""16"">i</text>
</svg>";
var svgImage2 = new SvgImage(circleSvg);
richPanel.AddChild(svgImage2);

richPanel.AddChild(new Separator());
richPanel.AddChild(new Label("RichLabel Control") { FontSize = 18 });

// RichLabel in text mode
var richText = new RichLabel("This is a RichLabel (text mode)");
richPanel.AddChild(richText);

// RichLabel in SVG mode
var richSvg = new RichLabel();
richSvg.Svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 120 30"" width=""120"" height=""30"">
  <rect x=""1"" y=""1"" width=""118"" height=""28"" rx=""6"" fill=""#28A745"" stroke=""#1E7E34"" stroke-width=""1""/>
  <text x=""60"" y=""20"" text-anchor=""middle"" fill=""white"" font-size=""14"">SVG Badge</text>
</svg>";
richPanel.AddChild(richSvg);

richPanel.AddChild(new Separator());
richPanel.AddChild(new Label("Tooltips") { FontSize = 18 });

var tipBtn1 = new Button("Hover me — text tooltip");
tipBtn1.Tooltip = "This is a simple text tooltip!";
richPanel.AddChild(tipBtn1);

var tipBtn2 = new Button("Hover me — SVG tooltip");
tipBtn2.Tooltip = new TooltipContent(Svg: starSvg);
richPanel.AddChild(tipBtn2);

var tipBtn3 = new Button("Hover me — text + SVG");
tipBtn3.Tooltip = new TooltipContent("Star icon:", starSvg);
richPanel.AddChild(tipBtn3);

var tipLabel = new Label("Labels can have tooltips too");
tipLabel.Tooltip = "Yes, any element can have a tooltip.";
richPanel.AddChild(tipLabel);

tabWidget.AddTab("Rich", richPanel);

// ============================================================
// Draggable dialog window
// ============================================================

var dialog = new DialogWindow("Settings")
{
    Layout = new StackLayout { Spacing = 10, CrossAlignment = Alignment.Start }
};
window.AddChild(dialog);

dialog.AddChild(new Label("Drag this window by its title bar"));
dialog.AddChild(new Checkbox("Dark mode", false));
dialog.AddChild(new Checkbox("Show FPS", false));
var okBtn = new Button("OK");
dialog.AddChild(okBtn);

// ============================================================
// Layout and run — Arrange AFTER all children are added
// ============================================================

menuBar.Measure(900, menuBarH);
menuBar.Arrange(new RectF(0, 0, 900, menuBarH));

float contentY = menuBarH + 8;
leftPanel.Measure(340, 610);
leftPanel.Arrange(new RectF(20, contentY, 340, 610));

tabWidget.Measure(500, 350);
tabWidget.Arrange(new RectF(380, contentY, 500, 350));

dialog.Measure(280, 220);
dialog.Arrange(new RectF(400, 400, 280, 220));

window.Run();
