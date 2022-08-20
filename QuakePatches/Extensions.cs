using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuakePatches;

internal static class Extensions
{
    public static int IndexOf(this ArraySegment<byte> array, byte[] sequence)
    {
        for(int i=0,si=0;i<array.Count;i++)
        {
            if (array[i] == sequence[si])
            {
                if (++si == sequence.Length)
                    return i - si;
            }
            else
            {
                si = 0;
            }
        }

        return -1;
    }
}
