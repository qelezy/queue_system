namespace WebApplication.Models
{
    public class FormFieldModel
    {
        public string Label { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public string Type { get; set; } = "text";
        public bool Required { get; set; }

        public List<FormSelectOption>? Options { get; set; }
    }

    public class FormSelectOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public bool Selected { get; set; }
    }
}
