using System;
using System.Runtime.InteropServices;

namespace MastersOfTempest.Networking
{
    public class ByteSerializer
    {

        /// <summary>
        /// Returns exact byte array representation of a struct without overhead (unlike BinaryFormatter class).
        /// Make sure that strings have a maximal size e.g. [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)]
        /// </summary>
        /// <typeparam name="T">The type of the struct</typeparam>
        /// <param name="t">The struct instance</param>
        /// <returns></returns>
        public static byte[] GetBytes<T>(T t)
        {
            int size = Marshal.SizeOf(t);
            byte[] data = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(t, ptr, true);
            Marshal.Copy(ptr, data, 0, size);
            Marshal.FreeHGlobal(ptr);

            return data;
        }

        /// <summary>
        /// Returns struct instance representation from a previously serialized byte array.
        /// </summary>
        /// <typeparam name="T">The type of the struct</typeparam>
        /// <param name="data">The bytes that represent the struct</param>
        /// <returns></returns>
        public static T FromBytes<T> (byte[] data)
        {
            int size = Marshal.SizeOf(typeof(T));

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, 0, ptr, size);
            T t = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);

            return t;
        }
    }
}
