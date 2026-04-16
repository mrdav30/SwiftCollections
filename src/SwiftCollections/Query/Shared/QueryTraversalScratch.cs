using System.Threading;

namespace SwiftCollections.Query;

internal sealed class QueryTraversalScratch
{
    private readonly ThreadLocal<SwiftIntStack> _intStack;

    public QueryTraversalScratch()
    {
        _intStack = new ThreadLocal<SwiftIntStack>(() => new SwiftIntStack(0));
    }

    public SwiftIntStack RentIntStack(int capacity)
    {
        SwiftIntStack stack = _intStack.Value;
        stack.EnsureCapacity(capacity);
        stack.Clear();
        return stack;
    }
}
