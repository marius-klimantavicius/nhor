use taffy::prelude::*;
use taffy::{Baselines, LayoutInput, LayoutOutput, Rect};

fn measure(inputs: LayoutInput, _: NodeId, context: Option<&mut f32>, _: &Style) -> LayoutOutput {
    let baseline = context.map(|c| *c).unwrap_or(0.0);
    LayoutOutput::from_sizes_and_baselines(Size {
        width: inputs.known_dimensions.width.unwrap_or(20.0),
        height: inputs.known_dimensions.height.unwrap_or(20.0),
    }, Rect::ZERO, Baselines::from_first(Some(baseline)))
}
fn emit(values: &[f32]) { println!("{:?}", values.iter().map(|v| v.to_bits()).collect::<Vec<_>>()); }

fn main() {
    for inset in [0.0f32, 7.0] {
        let mut tree: TaffyTree<f32> = TaffyTree::new();
        let first = tree.new_leaf_with_context(Style { position: Position::Relative,
            inset: Rect { top: length(inset), ..Rect::auto() }, ..Style::default() }, 4.0).unwrap();
        let block = tree.new_with_children(Style { display: Display::Block,
            padding: Rect { top: length(3.0), ..Rect::zero() },
            border: Rect { top: length(1.0), ..Rect::zero() }, ..Style::default() }, &[first]).unwrap();
        let second = tree.new_leaf_with_context(Style::default(), 12.0).unwrap();
        let root = tree.new_with_children(Style { align_items: AlignItems::BASELINE, ..Style::default() }, &[block, second]).unwrap();
        tree.compute_layout_with_measure(root, Size::MAX_CONTENT, |_, _, context, _| {
            LayoutOutput::from_sizes_and_baselines(Size { width: 20.0, height: 20.0 }, Rect::ZERO,
                Baselines::from_first(context.map(|c| *c)))
        }).unwrap();
        emit(&[tree.layout(block).unwrap().location.y, tree.layout(second).unwrap().location.y,
            tree.layout(first).unwrap().location.y, tree.layout(root).unwrap().size.height]);
    }
    {
        let mut tree: TaffyTree<f32> = TaffyTree::new();
        let first = tree.new_leaf_with_context(Style::default(), 4.0).unwrap();
        let auto_margin = tree.new_leaf_with_context(Style { margin: Rect { top: auto(), ..Rect::zero() }, ..Style::default() }, 30.0).unwrap();
        let root = tree.new_with_children(Style { align_items: AlignItems::BASELINE, ..Style::default() }, &[first, auto_margin]).unwrap();
        tree.compute_layout_with_measure(root, Size::MAX_CONTENT, |inputs, _, context, _| {
            let baseline = context.map(|c| *c).unwrap_or(0.0);
            LayoutOutput::from_sizes_and_baselines(Size {
                width: inputs.known_dimensions.width.unwrap_or(20.0),
                height: inputs.known_dimensions.height.unwrap_or(if baseline > 10.0 { 40.0 } else { 20.0 }),
            }, Rect::ZERO, Baselines::from_first(Some(baseline)))
        }).unwrap();
        emit(&[tree.layout(first).unwrap().location.y, tree.layout(root).unwrap().size.height]);
    }
    {
        let mut tree: TaffyTree<f32> = TaffyTree::new();
        let first = tree.new_leaf_with_context(Style::default(), 4.0).unwrap();
        let second = tree.new_leaf_with_context(Style::default(), 12.0).unwrap();
        let column = tree.new_with_children(Style { flex_direction: FlexDirection::ColumnReverse, ..Style::default() }, &[first, second]).unwrap();
        let sibling = tree.new_leaf_with_context(Style::default(), 20.0).unwrap();
        let root = tree.new_with_children(Style { align_items: AlignItems::BASELINE, ..Style::default() }, &[column, sibling]).unwrap();
        tree.compute_layout_with_measure(root, Size::MAX_CONTENT, measure).unwrap();
        emit(&[tree.layout(column).unwrap().location.y, tree.layout(sibling).unwrap().location.y,
            tree.layout(second).unwrap().location.y, tree.layout(first).unwrap().location.y, tree.layout(root).unwrap().size.height]);
    }
}
