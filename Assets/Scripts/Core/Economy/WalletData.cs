using System;
using System.Collections.Generic;

namespace Deepshaft.Economy
{
    /// <summary>
    /// Saved wallet contents. A list, not a dictionary, because JsonUtility
    /// can't serialize dictionaries. Use <see cref="Wallet"/> to read and change it.
    /// </summary>
    [Serializable]
    public class WalletData
    {
        public List<ResourceAmount> balances = new List<ResourceAmount>();
    }
}
