namespace Lithnet.Ecma2Framework
{
    public interface ISchemaContext : IConfigParameterContext
    {
        object CustomData { get; set; }
    }
}