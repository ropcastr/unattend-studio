namespace AutoUnattendStudio;

public sealed class OptimizationOption
{
    public string Id { get; set; } = "";
    public string Group { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Risk { get; set; }
    public string Kind { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Path { get; set; }
    public string? Name { get; set; }
    public string? RegistryType { get; set; }
    public string? Value { get; set; }
    public int ByteIndex { get; set; } = -1;
    public string? BitMask { get; set; }
    public bool SetBit { get; set; }
    public string? Package { get; set; }
    public string? TaskName { get; set; }
    public string? Subgroup { get; set; }
    public string? Setting { get; set; }
    public int Ac { get; set; } = -1;
    public int Dc { get; set; } = -1;
    public int Order { get; set; }
}
