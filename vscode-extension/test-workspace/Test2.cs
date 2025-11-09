public class GlobalField
{
    int a = 1;

    public void add()
    {
        a = 100 + 2;
    }
}

public class LocalField
{
    public void add()
    {
        int a = 1;
        a = 100 + 2;
    }
}
