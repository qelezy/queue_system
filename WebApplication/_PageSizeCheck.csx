using QuestPDF.Helpers;
var p = PageSizes.A4;
var l = PageSizes.A4.Landscape();
System.Console.WriteLine($"Portrait: {p.Width} x {p.Height}");
System.Console.WriteLine($"Landscape: {l.Width} x {l.Height}");
