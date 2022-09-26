using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ThinkIQ.Azure.Connector.Utils
{
    public static class SecureStringExtension
    {
        public static SecureString ConvertToSecureString(this string data)
        {
            var secure = new SecureString();
            foreach (var character in data.ToCharArray())
                secure.AppendChar(character);

            secure.MakeReadOnly();
            return secure;
        }

        public static string ConvertToString(this SecureString data)
        {
            var pointer = IntPtr.Zero;
            try
            {
                pointer = Marshal.SecureStringToGlobalAllocUnicode(data);
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(pointer);
            }
        }
    }
}
