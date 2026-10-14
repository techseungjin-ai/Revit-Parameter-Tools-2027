namespace ParameterValueCopier.Models
{
    public class DecimalPlaceOption
    {
        public int Places { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }
}
