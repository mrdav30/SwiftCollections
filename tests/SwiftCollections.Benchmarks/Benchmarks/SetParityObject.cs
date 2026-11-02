



namespace SwiftCollections.Benchmarks;

internal sealed class SetParityObject
{
    public string Id { get; set; }
    public string Name { get; set; }

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object obj)
    {
        return obj is SetParityObject other && Id == other.Id;
    }
}
