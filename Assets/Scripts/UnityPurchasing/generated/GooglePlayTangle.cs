// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("sywblACN7nCIEncuRyazuokSz+8meEQfr5viOLFREhDhf80c2zSCMDJc4XZ0NX83kdRTYwuccgtsqXfRdPKPRaTU+y5/h35cN2bTHR1JuyTQWApIFbAKpdNSNpTq0t7LwvlW8CFiQHaxx9fOIqTW2bmIWhAYnA9/w+wG7TllduveSgEoIlC5Tg9cZfhX1NrV5VfU39dX1NTVeGH0Ps+IkeVX1Pfl2NPc/1OdUyLY1NTU0NXWmuL/w86sSbT1BQ52w/4Xz6QAGD4P4z+Lst99nRLVk/uRrQchyvL1aq9Wq642UwSiudG/mSweuQSfDuDBzH0rCj93zf0F66Dz+y763mcgU4Hf1Wup2O//lINx/2Gl4mJB9K24cYcdASfsWXDS4NfW1NXU");
        private static int[] order = new int[] { 5,9,13,7,7,12,10,9,12,13,12,12,13,13,14 };
        private static int key = 213;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
