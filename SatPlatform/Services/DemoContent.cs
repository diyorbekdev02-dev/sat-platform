using SatPlatform.Data;

namespace SatPlatform.Services;

public record DemoQuestion(string Skill, Difficulty Difficulty, string? Passage, string Text, string[]? Options, int Correct, string? Answer, string Explanation);

/// <summary>Demo question bank: procedurally generated Math items (answers computed) plus Reading &amp; Writing items from templates.</summary>
public static partial class DemoContent
{
    public static List<DemoQuestion> Generate(Random rnd)
    {
        var list = new List<DemoQuestion>();
        list.AddRange(ReadingWriting(rnd));
        list.AddRange(Math(rnd));
        return list;
    }

    internal static string P(string s) => $"<p>{s}</p>";

    /// <summary>Builds a 4-option MC item from a correct value and distinct distractors, shuffled.</summary>
    internal static DemoQuestion Mc(Random rnd, string skill, Difficulty d, string text, string correct, IEnumerable<string> distractors, string explanation, string? passage = null)
    {
        var options = new List<string> { correct };
        foreach (var x in distractors) if (!options.Contains(x) && options.Count < 4) options.Add(x);
        var k = 1;
        while (options.Count < 4) { var x = correct + new string('\'', k++); if (!options.Contains(x)) options.Add(x); }
        var shuffled = options.OrderBy(_ => rnd.Next()).ToArray();
        return new DemoQuestion(skill, d, passage, P(text), shuffled, Array.IndexOf(shuffled, correct), null, P(explanation));
    }

    internal static DemoQuestion Spr(string skill, Difficulty d, string text, string answer, string explanation) =>
        new(skill, d, null, P(text), null, 0, answer, P(explanation));

    // ================================================================= Math

    private static IEnumerable<DemoQuestion> Math(Random r)
    {
        int N(int a, int b) => r.Next(a, b + 1);
        int NZ(int a, int b) { int v; do v = N(a, b); while (v == 0); return v; }
        string S(int v) => v < 0 ? $"({v})" : v.ToString();
        string Sign(int v) => v < 0 ? $"- {-v}" : $"+ {v}";

        // Linear equations in one variable
        for (var i = 0; i < 14; i++)
        {
            var a = NZ(2, 9); var x = N(-8, 9); var b = N(-12, 12); var c = a * x + b;
            var d = i < 5 ? Difficulty.Easy : i < 10 ? Difficulty.Medium : Difficulty.Hard;
            if (d == Difficulty.Hard)
            {
                var k = NZ(2, 6); var m = N(1, 9); // a(x + m) = c + k*x  → (a-k)x = c - a*m
                var rhs = a * (x + m) - k * x;
                yield return Spr("LINEAR_EQUATIONS", d, $"If ${a}(x + {m}) = {rhs} {Sign(k).Replace("+", "+").Replace("- ", "- ")}x$, what is the value of $x$?".Replace($"{rhs} + {k}x", $"{rhs} + {k}x"), x.ToString(),
                    $"Expand: ${a}x + {a * m} = {rhs} + {k}x$. Collect terms: ${a - k}x = {rhs - a * m}$, so $x = {x}$.");
            }
            else if (i % 2 == 0)
                yield return Spr("LINEAR_EQUATIONS", d, $"If ${a}x {Sign(b)} = {c}$, what is the value of $x$?", x.ToString(), $"Subtract ${b}$ from both sides: ${a}x = {c - b}$. Divide by ${a}$: $x = {x}$.");
            else
                yield return Mc(r, "LINEAR_EQUATIONS", d, $"What value of $x$ satisfies the equation ${a}x {Sign(b)} = {c}$?", x.ToString(), [(x + 1).ToString(), (-x).ToString(), (x - a).ToString(), (x + 2).ToString()],
                    $"${a}x = {c} - {S(b)} = {c - b}$, so $x = {c - b} \\div {a} = {x}$.");
        }

        // Linear equations in two variables (slope/intercept, points on a line)
        for (var i = 0; i < 10; i++)
        {
            var m = NZ(-4, 5); var b = N(-6, 6); var x0 = N(-5, 6); var y0 = m * x0 + b;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Mc(r, "LINEAR_EQUATIONS_TWO", d, $"The line $y = {m}x {Sign(b)}$ passes through the point $({x0}, k)$. What is the value of $k$?", y0.ToString(), [(y0 + m).ToString(), (m * x0 - b).ToString(), (y0 - 1).ToString(), (-y0).ToString()],
                    $"Substitute $x = {x0}$: $k = {m}({x0}) {Sign(b)} = {y0}$.");
            else
            {
                var x1 = x0 + NZ(1, 4); var y1 = m * x1 + b;
                yield return Mc(r, "LINEAR_EQUATIONS_TWO", d, $"A line passes through the points $({x0}, {y0})$ and $({x1}, {y1})$. What is the slope of the line?", m.ToString(), [(-m).ToString(), (m + 1).ToString(), (y1 - y0 == 0 ? 1 : (x1 - x0)).ToString(), (m - 1).ToString()],
                    $"Slope $= \\dfrac{{{y1} - {S(y0)}}}{{{x1} - {S(x0)}}} = \\dfrac{{{y1 - y0}}}{{{x1 - x0}}} = {m}$.");
            }
        }

        // Linear functions (word problems)
        string[] items = ["a gym membership", "a phone plan", "a car rental", "a tutoring service", "a streaming service"];
        for (var i = 0; i < 10; i++)
        {
            var fixedFee = N(10, 60); var rate = N(2, 15); var n = N(3, 12); var total = fixedFee + rate * n;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            var item = items[i % items.Length];
            if (i % 2 == 0)
                yield return Mc(r, "LINEAR_FUNCTIONS", d, $"The cost of {item} is a one-time fee of \\${fixedFee} plus \\${rate} per month. Which function $C$ gives the total cost, in dollars, for $n$ months?",
                    $"$C(n) = {rate}n + {fixedFee}$", [$"$C(n) = {fixedFee}n + {rate}$", $"$C(n) = {rate + fixedFee}n$", $"$C(n) = {rate}n - {fixedFee}$"],
                    $"The fixed fee is the constant term and the monthly rate multiplies $n$: $C(n) = {rate}n + {fixedFee}$.");
            else
                yield return Spr("LINEAR_FUNCTIONS", d, $"The function $f(n) = {rate}n + {fixedFee}$ gives the total cost, in dollars, of {item} for $n$ months. How many months does \\${total} buy?", n.ToString(),
                    $"Solve ${rate}n + {fixedFee} = {total}$: ${rate}n = {total - fixedFee}$, so $n = {n}$.");
        }

        // Systems of two linear equations
        for (var i = 0; i < 12; i++)
        {
            var x = N(-5, 7); var y = N(-5, 7); var a1 = NZ(1, 5); var b1 = NZ(-4, 5); var a2 = NZ(-4, 5); var b2 = NZ(1, 5);
            if (a1 * b2 - a2 * b1 == 0) { b2++; }
            var c1 = a1 * x + b1 * y; var c2 = a2 * x + b2 * y;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            string Eq(int a, int b, int c) => $"{(a == 1 ? "" : a == -1 ? "-" : a.ToString())}x {(b < 0 ? "-" : "+")} {(System.Math.Abs(b) == 1 ? "" : System.Math.Abs(b).ToString())}y = {c}";
            if (i % 3 == 0)
                yield return Spr("SYSTEMS", d, $"The system of equations ${Eq(a1, b1, c1)}$ and ${Eq(a2, b2, c2)}$ has solution $(x, y)$. What is the value of $x$?", x.ToString(),
                    $"Solve by elimination or substitution; the solution is $(x, y) = ({x}, {y})$, so $x = {x}$.");
            else if (i % 3 == 1)
                yield return Spr("SYSTEMS", d, $"If ${Eq(a1, b1, c1)}$ and ${Eq(a2, b2, c2)}$, what is the value of $y$?", y.ToString(), $"The system's solution is $({x}, {y})$, so $y = {y}$.");
            else
                yield return Mc(r, "SYSTEMS", d, $"What is the value of $x + y$ if $(x, y)$ is the solution to the system ${Eq(a1, b1, c1)}$, ${Eq(a2, b2, c2)}$?", (x + y).ToString(), [(x - y).ToString(), (x + y + 1).ToString(), (x * y).ToString(), (x + y - 2).ToString()],
                    $"The solution is $({x}, {y})$, so $x + y = {x + y}$.");
        }

        // Linear inequalities
        for (var i = 0; i < 10; i++)
        {
            var a = N(2, 8); var b = N(-10, 10); var x = N(-6, 8); var c = a * x + b;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Mc(r, "LINEAR_INEQUALITIES", d, $"Which of the following is the solution set of the inequality ${a}x {Sign(b)} > {c}$?", $"$x > {x}$", [$"$x < {x}$", $"$x > {x + b}$", $"$x < {-x}$"],
                    $"Subtract ${b}$: ${a}x > {c - b}$. Divide by the positive number ${a}$ (direction unchanged): $x > {x}$.");
            else
            {
                var k = N(-6, 6);
                yield return Mc(r, "LINEAR_INEQUALITIES", d, $"For which value of $x$ is ${a}x {Sign(b)} \\le {c}$ true?", (x - 1).ToString(), [(x + 1).ToString(), (x + 2).ToString(), (x + 5).ToString()],
                    $"The inequality is equivalent to $x \\le {x}$; among the choices only ${x - 1}$ satisfies it.");
            }
        }

        // Equivalent expressions
        for (var i = 0; i < 10; i++)
        {
            var a = N(1, 9); var b = N(1, 9);
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Mc(r, "EQUIVALENT_EXPRESSIONS", d, $"Which expression is equivalent to $(x + {a})(x + {b})$?", $"$x^2 + {a + b}x + {a * b}$", [$"$x^2 + {a * b}x + {a + b}$", $"$x^2 + {a + b}x - {a * b}$", $"$x^2 + {a * b}$"],
                    $"FOIL: $x^2 + {b}x + {a}x + {a * b} = x^2 + {a + b}x + {a * b}$.");
            else
                yield return Mc(r, "EQUIVALENT_EXPRESSIONS", d, $"Which expression is equivalent to $x^2 - {a * a}$?", $"$(x - {a})(x + {a})$", [$"$(x - {a})^2$", $"$(x + {a})^2$", $"$(x - {a})(x - {a * a})$"],
                    $"Difference of squares: $x^2 - {a}^2 = (x - {a})(x + {a})$.");
        }

        // Nonlinear equations
        for (var i = 0; i < 12; i++)
        {
            var p = N(-7, 7); var q = N(-7, 7); if (p == q) q += 1;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            var sb = -(p + q); var c = p * q;
            var eq = $"x^2 {(sb < 0 ? "-" : "+")} {(System.Math.Abs(sb) == 1 ? "" : System.Math.Abs(sb).ToString())}x {(c < 0 ? "-" : "+")} {System.Math.Abs(c)} = 0".Replace("+ 0x ", "").Replace("- 0x ", "");
            if (sb == 0) eq = $"x^2 {(c < 0 ? "-" : "+")} {System.Math.Abs(c)} = 0";
            if (i % 2 == 0)
                yield return Spr("NONLINEAR_EQUATIONS", d, $"What is the sum of the solutions to the equation ${eq}$?", (p + q).ToString(), $"The equation factors as $(x - {S(p)})(x - {S(q)}) = 0$, so the solutions are ${p}$ and ${q}$; their sum is ${p + q}$.");
            else
                yield return Mc(r, "NONLINEAR_EQUATIONS", d, $"Which of the following is a solution to ${eq}$?", p.ToString(), [(-p == q ? p + 3 : -p).ToString(), (p + 1).ToString(), (p * q + 1).ToString()],
                    $"Factoring gives $(x - {S(p)})(x - {S(q)}) = 0$, so $x = {p}$ or $x = {q}$.");
        }

        // Nonlinear functions
        for (var i = 0; i < 10; i++)
        {
            var a = N(1, 4); var h = N(-5, 5); var k = N(-9, 9);
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Mc(r, "NONLINEAR_FUNCTIONS", d, $"The function $f$ is defined by $f(x) = {a}(x - {S(h)})^2 {Sign(k)}$. What is the minimum value of $f$?", k.ToString(), [h.ToString(), (-k).ToString(), (a + k).ToString(), (k + 1).ToString()],
                    $"The vertex of $y = a(x - h)^2 + k$ with $a > 0$ is the minimum, at $({h}, {k})$. The minimum value is ${k}$.");
            else
            {
                var x = N(-3, 4); var v = a * (x - h) * (x - h) + k;
                yield return Spr("NONLINEAR_FUNCTIONS", d, $"If $g(x) = {a}(x - {S(h)})^2 {Sign(k)}$, what is $g({x})$?", v.ToString(), $"$g({x}) = {a}({x} - {S(h)})^2 {Sign(k)} = {a} \\cdot {(x - h) * (x - h)} {Sign(k)} = {v}$.");
            }
        }

        // Ratios, rates, units
        for (var i = 0; i < 10; i++)
        {
            var speed = N(40, 90); var hours = N(2, 6); var dist = speed * hours;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Spr("RATIOS_RATES", d, $"A train travels at a constant speed of {speed} kilometers per hour. How many kilometers does it travel in {hours} hours?", dist.ToString(), $"Distance $=$ speed $\\times$ time $= {speed} \\times {hours} = {dist}$ km.");
            else
            {
                var ratioA = N(2, 5); var ratioB = N(2, 7); var mult = N(3, 9);
                yield return Mc(r, "RATIOS_RATES", d, $"In a recipe, the ratio of flour to sugar is ${ratioA}:{ratioB}$. If a baker uses {ratioA * mult} cups of flour, how many cups of sugar are needed?", (ratioB * mult).ToString(), [(ratioA * mult).ToString(), (ratioB * mult + ratioB).ToString(), (ratioB + mult).ToString()],
                    $"The multiplier is ${ratioA * mult} \\div {ratioA} = {mult}$, so sugar $= {ratioB} \\times {mult} = {ratioB * mult}$ cups.");
            }
        }

        // Percentages
        for (var i = 0; i < 10; i++)
        {
            var price = N(20, 200); var pct = new[] { 10, 15, 20, 25, 30, 40, 50 }[N(0, 6)];
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            var result = price * (100 - pct) / 100.0;
            if (i % 2 == 0)
                yield return Mc(r, "PERCENTAGES", d, $"A jacket that regularly costs \\${price} is on sale for {pct}% off. What is the sale price?", $"\\${result:0.##}", [$"\\${price * pct / 100.0:0.##}", $"\\${price - pct:0.##}", $"\\${price * (100 + pct) / 100.0:0.##}"],
                    $"Sale price $= {price} \\times (1 - {pct / 100.0:0.##}) = {result:0.##}$.");
            else
            {
                var part = N(5, 40); var whole = N(50, 200); var p2 = System.Math.Round(part * 100.0 / whole, 1);
                yield return Spr("PERCENTAGES", d, $"In a class of {whole} students, {part} play a musical instrument. What percent of the students play an instrument? (Round to the nearest tenth of a percent.)", p2.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"$\\dfrac{{{part}}}{{{whole}}} \\times 100 \\approx {p2}$ percent.");
            }
        }

        // One-variable data (mean, median)
        for (var i = 0; i < 10; i++)
        {
            var n = 5; var vals = Enumerable.Range(0, n).Select(_ => N(2, 20)).OrderBy(v => v).ToList();
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            var mean = vals.Sum() / (double)n; var median = vals[2];
            var listText = string.Join(", ", vals.OrderBy(_ => r.Next()));
            if (i % 2 == 0)
                yield return Spr("ONE_VARIABLE_DATA", d, $"The data set {listText} shows the number of books read by five students last month. What is the median of the data set?", median.ToString(), $"Order the values: {string.Join(", ", vals)}. The middle value is ${median}$.");
            else
                yield return Spr("ONE_VARIABLE_DATA", d, $"What is the mean of the data set {listText}?", mean.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Mean $= \\dfrac{{{vals.Sum()}}}{{5}} = {mean}$.");
        }

        // Two-variable data (linear model)
        for (var i = 0; i < 8; i++)
        {
            var m = N(2, 9); var b = N(10, 60); var x = N(3, 12);
            var d = i < 3 ? Difficulty.Easy : i < 6 ? Difficulty.Medium : Difficulty.Hard;
            yield return Mc(r, "TWO_VARIABLE_DATA", d, $"A scatterplot of a plant's height $h$, in centimeters, versus time $t$, in weeks, is modeled by the line of best fit $h = {m}t + {b}$. According to the model, by how many centimeters does the height increase each week?", m.ToString(), [b.ToString(), (m + b).ToString(), (m * x).ToString()],
                $"In $h = {m}t + {b}$ the slope ${m}$ is the change in height per week; ${b}$ is the starting height.");
        }

        // Probability
        for (var i = 0; i < 10; i++)
        {
            var red = N(2, 9); var blue = N(2, 9); var green = N(1, 6); var total = red + blue + green;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            yield return Spr("PROBABILITY", d, $"A bag contains {red} red, {blue} blue, and {green} green marbles. If one marble is selected at random, what is the probability that it is blue? (Express your answer as a fraction.)", $"{blue}/{total}|{System.Math.Round(blue / (double)total, 3).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"Probability $= \\dfrac{{\\text{{blue}}}}{{\\text{{total}}}} = \\dfrac{{{blue}}}{{{total}}}$.");
        }

        // Sample statistics
        for (var i = 0; i < 6; i++)
        {
            var sample = N(200, 800); var yes = N(20, 60); var pop = N(5, 40) * 1000; var est = pop * yes / 100;
            var d = i < 2 ? Difficulty.Easy : i < 4 ? Difficulty.Medium : Difficulty.Hard;
            yield return Mc(r, "SAMPLE_STATISTICS", d, $"A random sample of {sample} residents of a city of {pop:N0} people was surveyed, and {yes}% said they use public transit weekly. Based on the sample, which is the best estimate of the number of residents who use public transit weekly?", $"{est:N0}", [$"{sample * yes / 100:N0}", $"{pop - est:N0}", $"{est / 2:N0}"],
                $"Apply the sample proportion to the population: ${yes}\\% \\times {pop:N0} = {est:N0}$.");
        }

        // Area and volume
        for (var i = 0; i < 10; i++)
        {
            var w = N(3, 12); var h = N(3, 12); var l = N(2, 9);
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Spr("AREA_VOLUME", d, $"A rectangular box has length {l} cm, width {w} cm, and height {h} cm. What is the volume of the box, in cubic centimeters?", (l * w * h).ToString(), $"$V = l \\times w \\times h = {l} \\times {w} \\times {h} = {l * w * h}$.");
            else
                yield return Mc(r, "AREA_VOLUME", d, $"The area of a triangle is {w * h} square units and its base is {w} units. What is the height of the triangle, in units?", (2 * h).ToString(), [h.ToString(), (w * h / 2).ToString(), (2 * h + 1).ToString()],
                    $"$A = \\tfrac{{1}}{{2}}bh \\Rightarrow {w * h} = \\tfrac{{1}}{{2}}({w})h \\Rightarrow h = {2 * h}$.");
        }

        // Lines, angles, triangles
        for (var i = 0; i < 10; i++)
        {
            var a = N(25, 80); var b = N(25, 80); var c = 180 - a - b;
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Spr("LINES_ANGLES_TRIANGLES", d, $"In a triangle, two of the angles measure {a}° and {b}°. What is the measure, in degrees, of the third angle?", c.ToString(), $"The angles of a triangle sum to 180°: $180 - {a} - {b} = {c}$.");
            else
                yield return Mc(r, "LINES_ANGLES_TRIANGLES", d, $"Two parallel lines are cut by a transversal. One of the angles formed measures {a}°. What is the measure of the angle that is supplementary to it?", $"{180 - a}°", [$"{a}°", $"{90 - a + 90}°".Replace($"{180 - a}°", $"{a + 10}°"), $"{360 - a}°"],
                    $"Supplementary angles sum to 180°: $180 - {a} = {180 - a}$.");
        }

        // Right triangles and trigonometry
        int[][] triples = [[3, 4, 5], [5, 12, 13], [8, 15, 17], [6, 8, 10], [7, 24, 25], [9, 12, 15]];
        for (var i = 0; i < 10; i++)
        {
            var t = triples[i % triples.Length];
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Spr("RIGHT_TRIANGLES_TRIG", d, $"A right triangle has legs of length {t[0]} and {t[1]}. What is the length of the hypotenuse?", t[2].ToString(), $"$c = \\sqrt{{{t[0]}^2 + {t[1]}^2}} = \\sqrt{{{t[0] * t[0] + t[1] * t[1]}}} = {t[2]}$.");
            else
                yield return Mc(r, "RIGHT_TRIANGLES_TRIG", d, $"In right triangle $ABC$, the right angle is at $C$, $AC = {t[0]}$, $BC = {t[1]}$, and $AB = {t[2]}$. What is $\\sin A$?", $"$\\dfrac{{{t[1]}}}{{{t[2]}}}$", [$"$\\dfrac{{{t[0]}}}{{{t[2]}}}$", $"$\\dfrac{{{t[1]}}}{{{t[0]}}}$", $"$\\dfrac{{{t[0]}}}{{{t[1]}}}$"],
                    $"$\\sin A = \\dfrac{{\\text{{opposite}}}}{{\\text{{hypotenuse}}}} = \\dfrac{{BC}}{{AB}} = \\dfrac{{{t[1]}}}{{{t[2]}}}$.");
        }

        // Circles
        for (var i = 0; i < 10; i++)
        {
            var h = N(-6, 6); var k = N(-6, 6); var rad = N(2, 9);
            var d = i < 4 ? Difficulty.Easy : i < 8 ? Difficulty.Medium : Difficulty.Hard;
            if (i % 2 == 0)
                yield return Mc(r, "CIRCLES", d, $"A circle in the $xy$-plane has equation $(x - {S(h)})^2 + (y - {S(k)})^2 = {rad * rad}$. What is the radius of the circle?", rad.ToString(), [(rad * rad).ToString(), (rad * 2).ToString(), System.Math.Abs(h).ToString()],
                    $"The equation $(x - h)^2 + (y - k)^2 = r^2$ gives $r^2 = {rad * rad}$, so $r = {rad}$.");
            else
                yield return Mc(r, "CIRCLES", d, $"What is the center of the circle with equation $(x - {S(h)})^2 + (y - {S(k)})^2 = {rad * rad}$?", $"$({h}, {k})$", [$"$({-h}, {-k})$", $"$({k}, {h})$", $"$({h}, {rad})$"],
                    $"The center is $(h, k) = ({h}, {k})$.");
        }
    }
}
