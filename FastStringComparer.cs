//-----------------------------------------------------------------------
// <copyright file="FastStringComparer.cs" company="Microsoft">
//     Copyright (c) Microsoft. All rights reserved.
//     Internal use only.
// </copyright>
//-----------------------------------------------------------------------

#define WIN64

#define _WIN32

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Xbox.Xpert.Contracts.Comparer
{
    /// <summary>
    /// Fast string ordinal comparer with consistent hash code calculation for sub string, char array, and byte array
    /// </summary>
    public class FastStringComparer : IEqualityComparer<string>
    {
        public static readonly FastStringComparer Instance = new FastStringComparer();

        /// <summary>
        /// Confirmed prime
        /// </summary>
        public const int Prime1 = 5381;

        /// <summary>
        /// 42326593 x 37, why?
        /// </summary>
        public const int Magic2 = 1566083941;
        
        bool IEqualityComparer<string>.Equals(string x, string y)
        {
            return FastStringComparer.Equals(x, y);
        }

        int IEqualityComparer<string>.GetHashCode(string obj)
        {
            return FastStringComparer.GetHashCode(obj);
        }

        /// <summary>
        /// Get Hash code for full string
        /// </summary>
        public static int GetHashCode(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            unsafe
            {
                fixed (char* p = s)
                {
                    return FastStringComparer.FastGetHash(p, s.Length);
                }
            }
        }

        public static bool Equals(string x, string y)
        {
#if WIN64
            if (object.ReferenceEquals(x, y))
            {
                return true;
            }

            if ((x == null) || (y == null))
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            return FastStringComparer.EqualsHelp64(x, y);
#else
            return string.Equals(x, y);
#endif
        }

        /// <summary>
        /// Get hash code for sub string
        /// </summary>
        public static int GetHashCode(string s, int index, int length)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (((uint)index + (uint)length) > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            unsafe
            {
                fixed (char* p = s)
                {
                    return FastStringComparer.FastGetHash(p + index, length);
                }
            }
        }

        /// <summary>
        /// Get Hash code for full char array
        /// </summary>
        public static int GetHashCode(char[] s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            unsafe
            {
                fixed (char* p = s)
                {
                    return FastStringComparer.FastGetHash(p, s.Length);
                }
            }
        }

        /// <summary>
        /// Get hash code for sub char array
        /// </summary>
        public static int GetHashCode(char[] s, int index, int length)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (((uint)index + (uint)length) > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            unsafe
            {
                fixed (char* p = s)
                {
                    return FastStringComparer.FastGetHash(p + index, length);
                }
            }
        }

        /// <summary>
        /// Get Hash code for full byte array, return true for all ASCII
        /// </summary>
        public static int GetHashCode(byte[] s, out bool isAscii)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            unsafe
            {
                fixed (byte* p = s)
                {
                    return FastStringComparer.FastGetHash(p, s.Length, out isAscii);
                }
            }
        }

        /// <summary>
        /// Get hash code for sub char array
        /// </summary>
        public static int GetHashCode(byte[] s, int index, int length, out bool isAscii)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (((uint)index + (uint)length) > s.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            unsafe
            {
                fixed (byte* p = s)
                {
                    return FastStringComparer.FastGetHash(p + index, length, out isAscii);
                }
            }
        }

        /// <summary>
        /// Fast inner hash code calculation without argument validation
        /// </summary>
        /// <remarks>The code reads 2 chars in a single read, so it's best to start at even char. 
        /// It does not assume string is zero-terminated as it needs to work on substring and char array.</remarks>
        /// <returns>Same hash code as string.GetHashCode() 32-bit version (The 64-bit version is slow and different).</returns>
        internal static unsafe int FastGetHash(char* src, int length)
        {
            int hash1 = (FastStringComparer.Prime1 << 16) + FastStringComparer.Prime1;
            int hash2 = hash1;

            // Optimal if src is even char aligned (e.g. in full string), okay if not.
            int* pint32 = (int*)(src);

            // End of groups of 4 chars
            int* pend = pint32 + ((length / 4) * 2);

            // Process 4 chars in one iteration, updating single pointer (less instruction than string.GetHashCode Win32 version)
            while (pint32 < pend)
            {
                // chars 0 and 1
                hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint32[0];

                // chars 2 and 3
                hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint32[1];

                // move to next 4 chars
                pint32 += 2;
            }

            // 0 to 3 chars left
            switch (length % 4)
            {
                case 1:
                    // Read single char, same value as read int32 assuming 0-termination
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ ((char*)pint32)[0];
                    break;

                case 2:
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint32[0];
                    break;

                case 3:
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint32[0];

                    // Read single char, same value as read int32 assuming 0-termination
                    hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ ((char*)pint32)[2];
                    break;

                default:
                    break;
            }

            // Mix hash1/hash2
            return hash1 + (hash2 * FastStringComparer.Magic2);
        }

        /// <summary>
        /// Fast inner hash code calculation without argument validation for byte array
        /// </summary>
        /// <remarks>The code reads 2 bytes in a single read, so it's best to start at even byte. 
        /// <returns>Same hash code as if converted to string in ASCII range.</returns>
        internal static unsafe int FastGetHash(byte* src, int length, out bool isAscii)
        {
            int hash1 = (FastStringComparer.Prime1 << 16) + FastStringComparer.Prime1;
            int hash2 = hash1;

            // Optimal if src is even byte aligned (e.g. in full byte array), okay if not.
            short* pint16 = (short*)(src);

            // End of groups of 4 bytes
            short* pend = pint16 + ((length / 4) * 2);

            int word0, word1;

            // Two bytes, one for each char
            int allBits = 0;

            // Process 4 bytes in one iteration, updating single pointer
            while (pint16 < pend)
            {
                word0 = pint16[0];

                // bytes 0 and 1, need to expand 2 bytes to 2 chars to be compatible with char version
                hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ ((word0 >> 8) << 16 | (word0 & 0xFF));

                word1 = pint16[1];

                // bytes 2 and 3, need to expand 2 bytes to 2 chars to be compatible with char version
                hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ ((word1 >> 8) << 16 | (word1 & 0xFF));

                // move to next 4 bytes
                pint16 += 2;

                allBits |= (word0 | word1);
            }

            // 0 to 3 bytes left
            switch (length % 4)
            {
                case 1:
                    // Read single byte
                    word0 = ((byte*)pint16)[0];
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ word0;
                    allBits |= word0;
                    break;

                case 2:
                    word0 = pint16[0];
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ ((word0 >> 8) << 16 | (word0 & 0xFF));
                    allBits |= word0;
                    break;

                case 3:
                    word0 = pint16[0];
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ ((word0 >> 8) << 16 | (word0 & 0xFF));

                    // Read single byte
                    word1 = ((byte*)pint16)[2];
                    hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ word1;
                    allBits |= (word0 | word1);
                    break;

                default:
                    break;
            }

            // ASCII range check
            isAscii = (allBits & 0x8080) == 0;

            // Mix hash1/hash2
            return hash1 + (hash2 * FastStringComparer.Magic2);
        }

        /// <summary>
        /// Mscorlib string.GetHashCode 32-bit version, copied here for comparison
        /// </summary>
        public static int GetHashCodeWin32(string text)
        {
            // null check added here, not in string.GetHashCode()
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            // There is a visible runtime cost checking this static variable
#if FEATURE_RANDOMIZED_STRING_HASHING
            if (HashHelpers.s_UseRandomizedStringHashing)
            {
                return InternalMarvin32HashString(this, this.Length, 0);
            }
#endif // FEATURE_RANDOMIZED_STRING_HASHING

            unsafe
            {
                fixed (char* src = text)
                {
#if _WIN32
                    int hash1 = (5381 << 16) + 5381;
#else
                    int hash1 = 5381;
#endif
                    int hash2 = hash1;

#if _WIN32
                    // 32 bit machines.
                    int* pint = (int *)src;
                    int len = text.Length;

                    // This assumes string is 0-terminated
                    while (len > 2)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len  -= 4;
                    }

                    // This assumes string is 0-terminated
                    if (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                    }
#else
                    // Super slow version when compiled for 16/64 bits OS
                    int c;
                    char* s = src;
                    while ((c = s[0]) != 0)
                    {
                        hash1 = ((hash1 << 5) + hash1) ^ c;
                        c = s[1];
                        if (c == 0)
                            break;
                        hash2 = ((hash2 << 5) + hash2) ^ c;
                        s += 2;
                    }
#endif

#if _DEBUG
                    // We want to ensure we can change our hash function daily.
                    // This is perfectly fine as long as you don't persist the
                    // value from GetHashCode to disk or count on String A 
                    // hashing before string B.  Those are bugs in your code.
                    hash1 ^= ThisAssembly.DailyBuildNumber;
#endif
                    return hash1 + (hash2 * 1566083941);
                }
            }
        }

#if WIN64
        /// <summary>
        /// Fast full string equality check, for 64-bit
        /// </summary>
        internal unsafe static bool EqualsHelp64(string strA, string strB)
        {
            Debug.Assert(strA != null);
            Debug.Assert(strB != null);
            Debug.Assert(strA.Length == strB.Length);

            // Count of qwords to compare, 2 for string length, 3 to round up
            int count = (strA.Length + 2 + 3) / 4;

            fixed (char* ap = strA) fixed (char* bp = strB)
            {
                // Move back to string length position
                long* a = (long *)(ap - 2);
                long* b = (long *)(bp - 2);

                long* end = a + count / 4 * 4;

                // Unroll by 16 chars, 32 bytes to match memcmp
                while (a < end)
                {
                    if (a[0] != b[0])
                    {
                        goto ReturnFalse;
                    }

                    if (a[1] != b[1])
                    {
                        goto ReturnFalse;
                    }

                    if (a[2] != b[2])
                    {
                        goto ReturnFalse;
                    }

                    if (a[3] != b[3])
                    {
                        goto ReturnFalse;
                    }

                    a += 4;
                    b += 4;
                }

                switch (count % 4)
                {
                    case 3:
                        return (a[0] == b[0]) && (a[1] == b[1]) && (a[2] == b[2]);

                    case 2:
                        return (a[0] == b[0]) && (a[1] == b[1]);

                    case 1:
                        return a[0] == b[0];

                    default:
                        return true;
                }

           ReturnFalse:
                return false;
            }
        }

#endif
    }
}
