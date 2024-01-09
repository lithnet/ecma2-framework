﻿namespace Lithnet.Ecma2Framework
{
    public interface ISchemaContext : IConfigParameterContext
    {
        IConnectionContext ConnectionContext { get; }

        object CustomData { get; set; }
    }
}