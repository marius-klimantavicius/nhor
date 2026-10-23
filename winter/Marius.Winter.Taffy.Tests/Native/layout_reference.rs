use serde_json::{json, Value};
use std::io::{self, Read};
use taffy::prelude::*;
use taffy::{Direction, Overflow};
use taffy_test_helpers::{test_measure_function, TestNodeContext, WritingMode};

fn f(v: &Value) -> f32 { v.as_f64().unwrap() as f32 }
fn dimension(v: &Value) -> Dimension {
    if v.is_null() { auto() }
    else if let Some(s) = v.as_str() { percent(s.trim_end_matches('%').parse::<f32>().unwrap() / 100.0) }
    else { length(f(v)) }
}
fn size(v: &Value) -> Size<Dimension> { Size { width: dimension(&v[0]), height: dimension(&v[1]) } }
fn spacing(v: &Value) -> Rect<LengthPercentage> {
    Rect { left: length(f(&v[0])), right: length(f(&v[1])), top: length(f(&v[2])), bottom: length(f(&v[3])) }
}
fn auto_spacing(v: &Value) -> Rect<LengthPercentageAuto> {
    Rect { left: length(f(&v[0])), right: length(f(&v[1])), top: length(f(&v[2])), bottom: length(f(&v[3])) }
}
fn overflow(v: &Value) -> Overflow {
    match v.as_str().unwrap() { "Visible" => Overflow::Visible, "Hidden" => Overflow::Hidden,
        "Clip" => Overflow::Clip, "Scroll" => Overflow::Scroll, _ => panic!("overflow") }
}
fn minmax(v: &Value) -> Size<LengthPercentageAuto> {
    let value = |v: &Value| if v.is_null() { auto() } else { length(f(v)) };
    Size { width: value(&v[0]), height: value(&v[1]) }
}
fn style(v: &Value) -> Style {
    let mut s = Style::default();
    for (key, v) in v.as_object().unwrap() {
        match key.as_str() {
            "display" => s.display = match v.as_str().unwrap() { "Block" => Display::Block, "Grid" => Display::Grid, "Flex" => Display::Flex, _ => panic!("display") },
            "size" => s.size = size(v), "min" => s.min_size = minmax(v), "max" => s.max_size = minmax(v),
            "padding" => s.padding = spacing(v), "border" => s.border = spacing(v), "margin" => s.margin = auto_spacing(v),
            "inset" => s.inset = auto_spacing(v),
            "position" => s.position = match v.as_str().unwrap() { "Relative" => Position::Relative, "Absolute" => Position::Absolute, _ => panic!("position") },
            "direction" => s.direction = if v == "Rtl" { Direction::Rtl } else { Direction::Ltr },
            "flex-direction" => s.flex_direction = match v.as_str().unwrap() { "Row" => FlexDirection::Row, "RowReverse" => FlexDirection::RowReverse,
                "Column" => FlexDirection::Column, "ColumnReverse" => FlexDirection::ColumnReverse, _ => panic!("direction") },
            "wrap" => s.flex_wrap = match v.as_str().unwrap() { "NoWrap" => FlexWrap::NoWrap, "Wrap" => FlexWrap::Wrap,
                "WrapReverse" => FlexWrap::WrapReverse, _ => panic!("wrap") },
            "gap" => s.gap = Size { width: length(f(&v[0])), height: length(f(&v[1])) },
            "grow" => s.flex_grow = f(v), "shrink" => s.flex_shrink = f(v), "basis" => s.flex_basis = dimension(v),
            "aspect" => s.aspect_ratio = Some(f(v)),
            "box" => s.box_sizing = if v == "ContentBox" { BoxSizing::ContentBox } else { BoxSizing::BorderBox },
            "align" => s.align_items = match v.as_str().unwrap() { "Baseline" => AlignItems::BASELINE, "Center" => AlignItems::CENTER,
                "Start" => AlignItems::START, "End" => AlignItems::END, "Stretch" => AlignItems::STRETCH, _ => panic!("align") },
            "overflow" => s.overflow = taffy::Point { x: overflow(&v[0]), y: overflow(&v[1]) },
            "scrollbar" => s.scrollbar_width = f(v),
            "columns" => s.grid_template_columns = v.as_array().unwrap().iter().map(|v| fr(f(v))).collect(),
            _ => panic!("untranslated probe style: {}", key),
        }
    }
    s
}
fn available(v: &Value) -> AvailableSpace {
    match v.as_str() { Some("min") => AvailableSpace::MinContent, Some("max") => AvailableSpace::MaxContent,
        None => AvailableSpace::Definite(f(v)), _ => panic!("available") }
}
fn main() {
    let mut input = String::new();
    io::stdin().read_to_string(&mut input).unwrap();
    let scenes: Value = serde_json::from_str(&input).unwrap();
    let mut results = Vec::new();
    for scene in scenes.as_array().unwrap() {
        let mut tree: TaffyTree<TestNodeContext> = TaffyTree::new();
        if !scene["round"].as_bool().unwrap() { tree.disable_rounding(); }
        let mut nodes = Vec::new();
        for definition in scene["nodes"].as_array().unwrap() {
            let children: Vec<_> = definition["children"].as_array().unwrap().iter().map(|i| nodes[i.as_u64().unwrap() as usize]).collect();
            let s = style(&definition["style"]);
            let node = if !children.is_empty() { tree.new_with_children(s, &children).unwrap() }
            else {
                let context = if let Some(text) = definition["text"].as_str() { TestNodeContext::ahem_text(text.to_string(),
                    if definition["vertical"].as_bool().unwrap() { WritingMode::Vertical } else { WritingMode::Horizontal }) }
                else { TestNodeContext::fixed(f(&definition["fixed"][0]), f(&definition["fixed"][1])) };
                tree.new_leaf_with_context(s, context).unwrap()
            };
            nodes.push(node);
        }
        let av = Size { width: available(&scene["viewport"][0]), height: available(&scene["viewport"][1]) };
        let mut phases = Vec::new();
        for phase in 0..5 {
            if phase == 2 { let mut s = tree.style(nodes[0]).unwrap().clone(); s.size.width = percent(0.333f32); tree.set_style(nodes[0], s).unwrap(); }
            if phase == 3 { tree.set_node_context(nodes[0], Some(TestNodeContext::fixed(43.125, 19.75))).unwrap(); }
            if phase == 4 { tree.remove(nodes[0]).unwrap(); }
            tree.compute_layout_with_measure(*nodes.last().unwrap(), av, test_measure_function).unwrap();
            let mut values = Vec::new();
            for node in nodes.iter().skip(if phase == 4 { 1 } else { 0 }) {
                let l = tree.layout(*node).unwrap();
                for v in [l.location.x, l.location.y, l.size.width, l.size.height, l.scroll_width(), l.scroll_height(),
                    l.border.left, l.border.right, l.border.top, l.border.bottom, l.padding.left, l.padding.right, l.padding.top, l.padding.bottom,
                    l.margin.left, l.margin.right, l.margin.top, l.margin.bottom] { values.push(v.to_bits()); }
            }
            phases.push(values);
        }
        results.push(phases);
    }
    println!("{}", json!(results));
}
