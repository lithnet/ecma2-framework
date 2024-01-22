namespace Lithnet.Ecma2Framework
{
    public static class EcmaExtensions
    {
        public static T GetCustomData<T>(this ImportContext context) where T : class
        {
            return (T)context.CustomData;
        }

        public static T GetCustomData<T>(this ExportContext context) where T : class
        {
            return (T)context.CustomData;
        }

        public static T GetCustomData<T>(this PasswordContext context) where T : class
        {
            return (T)context.CustomData;
        }
    }
}