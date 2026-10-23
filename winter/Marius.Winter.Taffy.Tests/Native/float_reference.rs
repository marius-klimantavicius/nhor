// Native oracle for the C# FloatContext contract corpus. No layout algorithm is duplicated here.
use std::io::{self, BufRead};
use taffy::compute::{FloatContext, FloatIntrinsicWidthCalculator};
use taffy::{AvailableSpace, Clear, Direction, FloatDirection, Size};

fn clear(value: usize) -> Clear {
    [Clear::None, Clear::Left, Clear::Right, Clear::Both][value]
}
fn direction(value: usize) -> FloatDirection {
    [FloatDirection::Left, FloatDirection::Right][value]
}
fn main() {
    let mut context = FloatContext::new();
    for line in io::stdin().lock().lines() {
        let line = line.unwrap();
        let fields: Vec<&str> = line.split_whitespace().collect();
        let f = |index: usize| fields[index].parse::<f32>().unwrap();
        let n = |index: usize| fields[index].parse::<usize>().unwrap();
        let mut output: Vec<String> = Vec::new();
        match fields[0] {
            "reset" => {
                context = FloatContext::new();
                context.set_width(f(1));
            }
            "place" => {
                let position = context.place_floated_box(n(1) as u64,
                    Size { width: f(2), height: f(3) }, f(4), [f(5), f(6)], direction(n(7)), clear(n(8)));
                output.extend([position.x, position.y].map(|v| v.to_bits().to_string()));
            }
            "content" => {
                let after = fields[5].parse::<usize>().ok();
                let slot = context.find_content_slot(f(1), [f(2), f(3)], clear(n(4)), after);
                output.push(slot.segment_id.map_or(String::from("-"), |v| v.to_string()));
                output.extend([slot.x, slot.y, slot.width, slot.height].map(|v| v.to_bits().to_string()));
            }
            "bfc" => {
                let after = fields[8].parse::<usize>().ok();
                let dir = if n(6) == 0 { Direction::Ltr } else { Direction::Rtl };
                let slot = context.find_bfc_slot(f(1), [f(2), f(3)], [f(4), f(5)], dir, clear(n(7)), after);
                output.push(slot.segment_id.map_or(String::from("-"), |v| v.to_string()));
                output.extend([slot.x, slot.y, slot.border_width, slot.stretch_width].map(|v| v.to_bits().to_string()));
            }
            "clear" => {
                output.push(context.cleared_threshold(clear(n(1))).map_or(String::from("-"), |v| v.to_bits().to_string()));
            }
            "intrinsic" => {
                let available = match fields[1] {
                    "min" => AvailableSpace::MinContent,
                    "max" => AvailableSpace::MaxContent,
                    _ => AvailableSpace::Definite(f(1)),
                };
                let mut calculator = FloatIntrinsicWidthCalculator::new(available);
                for index in (2..fields.len()).step_by(3) {
                    calculator.add_float(f(index), direction(n(index + 1)), clear(n(index + 2)));
                }
                output.push(calculator.result().to_bits().to_string());
            }
            "active" => {
                output.push(context.has_floats().to_string());
                output.push(context.has_active_floats(f(1)).to_string());
            }
            _ => panic!("unknown operation: {}", fields[0]),
        }
        println!("{}", output.join(" "));
    }
}
