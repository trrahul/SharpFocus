namespace SharpFocus.TestWorkspace;

public class FieldMutationSamples
{
    private int _singleField;
    private int _sharedField;

    public void MutateSingleField()
    {
        _singleField += 1;
    }

    public void ResetSingleField()
    {
        _singleField = 0;
    }

    public void MutateSharedFieldInInitializer()
    {
        _sharedField = 5;
    }

    public void MutateSharedFieldFromFirstMethod()
    {
        _sharedField++;
    }

    public void MutateSharedFieldFromSecondMethod()
    {
        _sharedField += 2;
    }

    public void ProcessSharedField()
    {
        if (_sharedField > 10)
        {
            _sharedField -= 3;
        }
    }
}
