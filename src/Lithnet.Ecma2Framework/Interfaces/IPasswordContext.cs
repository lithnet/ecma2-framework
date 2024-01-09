namespace Lithnet.Ecma2Framework
{
    public interface IPasswordContext : IConfigParameterContext
    {
        IConnectionContext ConnectionContext { get; }

        object CustomData { get; set; }
    }
}